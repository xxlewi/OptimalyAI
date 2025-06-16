using System;
using System.Collections.Generic;
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
    /// File system input adapter for reading files
    /// </summary>
    public class FileSystemInputAdapter : BaseInputAdapter
    {
        public override string Id => "filesystem_input";
        public override string Name => "Čtení souborů";
        public override string Description => "Čtení souborů z lokálního souborového systému";
        public override string Version => "1.0.0";
        public override string Category => "File";
        public override AdapterType Type => AdapterType.Input;

        public FileSystemInputAdapter(ILogger<FileSystemInputAdapter> logger) : base(logger)
        {
        }

        protected override void InitializeParameters()
        {
            AddParameter(new SimpleAdapterParameter
            {
                Name = "filePath",
                DisplayName = "Cesta k souboru",
                Description = "Absolutní nebo relativní cesta k souboru",
                Type = ToolParameterType.String,
                IsRequired = true,
                IsCritical = true,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.File,
                    HelpText = "Zadejte cestu k souboru nebo použijte prohlížeč souborů"
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
                Name = "readMode",
                DisplayName = "Režim čtení",
                Description = "Jak číst soubor",
                Type = ToolParameterType.String,
                IsRequired = false,
                DefaultValue = "text",
                Validation = new SimpleParameterValidation
                {
                    AllowedValues = new[] { "text", "binary", "lines", "json", "xml" }
                },
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Select,
                    HelpText = "text = celý text, binary = binární data, lines = po řádcích, json/xml = parsovat"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "lineRange",
                DisplayName = "Rozsah řádků",
                Description = "Číst pouze určité řádky (např. 1-100)",
                Type = ToolParameterType.String,
                IsRequired = false,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Text,
                    Placeholder = "1-100",
                    HelpText = "Prázdné = všechny řádky"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "pattern",
                DisplayName = "Vzor souborů",
                Description = "Vzor pro hledání více souborů (wildcards)",
                Type = ToolParameterType.String,
                IsRequired = false,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Text,
                    Placeholder = "*.txt, *.log",
                    HelpText = "Použijte pro čtení více souborů najednou"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "recursive",
                DisplayName = "Rekurzivně",
                Description = "Hledat soubory i v podsložkách",
                Type = ToolParameterType.Boolean,
                IsRequired = false,
                DefaultValue = false,
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
                var filePath = GetParameter<string>(configuration, "filePath");
                var encoding = GetParameter<string>(configuration, "encoding", "UTF-8");
                var readMode = GetParameter<string>(configuration, "readMode", "text");
                var pattern = GetParameter<string>(configuration, "pattern", string.Empty);
                var recursive = GetParameter<bool>(configuration, "recursive", false);

                var files = new List<FileInfo>();
                
                // Determine files to read
                if (!string.IsNullOrEmpty(pattern))
                {
                    var directory = Path.GetDirectoryName(filePath) ?? ".";
                    var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                    files.AddRange(Directory.GetFiles(directory, pattern, searchOption).Select(f => new FileInfo(f)));
                }
                else if (File.Exists(filePath))
                {
                    files.Add(new FileInfo(filePath));
                }
                else if (Directory.Exists(filePath))
                {
                    var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                    files.AddRange(Directory.GetFiles(filePath, "*.*", searchOption).Select(f => new FileInfo(f)));
                }
                else
                {
                    return CreateErrorResult(executionId, startTime, $"File or directory not found: {filePath}");
                }

                var data = new List<Dictionary<string, object>>();
                var enc = System.Text.Encoding.GetEncoding(encoding);

                foreach (var file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var fileData = new Dictionary<string, object>
                    {
                        ["fileName"] = file.Name,
                        ["filePath"] = file.FullName,
                        ["size"] = file.Length,
                        ["createdAt"] = file.CreationTimeUtc,
                        ["modifiedAt"] = file.LastWriteTimeUtc,
                        ["extension"] = file.Extension
                    };

                    switch (readMode.ToLower())
                    {
                        case "binary":
                            fileData["content"] = await File.ReadAllBytesAsync(file.FullName, cancellationToken);
                            fileData["contentType"] = "binary";
                            break;
                        
                        case "lines":
                            var lines = await File.ReadAllLinesAsync(file.FullName, enc, cancellationToken);
                            fileData["content"] = lines;
                            fileData["contentType"] = "lines";
                            fileData["lineCount"] = lines.Length;
                            break;
                        
                        case "json":
                            var jsonText = await File.ReadAllTextAsync(file.FullName, enc, cancellationToken);
                            fileData["content"] = System.Text.Json.JsonSerializer.Deserialize<object>(jsonText);
                            fileData["contentType"] = "json";
                            break;
                        
                        case "xml":
                            var xmlText = await File.ReadAllTextAsync(file.FullName, enc, cancellationToken);
                            var xmlDoc = new System.Xml.XmlDocument();
                            xmlDoc.LoadXml(xmlText);
                            fileData["content"] = xmlDoc.OuterXml;
                            fileData["contentType"] = "xml";
                            break;
                        
                        default: // text
                            fileData["content"] = await File.ReadAllTextAsync(file.FullName, enc, cancellationToken);
                            fileData["contentType"] = "text";
                            break;
                    }

                    data.Add(fileData);
                    metrics.ItemsProcessed++;
                    metrics.BytesProcessed += file.Length;
                }

                metrics.ProcessingTime = DateTime.UtcNow - startTime;
                metrics.ThroughputItemsPerSecond = metrics.ItemsProcessed / Math.Max(1, metrics.ProcessingTime.TotalSeconds);
                metrics.ThroughputMBPerSecond = (metrics.BytesProcessed / 1024.0 / 1024.0) / Math.Max(1, metrics.ProcessingTime.TotalSeconds);

                // Create schema
                var schema = new FileSystemDataSchema
                {
                    Id = "filesystem_data",
                    Name = "File System Data",
                    Description = $"Data from {files.Count} file(s)",
                    Fields = new List<SchemaField>
                    {
                        new SchemaField { Name = "fileName", Type = "string", IsRequired = true },
                        new SchemaField { Name = "filePath", Type = "string", IsRequired = true },
                        new SchemaField { Name = "size", Type = "number", IsRequired = true },
                        new SchemaField { Name = "content", Type = readMode == "binary" ? "binary" : "string", IsRequired = true },
                        new SchemaField { Name = "contentType", Type = "string", IsRequired = true }
                    }
                };

                Logger.LogInformation("Successfully read {Count} file(s), {Bytes} bytes", files.Count, metrics.BytesProcessed);

                return CreateSuccessResult(executionId, startTime, data, metrics, schema, data.Take(5).ToList());
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error reading files");
                return CreateExceptionResult(executionId, startTime, ex);
            }
        }

        protected override async Task PerformSourceValidationAsync(
            Dictionary<string, object> configuration,
            CancellationToken cancellationToken)
        {
            var filePath = GetParameter<string>(configuration, "filePath");
            
            if (string.IsNullOrEmpty(filePath))
                throw new InvalidOperationException("File path is required");

            // Check if path exists (file or directory)
            if (!File.Exists(filePath) && !Directory.Exists(filePath))
            {
                // Check if it's a pattern in an existing directory
                var directory = Path.GetDirectoryName(filePath);
                if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                {
                    throw new FileNotFoundException($"Path not found: {filePath}");
                }
            }

            await Task.CompletedTask;
        }

        public override IReadOnlyList<IAdapterSchema> GetOutputSchemas()
        {
            return new List<IAdapterSchema>
            {
                new FileSystemDataSchema
                {
                    Id = "filesystem_file",
                    Name = "File System File",
                    Description = "File data from file system",
                    JsonSchema = @"{
                        ""type"": ""object"",
                        ""properties"": {
                            ""fileName"": { ""type"": ""string"" },
                            ""filePath"": { ""type"": ""string"" },
                            ""size"": { ""type"": ""number"" },
                            ""content"": { ""type"": [""string"", ""array"", ""object""] },
                            ""contentType"": { ""type"": ""string"" },
                            ""createdAt"": { ""type"": ""string"", ""format"": ""date-time"" },
                            ""modifiedAt"": { ""type"": ""string"", ""format"": ""date-time"" },
                            ""extension"": { ""type"": ""string"" }
                        },
                        ""required"": [""fileName"", ""filePath"", ""content""]
                    }",
                    ExampleData = new Dictionary<string, object>
                    {
                        ["fileName"] = "example.txt",
                        ["filePath"] = "/path/to/example.txt",
                        ["size"] = 1024,
                        ["content"] = "File content here...",
                        ["contentType"] = "text",
                        ["extension"] = ".txt"
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
                MaxDataSizeBytes = 100 * 1024 * 1024, // 100 MB
                MaxConcurrentOperations = 20,
                SupportedFormats = new List<string> { "text", "json", "xml", "binary" },
                SupportedEncodings = new List<string> { "UTF-8", "UTF-16", "ASCII", "ISO-8859-1", "Windows-1250" },
                CustomCapabilities = new Dictionary<string, object>
                {
                    ["supportsWildcards"] = true,
                    ["supportsRecursive"] = true,
                    ["supportsLineRange"] = true,
                    ["maxFileSize"] = 100 * 1024 * 1024
                }
            };
        }

        protected override async Task PerformHealthCheckAsync()
        {
            // Test if we can access temp directory
            var tempPath = Path.GetTempPath();
            if (!Directory.Exists(tempPath))
            {
                throw new InvalidOperationException("Cannot access temp directory");
            }
            await Task.CompletedTask;
        }
    }

    /// <summary>
    /// Schema implementation for file system data
    /// </summary>
    internal class FileSystemDataSchema : IAdapterSchema
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string JsonSchema { get; set; }
        public object ExampleData { get; set; }
        public IReadOnlyList<SchemaField> Fields { get; set; } = new List<SchemaField>();
    }
}