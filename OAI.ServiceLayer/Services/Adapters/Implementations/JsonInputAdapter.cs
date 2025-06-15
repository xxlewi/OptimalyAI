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
    /// JSON file input adapter
    /// </summary>
    public class JsonInputAdapter : BaseInputAdapter
    {
        public override string Id => "json_input";
        public override string Name => "JSON Input";
        public override string Description => "Read data from JSON files with JSONPath support";
        public override string Version => "1.0.0";
        public override string Category => "File";

        public JsonInputAdapter(ILogger<JsonInputAdapter> logger) : base(logger)
        {
        }

        protected override void InitializeParameters()
        {
            AddParameter(new SimpleAdapterParameter
            {
                Name = "filePath",
                DisplayName = "File Path",
                Description = "Path to the JSON file",
                Type = ToolParameterType.String,
                IsRequired = true,
                IsCritical = true,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.File,
                    HelpText = "Select or provide path to JSON file",
                    FileExtensions = new[] { ".json" }
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "jsonPath",
                DisplayName = "JSON Path",
                Description = "JSONPath expression to extract specific data (e.g., $.items[*])",
                Type = ToolParameterType.String,
                IsRequired = false,
                DefaultValue = "$",
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Text,
                    Placeholder = "$",
                    HelpText = "Use $ for root, $.property for specific property, $.items[*] for arrays"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "arrayHandling",
                DisplayName = "Array Handling",
                Description = "How to handle JSON arrays",
                Type = ToolParameterType.String,
                IsRequired = false,
                DefaultValue = "flatten",
                Validation = new SimpleParameterValidation
                {
                    AllowedValues = new List<object> { "flatten", "preserve", "first", "last" }
                },
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Select,
                    HelpText = "flatten: each array item as separate record, preserve: keep as array, first/last: take only first/last item"
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
                    AllowedValues = new List<object> { "UTF-8", "UTF-16", "ASCII" }
                },
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Select
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "propertyNamingPolicy",
                DisplayName = "Property Naming",
                Description = "How to handle property names",
                Type = ToolParameterType.String,
                IsRequired = false,
                DefaultValue = "original",
                Validation = new SimpleParameterValidation
                {
                    AllowedValues = new List<object> { "original", "camelCase", "PascalCase", "snake_case" }
                },
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Select
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "ignoreNullValues",
                DisplayName = "Ignore Null Values",
                Description = "Skip properties with null values",
                Type = ToolParameterType.Boolean,
                IsRequired = false,
                DefaultValue = false
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "maxDepth",
                DisplayName = "Max Depth",
                Description = "Maximum depth for nested objects (0 = unlimited)",
                Type = ToolParameterType.Integer,
                IsRequired = false,
                DefaultValue = 0,
                Validation = new SimpleParameterValidation
                {
                    MinValue = 0,
                    MaxValue = 100
                },
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Number
                }
            });
        }

        protected override async Task<IAdapterResult> ExecuteReadAsync(
            Dictionary<string, object> configuration,
            string executionId,
            CancellationToken cancellationToken)
        {
            var filePath = GetParameter<string>(configuration, "filePath");
            var jsonPath = GetParameter<string>(configuration, "jsonPath", "$");
            var arrayHandling = GetParameter<string>(configuration, "arrayHandling", "flatten");
            var encoding = GetParameter<string>(configuration, "encoding", "UTF-8");
            var ignoreNullValues = GetParameter<bool>(configuration, "ignoreNullValues", false);
            var maxDepth = GetParameter<int>(configuration, "maxDepth", 0);

            var metrics = new AdapterMetrics();
            var startTime = DateTime.UtcNow;

            try
            {
                var fileEncoding = GetEncoding(encoding);
                var jsonContent = await File.ReadAllTextAsync(filePath, fileEncoding, cancellationToken);
                
                metrics.BytesProcessed = fileEncoding.GetByteCount(jsonContent);

                // Parse JSON
                using var document = JsonDocument.Parse(jsonContent);
                var root = document.RootElement;

                // Apply JSONPath if specified
                var selectedData = ApplyJsonPath(root, jsonPath);

                // Process data based on type and array handling
                var data = ProcessJsonData(selectedData, arrayHandling, ignoreNullValues, maxDepth);

                // Ensure data is a list
                List<Dictionary<string, object>> resultData;
                if (data is List<Dictionary<string, object>> list)
                {
                    resultData = list;
                }
                else if (data is Dictionary<string, object> dict)
                {
                    resultData = new List<Dictionary<string, object>> { dict };
                }
                else
                {
                    resultData = new List<Dictionary<string, object>> 
                    { 
                        new Dictionary<string, object> { ["value"] = data } 
                    };
                }

                metrics.ItemsProcessed = resultData.Count;

                // Calculate metrics
                metrics.ProcessingTime = DateTime.UtcNow - startTime;
                metrics.ThroughputItemsPerSecond = metrics.ItemsProcessed / Math.Max(1, metrics.ProcessingTime.TotalSeconds);
                metrics.ThroughputMBPerSecond = (metrics.BytesProcessed / 1024.0 / 1024.0) / Math.Max(1, metrics.ProcessingTime.TotalSeconds);

                // Create schema
                var schema = ExtractSchema(resultData);

                // Create preview
                var preview = resultData.Take(5).ToList();

                Logger.LogInformation("Successfully read {ItemCount} items from JSON file", resultData.Count);

                return CreateSuccessResult(executionId, startTime, resultData, metrics, schema, preview);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error reading JSON file: {FilePath}", filePath);
                return CreateExceptionResult(executionId, startTime, ex);
            }
        }

        private object ApplyJsonPath(JsonElement element, string jsonPath)
        {
            // Simple JSONPath implementation
            if (jsonPath == "$" || string.IsNullOrEmpty(jsonPath))
            {
                return element;
            }

            // Handle simple paths like $.property or $.items[*]
            var parts = jsonPath.TrimStart('$').TrimStart('.').Split('.');
            var current = element;

            foreach (var part in parts)
            {
                if (part.EndsWith("[*]"))
                {
                    // Array selector
                    var propertyName = part.Substring(0, part.Length - 3);
                    if (!string.IsNullOrEmpty(propertyName))
                    {
                        if (current.TryGetProperty(propertyName, out var prop))
                        {
                            current = prop;
                        }
                        else
                        {
                            throw new InvalidOperationException($"Property '{propertyName}' not found");
                        }
                    }

                    if (current.ValueKind == JsonValueKind.Array)
                    {
                        return current;
                    }
                }
                else
                {
                    // Property selector
                    if (current.TryGetProperty(part, out var prop))
                    {
                        current = prop;
                    }
                    else
                    {
                        throw new InvalidOperationException($"Property '{part}' not found");
                    }
                }
            }

            return current;
        }

        private object ProcessJsonData(object data, string arrayHandling, bool ignoreNullValues, int maxDepth)
        {
            if (data is JsonElement element)
            {
                return ProcessJsonElement(element, arrayHandling, ignoreNullValues, maxDepth, 0);
            }
            return data;
        }

        private object ProcessJsonElement(JsonElement element, string arrayHandling, bool ignoreNullValues, int maxDepth, int currentDepth)
        {
            if (maxDepth > 0 && currentDepth >= maxDepth)
            {
                return element.ToString();
            }

            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    var dict = new Dictionary<string, object>();
                    foreach (var property in element.EnumerateObject())
                    {
                        if (ignoreNullValues && property.Value.ValueKind == JsonValueKind.Null)
                            continue;
                        
                        dict[property.Name] = ProcessJsonElement(property.Value, arrayHandling, ignoreNullValues, maxDepth, currentDepth + 1);
                    }
                    return dict;

                case JsonValueKind.Array:
                    var items = element.EnumerateArray()
                        .Select(e => ProcessJsonElement(e, arrayHandling, ignoreNullValues, maxDepth, currentDepth + 1))
                        .ToList();

                    switch (arrayHandling)
                    {
                        case "flatten":
                            // Return items as separate records
                            return items.SelectMany<object, object>(item =>
                            {
                                if (item is List<object> subList)
                                    return subList;
                                return new[] { item };
                            }).ToList();
                        
                        case "first":
                            return items.FirstOrDefault();
                        
                        case "last":
                            return items.LastOrDefault();
                        
                        case "preserve":
                        default:
                            return items;
                    }

                case JsonValueKind.String:
                    return element.GetString();

                case JsonValueKind.Number:
                    if (element.TryGetInt64(out var longValue))
                        return longValue;
                    if (element.TryGetDouble(out var doubleValue))
                        return doubleValue;
                    return element.GetDecimal();

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

        private IAdapterSchema ExtractSchema(List<Dictionary<string, object>> data)
        {
            var fields = new List<SchemaField>();
            
            if (data.Any())
            {
                // Extract fields from first few records
                var sampleRecords = data.Take(10).ToList();
                var allKeys = sampleRecords.SelectMany(r => r.Keys).Distinct().ToList();

                foreach (var key in allKeys)
                {
                    var values = sampleRecords
                        .Where(r => r.ContainsKey(key))
                        .Select(r => r[key])
                        .Where(v => v != null)
                        .ToList();

                    var field = new SchemaField
                    {
                        Name = key,
                        Type = DetectType(values),
                        IsRequired = sampleRecords.All(r => r.ContainsKey(key) && r[key] != null)
                    };

                    fields.Add(field);
                }
            }

            return new JsonDataSchema
            {
                Id = "json_data",
                Name = "JSON Data",
                Description = "Data extracted from JSON file",
                Fields = fields
            };
        }

        private string DetectType(List<object> values)
        {
            if (!values.Any()) return "null";

            var types = values.Select(v => v switch
            {
                string _ => "string",
                bool _ => "boolean",
                int _ or long _ => "integer",
                float _ or double _ or decimal _ => "number",
                Dictionary<string, object> _ => "object",
                List<object> _ => "array",
                _ => "unknown"
            }).Distinct().ToList();

            if (types.Count == 1)
                return types[0];

            // Mixed types
            if (types.Contains("integer") && types.Contains("number"))
                return "number";

            return "mixed";
        }

        protected override async Task PerformSourceValidationAsync(
            Dictionary<string, object> configuration,
            CancellationToken cancellationToken)
        {
            var filePath = GetParameter<string>(configuration, "filePath");
            
            if (string.IsNullOrEmpty(filePath))
                throw new InvalidOperationException("File path is required");

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"JSON file not found: {filePath}");

            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Extension.ToLower() != ".json")
                throw new InvalidOperationException("Invalid file type. Expected JSON file (.json)");

            // Try to parse to validate JSON
            try
            {
                var content = await File.ReadAllTextAsync(filePath, cancellationToken);
                using var doc = JsonDocument.Parse(content);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException($"Invalid JSON file: {ex.Message}");
            }
        }

        public override IReadOnlyList<IAdapterSchema> GetOutputSchemas()
        {
            return new List<IAdapterSchema>
            {
                new JsonDataSchema
                {
                    Id = "json_structured",
                    Name = "JSON Structured Data",
                    Description = "Structured data extracted from JSON",
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
                            ["id"] = 1,
                            ["name"] = "Example",
                            ["active"] = true,
                            ["metadata"] = new { tags = new[] { "tag1", "tag2" } }
                        }
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
                    ["supportsJsonPath"] = true,
                    ["supportsNestedData"] = true,
                    ["supportsArrayFlattening"] = true,
                    ["supportsSchemaExtraction"] = true
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
                _ => Encoding.UTF8
            };
        }

        protected override async Task PerformHealthCheckAsync()
        {
            // Test JSON parsing
            var testJson = @"{""test"": true, ""value"": 123}";
            using var doc = JsonDocument.Parse(testJson);
            await Task.CompletedTask;
        }
    }

    /// <summary>
    /// Schema implementation for JSON data
    /// </summary>
    internal class JsonDataSchema : IAdapterSchema
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string JsonSchema { get; set; }
        public object ExampleData { get; set; }
        public IReadOnlyList<SchemaField> Fields { get; set; } = new List<SchemaField>();
    }
}