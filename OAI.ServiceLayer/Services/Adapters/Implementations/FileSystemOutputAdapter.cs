using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OAI.Core.Interfaces.Adapters;
using OAI.Core.Interfaces.Tools;
using OAI.ServiceLayer.Services.Adapters.Base;

namespace OAI.ServiceLayer.Services.Adapters.Implementations
{
    /// <summary>
    /// File system output adapter for writing files
    /// </summary>
    public class FileSystemOutputAdapter : BaseOutputAdapter
    {
        public override string Id => "filesystem_output";
        public override string Name => "Zápis souborů";
        public override string Description => "Zápis souborů do lokálního souborového systému";
        public override string Version => "1.0.0";
        public override AdapterType Type => AdapterType.Output;
        public override string Category => "File";

        public FileSystemOutputAdapter(ILogger<FileSystemOutputAdapter> logger) : base(logger)
        {
        }

        protected override void InitializeParameters()
        {
            AddParameter(new SimpleAdapterParameter
            {
                Name = "filePath",
                DisplayName = "Cesta k souboru",
                Description = "Absolutní nebo relativní cesta k souboru pro zápis",
                Type = ToolParameterType.String,
                IsRequired = true,
                IsCritical = true,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.File,
                    HelpText = "Zadejte cestu k souboru včetně názvu souboru"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "encoding",
                DisplayName = "Kódování",
                Description = "Kódování textu souboru",
                Type = ToolParameterType.String,
                IsRequired = false,
                DefaultValue = "UTF-8",
                Validation = new SimpleParameterValidation
                {
                    AllowedValues = new[] { "UTF-8", "UTF-16", "ASCII", "ISO-8859-1", "Windows-1250" }
                },
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Select,
                    HelpText = "Vyberte kódování textu"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "writeMode",
                DisplayName = "Režim zápisu",
                Description = "Jak zapsat soubor",
                Type = ToolParameterType.String,
                IsRequired = false,
                DefaultValue = "overwrite",
                Validation = new SimpleParameterValidation
                {
                    AllowedValues = new[] { "overwrite", "append", "create_new", "create_unique" }
                },
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Select,
                    HelpText = "overwrite = přepsat, append = připojit, create_new = vytvořit nový, create_unique = vytvořit s unikátním názvem"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "createDirectory",
                DisplayName = "Vytvořit složku",
                Description = "Vytvořit složku pokud neexistuje",
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
                Name = "format",
                DisplayName = "Formát",
                Description = "Formát pro serializaci dat",
                Type = ToolParameterType.String,
                IsRequired = false,
                DefaultValue = "auto",
                Validation = new SimpleParameterValidation
                {
                    AllowedValues = new[] { "auto", "text", "json", "xml", "csv", "binary" }
                },
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Select,
                    HelpText = "auto = automatická detekce dle přípony"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "jsonIndented",
                DisplayName = "JSON formátování",
                Description = "Formátovat JSON s odsazením",
                Type = ToolParameterType.Boolean,
                IsRequired = false,
                DefaultValue = true,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Checkbox,
                    HelpText = "Platí pouze pro JSON formát"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "csvDelimiter",
                DisplayName = "CSV oddělovač",
                Description = "Oddělovač pro CSV soubory",
                Type = ToolParameterType.String,
                IsRequired = false,
                DefaultValue = ",",
                Validation = new SimpleParameterValidation
                {
                    MaxLength = 1
                },
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Text,
                    Placeholder = ",",
                    HelpText = "Platí pouze pro CSV formát"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "filePermissions",
                DisplayName = "Oprávnění souboru",
                Description = "Unix oprávnění souboru (např. 644)",
                Type = ToolParameterType.String,
                IsRequired = false,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Text,
                    Placeholder = "644",
                    HelpText = "Platí pouze pro Unix/Linux systémy"
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
                var filePath = GetParameter<string>(configuration, "filePath");
                var encoding = GetParameter<string>(configuration, "encoding", "UTF-8");
                var writeMode = GetParameter<string>(configuration, "writeMode", "overwrite");
                var createDirectory = GetParameter<bool>(configuration, "createDirectory", true);
                var format = GetParameter<string>(configuration, "format", "auto");
                var jsonIndented = GetParameter<bool>(configuration, "jsonIndented", true);
                var csvDelimiter = GetParameter<string>(configuration, "csvDelimiter", ",");

                // Create directory if needed
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && createDirectory && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                    Logger.LogInformation("Created directory: {Directory}", directory);
                }

                // Determine format from extension if auto
                if (format == "auto")
                {
                    var extension = Path.GetExtension(filePath).ToLower();
                    format = extension switch
                    {
                        ".json" => "json",
                        ".xml" => "xml",
                        ".csv" => "csv",
                        ".txt" => "text",
                        _ => "text"
                    };
                }

                // Handle write mode
                var finalPath = filePath;
                switch (writeMode)
                {
                    case "create_new":
                        if (File.Exists(filePath))
                        {
                            return CreateExceptionResult(executionId, startTime, new InvalidOperationException($"File already exists: {filePath}"));
                        }
                        break;
                    
                    case "create_unique":
                        finalPath = GetUniqueFilePath(filePath);
                        break;
                }

                // Serialize data based on format
                byte[] contentBytes;
                var enc = Encoding.GetEncoding(encoding);

                switch (format.ToLower())
                {
                    case "json":
                        var jsonOptions = new System.Text.Json.JsonSerializerOptions
                        {
                            WriteIndented = jsonIndented,
                            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                        };
                        var jsonContent = System.Text.Json.JsonSerializer.Serialize(data, jsonOptions);
                        contentBytes = enc.GetBytes(jsonContent);
                        break;

                    case "xml":
                        if (data is string xmlString)
                        {
                            contentBytes = enc.GetBytes(xmlString);
                        }
                        else
                        {
                            // Simple XML serialization
                            var xmlContent = SerializeToXml(data);
                            contentBytes = enc.GetBytes(xmlContent);
                        }
                        break;

                    case "csv":
                        var csvContent = SerializeToCsv(data, csvDelimiter);
                        contentBytes = enc.GetBytes(csvContent);
                        break;

                    case "binary":
                        if (data is byte[] binaryData)
                        {
                            contentBytes = binaryData;
                        }
                        else
                        {
                            return CreateExceptionResult(executionId, startTime, new InvalidOperationException("Binary format requires byte[] data"));
                        }
                        break;

                    default: // text
                        var textContent = data?.ToString() ?? string.Empty;
                        contentBytes = enc.GetBytes(textContent);
                        break;
                }

                // Write file
                if (writeMode == "append" && File.Exists(finalPath))
                {
                    await File.AppendAllBytesAsync(finalPath, contentBytes, cancellationToken);
                }
                else
                {
                    await File.WriteAllBytesAsync(finalPath, contentBytes, cancellationToken);
                }

                // Set file permissions if specified (Unix/Linux only)
                if (!string.IsNullOrEmpty(GetParameter<string>(configuration, "filePermissions", null)) 
                    && Environment.OSVersion.Platform == PlatformID.Unix)
                {
                    // Would need platform-specific code to set permissions
                    Logger.LogWarning("File permissions setting not implemented for this platform");
                }

                metrics.ItemsProcessed = 1;
                metrics.BytesProcessed = contentBytes.Length;
                metrics.ProcessingTime = DateTime.UtcNow - startTime;
                metrics.ThroughputMBPerSecond = (metrics.BytesProcessed / 1024.0 / 1024.0) / Math.Max(0.001, metrics.ProcessingTime.TotalSeconds);

                var resultData = new Dictionary<string, object>
                {
                    ["filePath"] = finalPath,
                    ["bytesWritten"] = contentBytes.Length,
                    ["encoding"] = encoding,
                    ["format"] = format,
                    ["writeMode"] = writeMode,
                    ["timestamp"] = DateTime.UtcNow
                };

                Logger.LogInformation("Successfully wrote {Bytes} bytes to {FilePath}", contentBytes.Length, finalPath);

                return CreateSuccessResult(executionId, startTime, resultData, metrics);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error writing file");
                return CreateExceptionResult(executionId, startTime, ex);
            }
        }

        protected override async Task PerformDestinationValidationAsync(
            Dictionary<string, object> configuration,
            CancellationToken cancellationToken)
        {
            var filePath = GetParameter<string>(configuration, "filePath");
            var writeMode = GetParameter<string>(configuration, "writeMode", "overwrite");
            
            if (string.IsNullOrEmpty(filePath))
                throw new InvalidOperationException("File path is required");

            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                var createDirectory = GetParameter<bool>(configuration, "createDirectory", true);
                if (!Directory.Exists(directory) && !createDirectory)
                {
                    throw new DirectoryNotFoundException($"Directory not found: {directory}");
                }
            }

            // Check if we can write to the location
            if (writeMode == "create_new" && File.Exists(filePath))
            {
                throw new InvalidOperationException($"File already exists: {filePath}");
            }

            await Task.CompletedTask;
        }

        public override IReadOnlyList<IAdapterSchema> GetInputSchemas()
        {
            return new List<IAdapterSchema>
            {
                new FileSystemDataSchema
                {
                    Id = "any_data",
                    Name = "Any Data",
                    Description = "Any data to write to file",
                    JsonSchema = @"{
                        ""type"": [""object"", ""array"", ""string"", ""number"", ""boolean""],
                        ""description"": ""Any data that can be serialized to the target format""
                    }",
                    ExampleData = new Dictionary<string, object>
                    {
                        ["example"] = "This can be any data - text, JSON object, array, etc."
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
                MaxDataSizeBytes = 100 * 1024 * 1024, // 100 MB
                MaxConcurrentOperations = 10,
                SupportedFormats = new List<string> { "text", "json", "xml", "csv", "binary" },
                SupportedEncodings = new List<string> { "UTF-8", "UTF-16", "ASCII", "ISO-8859-1", "Windows-1250" },
                CustomCapabilities = new Dictionary<string, object>
                {
                    ["supportsAppend"] = true,
                    ["supportsCreateUnique"] = true,
                    ["supportsDirectoryCreation"] = true,
                    ["maxFileSize"] = 100 * 1024 * 1024
                }
            };
        }

        protected override async Task PerformHealthCheckAsync()
        {
            // Test if we can write to temp directory
            var tempPath = Path.GetTempPath();
            var testFile = Path.Combine(tempPath, $"health_check_{Guid.NewGuid()}.tmp");
            
            try
            {
                await File.WriteAllTextAsync(testFile, "health check");
                File.Delete(testFile);
            }
            catch
            {
                throw new InvalidOperationException("Cannot write to temp directory");
            }
        }

        private string GetUniqueFilePath(string originalPath)
        {
            if (!File.Exists(originalPath))
                return originalPath;

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

        private string SerializeToXml(object data)
        {
            // Simple XML serialization for basic types
            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            sb.AppendLine("<root>");
            
            if (data is IDictionary<string, object> dict)
            {
                foreach (var kvp in dict)
                {
                    sb.AppendLine($"  <{kvp.Key}>{System.Security.SecurityElement.Escape(kvp.Value?.ToString())}</{kvp.Key}>");
                }
            }
            else if (data is IEnumerable<object> list && !(data is string))
            {
                foreach (var item in list)
                {
                    sb.AppendLine($"  <item>{System.Security.SecurityElement.Escape(item?.ToString())}</item>");
                }
            }
            else
            {
                sb.AppendLine($"  <data>{System.Security.SecurityElement.Escape(data?.ToString())}</data>");
            }
            
            sb.AppendLine("</root>");
            return sb.ToString();
        }

        private string SerializeToCsv(object data, string delimiter)
        {
            var sb = new StringBuilder();
            
            if (data is IEnumerable<IDictionary<string, object>> rows)
            {
                var firstRow = rows.FirstOrDefault();
                if (firstRow != null)
                {
                    // Header
                    sb.AppendLine(string.Join(delimiter, firstRow.Keys.Select(k => EscapeCsvValue(k))));
                    
                    // Data rows
                    foreach (var row in rows)
                    {
                        var values = firstRow.Keys.Select(k => 
                            row.TryGetValue(k, out var value) ? EscapeCsvValue(value?.ToString()) : "");
                        sb.AppendLine(string.Join(delimiter, values));
                    }
                }
            }
            else if (data is IEnumerable<object> list && !(data is string))
            {
                foreach (var item in list)
                {
                    sb.AppendLine(EscapeCsvValue(item?.ToString()));
                }
            }
            else
            {
                sb.AppendLine(EscapeCsvValue(data?.ToString()));
            }
            
            return sb.ToString();
        }

        private string EscapeCsvValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";
            
            if (value.Contains("\"") || value.Contains(",") || value.Contains("\n") || value.Contains("\r"))
            {
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }
            
            return value;
        }
    }
}