using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;

namespace DesktopViewer;

public partial class RegionSelectWindow : Window
{
    [DllImport("user32.dll")]
    public static extern uint SetWindowDisplayAffinity(IntPtr hwnd, uint dwAffinity);

    const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;

    private System.Windows.Point _startPoint;
    private bool _isSelecting;

    /// <summary>
    /// The selected screen region in physical (pixel) coordinates.
    /// Null if the user cancelled.
    /// </summary>
    public System.Drawing.Rectangle? SelectedRegion { get; private set; }

    public RegionSelectWindow()
    {
        InitializeComponent();

        Loaded += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE);
            SelectionCanvas.Focus();
        };
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                SelectedRegion = null;
                Close();
            }
        };
    }

    private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _startPoint = e.GetPosition(SelectionCanvas);
        _isSelecting = true;

        Canvas.SetLeft(SelectionRect, _startPoint.X);
        Canvas.SetTop(SelectionRect, _startPoint.Y);
        SelectionRect.Width = 0;
        SelectionRect.Height = 0;
        SelectionRect.Visibility = Visibility.Visible;
        SizeLabel.Visibility = Visibility.Visible;

        SelectionCanvas.CaptureMouse();
    }

    private void Canvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isSelecting) return;

        var currentPoint = e.GetPosition(SelectionCanvas);

        var x = Math.Min(_startPoint.X, currentPoint.X);
        var y = Math.Min(_startPoint.Y, currentPoint.Y);
        var w = Math.Abs(currentPoint.X - _startPoint.X);
        var h = Math.Abs(currentPoint.Y - _startPoint.Y);

        Canvas.SetLeft(SelectionRect, x);
        Canvas.SetTop(SelectionRect, y);
        SelectionRect.Width = w;
        SelectionRect.Height = h;

        // Update size label
        SizeLabel.Text = $" {(int)w} × {(int)h} ";
        Canvas.SetLeft(SizeLabel, x);
        Canvas.SetTop(SizeLabel, y - 22);
    }

    private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isSelecting) return;

        _isSelecting = false;
        SelectionCanvas.ReleaseMouseCapture();

        var currentPoint = e.GetPosition(SelectionCanvas);

        // Convert WPF device-independent coordinates to physical pixel coordinates
        var source = PresentationSource.FromVisual(this);
        var dpiScaleX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        var dpiScaleY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;

        var x = (int)(Math.Min(_startPoint.X, currentPoint.X) * dpiScaleX);
        var y = (int)(Math.Min(_startPoint.Y, currentPoint.Y) * dpiScaleY);
        var w = (int)(Math.Abs(currentPoint.X - _startPoint.X) * dpiScaleX);
        var h = (int)(Math.Abs(currentPoint.Y - _startPoint.Y) * dpiScaleY);

        if (w > 10 && h > 10)
        {
            SelectedRegion = new System.Drawing.Rectangle(x, y, w, h);
        }
        else
        {
            SelectedRegion = null;
        }

        Close();
    }
}
