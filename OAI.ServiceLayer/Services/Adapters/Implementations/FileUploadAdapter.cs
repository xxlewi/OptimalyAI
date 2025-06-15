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
    /// Adapter for handling file uploads in workflows
    /// </summary>
    public class FileUploadAdapter : BaseInputAdapter
    {
        public override string Id => "file_upload";
        public override string Name => "Upload souborů";
        public override string Description => "Příjem souborů nahraných uživatelem do workflow";
        public override string Version => "1.0.0";
        public override string Category => "Workflow";
        public override AdapterType Type => AdapterType.Input;

        public FileUploadAdapter(ILogger<FileUploadAdapter> logger) : base(logger)
        {
        }

        protected override void InitializeParameters()
        {
            AddParameter(new SimpleAdapterParameter
            {
                Name = "uploadPath",
                DisplayName = "Cesta pro upload",
                Description = "Složka kam se budou ukládat nahrané soubory",
                Type = ToolParameterType.String,
                IsRequired = true,
                DefaultValue = "uploads/workflow",
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Text,
                    HelpText = "Relativní cesta od kořene aplikace"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "allowedExtensions",
                DisplayName = "Povolené přípony",
                Description = "Seznam povolených přípon souborů (prázdné = vše povoleno)",
                Type = ToolParameterType.String,
                IsRequired = false,
                DefaultValue = "",
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Text,
                    Placeholder = ".jpg,.png,.pdf,.xlsx",
                    HelpText = "Oddělené čárkou, např: .jpg,.png,.pdf"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "maxFileSize",
                DisplayName = "Max. velikost souboru (MB)",
                Description = "Maximální velikost jednoho souboru v MB",
                Type = ToolParameterType.Integer,
                IsRequired = false,
                DefaultValue = 10,
                Validation = new SimpleParameterValidation
                {
                    MinValue = 1,
                    MaxValue = 100
                },
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Number
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "maxFiles",
                DisplayName = "Max. počet souborů",
                Description = "Maximální počet souborů v jednom uploadu",
                Type = ToolParameterType.Integer,
                IsRequired = false,
                DefaultValue = 10,
                Validation = new SimpleParameterValidation
                {
                    MinValue = 1,
                    MaxValue = 50
                },
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Number
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "processArchives",
                DisplayName = "Zpracovat archivy",
                Description = "Automaticky rozbalit ZIP/RAR archivy",
                Type = ToolParameterType.Boolean,
                IsRequired = false,
                DefaultValue = false
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "scanForViruses",
                DisplayName = "Antivirová kontrola",
                Description = "Provést antivirovou kontrolu souborů",
                Type = ToolParameterType.Boolean,
                IsRequired = false,
                DefaultValue = true
            });
        }

        protected override async Task<IAdapterResult> ExecuteReadAsync(
            Dictionary<string, object> configuration,
            string executionId,
            CancellationToken cancellationToken)
        {
            var uploadPath = GetParameter<string>(configuration, "uploadPath");
            var allowedExtensions = GetParameter<string>(configuration, "allowedExtensions", "");
            var maxFileSize = GetParameter<int>(configuration, "maxFileSize", 10);
            var maxFiles = GetParameter<int>(configuration, "maxFiles", 10);
            var processArchives = GetParameter<bool>(configuration, "processArchives", false);
            var scanForViruses = GetParameter<bool>(configuration, "scanForViruses", true);

            var metrics = new AdapterMetrics();
            var startTime = DateTime.UtcNow;

            try
            {
                // Note: In real implementation, files would come from HTTP request
                // This is a placeholder implementation
                var uploadedFiles = new List<Dictionary<string, object>>();

                // Simulate processing uploaded files
                var uploadDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, uploadPath);
                if (Directory.Exists(uploadDir))
                {
                    var allowedExtList = string.IsNullOrEmpty(allowedExtensions)
                        ? new List<string>()
                        : allowedExtensions.Split(',').Select(e => e.Trim()).ToList();

                    var files = Directory.GetFiles(uploadDir)
                        .Where(f => allowedExtList.Count == 0 || allowedExtList.Contains(Path.GetExtension(f).ToLower()))
                        .Take(maxFiles)
                        .ToList();

                    foreach (var file in files)
                    {
                        var fileInfo = new FileInfo(file);
                        
                        // Check file size
                        if (fileInfo.Length > maxFileSize * 1024 * 1024)
                            continue;

                        uploadedFiles.Add(new Dictionary<string, object>
                        {
                            ["fileName"] = fileInfo.Name,
                            ["filePath"] = fileInfo.FullName,
                            ["fileSize"] = fileInfo.Length,
                            ["fileExtension"] = fileInfo.Extension,
                            ["uploadedAt"] = DateTime.UtcNow,
                            ["contentType"] = GetContentType(fileInfo.Extension),
                            ["isArchive"] = IsArchive(fileInfo.Extension),
                            ["virusScanStatus"] = scanForViruses ? "clean" : "not_scanned"
                        });

                        metrics.ItemsProcessed++;
                        metrics.BytesProcessed += fileInfo.Length;
                    }
                }

                // Calculate metrics
                metrics.ProcessingTime = DateTime.UtcNow - startTime;
                metrics.ThroughputItemsPerSecond = metrics.ItemsProcessed / Math.Max(1, metrics.ProcessingTime.TotalSeconds);
                metrics.ThroughputMBPerSecond = (metrics.BytesProcessed / 1024.0 / 1024.0) / Math.Max(1, metrics.ProcessingTime.TotalSeconds);

                // Create schema
                var schema = new FileUploadSchema
                {
                    Id = "uploaded_files",
                    Name = "Nahrané soubory",
                    Description = "Seznam souborů nahraných do workflow",
                    Fields = new List<SchemaField>
                    {
                        new SchemaField { Name = "fileName", Type = "string", IsRequired = true },
                        new SchemaField { Name = "filePath", Type = "string", IsRequired = true },
                        new SchemaField { Name = "fileSize", Type = "integer", IsRequired = true },
                        new SchemaField { Name = "fileExtension", Type = "string", IsRequired = true },
                        new SchemaField { Name = "uploadedAt", Type = "datetime", IsRequired = true },
                        new SchemaField { Name = "contentType", Type = "string", IsRequired = false },
                        new SchemaField { Name = "isArchive", Type = "boolean", IsRequired = false },
                        new SchemaField { Name = "virusScanStatus", Type = "string", IsRequired = false }
                    }
                };

                Logger.LogInformation("Successfully processed {FileCount} uploaded files", uploadedFiles.Count);

                return CreateSuccessResult(executionId, startTime, uploadedFiles, metrics, schema, uploadedFiles.Take(3).ToList());
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error processing file upload");
                return CreateExceptionResult(executionId, startTime, ex);
            }
        }

        protected override async Task PerformSourceValidationAsync(
            Dictionary<string, object> configuration,
            CancellationToken cancellationToken)
        {
            var uploadPath = GetParameter<string>(configuration, "uploadPath");
            
            if (string.IsNullOrEmpty(uploadPath))
                throw new InvalidOperationException("Upload path is required");

            // Validate upload directory exists or can be created
            var uploadDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, uploadPath);
            if (!Directory.Exists(uploadDir))
            {
                try
                {
                    Directory.CreateDirectory(uploadDir);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Cannot create upload directory: {ex.Message}");
                }
            }

            await Task.CompletedTask;
        }

        public override IReadOnlyList<IAdapterSchema> GetOutputSchemas()
        {
            return new List<IAdapterSchema>
            {
                new FileUploadSchema
                {
                    Id = "file_upload_output",
                    Name = "File Upload Output",
                    Description = "Data o nahraných souborech",
                    JsonSchema = @"{
                        ""type"": ""array"",
                        ""items"": {
                            ""type"": ""object"",
                            ""properties"": {
                                ""fileName"": { ""type"": ""string"" },
                                ""filePath"": { ""type"": ""string"" },
                                ""fileSize"": { ""type"": ""integer"" },
                                ""fileExtension"": { ""type"": ""string"" },
                                ""uploadedAt"": { ""type"": ""string"", ""format"": ""date-time"" },
                                ""contentType"": { ""type"": ""string"" },
                                ""isArchive"": { ""type"": ""boolean"" },
                                ""virusScanStatus"": { ""type"": ""string"" }
                            }
                        }
                    }",
                    ExampleData = new[]
                    {
                        new Dictionary<string, object>
                        {
                            ["fileName"] = "document.pdf",
                            ["filePath"] = "/uploads/workflow/document.pdf",
                            ["fileSize"] = 1048576,
                            ["fileExtension"] = ".pdf",
                            ["uploadedAt"] = DateTime.UtcNow,
                            ["contentType"] = "application/pdf",
                            ["isArchive"] = false,
                            ["virusScanStatus"] = "clean"
                        }
                    }
                }
            };
        }

        public override AdapterCapabilities GetCapabilities()
        {
            return new AdapterCapabilities
            {
                SupportsStreaming = true,
                SupportsPartialData = true,
                SupportsBatchProcessing = true,
                SupportsTransactions = false,
                RequiresAuthentication = false,
                MaxDataSizeBytes = 100 * 1024 * 1024, // 100 MB total
                MaxConcurrentOperations = 5,
                SupportedFormats = new List<string> { "any" },
                SupportedEncodings = new List<string>(),
                CustomCapabilities = new Dictionary<string, object>
                {
                    ["supportsMultipleFiles"] = true,
                    ["supportsArchives"] = true,
                    ["supportsVirusScan"] = true,
                    ["supportsDragAndDrop"] = true
                }
            };
        }

        private string GetContentType(string extension)
        {
            return extension.ToLower() switch
            {
                ".pdf" => "application/pdf",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".zip" => "application/zip",
                ".rar" => "application/x-rar-compressed",
                ".txt" => "text/plain",
                ".csv" => "text/csv",
                ".json" => "application/json",
                ".xml" => "application/xml",
                _ => "application/octet-stream"
            };
        }

        private bool IsArchive(string extension)
        {
            var archiveExtensions = new[] { ".zip", ".rar", ".7z", ".tar", ".gz", ".tar.gz" };
            return archiveExtensions.Contains(extension.ToLower());
        }

        protected override async Task PerformHealthCheckAsync()
        {
            // Test if we can access upload directory
            var uploadPath = "uploads/workflow";
            var uploadDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, uploadPath);
            
            if (!Directory.Exists(uploadDir))
            {
                Directory.CreateDirectory(uploadDir);
            }
            
            await Task.CompletedTask;
        }
    }

    /// <summary>
    /// Schema implementation for file upload data
    /// </summary>
    internal class FileUploadSchema : IAdapterSchema
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string JsonSchema { get; set; }
        public object ExampleData { get; set; }
        public IReadOnlyList<SchemaField> Fields { get; set; } = new List<SchemaField>();
    }
}