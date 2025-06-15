using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Logging;
using OAI.Core.Interfaces.Adapters;
using OAI.Core.Interfaces.Tools;
using OAI.ServiceLayer.Services.Adapters.Base;

namespace OAI.ServiceLayer.Services.Adapters.Implementations
{
    /// <summary>
    /// CSV file input adapter
    /// </summary>
    public class CsvInputAdapter : BaseInputAdapter
    {
        public override string Id => "csv_input";
        public override string Name => "CSV Input";
        public override string Description => "Read data from CSV files with flexible configuration";
        public override string Version => "1.0.0";
        public override string Category => "File";

        public CsvInputAdapter(ILogger<CsvInputAdapter> logger) : base(logger)
        {
        }

        protected override void InitializeParameters()
        {
            AddParameter(new SimpleAdapterParameter
            {
                Name = "filePath",
                DisplayName = "File Path",
                Description = "Path to the CSV file",
                Type = ToolParameterType.String,
                IsRequired = true,
                IsCritical = true,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.File,
                    HelpText = "Select or provide path to CSV file",
                    FileExtensions = new[] { ".csv", ".txt" }
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "delimiter",
                DisplayName = "Delimiter",
                Description = "Character used to separate values",
                Type = ToolParameterType.String,
                IsRequired = false,
                DefaultValue = ",",
                Validation = new SimpleParameterValidation
                {
                    AllowedValues = new List<object> { ",", ";", "\t", "|" }
                },
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Select,
                    HelpText = "Choose the delimiter character"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "hasHeaders",
                DisplayName = "Has Headers",
                Description = "Whether the first row contains column headers",
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
                Name = "encoding",
                DisplayName = "File Encoding",
                Description = "Text encoding of the file",
                Type = ToolParameterType.String,
                IsRequired = false,
                DefaultValue = "UTF-8",
                Validation = new SimpleParameterValidation
                {
                    AllowedValues = new List<object> { "UTF-8", "UTF-16", "ASCII", "Windows-1252", "ISO-8859-1" }
                },
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Select
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "skipRows",
                DisplayName = "Skip Rows",
                Description = "Number of rows to skip from the beginning",
                Type = ToolParameterType.Integer,
                IsRequired = false,
                DefaultValue = 0,
                Validation = new SimpleParameterValidation
                {
                    MinValue = 0,
                    MaxValue = 1000
                },
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Number
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "maxRows",
                DisplayName = "Max Rows",
                Description = "Maximum number of rows to read (0 = all)",
                Type = ToolParameterType.Integer,
                IsRequired = false,
                DefaultValue = 0,
                Validation = new SimpleParameterValidation
                {
                    MinValue = 0
                },
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Number,
                    HelpText = "Leave as 0 to read all rows"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "trimValues",
                DisplayName = "Trim Values",
                Description = "Remove leading and trailing whitespace from values",
                Type = ToolParameterType.Boolean,
                IsRequired = false,
                DefaultValue = true
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "skipEmptyRows",
                DisplayName = "Skip Empty Rows",
                Description = "Skip rows that are completely empty",
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
            var filePath = GetParameter<string>(configuration, "filePath");
            var delimiter = GetParameter<string>(configuration, "delimiter", ",");
            var hasHeaders = GetParameter<bool>(configuration, "hasHeaders", true);
            var encoding = GetParameter<string>(configuration, "encoding", "UTF-8");
            var skipRows = GetParameter<int>(configuration, "skipRows", 0);
            var maxRows = GetParameter<int>(configuration, "maxRows", 0);
            var trimValues = GetParameter<bool>(configuration, "trimValues", true);
            var skipEmptyRows = GetParameter<bool>(configuration, "skipEmptyRows", true);

            var metrics = new AdapterMetrics();
            var startTime = DateTime.UtcNow;

            try
            {
                var fileEncoding = GetEncoding(encoding);
                var config = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    Delimiter = delimiter,
                    HasHeaderRecord = hasHeaders,
                    TrimOptions = trimValues ? TrimOptions.Trim : TrimOptions.None,
                    IgnoreBlankLines = skipEmptyRows,
                    MissingFieldFound = null // Don't throw on missing fields
                };

                var data = new List<Dictionary<string, object>>();
                var headers = new List<string>();

                using (var reader = new StreamReader(filePath, fileEncoding))
                {
                    // Skip initial rows if requested
                    for (int i = 0; i < skipRows; i++)
                    {
                        await reader.ReadLineAsync();
                    }

                    using (var csv = new CsvReader(reader, config))
                    {
                        // Read headers
                        if (hasHeaders)
                        {
                            await csv.ReadAsync();
                            csv.ReadHeader();
                            headers = csv.HeaderRecord?.ToList() ?? new List<string>();
                        }

                        // Read records
                        var rowCount = 0;
                        while (await csv.ReadAsync())
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            if (maxRows > 0 && rowCount >= maxRows)
                                break;

                            var record = new Dictionary<string, object>();

                            if (hasHeaders && headers.Any())
                            {
                                // Use headers
                                foreach (var header in headers)
                                {
                                    try
                                    {
                                        record[header] = csv.GetField(header);
                                    }
                                    catch
                                    {
                                        record[header] = null;
                                    }
                                }
                            }
                            else
                            {
                                // Use column indices
                                for (int i = 0; i < csv.Parser.Record.Length; i++)
                                {
                                    record[$"Column{i + 1}"] = csv.Parser.Record[i];
                                }
                            }

                            data.Add(record);
                            rowCount++;
                            metrics.ItemsProcessed++;
                        }
                    }
                }

                // Calculate metrics
                metrics.ProcessingTime = DateTime.UtcNow - startTime;
                metrics.BytesProcessed = new FileInfo(filePath).Length;
                metrics.ThroughputItemsPerSecond = metrics.ItemsProcessed / Math.Max(1, metrics.ProcessingTime.TotalSeconds);
                metrics.ThroughputMBPerSecond = (metrics.BytesProcessed / 1024.0 / 1024.0) / Math.Max(1, metrics.ProcessingTime.TotalSeconds);

                // Create schema
                var schema = new CsvDataSchema
                {
                    Id = "csv_data",
                    Name = "CSV Data",
                    Description = $"Data from {Path.GetFileName(filePath)}",
                    Fields = (headers.Any() ? headers : Enumerable.Range(1, data.FirstOrDefault()?.Count ?? 0)
                        .Select(i => $"Column{i}")
                        .ToList())
                        .Select(h => new SchemaField
                        {
                            Name = h,
                            Type = "string",
                            IsRequired = false
                        }).ToList()
                };

                // Create preview
                var preview = data.Take(5).ToList();

                Logger.LogInformation("Successfully read {RowCount} rows from CSV file", data.Count);

                return CreateSuccessResult(executionId, startTime, data, metrics, schema, preview);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error reading CSV file: {FilePath}", filePath);
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

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"CSV file not found: {filePath}");

            var fileInfo = new FileInfo(filePath);
            var validExtensions = new[] { ".csv", ".txt" };
            
            if (!validExtensions.Contains(fileInfo.Extension.ToLower()))
                throw new InvalidOperationException($"Invalid file type. Expected CSV file (.csv or .txt)");

            await Task.CompletedTask;
        }

        public override IReadOnlyList<IAdapterSchema> GetOutputSchemas()
        {
            return new List<IAdapterSchema>
            {
                new CsvDataSchema
                {
                    Id = "csv_tabular",
                    Name = "CSV Tabular Data",
                    Description = "Tabular data read from CSV file",
                    JsonSchema = @"{
                        ""type"": ""array"",
                        ""items"": {
                            ""type"": ""object"",
                            ""additionalProperties"": true
                        }
                    }",
                    ExampleData = new[]
                    {
                        new Dictionary<string, object>
                        {
                            ["Name"] = "John Doe",
                            ["Age"] = "30",
                            ["Email"] = "john@example.com"
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
                MaxDataSizeBytes = 500 * 1024 * 1024, // 500 MB
                MaxConcurrentOperations = 20,
                SupportedFormats = new List<string> { "csv", "txt" },
                SupportedEncodings = new List<string> { "UTF-8", "UTF-16", "ASCII", "Windows-1252", "ISO-8859-1" },
                CustomCapabilities = new Dictionary<string, object>
                {
                    ["supportsCustomDelimiters"] = true,
                    ["supportsHeaderDetection"] = true,
                    ["supportsPartialReading"] = true
                }
            };
        }

        private Encoding GetEncoding(string encodingName)
        {
            return encodingName?.ToUpper() switch
            {
                "UTF-8" => Encoding.UTF8,
                "UTF-16" => Encoding.Unicode,
                "ASCII" => Encoding.ASCII,
                "WINDOWS-1252" => Encoding.GetEncoding(1252),
                "ISO-8859-1" => Encoding.GetEncoding("ISO-8859-1"),
                _ => Encoding.UTF8
            };
        }

        protected override async Task PerformHealthCheckAsync()
        {
            // Test if we can read CSV
            var testData = "header1,header2\nvalue1,value2";
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(testData));
            using var reader = new StreamReader(stream);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            
            await csv.ReadAsync();
        }
    }

    /// <summary>
    /// Schema implementation for CSV data
    /// </summary>
    internal class CsvDataSchema : IAdapterSchema
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string JsonSchema { get; set; }
        public object ExampleData { get; set; }
        public IReadOnlyList<SchemaField> Fields { get; set; } = new List<SchemaField>();
    }
}