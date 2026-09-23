using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;
using BitmapDecoder = Windows.Graphics.Imaging.BitmapDecoder;

namespace DesktopViewer.Services;

/// <summary>
/// Extracts text from screenshots using the built-in Windows.Media.Ocr engine.
/// Works offline, fast, and private — no data leaves the device.
/// </summary>
public static class OcrService
{
    /// <summary>
    /// Extracts text from a System.Drawing.Bitmap using Windows OCR.
    /// </summary>
    public static async Task<string> ExtractTextAsync(Bitmap bitmap)
    {
        try
        {
            // Convert System.Drawing.Bitmap → byte[] (PNG)
            byte[] pngBytes;
            using (var ms = new MemoryStream())
            {
                bitmap.Save(ms, ImageFormat.Png);
                pngBytes = ms.ToArray();
            }

            // Create an InMemoryRandomAccessStream from the PNG bytes
            using var stream = new InMemoryRandomAccessStream();
            using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
            {
                writer.WriteBytes(pngBytes);
                await writer.StoreAsync();
                await writer.FlushAsync();
            }

            // Decode to SoftwareBitmap
            stream.Seek(0);
            var decoder = await BitmapDecoder.CreateAsync(stream);
            var softwareBitmap = await decoder.GetSoftwareBitmapAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied);

            // Run OCR
            var ocrEngine = OcrEngine.TryCreateFromUserProfileLanguages();
            if (ocrEngine == null)
            {
                return "[OCR engine not available — no language pack installed]";
            }

            var ocrResult = await ocrEngine.RecognizeAsync(softwareBitmap);

            return ocrResult.Text ?? "";
        }
        catch (Exception ex)
        {
            return $"[OCR failed: {ex.Message}]";
        }
    }
}
