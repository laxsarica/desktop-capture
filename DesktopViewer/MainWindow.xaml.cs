using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Media.Animation;
using DesktopViewer.Services;

namespace DesktopViewer;

public partial class MainWindow : Window
{
    [DllImport("user32.dll")]
    public static extern uint SetWindowDisplayAffinity(IntPtr hwnd, uint dwAffinity);

    const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;

    private bool _isDragging;
    private System.Windows.Point _dragOffset;
    private bool _isProcessing;

    public MainWindow()
    {
        InitializeComponent();
        Opacity = 0.85;

        // Check for API key on startup and set display affinity
        Loaded += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE);

            if (string.IsNullOrWhiteSpace(App.Settings.OpenAiApiKey))
            {
                ShowSettings();
            }
        };
    }

    // ─── Drag support ────────────────────────────────────────

    private void Button_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDragging = true;
        _dragOffset = e.GetPosition(this);
        ((UIElement)sender).CaptureMouse();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_isDragging && e.LeftButton == MouseButtonState.Pressed)
        {
            var screenPos = PointToScreen(e.GetPosition(this));
            Left = screenPos.X - _dragOffset.X;
            Top = screenPos.Y - _dragOffset.Y;
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            CaptureBtn.ReleaseMouseCapture();
        }
    }

    // ─── Click handlers ──────────────────────────────────────

    private async void CaptureButton_Click(object sender, RoutedEventArgs e)
    {
        // Only trigger capture if not dragging
        if (!_isDragging)
        {
            await CaptureAndAnalyzeAsync(fullScreen: true);
        }
    }

    private void CaptureButton_RightClick(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        ContextMenu!.IsOpen = true;
    }

    private async void CaptureFullScreen_Click(object sender, RoutedEventArgs e)
    {
        await CaptureAndAnalyzeAsync(fullScreen: true);
    }

    private async void CaptureRegion_Click(object sender, RoutedEventArgs e)
    {
        await CaptureAndAnalyzeAsync(fullScreen: false);
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        ShowSettings();
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    // ─── Core capture flow ───────────────────────────────────

    private async Task CaptureAndAnalyzeAsync(bool fullScreen)
    {
        if (_isProcessing) return;

        _isProcessing = true;
        var spinStoryboard = (Storyboard)FindResource("SpinAnimation");
        var pulseStoryboard = (Storyboard)FindResource("PulseAnimation");

        try
        {
            // Click animation
            pulseStoryboard.Begin();

            // Brief delay to let the button animation play and
            // ensure overlay/context menu is not captured
            await Task.Delay(150);

            // Hide ourselves briefly during capture
            Opacity = 0;
            await Task.Delay(50);

            Bitmap screenshot;

            if (fullScreen)
            {
                screenshot = ScreenCaptureService.CaptureFullScreen();
            }
            else
            {
                // Show region selection overlay
                Opacity = 0; // Stay hidden during region select
                var regionWindow = new RegionSelectWindow();
                regionWindow.ShowDialog();

                if (regionWindow.SelectedRegion is null ||
                    regionWindow.SelectedRegion.Value.Width < 10 ||
                    regionWindow.SelectedRegion.Value.Height < 10)
                {
                    Opacity = 0.85;
                    return;
                }

                screenshot = ScreenCaptureService.CaptureRegion(regionWindow.SelectedRegion.Value);
            }

            // Restore visibility and show processing state
            Opacity = 0.85;
            ButtonIcon.Text = "⏳";
            spinStoryboard.Begin();

            // Validate API key
            if (string.IsNullOrWhiteSpace(App.Settings.OpenAiApiKey))
            {
                spinStoryboard.Stop();
                ButtonIcon.Text = "📷";
                ShowSettings();
                screenshot.Dispose();
                return;
            }

            // Run OCR
            var ocrText = await OcrService.ExtractTextAsync(screenshot);

            // Convert to PNG bytes
            var pngBytes = ScreenCaptureService.BitmapToPng(screenshot);
            screenshot.Dispose();

            // Send to OpenAI or Azure based on config
            using var aiService = new OpenAiService(App.Settings);
            var response = await aiService.AnalyzeAsync(pngBytes, ocrText);

            // Stop spinner
            spinStoryboard.Stop();
            ButtonIcon.Text = "📷";
            ButtonIcon.RenderTransform = new System.Windows.Media.RotateTransform(0);

            // Show result
            var resultWindow = new ResultWindow(response, ocrText);
            resultWindow.Left = Left + 60;
            resultWindow.Top = Top;
            resultWindow.Show();
        }
        catch (Exception ex)
        {
            spinStoryboard.Stop();
            ButtonIcon.Text = "📷";
            ButtonIcon.RenderTransform = new System.Windows.Media.RotateTransform(0);
            Opacity = 0.85;

            MessageBox.Show(
                $"Error: {ex.Message}",
                "DesktopViewer",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            _isProcessing = false;
        }
    }

    private void ShowSettings()
    {
        var settingsWindow = new SettingsWindow();
        settingsWindow.Owner = this;
        settingsWindow.ShowDialog();
    }
}
