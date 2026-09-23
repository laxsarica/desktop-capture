using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using DesktopViewer.Models;

namespace DesktopViewer;

public partial class ResultWindow : Window
{
    [DllImport("user32.dll")]
    public static extern uint SetWindowDisplayAffinity(IntPtr hwnd, uint dwAffinity);

    const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;

    private readonly string _ocrText;
    private bool _ocrVisible;
    private readonly DispatcherTimer _autoDismissTimer;

    public ResultWindow(AiResponse response, string ocrText)
    {
        InitializeComponent();

        Loaded += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE);
        };

        _ocrText = ocrText;

        // Populate answer
        AnswerText.Text = response.Answer;

        // Populate detected question
        if (!string.IsNullOrWhiteSpace(response.DetectedQuestion))
        {
            QuestionPanel.Visibility = Visibility.Visible;
            QuestionText.Text = response.DetectedQuestion;
        }

        // Populate confidence
        if (!string.IsNullOrWhiteSpace(response.Confidence))
        {
            ConfidencePanel.Visibility = Visibility.Visible;
            ConfidenceText.Text = response.Confidence;
            ConfidenceText.Foreground = response.Confidence?.ToLower() switch
            {
                "high" => (Brush)FindResource("SuccessBrush"),
                "medium" => (Brush)FindResource("WarningBrush"),
                _ => (Brush)FindResource("TextSecondaryBrush")
            };
        }

        // Populate OCR text
        OcrText.Text = string.IsNullOrWhiteSpace(ocrText) ? "(No text detected)" : ocrText;

        // Auto-dismiss after 60 seconds
        _autoDismissTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(60)
        };
        _autoDismissTimer.Tick += (_, _) =>
        {
            _autoDismissTimer.Stop();
            Close();
        };
        _autoDismissTimer.Start();

        // Reset timer on mouse interaction
        MouseEnter += (_, _) => _autoDismissTimer.Stop();
        MouseLeave += (_, _) => _autoDismissTimer.Start();
    }

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        _autoDismissTimer.Stop();
        Close();
    }

    private void ToggleOcr_Click(object sender, RoutedEventArgs e)
    {
        _ocrVisible = !_ocrVisible;
        OcrPanel.Visibility = _ocrVisible ? Visibility.Visible : Visibility.Collapsed;
        ToggleOcrBtn.Content = _ocrVisible ? "📝 Hide OCR Text" : "📝 Show OCR Text";
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(AnswerText.Text);
        }
        catch { }
    }
}
