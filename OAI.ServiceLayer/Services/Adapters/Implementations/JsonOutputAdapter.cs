using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OAI.Core.Interfaces.Adapters;
using OAI.Core.Interfaces.Tools;
using OAI.ServiceLayer.Services.Adapters.Base;

namespace OAI.ServiceLayer.Services.Adapters.Implementations
{
    /// <summary>
    /// JSON file output adapter
    /// </summary>
    public class JsonOutputAdapter : BaseOutputAdapter
    {
        public override string Id => "json_output";
        public override string Name => "JSON Output";
        public override string Description => "Write data to JSON files with formatting options";
        public override string Version => "1.0.0";
        public override string Category => "File";
        public override AdapterType Type => AdapterType.Output;

        public JsonOutputAdapter(ILogger<JsonOutputAdapter> logger) : base(logger)
        {
        }

        protected override void InitializeParameters()
        {
            AddParameter(new SimpleAdapterParameter
            {
                Name = "filePath",
                DisplayName = "File Path",
                Description = "Path where the JSON file will be saved",
                Type = ToolParameterType.String,
                IsRequired = true,
                IsCritical = true,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.File,
                    HelpText = "Specify the output JSON file path",
                    FileExtensions = new[] { ".json" }
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "indented",
                DisplayName = "Indented",
                Description = "Format JSON with indentation for readability",
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
                    AllowedValues = new List<object> { "UTF-8", "UTF-16", "ASCII" }
                },
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Select
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "rootProperty",
                DisplayName = "Root Property",
                Description = "Wrap data in a root property (optional)",
                Type = ToolParameterType.String,
                IsRequired = false,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Text,
                    Placeholder = "data",
                    HelpText = "Leave empty to write data as-is"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "includeMetadata",
                DisplayName = "Include Metadata",
                Description = "Add metadata to the JSON output",
                Type = ToolParameterType.Boolean,
                IsRequired = false,
                DefaultValue = false
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "propertyNamingPolicy",
                DisplayName = "Property Naming",
                Description = "How to format property names",
                Type = ToolParameterType.String,
                IsRequired = false,
                DefaultValue = "original",
                Validation = new SimpleParameterValidation
                {
                    AllowedValues = new List<object> { "original", "camelCase", "PascalCase", "snake_case", "UPPER_CASE" }
                },
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Select
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "includeNullValues",
                DisplayName = "Include Null Values",
                Description = "Include properties with null values",
                Type = ToolParameterType.Boolean,
                IsRequired = false,
                DefaultValue = true
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
                    AllowedValues = new List<object> { "overwrite", "merge_array", "merge_object" }
                },
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Select,
                    HelpText = "overwrite: replace file, merge_array: append to array, merge_object: merge objects"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "dateFormat",
                DisplayName = "Date Format",
                Description = "Format for date/time values",
                Type = ToolParameterType.String,
                IsRequired = false,
                DefaultValue = "ISO8601",
                Validation = new SimpleParameterValidation
                {
                    AllowedValues = new List<object> { "ISO8601", "Unix", "Custom" }
                },
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Select
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "customDateFormat",
                DisplayName = "Custom Date Format",
                Description = "Custom date format string (when dateFormat is 'Custom')",
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
            var indented = GetParameter<bool>(configuration, "indented", true);
            var encoding = GetParameter<string>(configuration, "encoding", "UTF-8");
            var rootProperty = GetParameter<string>(configuration, "rootProperty", null);
            var includeMetadata = GetParameter<bool>(configuration, "includeMetadata", false);
            var propertyNaming = GetParameter<string>(configuration, "propertyNamingPolicy", "original");
            var includeNullValues = GetParameter<bool>(configuration, "includeNullValues", true);
            var appendMode = GetParameter<string>(configuration, "appendMode", "overwrite");
            var dateFormat = GetParameter<string>(configuration, "dateFormat", "ISO8601");
            var customDateFormat = GetParameter<string>(configuration, "customDateFormat", "yyyy-MM-dd HH:mm:ss");

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

                // Prepare data
                object outputData = data;

                // Handle append modes
                if (appendMode != "overwrite" && File.Exists(filePath))
                {
                    var existingJson = await File.ReadAllTextAsync(filePath, GetEncoding(encoding), cancellationToken);
                    using var existingDoc = JsonDocument.Parse(existingJson);
                    
                    if (appendMode == "merge_array")
                    {
                        var existingArray = new List<object>();
                        if (existingDoc.RootElement.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var element in existingDoc.RootElement.EnumerateArray())
                            {
                                existingArray.Add(JsonToObject(element));
                            }
                        }
                        
                        // Add new data
                        if (data is IEnumerable<object> newArray)
                        {
                            existingArray.AddRange(newArray);
                        }
                        else
                        {
                            existingArray.Add(data);
                        }
                        
                        outputData = existingArray;
                    }
                    else if (appendMode == "merge_object")
                    {
                        var existingObject = JsonToObject(existingDoc.RootElement) as Dictionary<string, object> 
                            ?? new Dictionary<string, object>();
                        
                        if (data is Dictionary<string, object> newObject)
                        {
                            foreach (var kvp in newObject)
                            {
                                existingObject[kvp.Key] = kvp.Value;
                            }
                        }
                        
                        outputData = existingObject;
                    }
                }

                // Add metadata if requested
                if (includeMetadata)
                {
                    var metadata = new Dictionary<string, object>
                    {
                        ["_metadata"] = new
                        {
                            generated_at = DateTime.UtcNow,
                            adapter_id = Id,
                            execution_id = executionId,
                            record_count = CountRecords(data)
                        }
                    };

                    if (!string.IsNullOrEmpty(rootProperty))
                    {
                        metadata[rootProperty] = outputData;
                        outputData = metadata;
                    }
                    else
                    {
                        // Merge metadata with data
                        if (outputData is Dictionary<string, object> dict)
                        {
                            foreach (var kvp in metadata)
                            {
                                dict[kvp.Key] = kvp.Value;
                            }
                        }
                        else
                        {
                            metadata["data"] = outputData;
                            outputData = metadata;
                        }
                    }
                }
                else if (!string.IsNullOrEmpty(rootProperty))
                {
                    outputData = new Dictionary<string, object> { [rootProperty] = outputData };
                }

                // Configure JSON options
                var options = new JsonSerializerOptions
                {
                    WriteIndented = indented,
                    PropertyNamingPolicy = GetNamingPolicy(propertyNaming),
                    DefaultIgnoreCondition = includeNullValues 
                        ? System.Text.Json.Serialization.JsonIgnoreCondition.Never
                        : System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                // Add custom converters based on date format
                if (dateFormat == "Unix")
                {
                    options.Converters.Add(new UnixDateTimeConverter());
                }
                else if (dateFormat == "Custom")
                {
                    options.Converters.Add(new CustomDateTimeConverter(customDateFormat));
                }

                // Serialize and write
                var json = JsonSerializer.Serialize(outputData, options);
                var fileEncoding = GetEncoding(encoding);
                await File.WriteAllTextAsync(filePath, json, fileEncoding, cancellationToken);

                // Calculate metrics
                metrics.BytesProcessed = fileEncoding.GetByteCount(json);
                metrics.ItemsProcessed = CountRecords(outputData);
                metrics.ProcessingTime = DateTime.UtcNow - startTime;
                metrics.ThroughputItemsPerSecond = metrics.ItemsProcessed / Math.Max(1, metrics.ProcessingTime.TotalSeconds);
                metrics.ThroughputMBPerSecond = (metrics.BytesProcessed / 1024.0 / 1024.0) / Math.Max(1, metrics.ProcessingTime.TotalSeconds);

                Logger.LogInformation("Successfully wrote {ItemCount} items to JSON file", metrics.ItemsProcessed);

                return CreateSuccessResult(executionId, startTime, 
                    new { filePath = filePath, bytesWritten = metrics.BytesProcessed }, 
                    metrics);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error writing JSON file: {FilePath}", filePath);
                return CreateExceptionResult(executionId, startTime, ex);
            }
        }

        private int CountRecords(object data)
        {
            if (data is IEnumerable<object> enumerable)
                return enumerable.Count();
            if (data is Dictionary<string, object> dict)
                return dict.Count;
            return 1;
        }

        private object JsonToObject(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    var dict = new Dictionary<string, object>();
                    foreach (var property in element.EnumerateObject())
                    {
                        dict[property.Name] = JsonToObject(property.Value);
                    }
                    return dict;

                case JsonValueKind.Array:
                    return element.EnumerateArray().Select(JsonToObject).ToList();

                case JsonValueKind.String:
                    return element.GetString();

                case JsonValueKind.Number:
                    if (element.TryGetInt64(out var longValue))
                        return longValue;
                    return element.GetDouble();

                case JsonValueKind.True:
                    return true;

                case JsonValueKind.False:
                    return false;

                case JsonValueKind.Null:
                    return null;

                default:
                    return element.ToString();
            }
        }

        private JsonNamingPolicy GetNamingPolicy(string policy)
        {
            return policy switch
            {
                "camelCase" => JsonNamingPolicy.CamelCase,
                "PascalCase" => new PascalCaseNamingPolicy(),
                "snake_case" => new SnakeCaseNamingPolicy(),
                "UPPER_CASE" => new UpperCaseNamingPolicy(),
                _ => null // original
            };
        }

        private Encoding GetEncoding(string encodingName)
        {
            return encodingName?.ToUpper() switch
            {
                "UTF-8" => Encoding.UTF8,
                "UTF-16" => Encoding.Unicode,
                "ASCII" => Encoding.ASCII,
                _ => Encoding.UTF8
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
                new JsonOutputDataSchema
                {
                    Id = "any_data",
                    Name = "Any Data",
                    Description = "Any serializable data structure",
                    JsonSchema = @"{
                        ""type"": [""object"", ""array""],
                        ""additionalProperties"": true
                    }",
                    ExampleData = new
                    {
                        users = new[]
                        {
                            new { id = 1, name = "John Doe", active = true },
                            new { id = 2, name = "Jane Smith", active = false }
                        },
                        metadata = new { generated = DateTime.UtcNow, version = "1.0" }
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
                MaxConcurrentOperations = 10,
                SupportedFormats = new List<string> { "json" },
                SupportedEncodings = new List<string> { "UTF-8", "UTF-16", "ASCII" },
                CustomCapabilities = new Dictionary<string, object>
                {
                    ["supportsFormatting"] = true,
                    ["supportsMetadata"] = true,
                    ["supportsMerging"] = true,
                    ["supportsCustomNaming"] = true,
                    ["supportsCustomDateFormat"] = true
                }
            };
        }

        protected override async Task PerformHealthCheckAsync()
        {
            // Test JSON serialization
            var testData = new { test = true, timestamp = DateTime.UtcNow };
            var json = JsonSerializer.Serialize(testData);
            var deserialized = JsonSerializer.Deserialize<object>(json);
            await Task.CompletedTask;
        }
    }

    /// <summary>
    /// Schema implementation for JSON output data
    /// </summary>
    internal class JsonOutputDataSchema : IAdapterSchema
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string JsonSchema { get; set; }
        public object ExampleData { get; set; }
        public IReadOnlyList<SchemaField> Fields { get; set; } = new List<SchemaField>();
    }

    /// <summary>
    /// Custom JSON converters
    /// </summary>
    internal class UnixDateTimeConverter : System.Text.Json.Serialization.JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64()).DateTime;
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            writer.WriteNumberValue(((DateTimeOffset)value).ToUnixTimeSeconds());
        }
    }

    internal class CustomDateTimeConverter : System.Text.Json.Serialization.JsonConverter<DateTime>
    {
        private readonly string _format;

        public CustomDateTimeConverter(string format)
        {
            _format = format;
        }

        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return DateTime.ParseExact(reader.GetString(), _format, null);
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString(_format));
        }
    }

    /// <summary>
    /// Custom naming policies
    /// </summary>
    internal class PascalCaseNamingPolicy : JsonNamingPolicy
    {
        public override string ConvertName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;
            return char.ToUpper(name[0]) + name.Substring(1);
        }
    }

    internal class SnakeCaseNamingPolicy : JsonNamingPolicy
    {
        public override string ConvertName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            var result = new StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i]))
                {
                    result.Append('_');
                }
                result.Append(char.ToLower(name[i]));
            }
            return result.ToString();
        }
    }

    internal class UpperCaseNamingPolicy : JsonNamingPolicy
    {
        public override string ConvertName(string name)
        {
            return name?.ToUpper();
        }
    }
}