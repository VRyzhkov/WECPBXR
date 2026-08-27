using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using WECPBXR.UI.ViewModels;

namespace WECPBXR.UI;

public partial class MainWindow : Window
{
    private const double StandardWindowWidth = 1000;
    private const double StandardWindowHeight = 760;
    private const double CompactWindowScale = 0.5;
    private const string ReduceWindowIconData = "M 2 2 L 6 6 M 6 6 L 6 3 M 6 6 L 3 6 M 12 12 L 8 8 M 8 8 L 11 8 M 8 8 L 8 11";
    private const string RestoreWindowIconData = "M 6 6 L 2 2 M 2 2 L 5 2 M 2 2 L 2 5 M 8 8 L 12 12 M 12 12 L 9 12 M 12 12 L 12 9";

    private readonly MainWindowViewModel _viewModel;
    private bool _isCompact;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainWindowViewModel();
        DataContext = _viewModel;
    }

    private void Window_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (IsInteractiveElement(e.Source as Visual))
        {
            return;
        }

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void ScaleButton_Click(object? sender, RoutedEventArgs e)
    {
        _isCompact = !_isCompact;

        double previousWidth = Width;
        double previousHeight = Height;
        double nextWidth = _isCompact ? StandardWindowWidth * CompactWindowScale : StandardWindowWidth;
        double nextHeight = _isCompact ? StandardWindowHeight * CompactWindowScale : StandardWindowHeight;

        MinWidth = nextWidth;
        MinHeight = nextHeight;
        Width = nextWidth;
        Height = nextHeight;

        Position = new PixelPoint(
            Position.X + (int)Math.Round((previousWidth - nextWidth) / 2),
            Position.Y + (int)Math.Round((previousHeight - nextHeight) / 2));

        ScaleIcon.Data = Geometry.Parse(_isCompact ? RestoreWindowIconData : ReduceWindowIconData);
        ToolTip.SetTip(ScaleButton, _isCompact ? "Restore window size" : "Reduce window size");
    }

    private void TopmostButton_Click(object? sender, RoutedEventArgs e)
    {
        Topmost = !Topmost;

        TopmostPinIcon.RenderTransform = Topmost ? new RotateTransform(45) : null;
        ToolTip.SetTip(TopmostButton, Topmost ? "Unpin window" : "Pin window on top");
    }

    private void MinimizeButton_Click(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ControlSlot_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is StyledElement { DataContext: ControlSlotViewModel slot } &&
            e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _viewModel.HandleSlotClick(slot);
            e.Handled = true;
        }
    }

    private async void LogPanel_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;

        if (e.ClickCount != 2 || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        string logText = _viewModel.GetLogText();

        if (string.IsNullOrWhiteSpace(logText))
        {
            _viewModel.NotifyLogCopySkipped();
            return;
        }

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;

        if (clipboard is null)
        {
            _viewModel.NotifyLogCopyFailed("clipboard is not available");
            return;
        }

        try
        {
            await clipboard.SetTextAsync(logText);
            _viewModel.NotifyLogCopied();
        }
        catch (Exception exception)
        {
            _viewModel.NotifyLogCopyFailed(exception.Message);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.Dispose();
        base.OnClosed(e);
    }

    private static bool IsInteractiveElement(Visual? source)
    {
        while (source is not null)
        {
            if (source is Button or TextBox or ComboBox)
            {
                return true;
            }

            source = source.GetVisualParent();
        }

        return false;
    }
}
