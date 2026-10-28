using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using SendFiles.App.Icons;
using SendFiles.App.ViewModels;

namespace SendFiles.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly Storyboard _pulse;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        _pulse = CreatePulse();

        viewModel.PropertyChanged += OnViewModelChanged;
        IsVisibleChanged += (_, _) => UpdateAnimations();
        StateChanged += (_, _) => UpdateAnimations();
    }

    public void ShowFromTray()
    {
        Show();
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // The close button hides to the tray; "Quit" in the tray menu really exits.
        e.Cancel = true;
        Hide();
        base.OnClosing(e);
    }

    private void OnCardMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed && e.OriginalSource is not System.Windows.Controls.Button)
        {
            DragMove();
        }
    }

    private void OnMinimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnClose(object sender, RoutedEventArgs e) => Hide();

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.View):
                ApplyView();
                break;
            case nameof(MainViewModel.JustCopied):
                CopyGlyph.Data = _viewModel.JustCopied ? LucideIcons.Check : LucideIcons.Copy;
                break;
        }
    }

    private void ApplyView()
    {
        var problem = _viewModel.View is ConnectionView.NoNetwork or ConnectionView.Failed;
        ProblemPanel.Visibility = problem ? Visibility.Visible : Visibility.Collapsed;

        var dotKey = _viewModel.View switch
        {
            ConnectionView.Waiting => "Brush.Status.Ok",
            ConnectionView.NoNetwork => "Brush.Status.Warning",
            ConnectionView.Failed => "Brush.Status.Error",
            _ => "Brush.Text.Tertiary",
        };
        StatusDot.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, dotKey);
        PulseRing.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, dotKey);
        UpdateAnimations();
    }

    /// <summary>
    /// The pulse runs only while it can be seen: waiting for a device, window visible and not minimized.
    /// Hidden in the tray the app renders zero frames.
    /// </summary>
    private void UpdateAnimations()
    {
        var shouldPulse = _viewModel.View == ConnectionView.Waiting && IsVisible && WindowState != WindowState.Minimized
            && SystemParameters.ClientAreaAnimation;
        if (shouldPulse)
        {
            _pulse.Begin(this, isControllable: true);
        }
        else
        {
            _pulse.Stop(this);
        }
    }

    private Storyboard CreatePulse()
    {
        var duration = new Duration(TimeSpan.FromSeconds(1.8));
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var storyboard = new Storyboard { RepeatBehavior = RepeatBehavior.Forever };
        Timeline.SetDesiredFrameRate(storyboard, 30);

        var scaleX = new DoubleAnimation(1, 2.6, duration) { EasingFunction = ease };
        var scaleY = scaleX.Clone();
        var fade = new DoubleAnimation(0.55, 0, duration) { EasingFunction = ease };
        Storyboard.SetTarget(scaleX, PulseRing);
        Storyboard.SetTargetProperty(scaleX, new PropertyPath("RenderTransform.ScaleX"));
        Storyboard.SetTarget(scaleY, PulseRing);
        Storyboard.SetTargetProperty(scaleY, new PropertyPath("RenderTransform.ScaleY"));
        Storyboard.SetTarget(fade, PulseRing);
        Storyboard.SetTargetProperty(fade, new PropertyPath(OpacityProperty));
        storyboard.Children.Add(scaleX);
        storyboard.Children.Add(scaleY);
        storyboard.Children.Add(fade);
        return storyboard;
    }
}
