using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;

namespace TriasDeparture.Windows.Popup;

/// <summary>Flyout above the tray: closes on Esc or when it loses focus.</summary>
public partial class PopupWindow : Window
{
    private const int EdgeGap = 12;
    private readonly PopupViewModel _viewModel;

    /// <summary>When the popup was last hidden – a tray click right after that is the click that hid it.</summary>
    public DateTime HiddenAtUtc { get; private set; } = DateTime.MinValue;

    public PopupWindow(PopupViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;

        Deactivated += (_, _) => HidePopup();
        SourceInitialized += (_, _) => RoundCorners();
        PreviewKeyDown += OnPreviewKeyDown;
        PreviewTextInput += OnPreviewTextInput;
    }

    public void ShowPopup()
    {
        // Bottom right above the taskbar, like the Windows flyouts (work area = screen minus taskbar)
        var area = SystemParameters.WorkArea;
        Left = area.Right - Width - EdgeGap;
        Top  = area.Bottom - Height - EdgeGap;

        _viewModel.OnShown();
        Show();
        Activate();
        if (_viewModel.ShowStop) Focus();
        else SearchBox.Focus();
    }

    public void HidePopup()
    {
        if (!IsVisible) return;
        HiddenAtUtc = DateTime.UtcNow;
        _viewModel.OnHidden();
        Hide();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (!_viewModel.StepBack())
                HidePopup();
            else if (!_viewModel.ShowStop)
                SearchBox.Focus();
            e.Handled = true;
        }
        else if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (_viewModel.ShowStop) _viewModel.BackCommand.Execute(null);
            SearchBox.Focus();
            SearchBox.SelectAll();
            e.Handled = true;
        }
    }

    /// <summary>Typing anywhere goes into the search box.</summary>
    private void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (SearchBox.IsKeyboardFocused || _viewModel.ShowStop)
            return;
        SearchBox.Focus();
        SearchBox.CaretIndex = SearchBox.Text.Length;
    }

    /// <summary>Arrow down from the search box jumps into the result list (Enter then opens the stop).</summary>
    private void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Down) return;
        if (FirstButton(ResultList) is { } first)
        {
            first.Focus();
            e.Handled = true;
        }
    }

    private static Button? FirstButton(ItemsControl list) =>
        list.Items.Count > 0 && list.ItemContainerGenerator.ContainerFromIndex(0) is ContentPresenter presenter
            ? FindChild<Button>(presenter)
            : null;

    private static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T match) return match;
            if (FindChild<T>(child) is { } nested) return nested;
        }
        return null;
    }

    /// <summary>Rounded corners on Windows 11 (ignored on Windows 10).</summary>
    private void RoundCorners()
    {
        var handle = new WindowInteropHelper(this).Handle;
        var round = 2; // DWMWCP_ROUND
        _ = DwmSetWindowAttribute(handle, 33 /* DWMWA_WINDOW_CORNER_PREFERENCE */, ref round, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
