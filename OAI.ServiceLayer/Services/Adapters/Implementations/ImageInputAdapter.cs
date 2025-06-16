using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OAI.Core.Interfaces.Adapters;
using OAI.Core.Interfaces.Tools;
using OAI.ServiceLayer.Services.Adapters.Base;

namespace OAI.ServiceLayer.Services.Adapters.Implementations
{
    /// <summary>
    /// Image input adapter for reading images with metadata extraction
    /// </summary>
    public class ImageInputAdapter : BaseInputAdapter
    {
        public override string Id => "image_input";
        public override string Name => "Čtení obrázků";
        public override string Description => "Čtení obrázků s extrakcí metadat a základních informací";
        public override string Version => "1.0.0";
        public override string Category => "Image";

        private static readonly string[] SupportedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".webp" };
        
        public ImageInputAdapter(ILogger<ImageInputAdapter> logger) : base(logger)
        {
        }

        protected override void InitializeParameters()
        {
            AddParameter(new SimpleAdapterParameter
            {
                Name = "imagePath",
                DisplayName = "Cesta k obrázku",
                Description = "Absolutní nebo relativní cesta k obrázku nebo složce s obrázky",
                Type = ToolParameterType.String,
                IsRequired = true,
                IsCritical = true,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.File,
                    HelpText = "Zadejte cestu k obrázku nebo složce"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "includeImageData",
                DisplayName = "Zahrnout data obrázku",
                Description = "Zahrnout binární data obrázku do výstupu",
                Type = ToolParameterType.Boolean,
                IsRequired = false,
                DefaultValue = false,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Checkbox,
                    HelpText = "Pokud je vypnuto, vrátí se pouze metadata"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "includeBase64",
                DisplayName = "Zahrnout Base64",
                Description = "Zahrnout obrázek jako Base64 string",
                Type = ToolParameterType.Boolean,
                IsRequired = false,
                DefaultValue = true,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Checkbox,
                    HelpText = "Užitečné pro přenos obrázku v JSON"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "extractExif",
                DisplayName = "Extrahovat EXIF",
                Description = "Extrahovat EXIF metadata z obrázku",
                Type = ToolParameterType.Boolean,
                IsRequired = false,
                DefaultValue = true,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Checkbox,
                    HelpText = "EXIF obsahuje informace o fotoaparátu, GPS souřadnice, atd."
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "generateThumbnail",
                DisplayName = "Vytvořit náhled",
                Description = "Vytvořit náhled obrázku",
                Type = ToolParameterType.Boolean,
                IsRequired = false,
                DefaultValue = false,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Checkbox,
                    HelpText = "Vytvoří zmenšený náhled obrázku"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "thumbnailSize",
                DisplayName = "Velikost náhledu",
                Description = "Maximální velikost náhledu v pixelech",
                Type = ToolParameterType.Integer,
                IsRequired = false,
                DefaultValue = 200,
                Validation = new SimpleParameterValidation
                {
                    MinValue = 50,
                    MaxValue = 1000
                },
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Number,
                    HelpText = "Náhled bude proporcionálně zmenšen"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "pattern",
                DisplayName = "Vzor souborů",
                Description = "Vzor pro hledání obrázků (wildcards)",
                Type = ToolParameterType.String,
                IsRequired = false,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Text,
                    Placeholder = "*.jpg, *.png",
                    HelpText = "Použijte pro čtení více obrázků najednou"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "recursive",
                DisplayName = "Rekurzivně",
                Description = "Hledat obrázky i v podsložkách",
                Type = ToolParameterType.Boolean,
                IsRequired = false,
                DefaultValue = false,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Checkbox
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "analyzeColors",
                DisplayName = "Analyzovat barvy",
                Description = "Analyzovat dominantní barvy v obrázku",
                Type = ToolParameterType.Boolean,
                IsRequired = false,
                DefaultValue = false,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Checkbox,
                    HelpText = "Extrahuje nejčastější barvy z obrázku"
                }
            });
        }

        protected override async Task<IAdapterResult> ExecuteReadAsync(
            Dictionary<string, object> configuration,
            string executionId,
            CancellationToken cancellationToken)
        {
            var metrics = new AdapterMetrics();
            var startTime = DateTime.UtcNow;

            try
            {
                var imagePath = GetParameter<string>(configuration, "imagePath");
                var includeImageData = GetParameter<bool>(configuration, "includeImageData", false);
                var includeBase64 = GetParameter<bool>(configuration, "includeBase64", true);
                var extractExif = GetParameter<bool>(configuration, "extractExif", true);
                var generateThumbnail = GetParameter<bool>(configuration, "generateThumbnail", false);
                var thumbnailSize = GetParameter<int>(configuration, "thumbnailSize", 200);
                var pattern = GetParameter<string>(configuration, "pattern", string.Empty);
                var recursive = GetParameter<bool>(configuration, "recursive", false);
                var analyzeColors = GetParameter<bool>(configuration, "analyzeColors", false);

                var imageFiles = new List<FileInfo>();
                
                // Determine image files to process
                if (!string.IsNullOrEmpty(pattern))
                {
                    var directory = Path.GetDirectoryName(imagePath) ?? ".";
                    var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                    var files = Directory.GetFiles(directory, pattern, searchOption)
                        .Where(f => SupportedExtensions.Contains(Path.GetExtension(f).ToLower()))
                        .Select(f => new FileInfo(f));
                    imageFiles.AddRange(files);
                }
                else if (File.Exists(imagePath))
                {
                    var ext = Path.GetExtension(imagePath).ToLower();
                    if (!SupportedExtensions.Contains(ext))
                    {
                        return CreateErrorResult(executionId, startTime, $"Unsupported image format: {ext}");
                    }
                    imageFiles.Add(new FileInfo(imagePath));
                }
                else if (Directory.Exists(imagePath))
                {
                    var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                    var files = SupportedExtensions.SelectMany(ext => 
                        Directory.GetFiles(imagePath, $"*{ext}", searchOption)).Select(f => new FileInfo(f));
                    imageFiles.AddRange(files);
                }
                else
                {
                    return CreateErrorResult(executionId, startTime, $"Image file or directory not found: {imagePath}");
                }

                var results = new List<Dictionary<string, object>>();

                foreach (var imageFile in imageFiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var imageData = await ProcessImageAsync(imageFile, includeImageData, includeBase64, 
                            extractExif, generateThumbnail, thumbnailSize, analyzeColors, cancellationToken);
                        
                        results.Add(imageData);
                        metrics.ItemsProcessed++;
                        metrics.BytesProcessed += imageFile.Length;
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning(ex, "Failed to process image: {ImagePath}", imageFile.FullName);
                        
                        // Add error entry for this image
                        results.Add(new Dictionary<string, object>
                        {
                            ["fileName"] = imageFile.Name,
                            ["filePath"] = imageFile.FullName,
                            ["error"] = ex.Message,
                            ["processed"] = false
                        });
                    }
                }

                metrics.ProcessingTime = DateTime.UtcNow - startTime;
                metrics.ThroughputItemsPerSecond = metrics.ItemsProcessed / Math.Max(1, metrics.ProcessingTime.TotalSeconds);
                metrics.ThroughputMBPerSecond = (metrics.BytesProcessed / 1024.0 / 1024.0) / Math.Max(1, metrics.ProcessingTime.TotalSeconds);

                // Create schema
                var schema = new ImageDataSchema
                {
                    Id = "image_data",
                    Name = "Image Data",
                    Description = $"Data from {results.Count} image(s)",
                    Fields = new List<SchemaField>
                    {
                        new SchemaField { Name = "fileName", Type = "string", IsRequired = true },
                        new SchemaField { Name = "filePath", Type = "string", IsRequired = true },
                        new SchemaField { Name = "width", Type = "number", IsRequired = true },
                        new SchemaField { Name = "height", Type = "number", IsRequired = true },
                        new SchemaField { Name = "format", Type = "string", IsRequired = true },
                        new SchemaField { Name = "fileSize", Type = "number", IsRequired = true },
                        new SchemaField { Name = "aspectRatio", Type = "number", IsRequired = false },
                        new SchemaField { Name = "colorDepth", Type = "number", IsRequired = false },
                        new SchemaField { Name = "hasAlpha", Type = "boolean", IsRequired = false }
                    }
                };

                Logger.LogInformation("Successfully processed {Count} image(s), {Bytes} bytes", 
                    metrics.ItemsProcessed, metrics.BytesProcessed);

                return CreateSuccessResult(executionId, startTime, results, metrics, schema, results.Take(3).ToList());
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error reading images");
                return CreateExceptionResult(executionId, startTime, ex);
            }
        }

        private async Task<Dictionary<string, object>> ProcessImageAsync(
            FileInfo imageFile,
            bool includeImageData,
            bool includeBase64,
            bool extractExif,
            bool generateThumbnail,
            int thumbnailSize,
            bool analyzeColors,
            CancellationToken cancellationToken)
        {
            var result = new Dictionary<string, object>
            {
                ["fileName"] = imageFile.Name,
                ["filePath"] = imageFile.FullName,
                ["fileSize"] = imageFile.Length,
                ["createdAt"] = imageFile.CreationTimeUtc,
                ["modifiedAt"] = imageFile.LastWriteTimeUtc,
                ["extension"] = imageFile.Extension.ToLower(),
                ["processed"] = true
            };

            byte[] imageBytes = null;
            if (includeImageData || includeBase64 || generateThumbnail)
            {
                imageBytes = await File.ReadAllBytesAsync(imageFile.FullName, cancellationToken);
            }

            try
            {
                using (var image = Image.FromFile(imageFile.FullName))
                {
                    // Basic image properties
                    result["width"] = image.Width;
                    result["height"] = image.Height;
                    result["aspectRatio"] = Math.Round((double)image.Width / image.Height, 2);
                    result["format"] = image.RawFormat.ToString();
                    result["pixelFormat"] = image.PixelFormat.ToString();
                    result["colorDepth"] = Image.GetPixelFormatSize(image.PixelFormat);
                    result["hasAlpha"] = Image.IsAlphaPixelFormat(image.PixelFormat);
                    result["horizontalResolution"] = Math.Round(image.HorizontalResolution, 2);
                    result["verticalResolution"] = Math.Round(image.VerticalResolution, 2);

                    // Include raw image data
                    if (includeImageData && imageBytes != null)
                    {
                        result["imageData"] = imageBytes;
                    }

                    // Include Base64 representation
                    if (includeBase64 && imageBytes != null)
                    {
                        var mimeType = GetMimeType(imageFile.Extension);
                        result["base64"] = Convert.ToBase64String(imageBytes);
                        result["dataUrl"] = $"data:{mimeType};base64,{result["base64"]}";
                        result["mimeType"] = mimeType;
                    }

                    // Extract EXIF data
                    if (extractExif)
                    {
                        result["exifData"] = ExtractExifData(image);
                    }

                    // Generate thumbnail
                    if (generateThumbnail)
                    {
                        result["thumbnail"] = await GenerateThumbnailAsync(image, thumbnailSize, cancellationToken);
                    }

                    // Analyze colors
                    if (analyzeColors)
                    {
                        result["colorAnalysis"] = AnalyzeColors(image);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to process image metadata for: {ImagePath}", imageFile.FullName);
                result["processingError"] = ex.Message;
            }

            return result;
        }

        private Dictionary<string, object> ExtractExifData(Image image)
        {
            var exifData = new Dictionary<string, object>();

            try
            {
                foreach (PropertyItem prop in image.PropertyItems)
                {
                    var value = GetExifValue(prop);
                    var name = GetExifPropertyName(prop.Id);
                    if (!string.IsNullOrEmpty(name))
                    {
                        exifData[name] = value;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "Error extracting EXIF data");
                exifData["extractionError"] = ex.Message;
            }

            return exifData;
        }

        private async Task<Dictionary<string, object>> GenerateThumbnailAsync(Image originalImage, int maxSize, CancellationToken cancellationToken)
        {
            try
            {
                var thumbnail = GenerateThumbnail(originalImage, maxSize);
                using (var ms = new MemoryStream())
                {
                    thumbnail.Save(ms, ImageFormat.Jpeg);
                    var thumbnailBytes = ms.ToArray();
                    
                    return new Dictionary<string, object>
                    {
                        ["width"] = thumbnail.Width,
                        ["height"] = thumbnail.Height,
                        ["base64"] = Convert.ToBase64String(thumbnailBytes),
                        ["dataUrl"] = $"data:image/jpeg;base64,{Convert.ToBase64String(thumbnailBytes)}",
                        ["size"] = thumbnailBytes.Length
                    };
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to generate thumbnail");
                return new Dictionary<string, object> { ["error"] = ex.Message };
            }
        }

        private Image GenerateThumbnail(Image original, int maxSize)
        {
            var ratio = Math.Min((double)maxSize / original.Width, (double)maxSize / original.Height);
            var newWidth = (int)(original.Width * ratio);
            var newHeight = (int)(original.Height * ratio);
            
            return original.GetThumbnailImage(newWidth, newHeight, null, IntPtr.Zero);
        }

        private Dictionary<string, object> AnalyzeColors(Image image)
        {
            try
            {
                var colorCounts = new Dictionary<Color, int>();
                using (var bitmap = new Bitmap(image))
                {
                    // Sample colors (for performance, don't check every pixel on large images)
                    var stepX = Math.Max(1, bitmap.Width / 100);
                    var stepY = Math.Max(1, bitmap.Height / 100);
                    
                    for (int x = 0; x < bitmap.Width; x += stepX)
                    {
                        for (int y = 0; y < bitmap.Height; y += stepY)
                        {
                            var color = bitmap.GetPixel(x, y);
                            // Quantize color to reduce noise
                            var quantizedColor = Color.FromArgb(
                                (color.R / 32) * 32,
                                (color.G / 32) * 32,
                                (color.B / 32) * 32);
                            
                            colorCounts[quantizedColor] = colorCounts.GetValueOrDefault(quantizedColor, 0) + 1;
                        }
                    }
                }

                var dominantColors = colorCounts
                    .OrderByDescending(kvp => kvp.Value)
                    .Take(10)
                    .Select(kvp => new Dictionary<string, object>
                    {
                        ["color"] = $"#{kvp.Key.R:X2}{kvp.Key.G:X2}{kvp.Key.B:X2}",
                        ["rgb"] = new { r = kvp.Key.R, g = kvp.Key.G, b = kvp.Key.B },
                        ["count"] = kvp.Value,
                        ["percentage"] = Math.Round((double)kvp.Value / colorCounts.Values.Sum() * 100, 2)
                    })
                    .ToList();

                return new Dictionary<string, object>
                {
                    ["dominantColors"] = dominantColors,
                    ["totalSamplePoints"] = colorCounts.Values.Sum(),
                    ["uniqueColors"] = colorCounts.Count
                };
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to analyze colors");
                return new Dictionary<string, object> { ["error"] = ex.Message };
            }
        }

        private string GetMimeType(string extension)
        {
            return extension.ToLower() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".tiff" => "image/tiff",
                ".webp" => "image/webp",
                _ => "application/octet-stream"
            };
        }

        private object GetExifValue(PropertyItem prop)
        {
            switch (prop.Type)
            {
                case 1: // Byte
                    return prop.Value[0];
                case 2: // ASCII
                    return System.Text.Encoding.ASCII.GetString(prop.Value).TrimEnd('\0');
                case 3: // Short
                    return BitConverter.ToInt16(prop.Value, 0);
                case 4: // Long
                    return BitConverter.ToInt32(prop.Value, 0);
                case 5: // Rational
                    var numerator = BitConverter.ToInt32(prop.Value, 0);
                    var denominator = BitConverter.ToInt32(prop.Value, 4);
                    return denominator != 0 ? (double)numerator / denominator : 0;
                default:
                    return Convert.ToBase64String(prop.Value);
            }
        }

        private string GetExifPropertyName(int id)
        {
            return id switch
            {
                0x010E => "ImageDescription",
                0x010F => "Make",
                0x0110 => "Model",
                0x0112 => "Orientation",
                0x011A => "XResolution",
                0x011B => "YResolution",
                0x0128 => "ResolutionUnit",
                0x0132 => "DateTime",
                0x829A => "ExposureTime",
                0x829D => "FNumber",
                0x8822 => "ExposureProgram",
                0x8827 => "ISOSpeedRatings",
                0x9000 => "ExifVersion",
                0x9003 => "DateTimeOriginal",
                0x9004 => "DateTimeDigitized",
                0x920A => "FocalLength",
                0x0001 => "GPSLatitudeRef",
                0x0002 => "GPSLatitude",
                0x0003 => "GPSLongitudeRef",
                0x0004 => "GPSLongitude",
                _ => null
            };
        }

        protected override async Task PerformSourceValidationAsync(
            Dictionary<string, object> configuration,
            CancellationToken cancellationToken)
        {
            var imagePath = GetParameter<string>(configuration, "imagePath");
            
            if (string.IsNullOrEmpty(imagePath))
                throw new InvalidOperationException("Image path is required");

            if (!File.Exists(imagePath) && !Directory.Exists(imagePath))
            {
                var directory = Path.GetDirectoryName(imagePath);
                if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                {
                    throw new FileNotFoundException($"Path not found: {imagePath}");
                }
            }

            await Task.CompletedTask;
        }

        public override IReadOnlyList<IAdapterSchema> GetOutputSchemas()
        {
            return new List<IAdapterSchema>
            {
                new ImageDataSchema
                {
                    Id = "image_file",
                    Name = "Image File Data",
                    Description = "Complete image data with metadata",
                    JsonSchema = @"{
                        ""type"": ""object"",
                        ""properties"": {
                            ""fileName"": { ""type"": ""string"" },
                            ""filePath"": { ""type"": ""string"" },
                            ""width"": { ""type"": ""number"" },
                            ""height"": { ""type"": ""number"" },
                            ""format"": { ""type"": ""string"" },
                            ""aspectRatio"": { ""type"": ""number"" },
                            ""fileSize"": { ""type"": ""number"" },
                            ""base64"": { ""type"": ""string"" },
                            ""dataUrl"": { ""type"": ""string"" },
                            ""exifData"": { ""type"": ""object"" },
                            ""thumbnail"": { ""type"": ""object"" },
                            ""colorAnalysis"": { ""type"": ""object"" }
                        },
                        ""required"": [""fileName"", ""filePath"", ""width"", ""height"", ""format""]
                    }",
                    ExampleData = new Dictionary<string, object>
                    {
                        ["fileName"] = "photo.jpg",
                        ["filePath"] = "/path/to/photo.jpg",
                        ["width"] = 1920,
                        ["height"] = 1080,
                        ["format"] = "Jpeg",
                        ["aspectRatio"] = 1.78,
                        ["fileSize"] = 524288
                    }
                }
            };
        }

        public override AdapterCapabilities GetCapabilities()
        {
            return new AdapterCapabilities
            {
                SupportsStreaming = false,
                SupportsPartialData = true,
                SupportsBatchProcessing = true,
                SupportsTransactions = false,
                RequiresAuthentication = false,
                MaxDataSizeBytes = 50 * 1024 * 1024, // 50 MB per image
                MaxConcurrentOperations = 5,
                SupportedFormats = new List<string> { "JPEG", "PNG", "GIF", "BMP", "TIFF", "WebP" },
                SupportedEncodings = new List<string> { "Binary", "Base64" },
                CustomCapabilities = new Dictionary<string, object>
                {
                    ["supportsMetadataExtraction"] = true,
                    ["supportsExifData"] = true,
                    ["supportsThumbnailGeneration"] = true,
                    ["supportsColorAnalysis"] = true,
                    ["supportsWildcards"] = true,
                    ["supportsRecursive"] = true,
                    ["maxImageSize"] = 50 * 1024 * 1024
                }
            };
        }

        protected override async Task PerformHealthCheckAsync()
        {
            // Test basic image processing capabilities
            try
            {
                using (var testImage = new Bitmap(1, 1))
                {
                    testImage.SetPixel(0, 0, Color.Red);
                    // Test successful if no exception
                }
            }
            catch
            {
                throw new InvalidOperationException("Image processing capabilities not available");
            }
            
            await Task.CompletedTask;
        }
    }

    /// <summary>
    /// Schema implementation for image data
    /// </summary>
    internal class ImageDataSchema : IAdapterSchema
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string JsonSchema { get; set; }
        public object ExampleData { get; set; }
        public IReadOnlyList<SchemaField> Fields { get; set; } = new List<SchemaField>();
    }
}