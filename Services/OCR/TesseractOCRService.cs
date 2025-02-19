using System.Drawing;
using System.Drawing.Imaging;
using Tesseract;
using OMNI.Models;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace OMNI.Services.OCR
{
    public class TesseractOCRService : IOCRService
    {
        private readonly TesseractEngine engine;
        private OCRSettings settings;
        private bool disposed;
        private readonly object lockObject = new object();

        // Updated regex pattern to better handle negative coordinates
        private static readonly Regex CoordinatePattern = new(
            @"^Your location:\s*(-?\d{1,4}\.\d{2})\s+(-?\d{1,4}\.\d{2})\s+(-?\d{1,4}\.\d{2})\s+(\d{1,3})",
            RegexOptions.Compiled
        );

        public TesseractOCRService()
        {
            var tessDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");
            if (!Directory.Exists(tessDataPath))
            {
                throw new DirectoryNotFoundException($"Tesseract data directory not found at: {tessDataPath}");
            }

            try
            {
                engine = new TesseractEngine(tessDataPath, "eng", EngineMode.Default);
                settings = new OCRSettings
                {
                    AllowedCharacters = "Your location:-.0123456789 "  // Added minus sign to allowed characters for Halnir cave cords.
                };
                ConfigureEngine();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to initialize Tesseract: {ex.Message}", ex);
            }
        }

        private void ConfigureEngine()
        {
            engine.SetVariable("tessedit_char_whitelist", settings.AllowedCharacters);
            engine.SetVariable("classify_bln_numeric_mode", "1");
            engine.SetVariable("tessedit_ocr_engine_mode", "3");
        }

        public async Task<(Coordinates? Coordinates, string RawText)> ProcessImageAsync(Bitmap image)
        {
            if (disposed) throw new ObjectDisposedException(nameof(TesseractOCRService));

            return await Task.Run(() =>
            {
                lock (lockObject)
                {
                    try
                    {
                        using var processedImage = PreProcessImage(image);
                        using var memoryStream = new MemoryStream();
                        processedImage.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Png);
                        memoryStream.Position = 0;

                        using var pix = Pix.LoadFromMemory(memoryStream.ToArray());
                        using var page = engine.Process(pix);

                        string text = page.GetText()?.Trim().Replace("\n", " ").Replace("\r", "") ?? string.Empty;
                        Debug.WriteLine($"Raw OCR capture: '{text}'");  // Added quotes to see whitespace cause im blind

                        // Debug each character
                        Debug.WriteLine("Character by character analysis:");
                        foreach (char c in text)
                        {
                            Debug.WriteLine($"'{c}' - ASCII: {(int)c}");
                        }

                        var coordinates = ParseCoordinates(text);
                        return (coordinates, text);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"OCR Error: {ex}");
                        return (null, $"Error: {ex.Message}");
                    }
                }
            });
        }

        private unsafe Bitmap PreProcessImage(Bitmap original)
        {
            var processed = new Bitmap(original.Width, original.Height);

            try
            {
                var bmpData = original.LockBits(
                    new Rectangle(0, 0, original.Width, original.Height),
                    ImageLockMode.ReadOnly,
                    PixelFormat.Format32bppArgb);

                var processedData = processed.LockBits(
                    new Rectangle(0, 0, processed.Width, processed.Height),
                    ImageLockMode.WriteOnly,
                    PixelFormat.Format32bppArgb);

                try
                {
                    byte* ptr = (byte*)bmpData.Scan0;
                    byte* outPtr = (byte*)processedData.Scan0;
                    int remain = bmpData.Stride - (original.Width * 4);

                    for (int y = 0; y < original.Height; y++)
                    {
                        for (int x = 0; x < original.Width; x++)
                        {
                            float brightness =
                                ptr[0] * 0.11f +
                                ptr[1] * 0.59f +
                                ptr[2] * 0.3f;

                            byte color = (byte)(brightness > settings.BrightnessThreshold ? 255 : 0);
                            outPtr[0] = outPtr[1] = outPtr[2] = settings.InvertColors ? (byte)(255 - color) : color;
                            outPtr[3] = 255;

                            ptr += 4;
                            outPtr += 4;
                        }
                        ptr += remain;
                        outPtr += remain;
                    }
                }
                finally
                {
                    original.UnlockBits(bmpData);
                    processed.UnlockBits(processedData);
                }

                return processed;
            }
            catch
            {
                processed.Dispose();
                throw;
            }
        }

        private static Coordinates? ParseCoordinates(string text)
        {
            try
            {
                var match = CoordinatePattern.Match(text);
                Debug.WriteLine($"Attempting to parse coordinates from: {text}");

                if (match.Success &&
                    float.TryParse(match.Groups[1].Value, out float x) &&
                    float.TryParse(match.Groups[3].Value, out float y) &&  // Using third group for Y coordinate
                    float.TryParse(match.Groups[4].Value, out float heading))
                {
                    Debug.WriteLine($"Raw values - X: {match.Groups[1].Value}, Z: {match.Groups[2].Value}, Y: {match.Groups[3].Value}, H: {match.Groups[4].Value}");
                    Debug.WriteLine($"Parsed coordinates: X={x}, Y={y}, H={heading}");
                    return new Coordinates(x, y, heading);
                }

                Debug.WriteLine("Failed to parse coordinates");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error parsing coordinates: {ex.Message}");
                return null;
            }
        }

        public void UpdateSettings(OCRSettings newSettings)
        {
            settings = newSettings;
            ConfigureEngine();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    lock (lockObject)
                    {
                        engine?.Dispose();
                    }
                }
                disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}