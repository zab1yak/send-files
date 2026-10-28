using System.Globalization;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SendFiles.App.Resources;
using SendFiles.Core.Hosting;
using SendFiles.Core.Qr;

namespace SendFiles.App.ViewModels;

public enum ConnectionView
{
    Starting,
    Waiting,
    NoNetwork,
    Failed,
}

public sealed partial class MainViewModel : ObservableObject
{
    private readonly SendFilesServer _server;

    public MainViewModel(SendFilesServer server)
    {
        _server = server;
        var dispatcher = Application.Current.Dispatcher;
        _server.StateChanged += (_, state) =>
        {
            if (!dispatcher.HasShutdownStarted)
            {
                dispatcher.BeginInvoke(() => Apply(state));
            }
        };
    }

    public string ComputerName { get; } = DevOverrides.ComputerName ?? Environment.MachineName;

    [ObservableProperty]
    public partial ConnectionView View { get; set; } = ConnectionView.Starting;

    [ObservableProperty]
    public partial string StatusText { get; set; } = Strings.Status_Starting;

    [ObservableProperty]
    public partial string? HintText { get; set; }

    [ObservableProperty]
    public partial string? AddressText { get; set; }

    [ObservableProperty]
    public partial Geometry? QrGeometry { get; set; }

    [ObservableProperty]
    public partial bool JustCopied { get; set; }

    public bool HasQr => QrGeometry is not null;

    [RelayCommand]
    private void CopyAddress()
    {
        if (_server.ConnectUri is not { } uri)
        {
            return;
        }

        try
        {
            Clipboard.SetText(uri.AbsoluteUri);
            JustCopied = true;
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            // Clipboard is busy (another app holds it); the user can retry.
        }
    }

    [RelayCommand]
    private Task RetryAsync() => _server.RefreshAsync();

    partial void OnQrGeometryChanged(Geometry? value) => OnPropertyChanged(nameof(HasQr));

    private void Apply(ServerState state)
    {
        JustCopied = false;
        switch (state.Status)
        {
            case ServerStatus.Running:
                View = ConnectionView.Waiting;
                StatusText = Strings.Status_Waiting;
                HintText = null;
                AddressText = string.Create(CultureInfo.InvariantCulture, $"{state.Primary}:{state.Port}");
                QrGeometry = _server.CreateQr() is { } qr ? QrGeometryBuilder.Build(qr) : null;
                break;
            case ServerStatus.NoNetwork:
                ShowProblem(ConnectionView.NoNetwork, Strings.Status_NoNetwork, Strings.Status_NoNetworkHint);
                break;
            case ServerStatus.Failed:
                ShowProblem(ConnectionView.Failed, Strings.Status_Failed, Strings.Status_FailedHint);
                break;
            default:
                View = ConnectionView.Starting;
                StatusText = Strings.Status_Starting;
                break;
        }
    }

    private void ShowProblem(ConnectionView view, string status, string hint)
    {
        View = view;
        StatusText = status;
        HintText = hint;
        AddressText = null;
        QrGeometry = null;
    }
}

internal static class QrGeometryBuilder
{
    /// <summary>One geometry for the whole code; horizontal runs are merged so edges stay crisp at any DPI.</summary>
    public static Geometry Build(QrMatrix qr)
    {
        var geometry = new StreamGeometry { FillRule = FillRule.Nonzero };
        using (var context = geometry.Open())
        {
            for (var row = 0; row < qr.Size; row++)
            {
                var column = 0;
                while (column < qr.Size)
                {
                    if (!qr[row, column])
                    {
                        column++;
                        continue;
                    }

                    var start = column;
                    while (column < qr.Size && qr[row, column])
                    {
                        column++;
                    }

                    context.BeginFigure(new Point(start, row), isFilled: true, isClosed: true);
                    context.PolyLineTo(
                        [new Point(column, row), new Point(column, row + 1), new Point(start, row + 1)],
                        isStroked: false, isSmoothJoin: false);
                }
            }
        }

        geometry.Freeze();
        return geometry;
    }
}
