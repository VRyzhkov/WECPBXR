using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using WECPBXR.UI.ViewModels;

namespace WECPBXR.UI;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;

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
