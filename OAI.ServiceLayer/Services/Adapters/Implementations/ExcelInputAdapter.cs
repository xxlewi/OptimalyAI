using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Microsoft.Extensions.Logging;
using OAI.Core.Interfaces.Adapters;
using OAI.Core.Interfaces.Tools;
using OAI.ServiceLayer.Services.Adapters.Base;

namespace OAI.ServiceLayer.Services.Adapters.Implementations
{
    /// <summary>
    /// Excel file input adapter
    /// </summary>
    public class ExcelInputAdapter : BaseInputAdapter
    {
        public override string Id => "excel_input";
        public override string Name => "Excel Input";
        public override string Description => "Read data from Excel files (XLSX, XLS)";
        public override string Version => "1.0.0";
        public override string Category => "File";

        public ExcelInputAdapter(ILogger<ExcelInputAdapter> logger) : base(logger)
        {
        }

        protected override void InitializeParameters()
        {
            AddParameter(new SimpleAdapterParameter
            {
                Name = "filePath",
                DisplayName = "File Path",
                Description = "Path to the Excel file",
                Type = ToolParameterType.String,
                IsRequired = true,
                IsCritical = true,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.File,
                    HelpText = "Select or provide path to Excel file",
                    FileExtensions = new[] { ".xlsx", ".xls" }
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "sheetName",
                DisplayName = "Sheet Name",
                Description = "Name of the sheet to read (leave empty for first sheet)",
                Type = ToolParameterType.String,
                IsRequired = false,
                DefaultValue = string.Empty,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Text,
                    Placeholder = "Sheet1"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "hasHeaders",
                DisplayName = "First Row Contains Headers",
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
                Name = "range",
                DisplayName = "Cell Range",
                Description = "Specific range to read (e.g., A1:D10)",
                Type = ToolParameterType.String,
                IsRequired = false,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Text,
                    Placeholder = "A1:Z1000",
                    HelpText = "Leave empty to read entire sheet"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "dateFormat",
                DisplayName = "Date Format",
                Description = "Format for parsing date values",
                Type = ToolParameterType.String,
                IsRequired = false,
                DefaultValue = "yyyy-MM-dd",
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Text,
                    Placeholder = "yyyy-MM-dd"
                }
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
            var sheetName = GetParameter<string>(configuration, "sheetName", string.Empty);
            var hasHeaders = GetParameter<bool>(configuration, "hasHeaders", true);
            var range = GetParameter<string>(configuration, "range", string.Empty);
            var skipEmptyRows = GetParameter<bool>(configuration, "skipEmptyRows", true);

            var metrics = new AdapterMetrics();
            var startTime = DateTime.UtcNow;

            try
            {
                using var workbook = new XLWorkbook(filePath);
                
                // Get the worksheet
                IXLWorksheet worksheet;
                if (string.IsNullOrEmpty(sheetName))
                {
                    worksheet = workbook.Worksheets.First();
                }
                else
                {
                    worksheet = workbook.Worksheet(sheetName);
                    if (worksheet == null)
                    {
                        return CreateErrorResult(executionId, startTime,
                            $"Sheet '{sheetName}' not found",
                            $"Available sheets: {string.Join(", ", workbook.Worksheets.Select(w => w.Name))}");
                    }
                }

                // Determine the range to read
                IXLRange dataRange;
                if (string.IsNullOrEmpty(range))
                {
                    dataRange = worksheet.RangeUsed();
                }
                else
                {
                    dataRange = worksheet.Range(range);
                }

                if (dataRange == null)
                {
                    return CreateSuccessResult(executionId, startTime, 
                        new List<Dictionary<string, object>>(), metrics);
                }

                // Read data
                var data = new List<Dictionary<string, object>>();
                var rows = dataRange.Rows().ToList();
                
                // Get headers
                List<string> headers;
                int startRowIndex = 0;
                
                if (hasHeaders && rows.Count > 0)
                {
                    headers = rows[0].Cells()
                        .Select(c => string.IsNullOrWhiteSpace(c.Value.ToString()) 
                            ? $"Column{c.Address.ColumnNumber}" 
                            : c.Value.ToString())
                        .ToList();
                    startRowIndex = 1;
                }
                else
                {
                    headers = Enumerable.Range(1, dataRange.ColumnCount())
                        .Select(i => $"Column{i}")
                        .ToList();
                }

                // Read data rows
                for (int i = startRowIndex; i < rows.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var row = rows[i];
                    var cells = row.Cells().ToList();
                    
                    // Check if row is empty
                    if (skipEmptyRows && cells.All(c => string.IsNullOrWhiteSpace(c.Value.ToString())))
                    {
                        continue;
                    }

                    var rowData = new Dictionary<string, object>();
                    for (int j = 0; j < Math.Min(headers.Count, cells.Count); j++)
                    {
                        var cellValue = GetCellValue(cells[j]);
                        rowData[headers[j]] = cellValue;
                    }

                    data.Add(rowData);
                    metrics.ItemsProcessed++;
                }

                // Calculate metrics
                metrics.ProcessingTime = DateTime.UtcNow - startTime;
                metrics.BytesProcessed = new FileInfo(filePath).Length;
                metrics.ThroughputItemsPerSecond = metrics.ItemsProcessed / Math.Max(1, metrics.ProcessingTime.TotalSeconds);
                metrics.ThroughputMBPerSecond = (metrics.BytesProcessed / 1024.0 / 1024.0) / Math.Max(1, metrics.ProcessingTime.TotalSeconds);

                // Create schema
                var schema = new ExcelDataSchema
                {
                    Id = "excel_data",
                    Name = "Excel Data",
                    Description = $"Data from {Path.GetFileName(filePath)}",
                    Fields = headers.Select(h => new SchemaField
                    {
                        Name = h,
                        Type = "string", // Could be improved with type detection
                        IsRequired = false
                    }).ToList()
                };

                // Create preview (first 5 rows)
                var preview = data.Take(5).ToList();

                Logger.LogInformation("Successfully read {RowCount} rows from Excel file", data.Count);

                return CreateSuccessResult(executionId, startTime, data, metrics, schema, preview);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error reading Excel file: {FilePath}", filePath);
                return CreateExceptionResult(executionId, startTime, ex);
            }
        }

        private object GetCellValue(IXLCell cell)
        {
            if (cell.IsEmpty())
                return null;

            // Handle different data types
            if (cell.DataType == XLDataType.Number)
                return cell.Value;
            
            if (cell.DataType == XLDataType.DateTime)
                return cell.GetDateTime();
            
            if (cell.DataType == XLDataType.Boolean)
                return cell.GetBoolean();

            // Default to string
            return cell.Value.ToString();
        }

        protected override async Task PerformSourceValidationAsync(
            Dictionary<string, object> configuration,
            CancellationToken cancellationToken)
        {
            var filePath = GetParameter<string>(configuration, "filePath");
            
            if (string.IsNullOrEmpty(filePath))
                throw new InvalidOperationException("File path is required");

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Excel file not found: {filePath}");

            var fileInfo = new FileInfo(filePath);
            var validExtensions = new[] { ".xlsx", ".xls" };
            
            if (!validExtensions.Contains(fileInfo.Extension.ToLower()))
                throw new InvalidOperationException($"Invalid file type. Expected Excel file (.xlsx or .xls)");

            await Task.CompletedTask;
        }

        public override IReadOnlyList<IAdapterSchema> GetOutputSchemas()
        {
            return new List<IAdapterSchema>
            {
                new ExcelDataSchema
                {
                    Id = "excel_tabular",
                    Name = "Excel Tabular Data",
                    Description = "Tabular data read from Excel file",
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
                SupportsStreaming = false,
                SupportsPartialData = true,
                SupportsBatchProcessing = true,
                SupportsTransactions = false,
                RequiresAuthentication = false,
                MaxDataSizeBytes = 100 * 1024 * 1024, // 100 MB
                MaxConcurrentOperations = 10,
                SupportedFormats = new List<string> { "xlsx", "xls" },
                SupportedEncodings = new List<string> { "UTF-8" },
                CustomCapabilities = new Dictionary<string, object>
                {
                    ["supportsMultipleSheets"] = true,
                    ["supportsFormulas"] = true,
                    ["supportsPivotTables"] = false,
                    ["maxRowCount"] = 1048576,
                    ["maxColumnCount"] = 16384
                }
            };
        }

        protected override async Task PerformHealthCheckAsync()
        {
            // Test if we can create a workbook
            using var workbook = new XLWorkbook();
            workbook.AddWorksheet("Test");
            await Task.CompletedTask;
        }
    }

    /// <summary>
    /// Schema implementation for Excel data
    /// </summary>
    internal class ExcelDataSchema : IAdapterSchema
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string JsonSchema { get; set; }
        public object ExampleData { get; set; }
        public IReadOnlyList<SchemaField> Fields { get; set; } = new List<SchemaField>();
    }
}