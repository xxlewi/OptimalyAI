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
    /// CSV file output adapter
    /// </summary>
    public class CsvOutputAdapter : BaseOutputAdapter
    {
        public override string Id => "csv_output";
        public override string Name => "CSV Output";
        public override string Description => "Write data to CSV files with customizable formatting";
        public override string Version => "1.0.0";
        public override string Category => "File";
        public override AdapterType Type => AdapterType.Output;

        public CsvOutputAdapter(ILogger<CsvOutputAdapter> logger) : base(logger)
        {
        }

        protected override void InitializeParameters()
        {
            AddParameter(new SimpleAdapterParameter
            {
                Name = "filePath",
                DisplayName = "File Path",
                Description = "Path where the CSV file will be saved",
                Type = ToolParameterType.String,
                IsRequired = true,
                IsCritical = true,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.File,
                    HelpText = "Specify the output CSV file path",
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
                Name = "includeHeaders",
                DisplayName = "Include Headers",
                Description = "Whether to write column headers as the first row",
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
                Description = "Text encoding for the file",
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
                Name = "appendMode",
                DisplayName = "Append Mode",
                Description = "How to handle existing files",
                Type = ToolParameterType.String,
                IsRequired = false,
                DefaultValue = "overwrite",
                Validation = new SimpleParameterValidation
                {
                    AllowedValues = new List<object> { "overwrite", "append" }
                },
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Select,
                    HelpText = "overwrite: replace existing file, append: add to existing file"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "quote",
                DisplayName = "Quote Character",
                Description = "Character used to quote fields containing special characters",
                Type = ToolParameterType.String,
                IsRequired = false,
                DefaultValue = "\"",
                Validation = new SimpleParameterValidation
                {
                    AllowedValues = new List<object> { "\"", "'", "" }
                },
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Select,
                    HelpText = "Leave empty for no quoting"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "escapeCharacter",
                DisplayName = "Escape Character",
                Description = "Character used to escape special characters",
                Type = ToolParameterType.String,
                IsRequired = false,
                DefaultValue = "\\",
                Validation = new SimpleParameterValidation
                {
                    MaxLength = 1
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "newLine",
                DisplayName = "New Line Character",
                Description = "Character(s) used for line breaks",
                Type = ToolParameterType.String,
                IsRequired = false,
                DefaultValue = "CRLF",
                Validation = new SimpleParameterValidation
                {
                    AllowedValues = new List<object> { "CRLF", "LF", "CR" }
                },
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Select,
                    HelpText = "CRLF: Windows, LF: Unix/Mac, CR: Old Mac"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "nullValue",
                DisplayName = "Null Value Representation",
                Description = "How to represent null values in the CSV",
                Type = ToolParameterType.String,
                IsRequired = false,
                DefaultValue = "",
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Text,
                    Placeholder = "Leave empty for blank",
                    HelpText = "Text to use for null values (e.g., 'NULL', 'N/A')"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "dateFormat",
                DisplayName = "Date Format",
                Description = "Format for date/time values",
                Type = ToolParameterType.String,
                IsRequired = false,
                DefaultValue = "yyyy-MM-dd HH:mm:ss",
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Text,
                    HelpText = "C# date format string"
                }
            });
        }

        protected override async Task<IAdapterResult> ExecuteWriteAsync(
            object data,
            Dictionary<string, object> configuration,
            string executionId,
            CancellationToken cancellationToken)
        {
            var filePath = GetParameter<string>(configuration, "filePath");
            var delimiter = GetParameter<string>(configuration, "delimiter", ",");
            var includeHeaders = GetParameter<bool>(configuration, "includeHeaders", true);
            var encoding = GetParameter<string>(configuration, "encoding", "UTF-8");
            var appendMode = GetParameter<string>(configuration, "appendMode", "overwrite");
            var quote = GetParameter<string>(configuration, "quote", "\"");
            var escapeChar = GetParameter<string>(configuration, "escapeCharacter", "\\");
            var newLine = GetParameter<string>(configuration, "newLine", "CRLF");
            var nullValue = GetParameter<string>(configuration, "nullValue", "");
            var dateFormat = GetParameter<string>(configuration, "dateFormat", "yyyy-MM-dd HH:mm:ss");

            var metrics = new AdapterMetrics();
            var startTime = DateTime.UtcNow;

            try
            {
                // Ensure directory exists
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var fileEncoding = GetEncoding(encoding);
                var config = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    Delimiter = delimiter,
                    HasHeaderRecord = includeHeaders && appendMode != "append",
                    Quote = string.IsNullOrEmpty(quote) ? '\0' : quote[0],
                    Escape = string.IsNullOrEmpty(escapeChar) ? '\0' : escapeChar[0],
                    NewLine = GetNewLine(newLine),
                    MissingFieldFound = null
                };

                // Convert data to enumerable of records
                var records = ConvertToRecords(data);
                if (!records.Any())
                {
                    Logger.LogWarning("No data to write to CSV");
                    return CreateSuccessResult(executionId, startTime, 
                        new { filePath = filePath, rowsWritten = 0 }, 
                        metrics);
                }

                // Determine write mode
                var fileMode = appendMode == "append" && File.Exists(filePath) 
                    ? FileMode.Append 
                    : FileMode.Create;

                using (var stream = new FileStream(filePath, fileMode, FileAccess.Write, FileShare.Read))
                using (var writer = new StreamWriter(stream, fileEncoding))
                using (var csv = new CsvWriter(writer, config))
                {
                    // Configure type conversion
                    csv.Context.TypeConverterOptionsCache.GetOptions<DateTime>().Formats = new[] { dateFormat };
                    csv.Context.TypeConverterOptionsCache.GetOptions<DateTime?>().Formats = new[] { dateFormat };
                    csv.Context.TypeConverterOptionsCache.GetOptions<string>().NullValues.Add(nullValue);

                    // Write records
                    var isFirstRecord = true;
                    foreach (var record in records)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (record is Dictionary<string, object> dict)
                        {
                            // Handle dictionary records
                            if (isFirstRecord && includeHeaders && fileMode != FileMode.Append)
                            {
                                // Write headers
                                foreach (var key in dict.Keys)
                                {
                                    csv.WriteField(key);
                                }
                                await csv.NextRecordAsync();
                            }

                            // Write values
                            foreach (var value in dict.Values)
                            {
                                if (value == null)
                                {
                                    csv.WriteField(nullValue);
                                }
                                else if (value is DateTime dt)
                                {
                                    csv.WriteField(dt.ToString(dateFormat));
                                }
                                else
                                {
                                    csv.WriteField(value);
                                }
                            }
                            await csv.NextRecordAsync();
                        }
                        else
                        {
                            // Handle object records
                            csv.WriteRecord(record);
                            await csv.NextRecordAsync();
                        }

                        metrics.ItemsProcessed++;
                        isFirstRecord = false;
                    }

                    await writer.FlushAsync();
                }

                // Calculate metrics
                var fileInfo = new FileInfo(filePath);
                metrics.BytesProcessed = fileInfo.Length;
                metrics.ProcessingTime = DateTime.UtcNow - startTime;
                metrics.ThroughputItemsPerSecond = metrics.ItemsProcessed / Math.Max(1, metrics.ProcessingTime.TotalSeconds);
                metrics.ThroughputMBPerSecond = (metrics.BytesProcessed / 1024.0 / 1024.0) / Math.Max(1, metrics.ProcessingTime.TotalSeconds);

                Logger.LogInformation("Successfully wrote {ItemCount} items to CSV file", metrics.ItemsProcessed);

                return CreateSuccessResult(executionId, startTime, 
                    new { filePath = filePath, rowsWritten = metrics.ItemsProcessed }, 
                    metrics);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error writing CSV file: {FilePath}", filePath);
                return CreateExceptionResult(executionId, startTime, ex);
            }
        }

        private IEnumerable<object> ConvertToRecords(object data)
        {
            if (data == null)
                return Enumerable.Empty<object>();

            if (data is IEnumerable<Dictionary<string, object>> dictEnum)
                return dictEnum;

            if (data is IEnumerable<object> objEnum)
                return objEnum.Where(x => x != null);

            // Single object
            return new[] { data };
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

        private string GetNewLine(string newLineSetting)
        {
            return newLineSetting switch
            {
                "LF" => "\n",
                "CR" => "\r",
                "CRLF" => "\r\n",
                _ => Environment.NewLine
            };
        }

        protected override async Task PerformDestinationValidationAsync(
            Dictionary<string, object> configuration,
            CancellationToken cancellationToken)
        {
            var filePath = GetParameter<string>(configuration, "filePath");
            
            if (string.IsNullOrEmpty(filePath))
                throw new InvalidOperationException("File path is required");

            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                try
                {
                    Directory.CreateDirectory(directory);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Cannot create directory: {ex.Message}");
                }
            }

            // Check if we can write to the location
            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Exists && fileInfo.IsReadOnly)
            {
                throw new InvalidOperationException("File is read-only");
            }

            await Task.CompletedTask;
        }

        public override IReadOnlyList<IAdapterSchema> GetInputSchemas()
        {
            return new List<IAdapterSchema>
            {
                new CsvOutputDataSchema
                {
                    Id = "tabular_data",
                    Name = "Tabular Data",
                    Description = "Structured data to write to CSV",
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
                            ["Age"] = 30,
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
                    ["supportsAppend"] = true,
                    ["supportsQuoting"] = true,
                    ["supportsCustomDateFormat"] = true
                }
            };
        }

        protected override async Task PerformHealthCheckAsync()
        {
            // Test CSV writing
            var testData = new[] { new { test = "value" } };
            using var stream = new MemoryStream();
            using var writer = new StreamWriter(stream);
            using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
            
            csv.WriteRecords(testData);
            await writer.FlushAsync();
        }
    }

    /// <summary>
    /// Schema implementation for CSV output data
    /// </summary>
    internal class CsvOutputDataSchema : IAdapterSchema
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string JsonSchema { get; set; }
        public object ExampleData { get; set; }
        public IReadOnlyList<SchemaField> Fields { get; set; } = new List<SchemaField>();
    }
}