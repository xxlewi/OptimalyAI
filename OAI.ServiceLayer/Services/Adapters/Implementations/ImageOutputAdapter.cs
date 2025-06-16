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
    /// Image output adapter for saving images with format conversion and optimization
    /// </summary>
    public class ImageOutputAdapter : BaseOutputAdapter
    {
        public override string Id => "image_output";
        public override string Name => "Zápis obrázků";
        public override string Description => "Ukládání obrázků s možností konverze formátu a optimalizace";
        public override string Version => "1.0.0";
        public override string Category => "Image";
        public override AdapterType Type => AdapterType.Output;

        private static readonly Dictionary<string, ImageFormat> SupportedFormats = new()
        {
            [".jpg"] = ImageFormat.Jpeg,
            [".jpeg"] = ImageFormat.Jpeg,
            [".png"] = ImageFormat.Png,
            [".gif"] = ImageFormat.Gif,
            [".bmp"] = ImageFormat.Bmp,
            [".tiff"] = ImageFormat.Tiff
        };

        public ImageOutputAdapter(ILogger<ImageOutputAdapter> logger) : base(logger)
        {
        }

        protected override void InitializeParameters()
        {
            AddParameter(new SimpleAdapterParameter
            {
                Name = "outputPath",
                DisplayName = "Výstupní cesta",
                Description = "Cesta k výstupnímu souboru nebo složce",
                Type = ToolParameterType.String,
                IsRequired = true,
                IsCritical = true,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.File,
                    HelpText = "Zadejte cestu včetně názvu souboru a přípony"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "format",
                DisplayName = "Výstupní formát",
                Description = "Formát výstupního obrázku",
                Type = ToolParameterType.String,
                IsRequired = false,
                DefaultValue = "auto",
                Validation = new SimpleParameterValidation
                {
                    AllowedValues = new[] { "auto", "jpeg", "png", "gif", "bmp", "tiff" }
                },
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Select,
                    HelpText = "auto = automatická detekce dle přípony souboru"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "quality",
                DisplayName = "Kvalita JPEG",
                Description = "Kvalita komprese pro JPEG formát (1-100)",
                Type = ToolParameterType.Integer,
                IsRequired = false,
                DefaultValue = 85,
                Validation = new SimpleParameterValidation
                {
                    MinValue = 1,
                    MaxValue = 100
                },
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Range,
                    HelpText = "Vyšší hodnota = lepší kvalita, větší soubor"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "resizeWidth",
                DisplayName = "Nová šířka",
                Description = "Nová šířka obrázku v pixelech (volitelné)",
                Type = ToolParameterType.Integer,
                IsRequired = false,
                Validation = new SimpleParameterValidation
                {
                    MinValue = 1,
                    MaxValue = 10000
                },
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Number,
                    HelpText = "Ponechte prázdné pro zachování původní velikosti"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "resizeHeight",
                DisplayName = "Nová výška",
                Description = "Nová výška obrázku v pixelech (volitelné)",
                Type = ToolParameterType.Integer,
                IsRequired = false,
                Validation = new SimpleParameterValidation
                {
                    MinValue = 1,
                    MaxValue = 10000
                },
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Number,
                    HelpText = "Ponechte prázdné pro zachování původní velikosti"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "maintainAspectRatio",
                DisplayName = "Zachovat proporce",
                Description = "Zachovat poměr stran při změně velikosti",
                Type = ToolParameterType.Boolean,
                IsRequired = false,
                DefaultValue = true,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Checkbox,
                    HelpText = "Při zapnutí se použije menší z nových rozměrů"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "resizeMode",
                DisplayName = "Režim změny velikosti",
                Description = "Jak změnit velikost obrázku",
                Type = ToolParameterType.String,
                IsRequired = false,
                DefaultValue = "fit",
                Validation = new SimpleParameterValidation
                {
                    AllowedValues = new[] { "fit", "fill", "crop", "stretch" }
                },
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Select,
                    HelpText = "fit=vejde se celý, fill=vyplní celý prostor, crop=ořízne, stretch=roztáhne"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "backgroundColor",
                DisplayName = "Barva pozadí",
                Description = "Barva pozadí pro průhledné oblasti (hex)",
                Type = ToolParameterType.String,
                IsRequired = false,
                DefaultValue = "#FFFFFF",
                Validation = new SimpleParameterValidation
                {
                    Pattern = "^#[0-9A-Fa-f]{6}$"
                },
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Color,
                    Placeholder = "#FFFFFF",
                    HelpText = "Použije se pro JPEG a jiné formáty bez průhlednosti"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "createDirectory",
                DisplayName = "Vytvořit složku",
                Description = "Vytvořit výstupní složku pokud neexistuje",
                Type = ToolParameterType.Boolean,
                IsRequired = false,
                DefaultValue = true,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Checkbox
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "overwriteMode",
                DisplayName = "Režim přepisu",
                Description = "Co udělat pokud soubor již existuje",
                Type = ToolParameterType.String,
                IsRequired = false,
                DefaultValue = "overwrite",
                Validation = new SimpleParameterValidation
                {
                    AllowedValues = new[] { "overwrite", "skip", "rename", "error" }
                },
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Select,
                    HelpText = "overwrite=přepsat, skip=přeskočit, rename=přejmenovat, error=chyba"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "addWatermark",
                DisplayName = "Přidat vodoznak",
                Description = "Přidat textový vodoznak na obrázek",
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
                Name = "watermarkText",
                DisplayName = "Text vodoznaku",
                Description = "Text pro vodoznak",
                Type = ToolParameterType.String,
                IsRequired = false,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Text,
                    Placeholder = "© 2024 OptimalyAI",
                    HelpText = "Zobrazí se pouze pokud je vodoznak povolen"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "watermarkPosition",
                DisplayName = "Pozice vodoznaku",
                Description = "Kde umístit vodoznak",
                Type = ToolParameterType.String,
                IsRequired = false,
                DefaultValue = "bottom-right",
                Validation = new SimpleParameterValidation
                {
                    AllowedValues = new[] { "top-left", "top-right", "bottom-left", "bottom-right", "center" }
                },
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Select
                }
            });
        }

        protected override async Task<IAdapterResult> ExecuteWriteAsync(
            object data,
            Dictionary<string, object> configuration,
            string executionId,
            CancellationToken cancellationToken)
        {
            var metrics = new AdapterMetrics();
            var startTime = DateTime.UtcNow;

            try
            {
                var outputPath = GetParameter<string>(configuration, "outputPath");
                var format = GetParameter<string>(configuration, "format", "auto");
                var quality = GetParameter<int>(configuration, "quality", 85);
                var resizeWidth = GetParameter<int?>(configuration, "resizeWidth", null);
                var resizeHeight = GetParameter<int?>(configuration, "resizeHeight", null);
                var maintainAspectRatio = GetParameter<bool>(configuration, "maintainAspectRatio", true);
                var resizeMode = GetParameter<string>(configuration, "resizeMode", "fit");
                var backgroundColor = GetParameter<string>(configuration, "backgroundColor", "#FFFFFF");
                var createDirectory = GetParameter<bool>(configuration, "createDirectory", true);
                var overwriteMode = GetParameter<string>(configuration, "overwriteMode", "overwrite");
                var addWatermark = GetParameter<bool>(configuration, "addWatermark", false);
                var watermarkText = GetParameter<string>(configuration, "watermarkText", string.Empty);
                var watermarkPosition = GetParameter<string>(configuration, "watermarkPosition", "bottom-right");

                // Create output directory if needed
                var directory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(directory) && createDirectory && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                    Logger.LogInformation("Created directory: {Directory}", directory);
                }

                // Determine output format
                var outputFormat = DetermineOutputFormat(format, outputPath);
                
                // Handle overwrite mode
                var finalPath = HandleOverwriteMode(outputPath, overwriteMode);
                if (finalPath == null)
                {
                    // Skip mode and file exists
                    return CreateSuccessResult(executionId, startTime, new { skipped = true, originalPath = outputPath }, metrics);
                }

                Image sourceImage = null;
                byte[] imageBytes = null;

                // Process input data
                if (data is byte[] bytes)
                {
                    imageBytes = bytes;
                    using (var ms = new MemoryStream(bytes))
                    {
                        sourceImage = Image.FromStream(ms);
                    }
                }
                else if (data is string base64String)
                {
                    // Handle base64 or data URL
                    if (base64String.StartsWith("data:"))
                    {
                        var base64Index = base64String.IndexOf("base64,") + 7;
                        base64String = base64String.Substring(base64Index);
                    }
                    
                    imageBytes = Convert.FromBase64String(base64String);
                    using (var ms = new MemoryStream(imageBytes))
                    {
                        sourceImage = Image.FromStream(ms);
                    }
                }
                else if (data is Dictionary<string, object> imageData)
                {
                    // Extract from image data structure
                    if (imageData.TryGetValue("imageData", out var imgData) && imgData is byte[] imgBytes)
                    {
                        imageBytes = imgBytes;
                        using (var ms = new MemoryStream(imgBytes))
                        {
                            sourceImage = Image.FromStream(ms);
                        }
                    }
                    else if (imageData.TryGetValue("base64", out var base64Data) && base64Data is string b64)
                    {
                        imageBytes = Convert.FromBase64String(b64);
                        using (var ms = new MemoryStream(imageBytes))
                        {
                            sourceImage = Image.FromStream(ms);
                        }
                    }
                    else
                    {
                        return CreateExceptionResult(executionId, startTime, new InvalidOperationException("No valid image data found in input"));
                    }
                }
                else
                {
                    return CreateExceptionResult(executionId, startTime, new InvalidOperationException("Unsupported input data type for image output"));
                }

                if (sourceImage == null)
                {
                    return CreateExceptionResult(executionId, startTime, new InvalidOperationException("Failed to create image from input data"));
                }

                using (sourceImage)
                {
                    // Process the image
                    using (var processedImage = await ProcessImageAsync(sourceImage, resizeWidth, resizeHeight, 
                        maintainAspectRatio, resizeMode, backgroundColor, addWatermark, watermarkText, 
                        watermarkPosition, cancellationToken))
                    {
                        // Save the image
                        var savedBytes = await SaveImageAsync(processedImage, finalPath, outputFormat, quality, cancellationToken);
                        
                        metrics.ItemsProcessed = 1;
                        metrics.BytesProcessed = savedBytes;
                        metrics.ProcessingTime = DateTime.UtcNow - startTime;
                        metrics.ThroughputMBPerSecond = (metrics.BytesProcessed / 1024.0 / 1024.0) / Math.Max(0.001, metrics.ProcessingTime.TotalSeconds);

                        var resultData = new Dictionary<string, object>
                        {
                            ["outputPath"] = finalPath,
                            ["originalPath"] = outputPath,
                            ["format"] = outputFormat.ToString(),
                            ["bytesWritten"] = savedBytes,
                            ["originalSize"] = imageBytes?.Length ?? 0,
                            ["compression"] = imageBytes != null ? Math.Round((1.0 - (double)savedBytes / imageBytes.Length) * 100, 2) : 0,
                            ["dimensions"] = new { width = processedImage.Width, height = processedImage.Height },
                            ["timestamp"] = DateTime.UtcNow
                        };

                        Logger.LogInformation("Successfully saved image to {OutputPath}, {Bytes} bytes", finalPath, savedBytes);

                        return CreateSuccessResult(executionId, startTime, resultData, metrics);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error saving image");
                return CreateExceptionResult(executionId, startTime, ex);
            }
        }

        private async Task<Image> ProcessImageAsync(
            Image sourceImage, 
            int? resizeWidth, 
            int? resizeHeight,
            bool maintainAspectRatio,
            string resizeMode,
            string backgroundColor,
            bool addWatermark,
            string watermarkText,
            string watermarkPosition,
            CancellationToken cancellationToken)
        {
            Image processedImage = sourceImage;

            // Resize if needed
            if (resizeWidth.HasValue || resizeHeight.HasValue)
            {
                processedImage = ResizeImage(sourceImage, resizeWidth, resizeHeight, maintainAspectRatio, resizeMode, backgroundColor);
            }
            else
            {
                // Create a copy even if not resizing to avoid modifying the original
                processedImage = new Bitmap(sourceImage);
            }

            // Add watermark if requested
            if (addWatermark && !string.IsNullOrEmpty(watermarkText))
            {
                var watermarkedImage = AddWatermarkToImage(processedImage, watermarkText, watermarkPosition);
                if (processedImage != sourceImage)
                {
                    processedImage.Dispose();
                }
                processedImage = watermarkedImage;
            }

            return await Task.FromResult(processedImage);
        }

        private Image ResizeImage(Image sourceImage, int? newWidth, int? newHeight, bool maintainAspectRatio, string resizeMode, string backgroundColor)
        {
            var targetWidth = newWidth ?? sourceImage.Width;
            var targetHeight = newHeight ?? sourceImage.Height;

            if (maintainAspectRatio && newWidth.HasValue && newHeight.HasValue)
            {
                var sourceRatio = (double)sourceImage.Width / sourceImage.Height;
                var targetRatio = (double)targetWidth / targetHeight;

                switch (resizeMode.ToLower())
                {
                    case "fit":
                        if (sourceRatio > targetRatio)
                        {
                            targetHeight = (int)(targetWidth / sourceRatio);
                        }
                        else
                        {
                            targetWidth = (int)(targetHeight * sourceRatio);
                        }
                        break;
                    case "fill":
                        if (sourceRatio < targetRatio)
                        {
                            targetHeight = (int)(targetWidth / sourceRatio);
                        }
                        else
                        {
                            targetWidth = (int)(targetHeight * sourceRatio);
                        }
                        break;
                    // "crop" and "stretch" use the target dimensions as-is
                }
            }
            else if (maintainAspectRatio)
            {
                var ratio = Math.Min(
                    newWidth.HasValue ? (double)newWidth.Value / sourceImage.Width : double.MaxValue,
                    newHeight.HasValue ? (double)newHeight.Value / sourceImage.Height : double.MaxValue
                );
                
                targetWidth = (int)(sourceImage.Width * ratio);
                targetHeight = (int)(sourceImage.Height * ratio);
            }

            var resizedImage = new Bitmap(targetWidth, targetHeight);
            
            // Parse background color
            var bgColor = ColorTranslator.FromHtml(backgroundColor);
            
            using (var graphics = Graphics.FromImage(resizedImage))
            {
                graphics.Clear(bgColor);
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                
                Rectangle destRect;
                if (resizeMode.ToLower() == "crop" && newWidth.HasValue && newHeight.HasValue)
                {
                    // Center the image and crop
                    var sourceRatio = (double)sourceImage.Width / sourceImage.Height;
                    var targetRatio = (double)newWidth.Value / newHeight.Value;
                    
                    int sourceWidth, sourceHeight, sourceX = 0, sourceY = 0;
                    
                    if (sourceRatio > targetRatio)
                    {
                        sourceHeight = sourceImage.Height;
                        sourceWidth = (int)(sourceHeight * targetRatio);
                        sourceX = (sourceImage.Width - sourceWidth) / 2;
                    }
                    else
                    {
                        sourceWidth = sourceImage.Width;
                        sourceHeight = (int)(sourceWidth / targetRatio);
                        sourceY = (sourceImage.Height - sourceHeight) / 2;
                    }
                    
                    var sourceRect = new Rectangle(sourceX, sourceY, sourceWidth, sourceHeight);
                    destRect = new Rectangle(0, 0, newWidth.Value, newHeight.Value);
                    graphics.DrawImage(sourceImage, destRect, sourceRect, GraphicsUnit.Pixel);
                }
                else
                {
                    // Normal resize
                    destRect = new Rectangle(0, 0, targetWidth, targetHeight);
                    graphics.DrawImage(sourceImage, destRect);
                }
            }

            return resizedImage;
        }

        private Image AddWatermarkToImage(Image sourceImage, string watermarkText, string position)
        {
            var watermarkedImage = new Bitmap(sourceImage);
            
            using (var graphics = Graphics.FromImage(watermarkedImage))
            {
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                
                // Calculate font size based on image size
                var fontSize = Math.Max(12, sourceImage.Width / 50);
                using (var font = new Font("Arial", fontSize, FontStyle.Bold))
                using (var textBrush = new SolidBrush(Color.FromArgb(128, Color.White)))
                using (var shadowBrush = new SolidBrush(Color.FromArgb(128, Color.Black)))
                {
                    var textSize = graphics.MeasureString(watermarkText, font);
                    
                    // Calculate position
                    PointF textPosition = position.ToLower() switch
                    {
                        "top-left" => new PointF(10, 10),
                        "top-right" => new PointF(sourceImage.Width - textSize.Width - 10, 10),
                        "bottom-left" => new PointF(10, sourceImage.Height - textSize.Height - 10),
                        "bottom-right" => new PointF(sourceImage.Width - textSize.Width - 10, sourceImage.Height - textSize.Height - 10),
                        "center" => new PointF((sourceImage.Width - textSize.Width) / 2, (sourceImage.Height - textSize.Height) / 2),
                        _ => new PointF(sourceImage.Width - textSize.Width - 10, sourceImage.Height - textSize.Height - 10)
                    };
                    
                    // Draw shadow
                    graphics.DrawString(watermarkText, font, shadowBrush, textPosition.X + 1, textPosition.Y + 1);
                    // Draw text
                    graphics.DrawString(watermarkText, font, textBrush, textPosition);
                }
            }
            
            return watermarkedImage;
        }

        private async Task<long> SaveImageAsync(Image image, string outputPath, ImageFormat format, int quality, CancellationToken cancellationToken)
        {
            var encoderParameters = new EncoderParameters(1);
            var qualityParameter = new EncoderParameter(Encoder.Quality, quality);
            encoderParameters.Param[0] = qualityParameter;

            var codec = ImageCodecInfo.GetImageDecoders().FirstOrDefault(c => c.FormatID == format.Guid);
            
            if (format == ImageFormat.Jpeg && codec != null)
            {
                image.Save(outputPath, codec, encoderParameters);
            }
            else
            {
                image.Save(outputPath, format);
            }

            var fileInfo = new FileInfo(outputPath);
            return fileInfo.Length;
        }

        private ImageFormat DetermineOutputFormat(string format, string outputPath)
        {
            if (format != "auto")
            {
                return format.ToLower() switch
                {
                    "jpeg" or "jpg" => ImageFormat.Jpeg,
                    "png" => ImageFormat.Png,
                    "gif" => ImageFormat.Gif,
                    "bmp" => ImageFormat.Bmp,
                    "tiff" => ImageFormat.Tiff,
                    _ => ImageFormat.Jpeg
                };
            }

            var extension = Path.GetExtension(outputPath).ToLower();
            return SupportedFormats.GetValueOrDefault(extension, ImageFormat.Jpeg);
        }

        private string HandleOverwriteMode(string outputPath, string overwriteMode)
        {
            if (!File.Exists(outputPath))
                return outputPath;

            return overwriteMode.ToLower() switch
            {
                "skip" => null, // Signal to skip
                "error" => throw new InvalidOperationException($"File already exists: {outputPath}"),
                "rename" => GetUniqueFilePath(outputPath),
                _ => outputPath // overwrite
            };
        }

        private string GetUniqueFilePath(string originalPath)
        {
            var directory = Path.GetDirectoryName(originalPath) ?? ".";
            var fileName = Path.GetFileNameWithoutExtension(originalPath);
            var extension = Path.GetExtension(originalPath);
            
            var counter = 1;
            string newPath;
            do
            {
                newPath = Path.Combine(directory, $"{fileName}_{counter}{extension}");
                counter++;
            } while (File.Exists(newPath));

            return newPath;
        }

        protected override async Task PerformDestinationValidationAsync(
            Dictionary<string, object> configuration,
            CancellationToken cancellationToken)
        {
            var outputPath = GetParameter<string>(configuration, "outputPath");
            var overwriteMode = GetParameter<string>(configuration, "overwriteMode", "overwrite");
            
            if (string.IsNullOrEmpty(outputPath))
                throw new InvalidOperationException("Output path is required");

            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory))
            {
                var createDirectory = GetParameter<bool>(configuration, "createDirectory", true);
                if (!Directory.Exists(directory) && !createDirectory)
                {
                    throw new DirectoryNotFoundException($"Directory not found: {directory}");
                }
            }

            if (overwriteMode == "error" && File.Exists(outputPath))
            {
                throw new InvalidOperationException($"File already exists: {outputPath}");
            }

            await Task.CompletedTask;
        }

        public override IReadOnlyList<IAdapterSchema> GetInputSchemas()
        {
            return new List<IAdapterSchema>
            {
                new ImageDataSchema
                {
                    Id = "image_data_input",
                    Name = "Image Data Input",
                    Description = "Image data for output processing",
                    JsonSchema = @"{
                        ""oneOf"": [
                            {
                                ""type"": ""string"",
                                ""description"": ""Base64 encoded image or data URL""
                            },
                            {
                                ""type"": ""object"",
                                ""properties"": {
                                    ""imageData"": { ""type"": ""string"", ""format"": ""byte"" },
                                    ""base64"": { ""type"": ""string"" },
                                    ""dataUrl"": { ""type"": ""string"" }
                                }
                            }
                        ]
                    }",
                    ExampleData = new Dictionary<string, object>
                    {
                        ["base64"] = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8/5+hHgAHggJ/PchI7wAAAABJRU5ErkJggg=="
                    }
                }
            };
        }

        public override AdapterCapabilities GetCapabilities()
        {
            return new AdapterCapabilities
            {
                SupportsStreaming = false,
                SupportsPartialData = false,
                SupportsBatchProcessing = false,
                SupportsTransactions = false,
                RequiresAuthentication = false,
                MaxDataSizeBytes = 50 * 1024 * 1024, // 50 MB
                MaxConcurrentOperations = 3,
                SupportedFormats = new List<string> { "JPEG", "PNG", "GIF", "BMP", "TIFF" },
                SupportedEncodings = new List<string> { "Binary", "Base64" },
                CustomCapabilities = new Dictionary<string, object>
                {
                    ["supportsFormatConversion"] = true,
                    ["supportsResizing"] = true,
                    ["supportsQualityControl"] = true,
                    ["supportsWatermark"] = true,
                    ["supportsMultipleResizeModes"] = true,
                    ["maxImageSize"] = 50 * 1024 * 1024,
                    ["maxDimensions"] = new { width = 10000, height = 10000 }
                }
            };
        }

        protected override async Task PerformHealthCheckAsync()
        {
            // Test basic image creation and saving capabilities
            try
            {
                using (var testImage = new Bitmap(1, 1))
                {
                    testImage.SetPixel(0, 0, Color.Red);
                    
                    var tempPath = Path.GetTempFileName() + ".png";
                    testImage.Save(tempPath, ImageFormat.Png);
                    
                    if (File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                    }
                }
            }
            catch
            {
                throw new InvalidOperationException("Image processing and saving capabilities not available");
            }
            
            await Task.CompletedTask;
        }
    }
}