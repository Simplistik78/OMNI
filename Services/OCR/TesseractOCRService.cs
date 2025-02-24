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

        // Updated regex pattern to handle both comma and period decimal separators
        // More flexible to handle: 
        // - Different numbers of decimal places (1-3)
        // - Better space handling between coordinates
        private static readonly Regex CoordinatePattern = new(
            @"^Your location:\s*(-?\d{1,4}[.,]\d{1,3})\s+(-?\d{1,4}[.,]\d{1,3})\s+(-?\d{1,4}[.,]\d{1,3})\s+(-?\d{1,3})",
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
                    AllowedCharacters = "Your location:-.0123456789, "  // Added comma to allowed characters
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

                if (match.Success)
                {
                    Debug.WriteLine("==================== OCR COORDINATE MATCH DEBUG ====================");
                    Debug.WriteLine($"Full match: {match.Value}");
                    Debug.WriteLine($"Group 1 (X): {match.Groups[1].Value}");
                    Debug.WriteLine($"Group 2 (Z): {match.Groups[2].Value}");
                    Debug.WriteLine($"Group 3 (Y): {match.Groups[3].Value}");
                    Debug.WriteLine($"Group 4 (H): {match.Groups[4].Value}");
                    Debug.WriteLine("System culture: " + System.Globalization.CultureInfo.CurrentCulture.Name);
                    Debug.WriteLine("System decimal separator: " + System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator);

                    // Normalize all values to use period decimal separator
                    string NormalizeDecimal(string input) => input.Replace(',', '.');

                    string xStr = NormalizeDecimal(match.Groups[1].Value);
                    string zStr = NormalizeDecimal(match.Groups[2].Value); // Not used but parse for debug
                    string yStr = NormalizeDecimal(match.Groups[3].Value);
                    string headingStr = match.Groups[4].Value;

                    Debug.WriteLine($"Normalized values - X: {xStr}, Z: {zStr}, Y: {yStr}, H: {headingStr}");

                    // Try each field separately with detailed logging
                    bool xOk = float.TryParse(xStr, System.Globalization.NumberStyles.Float,
                                             System.Globalization.CultureInfo.InvariantCulture, out float x);
                    bool zOk = float.TryParse(zStr, System.Globalization.NumberStyles.Float,
                                             System.Globalization.CultureInfo.InvariantCulture, out float z);
                    bool yOk = float.TryParse(yStr, System.Globalization.NumberStyles.Float,
                                             System.Globalization.CultureInfo.InvariantCulture, out float y);
                    bool hOk = float.TryParse(headingStr, System.Globalization.NumberStyles.Float,
                                             System.Globalization.CultureInfo.InvariantCulture, out float heading);

                    Debug.WriteLine($"Parse results - X: {xOk} ({x}), Z: {zOk} ({z}), Y: {yOk} ({y}), H: {hOk} ({heading})");

                    if (xOk && zOk && yOk && hOk)
                    {
                        var coordinates = new Coordinates(x, y, heading);
                        Debug.WriteLine($"FINAL OCR Coordinates: X={coordinates.X}, Y={coordinates.Y}, H={coordinates.Heading}");
                        return coordinates;
                    }
                    else
                    {
                        // Try alternate parsing methods for diagnostic purposes
                        Debug.WriteLine("Attempting alternate OCR parsing methods:");

                        // Try with current culture
                        Debug.WriteLine("--- Current Culture ---");
                        float.TryParse(yStr, out float yCurrentCulture);
                        Debug.WriteLine($"Y with current culture: {yCurrentCulture}");

                        // Try with German culture
                        var germanCulture = new System.Globalization.CultureInfo("de-DE");
                        Debug.WriteLine("--- German Culture ---");
                        float.TryParse(yStr, System.Globalization.NumberStyles.Float, germanCulture, out float yGerman);
                        Debug.WriteLine($"Y with German culture: {yGerman}");

                        // Try Double parsing (sometimes has better tolerance)
                        Debug.WriteLine("--- Using Double ---");
                        if (double.TryParse(yStr, System.Globalization.NumberStyles.Float,
                                           System.Globalization.CultureInfo.InvariantCulture, out double yDouble))
                        {
                            Debug.WriteLine($"Y as double: {yDouble}");
                        }

                        // Try manual parsing as fallback
                        Debug.WriteLine("--- Manual Parsing ---");
                        try
                        {
                            string[] parts = yStr.Split('.');
                            if (parts.Length == 2)
                            {
                                int integerPart = int.Parse(parts[0]);
                                int decimalPart = int.Parse(parts[1]);
                                float manualResult = integerPart + (decimalPart / 100.0f);
                                Debug.WriteLine($"Y manual parse: {manualResult}");

                                // If manual parsing succeeds where automatic fails,
                                // and we got valid X and heading, create coordinates
                                if (!yOk && xOk && hOk)
                                {
                                    Debug.WriteLine("Using manually parsed Y value as fallback");
                                    var coordinates = new Coordinates(x, manualResult, heading);
                                    Debug.WriteLine($"FALLBACK OCR Coordinates: X={coordinates.X}, Y={coordinates.Y}, H={coordinates.Heading}");
                                    return coordinates;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Manual parsing failed: {ex.Message}");
                        }

                        Debug.WriteLine("======== END OCR COORDINATE DEBUG ========");
                        Debug.WriteLine($"Failed to parse coordinates - X:{xStr}, Z:{zStr}, Y:{yStr}, H:{headingStr}");
                    }
                }
                else
                {
                    Debug.WriteLine("Failed to parse coordinates - regex did not match");
                }
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