using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace DesktopViewer.Services;

/// <summary>
/// Captures the screen or a selected region using GDI+.
/// DPI-aware via SetProcessDPIAware for correct scaling on high-DPI displays.
/// </summary>
public static class ScreenCaptureService
{
    [DllImport("user32.dll")]
    private static extern bool SetProcessDPIAware();

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    // SM_XVIRTUALSCREEN, SM_YVIRTUALSCREEN, SM_CXVIRTUALSCREEN, SM_CYVIRTUALSCREEN
    private const int SM_XVIRTUALSCREEN = 76;
    private const int SM_YVIRTUALSCREEN = 77;
    private const int SM_CXVIRTUALSCREEN = 78;
    private const int SM_CYVIRTUALSCREEN = 79;

    static ScreenCaptureService()
    {
        // Ensure DPI awareness for correct capture on scaled displays
        try { SetProcessDPIAware(); } catch { /* Best effort */ }
    }

    /// <summary>
    /// Captures the entire virtual screen (all monitors).
    /// </summary>
    public static Bitmap CaptureFullScreen()
    {
        int left = GetSystemMetrics(SM_XVIRTUALSCREEN);
        int top = GetSystemMetrics(SM_YVIRTUALSCREEN);
        int width = GetSystemMetrics(SM_CXVIRTUALSCREEN);
        int height = GetSystemMetrics(SM_CYVIRTUALSCREEN);

        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(left, top, 0, 0, new Size(width, height));
        return bitmap;
    }

    /// <summary>
    /// Captures a specific region of the screen.
    /// </summary>
    public static Bitmap CaptureRegion(Rectangle region)
    {
        var bitmap = new Bitmap(region.Width, region.Height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(region.Left, region.Top, 0, 0, region.Size);
        return bitmap;
    }

    /// <summary>
    /// Converts a Bitmap to a PNG byte array for API transmission.
    /// </summary>
    public static byte[] BitmapToPng(Bitmap bitmap)
    {
        using var ms = new MemoryStream();
        bitmap.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }
}
