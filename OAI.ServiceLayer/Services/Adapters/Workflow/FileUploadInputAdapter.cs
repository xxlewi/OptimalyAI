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

namespace OAI.ServiceLayer.Services.Adapters.Workflow
{
    /// <summary>
    /// Input adapter for file uploads in workflow
    /// </summary>
    public class FileUploadInputAdapter : BaseInputAdapter
    {
        public override string Id => "file-upload-input";
        public override string Name => "File Upload Input";
        public override string Description => "Reads uploaded files from workflow input";
        public override string Version => "1.0.0";
        public override string Category => "File";

        public FileUploadInputAdapter(ILogger<FileUploadInputAdapter> logger) : base(logger)
        {
            InitializeParameters();
        }

        private void InitializeParameters()
        {
            AddParameter(new AdapterParameter
            {
                Name = "uploadPath",
                DisplayName = "Upload Path",
                Description = "Path to uploaded files directory",
                Type = ToolParameterType.String,
                IsRequired = true,
                DefaultValue = "/uploads"
            });

            AddParameter(new AdapterParameter
            {
                Name = "filePattern",
                DisplayName = "File Pattern",
                Description = "Pattern to match files (e.g., *.csv, *.json)",
                Type = ToolParameterType.String,
                IsRequired = false,
                DefaultValue = "*.*"
            });

            AddParameter(new AdapterParameter
            {
                Name = "recursive",
                DisplayName = "Recursive",
                Description = "Search subdirectories",
                Type = ToolParameterType.Boolean,
                IsRequired = false,
                DefaultValue = false
            });

            AddParameter(new AdapterParameter
            {
                Name = "maxFiles",
                DisplayName = "Maximum Files",
                Description = "Maximum number of files to process",
                Type = ToolParameterType.Integer,
                IsRequired = false,
                DefaultValue = 100,
                Validation = new ParameterValidation
                {
                    MinValue = 1,
                    MaxValue = 1000
                }
            });
        }

        protected override async Task<bool> ValidateSourceAsync(
            Dictionary<string, object> configuration, 
            CancellationToken cancellationToken)
        {
            var uploadPath = GetParameter<string>(configuration, "uploadPath");
            return await Task.FromResult(Directory.Exists(uploadPath));
        }

        protected override IEnumerable<IDataSchema> GetSupportedInputSchemas()
        {
            yield return new DataSchema
            {
                Id = "file-info",
                Name = "File Information",
                Description = "File metadata",
                Fields = new List<SchemaField>
                {
                    new SchemaField { Name = "path", Type = "string", IsRequired = true },
                    new SchemaField { Name = "name", Type = "string", IsRequired = true },
                    new SchemaField { Name = "size", Type = "integer", IsRequired = true },
                    new SchemaField { Name = "lastModified", Type = "datetime", IsRequired = true },
                    new SchemaField { Name = "content", Type = "string", IsRequired = false }
                }
            };
        }

        protected override async Task<object> ProcessDataAsync(
            Dictionary<string, object> configuration,
            CancellationToken cancellationToken)
        {
            var uploadPath = GetParameter<string>(configuration, "uploadPath");
            var filePattern = GetParameter<string>(configuration, "filePattern", "*.*");
            var recursive = GetParameter<bool>(configuration, "recursive", false);
            var maxFiles = GetParameter<int>(configuration, "maxFiles", 100);

            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var files = Directory.GetFiles(uploadPath, filePattern, searchOption)
                .Take(maxFiles)
                .ToList();

            var results = new List<Dictionary<string, object>>();

            foreach (var file in files)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var fileInfo = new FileInfo(file);
                var fileData = new Dictionary<string, object>
                {
                    ["path"] = file,
                    ["name"] = fileInfo.Name,
                    ["size"] = fileInfo.Length,
                    ["lastModified"] = fileInfo.LastWriteTimeUtc,
                    ["extension"] = fileInfo.Extension
                };

                // Optionally read file content for small files
                if (fileInfo.Length < 1024 * 1024) // Less than 1MB
                {
                    try
                    {
                        fileData["content"] = await File.ReadAllTextAsync(file, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning(ex, "Could not read content of file {File}", file);
                    }
                }

                results.Add(fileData);
            }

            return results;
        }

        public override AdapterCapabilities GetCapabilities()
        {
            return new AdapterCapabilities
            {
                SupportsBatch = true,
                SupportsStreaming = false,
                SupportsTransactions = false,
                MaxBatchSize = 1000,
                SupportedOperations = new[] { "read" }
            };
        }
    }
}