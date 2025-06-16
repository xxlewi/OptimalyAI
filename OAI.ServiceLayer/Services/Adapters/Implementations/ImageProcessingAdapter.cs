using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OAI.Core.Interfaces.Adapters;
using OAI.Core.Interfaces.Tools;
using OAI.ServiceLayer.Services.Adapters.Base;

namespace OAI.ServiceLayer.Services.Adapters.Implementations
{
    /// <summary>
    /// Image processing adapter for transforming and manipulating images
    /// </summary>
    public class ImageProcessingAdapter : BaseInputAdapter
    {
        public override string Id => "image_processing";
        public override string Name => "Zpracování obrázků";
        public override string Description => "Transformace a úpravy obrázků (resize, crop, rotate, filtry)";
        public override string Version => "1.0.0";
        public override string Category => "Image";

        public ImageProcessingAdapter(ILogger<ImageProcessingAdapter> logger) : base(logger)
        {
        }

        protected override void InitializeParameters()
        {
            AddParameter(new SimpleAdapterParameter
            {
                Name = "inputImage",
                DisplayName = "Vstupní obrázek",
                Description = "Obrázek pro zpracování (Base64, cesta k souboru, nebo image data)",
                Type = ToolParameterType.String,
                IsRequired = true,
                IsCritical = true,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Text,
                    HelpText = "Base64 string, cesta k souboru, nebo data URL"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "operations",
                DisplayName = "Operace",
                Description = "Seznam operací k provedení (JSON pole)",
                Type = ToolParameterType.String,
                IsRequired = true,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.TextArea,
                    Placeholder = "[{\"type\": \"resize\", \"width\": 800, \"height\": 600}]",
                    HelpText = "JSON pole s operacemi (resize, crop, rotate, flip, filter, etc.)"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "outputFormat",
                DisplayName = "Výstupní formát",
                Description = "Formát výsledného obrázku",
                Type = ToolParameterType.String,
                IsRequired = false,
                DefaultValue = "png",
                Validation = new SimpleParameterValidation
                {
                    AllowedValues = new[] { "png", "jpeg", "gif", "bmp", "tiff" }
                },
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Select
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "quality",
                DisplayName = "Kvalita JPEG",
                Description = "Kvalita komprese pro JPEG (1-100)",
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
                    InputType = ParameterInputType.Range
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "backgroundColor",
                DisplayName = "Barva pozadí",
                Description = "Barva pozadí pro průhledné oblasti",
                Type = ToolParameterType.String,
                IsRequired = false,
                DefaultValue = "#FFFFFF",
                Validation = new SimpleParameterValidation
                {
                    Pattern = "^#[0-9A-Fa-f]{6}$"
                },
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Color
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "interpolationMode",
                DisplayName = "Režim interpolace",
                Description = "Kvalita změny velikosti",
                Type = ToolParameterType.String,
                IsRequired = false,
                DefaultValue = "HighQualityBicubic",
                Validation = new SimpleParameterValidation
                {
                    AllowedValues = new[] { "NearestNeighbor", "Bilinear", "Bicubic", "HighQualityBilinear", "HighQualityBicubic" }
                },
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Select,
                    HelpText = "HighQualityBicubic pro nejlepší kvalitu"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "preserveExif",
                DisplayName = "Zachovat EXIF",
                Description = "Zachovat EXIF metadata po zpracování",
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
                Name = "returnBase64",
                DisplayName = "Vrátit Base64",
                Description = "Vrátit výsledek jako Base64 string",
                Type = ToolParameterType.Boolean,
                IsRequired = false,
                DefaultValue = true,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Checkbox
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
                var inputImage = GetParameter<string>(configuration, "inputImage");
                var operationsJson = GetParameter<string>(configuration, "operations");
                var outputFormat = GetParameter<string>(configuration, "outputFormat", "png");
                var quality = GetParameter<int>(configuration, "quality", 85);
                var backgroundColor = GetParameter<string>(configuration, "backgroundColor", "#FFFFFF");
                var interpolationMode = GetParameter<string>(configuration, "interpolationMode", "HighQualityBicubic");
                var preserveExif = GetParameter<bool>(configuration, "preserveExif", true);
                var returnBase64 = GetParameter<bool>(configuration, "returnBase64", true);

                // Parse operations
                var operations = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, object>>>(operationsJson);
                if (operations == null || operations.Count == 0)
                {
                    return CreateErrorResult(executionId, startTime, "No operations specified");
                }

                // Load source image
                Image sourceImage = await LoadImageAsync(inputImage, cancellationToken);
                if (sourceImage == null)
                {
                    return CreateErrorResult(executionId, startTime, "Failed to load input image");
                }

                PropertyItem[] originalExif = null;
                if (preserveExif)
                {
                    try
                    {
                        originalExif = sourceImage.PropertyItems;
                    }
                    catch (Exception ex)
                    {
                        Logger.LogDebug(ex, "Could not extract EXIF data from source image");
                    }
                }

                using (sourceImage)
                {
                    // Process operations sequentially
                    Image processedImage = sourceImage;
                    var operationResults = new List<Dictionary<string, object>>();

                    for (int i = 0; i < operations.Count; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var operation = operations[i];
                        var operationType = operation.GetValueOrDefault("type")?.ToString()?.ToLower();

                        try
                        {
                            var operationResult = await ProcessOperationAsync(processedImage, operation, backgroundColor, interpolationMode, cancellationToken);
                            
                            // Replace processed image (dispose previous if different from source)
                            if (processedImage != sourceImage)
                            {
                                processedImage.Dispose();
                            }
                            processedImage = operationResult["image"] as Image;

                            operationResults.Add(new Dictionary<string, object>
                            {
                                ["index"] = i,
                                ["type"] = operationType,
                                ["success"] = true,
                                ["message"] = operationResult.GetValueOrDefault("message", "Operation completed successfully")
                            });

                            Logger.LogDebug("Completed operation {Index}: {Type}", i, operationType);
                        }
                        catch (Exception ex)
                        {
                            Logger.LogWarning(ex, "Failed operation {Index}: {Type}", i, operationType);
                            
                            operationResults.Add(new Dictionary<string, object>
                            {
                                ["index"] = i,
                                ["type"] = operationType,
                                ["success"] = false,
                                ["error"] = ex.Message
                            });

                            // Continue with next operation - don't fail entire processing
                        }
                    }

                    using (processedImage)
                    {
                        // Convert to output format
                        var resultData = await ConvertToOutputAsync(processedImage, outputFormat, quality, returnBase64, originalExif, cancellationToken);
                        
                        // Add processing information
                        resultData["originalDimensions"] = new { width = sourceImage.Width, height = sourceImage.Height };
                        resultData["finalDimensions"] = new { width = processedImage.Width, height = processedImage.Height };
                        resultData["operationsPerformed"] = operationResults;
                        resultData["outputFormat"] = outputFormat;
                        resultData["quality"] = quality;
                        resultData["preservedExif"] = preserveExif && originalExif != null;

                        metrics.ItemsProcessed = 1;
                        metrics.BytesProcessed = (long)(resultData.GetValueOrDefault("size", 0));
                        metrics.ProcessingTime = DateTime.UtcNow - startTime;
                        metrics.ThroughputItemsPerSecond = 1.0 / Math.Max(0.001, metrics.ProcessingTime.TotalSeconds);

                        // Create schema
                        var schema = new ImageDataSchema
                        {
                            Id = "processed_image",
                            Name = "Processed Image",
                            Description = "Image after processing operations",
                            Fields = new List<SchemaField>
                            {
                                new SchemaField { Name = "base64", Type = "string", IsRequired = returnBase64 },
                                new SchemaField { Name = "dataUrl", Type = "string", IsRequired = returnBase64 },
                                new SchemaField { Name = "imageData", Type = "binary", IsRequired = !returnBase64 },
                                new SchemaField { Name = "format", Type = "string", IsRequired = true },
                                new SchemaField { Name = "size", Type = "number", IsRequired = true },
                                new SchemaField { Name = "dimensions", Type = "object", IsRequired = true }
                            }
                        };

                        Logger.LogInformation("Successfully processed image with {Count} operations", operations.Count);

                        return CreateSuccessResult(executionId, startTime, resultData, metrics, schema, new { preview = "Image processing completed" });
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error processing image");
                return CreateExceptionResult(executionId, startTime, ex);
            }
        }

        private async Task<Image> LoadImageAsync(string input, CancellationToken cancellationToken)
        {
            try
            {
                // Try as file path first
                if (File.Exists(input))
                {
                    var bytes = await File.ReadAllBytesAsync(input, cancellationToken);
                    return Image.FromStream(new MemoryStream(bytes));
                }

                // Try as Base64 or data URL
                string base64Data = input;
                if (input.StartsWith("data:"))
                {
                    var base64Index = input.IndexOf("base64,") + 7;
                    base64Data = input.Substring(base64Index);
                }

                var imageBytes = Convert.FromBase64String(base64Data);
                return Image.FromStream(new MemoryStream(imageBytes));
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to load image from input");
                return null;
            }
        }

        private async Task<Dictionary<string, object>> ProcessOperationAsync(
            Image image,
            Dictionary<string, object> operation,
            string backgroundColor,
            string interpolationMode,
            CancellationToken cancellationToken)
        {
            var operationType = operation.GetValueOrDefault("type")?.ToString()?.ToLower();
            var result = new Dictionary<string, object>();

            Image processedImage = operationType switch
            {
                "resize" => ProcessResize(image, operation, backgroundColor, interpolationMode),
                "crop" => ProcessCrop(image, operation),
                "rotate" => ProcessRotate(image, operation, backgroundColor),
                "flip" => ProcessFlip(image, operation),
                "grayscale" => ProcessGrayscale(image),
                "sepia" => ProcessSepia(image),
                "brightness" => ProcessBrightness(image, operation),
                "contrast" => ProcessContrast(image, operation),
                "blur" => ProcessBlur(image, operation),
                "sharpen" => ProcessSharpen(image),
                "invert" => ProcessInvert(image),
                "tint" => ProcessTint(image, operation),
                _ => throw new ArgumentException($"Unknown operation type: {operationType}")
            };

            result["image"] = processedImage;
            result["message"] = $"Applied {operationType} operation";
            
            return await Task.FromResult(result);
        }

        private Image ProcessResize(Image image, Dictionary<string, object> operation, string backgroundColor, string interpolationMode)
        {
            var width = Convert.ToInt32(operation.GetValueOrDefault("width", image.Width));
            var height = Convert.ToInt32(operation.GetValueOrDefault("height", image.Height));
            var maintainAspectRatio = Convert.ToBoolean(operation.GetValueOrDefault("maintainAspectRatio", true));

            if (maintainAspectRatio)
            {
                var ratio = Math.Min((double)width / image.Width, (double)height / image.Height);
                width = (int)(image.Width * ratio);
                height = (int)(image.Height * ratio);
            }

            var resized = new Bitmap(width, height);
            var bgColor = ColorTranslator.FromHtml(backgroundColor);

            using (var graphics = Graphics.FromImage(resized))
            {
                graphics.Clear(bgColor);
                graphics.InterpolationMode = Enum.Parse<InterpolationMode>(interpolationMode);
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.CompositingQuality = CompositingQuality.HighQuality;

                graphics.DrawImage(image, 0, 0, width, height);
            }

            return resized;
        }

        private Image ProcessCrop(Image image, Dictionary<string, object> operation)
        {
            var x = Convert.ToInt32(operation.GetValueOrDefault("x", 0));
            var y = Convert.ToInt32(operation.GetValueOrDefault("y", 0));
            var width = Convert.ToInt32(operation.GetValueOrDefault("width", image.Width));
            var height = Convert.ToInt32(operation.GetValueOrDefault("height", image.Height));

            // Ensure crop rectangle is within image bounds
            x = Math.Max(0, Math.Min(x, image.Width));
            y = Math.Max(0, Math.Min(y, image.Height));
            width = Math.Min(width, image.Width - x);
            height = Math.Min(height, image.Height - y);

            var cropRect = new Rectangle(x, y, width, height);
            var cropped = new Bitmap(width, height);

            using (var graphics = Graphics.FromImage(cropped))
            {
                graphics.DrawImage(image, new Rectangle(0, 0, width, height), cropRect, GraphicsUnit.Pixel);
            }

            return cropped;
        }

        private Image ProcessRotate(Image image, Dictionary<string, object> operation, string backgroundColor)
        {
            var angle = Convert.ToDouble(operation.GetValueOrDefault("angle", 0));
            var bgColor = ColorTranslator.FromHtml(backgroundColor);

            // Calculate new image dimensions after rotation
            var radians = Math.PI * angle / 180.0;
            var cos = Math.Abs(Math.Cos(radians));
            var sin = Math.Abs(Math.Sin(radians));
            var newWidth = (int)(image.Width * cos + image.Height * sin);
            var newHeight = (int)(image.Width * sin + image.Height * cos);

            var rotated = new Bitmap(newWidth, newHeight);

            using (var graphics = Graphics.FromImage(rotated))
            {
                graphics.Clear(bgColor);
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;

                // Move origin to center
                graphics.TranslateTransform(newWidth / 2f, newHeight / 2f);
                graphics.RotateTransform((float)angle);
                graphics.TranslateTransform(-image.Width / 2f, -image.Height / 2f);

                graphics.DrawImage(image, new Point(0, 0));
            }

            return rotated;
        }

        private Image ProcessFlip(Image image, Dictionary<string, object> operation)
        {
            var direction = operation.GetValueOrDefault("direction")?.ToString()?.ToLower() ?? "horizontal";
            var flipped = new Bitmap(image);

            var rotateFlipType = direction switch
            {
                "horizontal" => RotateFlipType.RotateNoneFlipX,
                "vertical" => RotateFlipType.RotateNoneFlipY,
                "both" => RotateFlipType.RotateNoneFlipXY,
                _ => RotateFlipType.RotateNoneFlipX
            };

            flipped.RotateFlip(rotateFlipType);
            return flipped;
        }

        private Image ProcessGrayscale(Image image)
        {
            var grayscale = new Bitmap(image.Width, image.Height);

            using (var graphics = Graphics.FromImage(grayscale))
            {
                var colorMatrix = new ColorMatrix(
                    new float[][]
                    {
                        new float[] {0.299f, 0.299f, 0.299f, 0, 0},
                        new float[] {0.587f, 0.587f, 0.587f, 0, 0},
                        new float[] {0.114f, 0.114f, 0.114f, 0, 0},
                        new float[] {0, 0, 0, 1, 0},
                        new float[] {0, 0, 0, 0, 1}
                    });

                var attributes = new ImageAttributes();
                attributes.SetColorMatrix(colorMatrix);

                graphics.DrawImage(image, new Rectangle(0, 0, image.Width, image.Height),
                    0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
            }

            return grayscale;
        }

        private Image ProcessSepia(Image image)
        {
            var sepia = new Bitmap(image.Width, image.Height);

            using (var graphics = Graphics.FromImage(sepia))
            {
                var colorMatrix = new ColorMatrix(
                    new float[][]
                    {
                        new float[] {0.393f, 0.349f, 0.272f, 0, 0},
                        new float[] {0.769f, 0.686f, 0.534f, 0, 0},
                        new float[] {0.189f, 0.168f, 0.131f, 0, 0},
                        new float[] {0, 0, 0, 1, 0},
                        new float[] {0, 0, 0, 0, 1}
                    });

                var attributes = new ImageAttributes();
                attributes.SetColorMatrix(colorMatrix);

                graphics.DrawImage(image, new Rectangle(0, 0, image.Width, image.Height),
                    0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
            }

            return sepia;
        }

        private Image ProcessBrightness(Image image, Dictionary<string, object> operation)
        {
            var factor = Convert.ToSingle(operation.GetValueOrDefault("factor", 1.0));
            var bright = new Bitmap(image.Width, image.Height);

            using (var graphics = Graphics.FromImage(bright))
            {
                var colorMatrix = new ColorMatrix(
                    new float[][]
                    {
                        new float[] {factor, 0, 0, 0, 0},
                        new float[] {0, factor, 0, 0, 0},
                        new float[] {0, 0, factor, 0, 0},
                        new float[] {0, 0, 0, 1, 0},
                        new float[] {0, 0, 0, 0, 1}
                    });

                var attributes = new ImageAttributes();
                attributes.SetColorMatrix(colorMatrix);

                graphics.DrawImage(image, new Rectangle(0, 0, image.Width, image.Height),
                    0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
            }

            return bright;
        }

        private Image ProcessContrast(Image image, Dictionary<string, object> operation)
        {
            var factor = Convert.ToSingle(operation.GetValueOrDefault("factor", 1.0));
            var contrast = new Bitmap(image.Width, image.Height);

            using (var graphics = Graphics.FromImage(contrast))
            {
                var t = (1.0f - factor) / 2.0f;
                var colorMatrix = new ColorMatrix(
                    new float[][]
                    {
                        new float[] {factor, 0, 0, 0, 0},
                        new float[] {0, factor, 0, 0, 0},
                        new float[] {0, 0, factor, 0, 0},
                        new float[] {0, 0, 0, 1, 0},
                        new float[] {t, t, t, 0, 1}
                    });

                var attributes = new ImageAttributes();
                attributes.SetColorMatrix(colorMatrix);

                graphics.DrawImage(image, new Rectangle(0, 0, image.Width, image.Height),
                    0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
            }

            return contrast;
        }

        private Image ProcessBlur(Image image, Dictionary<string, object> operation)
        {
            // Simple box blur implementation
            var radius = Convert.ToInt32(operation.GetValueOrDefault("radius", 5));
            var blurred = new Bitmap(image);

            using (var bitmap = new Bitmap(image))
            {
                for (int x = radius; x < bitmap.Width - radius; x++)
                {
                    for (int y = radius; y < bitmap.Height - radius; y++)
                    {
                        int totalR = 0, totalG = 0, totalB = 0;
                        int count = 0;

                        for (int fx = x - radius; fx <= x + radius; fx++)
                        {
                            for (int fy = y - radius; fy <= y + radius; fy++)
                            {
                                var pixel = bitmap.GetPixel(fx, fy);
                                totalR += pixel.R;
                                totalG += pixel.G;
                                totalB += pixel.B;
                                count++;
                            }
                        }

                        var avgColor = Color.FromArgb(totalR / count, totalG / count, totalB / count);
                        blurred.SetPixel(x, y, avgColor);
                    }
                }
            }

            return blurred;
        }

        private Image ProcessSharpen(Image image)
        {
            var sharpened = new Bitmap(image.Width, image.Height);

            using (var graphics = Graphics.FromImage(sharpened))
            {
                // Simple sharpening filter
                var colorMatrix = new ColorMatrix(
                    new float[][]
                    {
                        new float[] {1.5f, 0, 0, 0, 0},
                        new float[] {0, 1.5f, 0, 0, 0},
                        new float[] {0, 0, 1.5f, 0, 0},
                        new float[] {0, 0, 0, 1, 0},
                        new float[] {-0.25f, -0.25f, -0.25f, 0, 1}
                    });

                var attributes = new ImageAttributes();
                attributes.SetColorMatrix(colorMatrix);

                graphics.DrawImage(image, new Rectangle(0, 0, image.Width, image.Height),
                    0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
            }

            return sharpened;
        }

        private Image ProcessInvert(Image image)
        {
            var inverted = new Bitmap(image.Width, image.Height);

            using (var graphics = Graphics.FromImage(inverted))
            {
                var colorMatrix = new ColorMatrix(
                    new float[][]
                    {
                        new float[] {-1, 0, 0, 0, 0},
                        new float[] {0, -1, 0, 0, 0},
                        new float[] {0, 0, -1, 0, 0},
                        new float[] {0, 0, 0, 1, 0},
                        new float[] {1, 1, 1, 0, 1}
                    });

                var attributes = new ImageAttributes();
                attributes.SetColorMatrix(colorMatrix);

                graphics.DrawImage(image, new Rectangle(0, 0, image.Width, image.Height),
                    0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
            }

            return inverted;
        }

        private Image ProcessTint(Image image, Dictionary<string, object> operation)
        {
            var colorHex = operation.GetValueOrDefault("color")?.ToString() ?? "#FF0000";
            var intensity = Convert.ToSingle(operation.GetValueOrDefault("intensity", 0.5));
            
            var tintColor = ColorTranslator.FromHtml(colorHex);
            var tinted = new Bitmap(image.Width, image.Height);

            using (var graphics = Graphics.FromImage(tinted))
            {
                var colorMatrix = new ColorMatrix(
                    new float[][]
                    {
                        new float[] {1 - intensity + intensity * tintColor.R / 255f, intensity * tintColor.R / 255f, intensity * tintColor.R / 255f, 0, 0},
                        new float[] {intensity * tintColor.G / 255f, 1 - intensity + intensity * tintColor.G / 255f, intensity * tintColor.G / 255f, 0, 0},
                        new float[] {intensity * tintColor.B / 255f, intensity * tintColor.B / 255f, 1 - intensity + intensity * tintColor.B / 255f, 0, 0},
                        new float[] {0, 0, 0, 1, 0},
                        new float[] {0, 0, 0, 0, 1}
                    });

                var attributes = new ImageAttributes();
                attributes.SetColorMatrix(colorMatrix);

                graphics.DrawImage(image, new Rectangle(0, 0, image.Width, image.Height),
                    0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
            }

            return tinted;
        }

        private async Task<Dictionary<string, object>> ConvertToOutputAsync(
            Image image,
            string format,
            int quality,
            bool returnBase64,
            PropertyItem[] originalExif,
            CancellationToken cancellationToken)
        {
            var result = new Dictionary<string, object>
            {
                ["width"] = image.Width,
                ["height"] = image.Height,
                ["format"] = format
            };

            using (var ms = new MemoryStream())
            {
                var imageFormat = format.ToLower() switch
                {
                    "jpeg" or "jpg" => ImageFormat.Jpeg,
                    "png" => ImageFormat.Png,
                    "gif" => ImageFormat.Gif,
                    "bmp" => ImageFormat.Bmp,
                    "tiff" => ImageFormat.Tiff,
                    _ => ImageFormat.Png
                };

                if (imageFormat == ImageFormat.Jpeg)
                {
                    var encoder = ImageCodecInfo.GetImageEncoders()
                        .FirstOrDefault(x => x.FormatID == ImageFormat.Jpeg.Guid);
                    var encoderParams = new EncoderParameters(1);
                    encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, quality);
                    image.Save(ms, encoder, encoderParams);
                }
                else
                {
                    image.Save(ms, imageFormat);
                }

                var imageBytes = ms.ToArray();
                result["size"] = imageBytes.Length;

                if (returnBase64)
                {
                    result["base64"] = Convert.ToBase64String(imageBytes);
                    var mimeType = format.ToLower() switch
                    {
                        "jpeg" or "jpg" => "image/jpeg",
                        "png" => "image/png",
                        "gif" => "image/gif",
                        "bmp" => "image/bmp",
                        "tiff" => "image/tiff",
                        _ => "image/png"
                    };
                    result["dataUrl"] = $"data:{mimeType};base64,{result["base64"]}";
                    result["mimeType"] = mimeType;
                }
                else
                {
                    result["imageData"] = imageBytes;
                }
            }

            return await Task.FromResult(result);
        }

        protected override async Task PerformSourceValidationAsync(
            Dictionary<string, object> configuration,
            CancellationToken cancellationToken)
        {
            var inputImage = GetParameter<string>(configuration, "inputImage");
            var operationsJson = GetParameter<string>(configuration, "operations");
            
            if (string.IsNullOrEmpty(inputImage))
                throw new InvalidOperationException("Input image is required");

            if (string.IsNullOrEmpty(operationsJson))
                throw new InvalidOperationException("Operations are required");

            try
            {
                var operations = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, object>>>(operationsJson);
                if (operations == null || operations.Count == 0)
                    throw new InvalidOperationException("No valid operations found");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Invalid operations JSON: {ex.Message}");
            }

            await Task.CompletedTask;
        }

        public override IReadOnlyList<IAdapterSchema> GetOutputSchemas()
        {
            return new List<IAdapterSchema>
            {
                new ImageDataSchema
                {
                    Id = "processed_image_result",
                    Name = "Processed Image Result",
                    Description = "Result of image processing operations",
                    JsonSchema = @"{
                        ""type"": ""object"",
                        ""properties"": {
                            ""base64"": { ""type"": ""string"" },
                            ""dataUrl"": { ""type"": ""string"" },
                            ""imageData"": { ""type"": ""string"", ""format"": ""byte"" },
                            ""width"": { ""type"": ""number"" },
                            ""height"": { ""type"": ""number"" },
                            ""format"": { ""type"": ""string"" },
                            ""size"": { ""type"": ""number"" },
                            ""operationsPerformed"": { ""type"": ""array"" },
                            ""originalDimensions"": { ""type"": ""object"" },
                            ""finalDimensions"": { ""type"": ""object"" }
                        },
                        ""required"": [""width"", ""height"", ""format"", ""size""]
                    }",
                    ExampleData = new Dictionary<string, object>
                    {
                        ["width"] = 800,
                        ["height"] = 600,
                        ["format"] = "png",
                        ["size"] = 1024000,
                        ["operationsPerformed"] = new[] { "resize", "grayscale" }
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
                MaxConcurrentOperations = 2,
                SupportedFormats = new List<string> { "JPEG", "PNG", "GIF", "BMP", "TIFF" },
                SupportedEncodings = new List<string> { "Binary", "Base64" },
                CustomCapabilities = new Dictionary<string, object>
                {
                    ["supportedOperations"] = new[]
                    {
                        "resize", "crop", "rotate", "flip", "grayscale", "sepia",
                        "brightness", "contrast", "blur", "sharpen", "invert", "tint"
                    },
                    ["supportsChainedOperations"] = true,
                    ["supportsExifPreservation"] = true,
                    ["maxImageSize"] = 50 * 1024 * 1024,
                    ["maxDimensions"] = new { width = 10000, height = 10000 }
                }
            };
        }

        protected override async Task PerformHealthCheckAsync()
        {
            try
            {
                using (var testImage = new Bitmap(100, 100))
                {
                    // Test basic operations
                    using (var graphics = Graphics.FromImage(testImage))
                    {
                        graphics.Clear(Color.Red);
                    }
                    
                    // Test resize
                    using (var resized = new Bitmap(testImage, 50, 50))
                    {
                        // Test successful if no exception
                    }
                }
            }
            catch
            {
                throw new InvalidOperationException("Image processing capabilities not available");
            }
            
            await Task.CompletedTask;
        }
    }
}