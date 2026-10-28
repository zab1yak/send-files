using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using SendFiles.Core.Devices;

namespace SendFiles.Core.Pairing;

/// <summary>Shown on the PC: "Allow «iPhone (Safari)»? Code 1234".</summary>
public sealed record PairingRequest(string Id, string DeviceName, string Code, DateTimeOffset ExpiresAt);

/// <summary>Implemented by the host (WPF, Dynamic Island): ask the user and return their answer.</summary>
public interface IPairingApprover
{
    Task<bool> RequestAsync(PairingRequest request, CancellationToken cancellationToken);
}

public enum PairingStartError
{
    None,
    TokenInvalid,
    Busy,
    Paused,
}

public enum PairingStatus
{
    Pending,
    Approved,
    Denied,
    Expired,
}

public sealed record PairingStartResult(PairingStartError Error, string? RequestId = null, string? RequestSecret = null, string? Code = null, int ExpiresInSec = 0);

public sealed record PairingWaitResult(PairingStatus Status, string? DeviceId = null, string? Key = null);

/// <summary>
/// One-time QR token (5 min, single use, only its hash kept) → pairing request with a 4-digit code →
/// host approval → device key delivered once to the request's owner (proved by the request secret).
/// </summary>
public sealed class PairingService : IDisposable
{
    public static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan ApprovalTimeout = TimeSpan.FromSeconds(120);
    public static readonly TimeSpan WaitPollLimit = TimeSpan.FromSeconds(25);
    private static readonly TimeSpan FailureWindow = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan PauseAfterFailures = TimeSpan.FromSeconds(60);
    private const int MaxFailures = 20;

    private readonly DeviceStore _devices;
    private readonly TimeProvider _time;
    private readonly Func<IPairingApprover?> _approver;
    private readonly Lock _gate = new();
    private readonly ITimer _rotation;
    private readonly Queue<DateTimeOffset> _failures = new();
    private string _token = string.Empty;
    private byte[] _tokenHash = [];
    private DateTimeOffset _tokenExpiresAt;
    private DateTimeOffset _pausedUntil;
    private ActiveRequest? _active;

    public PairingService(DeviceStore devices, TimeProvider time, Func<IPairingApprover?> approver)
    {
        _devices = devices;
        _time = time;
        _approver = approver;
        _rotation = time.CreateTimer(_ => RegenerateToken(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        RegenerateToken();
    }

    /// <summary>The QR token changed (used, expired or regenerated): redraw the QR code.</summary>
    public event EventHandler? TokenChanged;

    /// <summary>A request is waiting for the host's answer, or was resolved (for UI state).</summary>
    public event EventHandler<PairingRequest>? RequestStarted;

    /// <summary>Raw token for the QR fragment. Never logged, never sent anywhere but the QR.</summary>
    public string CurrentToken
    {
        get
        {
            lock (_gate)
            {
                return _token;
            }
        }
    }

    public void RegenerateToken()
    {
        lock (_gate)
        {
            _token = DeviceKey.RandomToken();
            _tokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(_token));
            _tokenExpiresAt = _time.GetUtcNow() + TokenLifetime;
            _rotation.Change(TokenLifetime, Timeout.InfiniteTimeSpan);
        }

        TokenChanged?.Invoke(this, EventArgs.Empty);
    }

    public PairingStartResult Start(string? token, string deviceName)
    {
        ActiveRequest request;
        lock (_gate)
        {
            var now = _time.GetUtcNow();
            if (now < _pausedUntil)
            {
                return new PairingStartResult(PairingStartError.Paused);
            }

            if (!TokenMatches(token) || now >= _tokenExpiresAt)
            {
                RecordFailure(now);
                return new PairingStartResult(PairingStartError.TokenInvalid);
            }

            if (_active is { IsFinished: false } busy && now < busy.Request.ExpiresAt)
            {
                return new PairingStartResult(PairingStartError.Busy);
            }

            var code = RandomNumberGenerator.GetInt32(10_000).ToString("D4", CultureInfo.InvariantCulture);
            request = new ActiveRequest(
                new PairingRequest(DeviceKey.RandomToken(16), deviceName, code, now + ApprovalTimeout),
                DeviceKey.RandomToken());
            _active = request;
        }

        // Single use: the scanned code is dead the moment it is redeemed.
        RegenerateToken();
        _ = AskHostAsync(request);
        RequestStarted?.Invoke(this, request.Request);

        return new PairingStartResult(PairingStartError.None, request.Request.Id, request.Secret,
            request.Request.Code, (int)ApprovalTimeout.TotalSeconds);
    }

    /// <summary>Long-poll for the outcome. Returns the key only once, to the holder of the request secret.</summary>
    public async Task<PairingWaitResult?> WaitAsync(string requestId, string requestSecret, CancellationToken cancellationToken)
    {
        ActiveRequest? request;
        lock (_gate)
        {
            request = _active;
        }

        if (request is null || !Matches(request, requestId, requestSecret))
        {
            return null;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(WaitPollLimit);
        try
        {
            await request.Outcome.Task.WaitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new PairingWaitResult(PairingStatus.Pending);
        }

        lock (_gate)
        {
            var outcome = request.Outcome.Task.Result;
            if (outcome.Status != PairingStatus.Approved)
            {
                return outcome;
            }

            if (request.KeyDelivered)
            {
                return null; // the key is handed out exactly once
            }

            request.KeyDelivered = true;
            if (ReferenceEquals(_active, request))
            {
                _active = null;
            }

            return outcome;
        }
    }

    public bool Cancel(string requestId, string requestSecret)
    {
        lock (_gate)
        {
            if (_active is not { } request || !Matches(request, requestId, requestSecret))
            {
                return false;
            }

            _active = null;
            request.Cancellation.Cancel();
            request.Outcome.TrySetResult(new PairingWaitResult(PairingStatus.Denied));
            return true;
        }
    }

    public void Dispose()
    {
        _rotation.Dispose();
        lock (_gate)
        {
            _active?.Cancellation.Cancel();
        }
    }

    private async Task AskHostAsync(ActiveRequest request)
    {
        PairingWaitResult outcome;
        try
        {
            var approver = _approver();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(request.Cancellation.Token);
            timeout.CancelAfter(ApprovalTimeout);
            var approved = approver is not null
                && await approver.RequestAsync(request.Request, timeout.Token).ConfigureAwait(false);

            if (approved && !request.Cancellation.IsCancellationRequested)
            {
                var (device, key) = _devices.Add(request.Request.DeviceName);
                outcome = new PairingWaitResult(PairingStatus.Approved, device.Id, key);
            }
            else
            {
                outcome = new PairingWaitResult(PairingStatus.Denied);
            }
        }
        catch (OperationCanceledException)
        {
            outcome = new PairingWaitResult(request.Cancellation.IsCancellationRequested ? PairingStatus.Denied : PairingStatus.Expired);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            outcome = new PairingWaitResult(PairingStatus.Denied);
        }

        request.Outcome.TrySetResult(outcome);
    }

    private bool TokenMatches(string? token)
    {
        if (string.IsNullOrEmpty(token) || token.Length > 128)
        {
            return false;
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return CryptographicOperations.FixedTimeEquals(hash, _tokenHash);
    }

    private static bool Matches(ActiveRequest request, string requestId, string requestSecret) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(requestId), Encoding.UTF8.GetBytes(request.Request.Id))
        & CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(requestSecret), Encoding.UTF8.GetBytes(request.Secret));

    private void RecordFailure(DateTimeOffset now)
    {
        _failures.Enqueue(now);
        while (_failures.Count > 0 && now - _failures.Peek() > FailureWindow)
        {
            _failures.Dequeue();
        }

        if (_failures.Count >= MaxFailures)
        {
            // Someone is guessing: pause pairing and invalidate the current code.
            _failures.Clear();
            _pausedUntil = now + PauseAfterFailures;
            _token = DeviceKey.RandomToken();
            _tokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(_token));
            _tokenExpiresAt = now + TokenLifetime;
            ThreadPool.QueueUserWorkItem(_ => TokenChanged?.Invoke(this, EventArgs.Empty));
        }
    }

    private sealed class ActiveRequest(PairingRequest request, string secret)
    {
        public PairingRequest Request { get; } = request;

        public string Secret { get; } = secret;

        public CancellationTokenSource Cancellation { get; } = new();

        public TaskCompletionSource<PairingWaitResult> Outcome { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool KeyDelivered { get; set; }

        public bool IsFinished => Outcome.Task.IsCompleted;
    }
}
