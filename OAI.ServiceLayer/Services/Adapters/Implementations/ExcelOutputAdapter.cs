using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OfficeOpenXml;
using OAI.Core.Interfaces.Adapters;
using OAI.Core.Interfaces.Tools;
using OAI.ServiceLayer.Services.Adapters.Base;

namespace OAI.ServiceLayer.Services.Adapters.Implementations
{
    /// <summary>
    /// Excel file output adapter
    /// </summary>
    public class ExcelOutputAdapter : BaseOutputAdapter
    {
        public override string Id => "excel_output";
        public override string Name => "Excel Output";
        public override string Description => "Write data to Excel files with advanced formatting options";
        public override string Version => "1.0.0";
        public override string Category => "File";
        public override AdapterType Type => AdapterType.Output;

        static ExcelOutputAdapter()
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        public ExcelOutputAdapter(ILogger<ExcelOutputAdapter> logger) : base(logger)
        {
        }

        protected override void InitializeParameters()
        {
            AddParameter(new SimpleAdapterParameter
            {
                Name = "filePath",
                DisplayName = "File Path",
                Description = "Path where the Excel file will be saved",
                Type = ToolParameterType.String,
                IsRequired = true,
                IsCritical = true,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.File,
                    HelpText = "Specify the output Excel file path",
                    FileExtensions = new[] { ".xlsx", ".xlsm" }
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "sheetName",
                DisplayName = "Sheet Name",
                Description = "Name of the worksheet",
                Type = ToolParameterType.String,
                IsRequired = false,
                DefaultValue = "Sheet1",
                Validation = new SimpleParameterValidation
                {
                    MaxLength = 31 // Excel sheet name limit
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "startRow",
                DisplayName = "Start Row",
                Description = "Starting row for data (1-based)",
                Type = ToolParameterType.Integer,
                IsRequired = false,
                DefaultValue = 1,
                Validation = new SimpleParameterValidation
                {
                    MinValue = 1,
                    MaxValue = 1048576 // Excel row limit
                },
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Number
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "startColumn",
                DisplayName = "Start Column",
                Description = "Starting column for data (1-based)",
                Type = ToolParameterType.Integer,
                IsRequired = false,
                DefaultValue = 1,
                Validation = new SimpleParameterValidation
                {
                    MinValue = 1,
                    MaxValue = 16384 // Excel column limit
                },
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Number
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "includeHeaders",
                DisplayName = "Include Headers",
                Description = "Include column headers in the first row",
                Type = ToolParameterType.Boolean,
                IsRequired = false,
                DefaultValue = true
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "autoFitColumns",
                DisplayName = "Auto Fit Columns",
                Description = "Automatically adjust column widths to fit content",
                Type = ToolParameterType.Boolean,
                IsRequired = false,
                DefaultValue = true
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "formatAsTable",
                DisplayName = "Format as Table",
                Description = "Apply Excel table formatting",
                Type = ToolParameterType.Boolean,
                IsRequired = false,
                DefaultValue = false
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "tableStyle",
                DisplayName = "Table Style",
                Description = "Excel table style to apply",
                Type = ToolParameterType.String,
                IsRequired = false,
                DefaultValue = "TableStyleMedium2",
                Validation = new SimpleParameterValidation
                {
                    AllowedValues = new List<object> 
                    { 
                        "TableStyleLight1", "TableStyleLight2", "TableStyleMedium1", 
                        "TableStyleMedium2", "TableStyleDark1", "TableStyleDark2" 
                    }
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
                Description = "Append to existing file or overwrite",
                Type = ToolParameterType.String,
                IsRequired = false,
                DefaultValue = "overwrite",
                Validation = new SimpleParameterValidation
                {
                    AllowedValues = new List<object> { "overwrite", "append", "new_sheet" }
                },
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Select,
                    HelpText = "overwrite: replace file, append: add to existing sheet, new_sheet: create new sheet"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "password",
                DisplayName = "Password",
                Description = "Password to protect the Excel file (optional)",
                Type = ToolParameterType.String,
                IsRequired = false,
                IsSensitive = true,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Password
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
            var sheetName = GetParameter<string>(configuration, "sheetName", "Sheet1");
            var startRow = GetParameter<int>(configuration, "startRow", 1);
            var startColumn = GetParameter<int>(configuration, "startColumn", 1);
            var includeHeaders = GetParameter<bool>(configuration, "includeHeaders", true);
            var autoFitColumns = GetParameter<bool>(configuration, "autoFitColumns", true);
            var formatAsTable = GetParameter<bool>(configuration, "formatAsTable", false);
            var tableStyle = GetParameter<string>(configuration, "tableStyle", "TableStyleMedium2");
            var appendMode = GetParameter<string>(configuration, "appendMode", "overwrite");
            var password = GetParameter<string>(configuration, "password", null);

            var metrics = new AdapterMetrics();
            var startTime = DateTime.UtcNow;

            try
            {
                // Convert data to DataTable
                var dataTable = ConvertToDataTable(data);
                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    return CreateSuccessResult(executionId, startTime, null, metrics);
                }

                FileInfo fileInfo = new FileInfo(filePath);
                ExcelPackage package;

                // Handle append modes
                if (appendMode != "overwrite" && fileInfo.Exists)
                {
                    package = new ExcelPackage(fileInfo);
                    if (!string.IsNullOrEmpty(password))
                    {
                        package.Encryption.Password = password;
                    }
                }
                else
                {
                    // Create directory if it doesn't exist
                    fileInfo.Directory?.Create();
                    package = new ExcelPackage();
                }

                using (package)
                {
                    ExcelWorksheet worksheet;

                    if (appendMode == "new_sheet" && fileInfo.Exists)
                    {
                        // Generate unique sheet name
                        var baseSheetName = sheetName;
                        var counter = 1;
                        while (package.Workbook.Worksheets.Any(ws => ws.Name == sheetName))
                        {
                            sheetName = $"{baseSheetName}_{counter++}";
                        }
                        worksheet = package.Workbook.Worksheets.Add(sheetName);
                    }
                    else if (appendMode == "append" && package.Workbook.Worksheets.Any(ws => ws.Name == sheetName))
                    {
                        worksheet = package.Workbook.Worksheets[sheetName];
                        // Find the last row with data
                        if (worksheet.Dimension != null)
                        {
                            startRow = worksheet.Dimension.End.Row + 1;
                            includeHeaders = false; // Don't repeat headers when appending
                        }
                    }
                    else
                    {
                        // Remove existing sheet if it exists
                        if (package.Workbook.Worksheets.Any(ws => ws.Name == sheetName))
                        {
                            package.Workbook.Worksheets.Delete(sheetName);
                        }
                        worksheet = package.Workbook.Worksheets.Add(sheetName);
                    }

                    // Write data
                    var currentRow = startRow;
                    var currentCol = startColumn;

                    // Write headers if requested
                    if (includeHeaders)
                    {
                        for (int col = 0; col < dataTable.Columns.Count; col++)
                        {
                            worksheet.Cells[currentRow, currentCol + col].Value = dataTable.Columns[col].ColumnName;
                            worksheet.Cells[currentRow, currentCol + col].Style.Font.Bold = true;
                        }
                        currentRow++;
                    }

                    // Write data rows
                    foreach (DataRow row in dataTable.Rows)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        for (int col = 0; col < dataTable.Columns.Count; col++)
                        {
                            var value = row[col];
                            var cell = worksheet.Cells[currentRow, currentCol + col];
                            
                            // Set value with appropriate type
                            if (value != null && value != DBNull.Value)
                            {
                                if (value is DateTime dateValue)
                                {
                                    cell.Value = dateValue;
                                    cell.Style.Numberformat.Format = "yyyy-mm-dd hh:mm:ss";
                                }
                                else if (IsNumeric(value))
                                {
                                    cell.Value = Convert.ToDouble(value);
                                }
                                else
                                {
                                    cell.Value = value.ToString();
                                }
                            }
                        }
                        currentRow++;
                        metrics.ItemsProcessed++;
                    }

                    // Apply table formatting if requested
                    if (formatAsTable && metrics.ItemsProcessed > 0)
                    {
                        var tableRange = worksheet.Cells[
                            startRow, 
                            startColumn, 
                            currentRow - 1, 
                            startColumn + dataTable.Columns.Count - 1];
                        
                        var table = worksheet.Tables.Add(tableRange, $"Table_{Guid.NewGuid():N}".Substring(0, 15));
                        table.TableStyle = GetTableStyle(tableStyle);
                        table.ShowHeader = includeHeaders;
                    }

                    // Auto-fit columns if requested
                    if (autoFitColumns)
                    {
                        worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();
                    }

                    // Set password if provided
                    if (!string.IsNullOrEmpty(password))
                    {
                        package.Encryption.Password = password;
                    }

                    // Save the file
                    await package.SaveAsAsync(fileInfo, cancellationToken);
                    
                    metrics.BytesProcessed = fileInfo.Length;
                }

                // Calculate metrics
                metrics.ProcessingTime = DateTime.UtcNow - startTime;
                metrics.ThroughputItemsPerSecond = metrics.ItemsProcessed / Math.Max(1, metrics.ProcessingTime.TotalSeconds);
                metrics.ThroughputMBPerSecond = (metrics.BytesProcessed / 1024.0 / 1024.0) / Math.Max(1, metrics.ProcessingTime.TotalSeconds);

                Logger.LogInformation("Successfully wrote {ItemCount} items to Excel file", metrics.ItemsProcessed);

                return CreateSuccessResult(executionId, startTime, 
                    new { filePath = filePath, rowsWritten = metrics.ItemsProcessed }, 
                    metrics);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error writing Excel file: {FilePath}", filePath);
                return CreateExceptionResult(executionId, startTime, ex);
            }
        }

        private DataTable ConvertToDataTable(object data)
        {
            var dataTable = new DataTable();

            if (data is DataTable dt)
            {
                return dt;
            }

            if (data is IEnumerable<Dictionary<string, object>> dictList)
            {
                // Get all unique columns
                var columns = dictList
                    .Where(d => d != null)
                    .SelectMany(d => d.Keys)
                    .Distinct()
                    .ToList();

                // Create columns
                foreach (var col in columns)
                {
                    dataTable.Columns.Add(col, typeof(object));
                }

                // Add rows
                foreach (var dict in dictList)
                {
                    var row = dataTable.NewRow();
                    foreach (var kvp in dict)
                    {
                        if (dataTable.Columns.Contains(kvp.Key))
                        {
                            row[kvp.Key] = kvp.Value ?? DBNull.Value;
                        }
                    }
                    dataTable.Rows.Add(row);
                }
            }
            else if (data is IEnumerable<object> objList)
            {
                var items = objList.ToList();
                if (items.Any())
                {
                    // Use reflection to get properties from first item
                    var firstItem = items.First();
                    var properties = firstItem.GetType().GetProperties();

                    foreach (var prop in properties)
                    {
                        dataTable.Columns.Add(prop.Name, typeof(object));
                    }

                    foreach (var item in items)
                    {
                        var row = dataTable.NewRow();
                        foreach (var prop in properties)
                        {
                            row[prop.Name] = prop.GetValue(item) ?? DBNull.Value;
                        }
                        dataTable.Rows.Add(row);
                    }
                }
            }

            return dataTable;
        }

        private bool IsNumeric(object value)
        {
            return value is sbyte || value is byte || value is short || value is ushort ||
                   value is int || value is uint || value is long || value is ulong ||
                   value is float || value is double || value is decimal;
        }

        private OfficeOpenXml.Table.TableStyles GetTableStyle(string styleName)
        {
            return styleName switch
            {
                "TableStyleLight1" => OfficeOpenXml.Table.TableStyles.Light1,
                "TableStyleLight2" => OfficeOpenXml.Table.TableStyles.Light2,
                "TableStyleMedium1" => OfficeOpenXml.Table.TableStyles.Medium1,
                "TableStyleMedium2" => OfficeOpenXml.Table.TableStyles.Medium2,
                "TableStyleDark1" => OfficeOpenXml.Table.TableStyles.Dark1,
                "TableStyleDark2" => OfficeOpenXml.Table.TableStyles.Dark2,
                _ => OfficeOpenXml.Table.TableStyles.Medium2
            };
        }

        protected override async Task PerformDestinationValidationAsync(
            Dictionary<string, object> configuration,
            CancellationToken cancellationToken)
        {
            var filePath = GetParameter<string>(configuration, "filePath");
            
            if (string.IsNullOrEmpty(filePath))
                throw new InvalidOperationException("File path is required");

            var fileInfo = new FileInfo(filePath);
            
            // Check directory permissions
            if (!fileInfo.Directory.Exists)
            {
                try
                {
                    fileInfo.Directory.Create();
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Cannot create directory: {ex.Message}");
                }
            }

            // Check if we can write to the location
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
                new ExcelOutputDataSchema
                {
                    Id = "tabular_data",
                    Name = "Tabular Data",
                    Description = "Structured tabular data to write to Excel",
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
                            ["Email"] = "john@example.com",
                            ["Salary"] = 50000.00
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
                MaxConcurrentOperations = 5,
                SupportedFormats = new List<string> { "xlsx", "xlsm" },
                SupportedEncodings = new List<string> { "UTF-8" },
                CustomCapabilities = new Dictionary<string, object>
                {
                    ["supportsFormatting"] = true,
                    ["supportsFormulas"] = true,
                    ["supportsTables"] = true,
                    ["supportsMultipleSheets"] = true,
                    ["supportsPassword"] = true,
                    ["maxRows"] = 1048576,
                    ["maxColumns"] = 16384
                }
            };
        }

        protected override async Task PerformHealthCheckAsync()
        {
            // Test EPPlus functionality
            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("Test");
            worksheet.Cells[1, 1].Value = "Test";
            await Task.CompletedTask;
        }
    }

    /// <summary>
    /// Schema implementation for Excel output data
    /// </summary>
    internal class ExcelOutputDataSchema : IAdapterSchema
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string JsonSchema { get; set; }
        public object ExampleData { get; set; }
        public IReadOnlyList<SchemaField> Fields { get; set; } = new List<SchemaField>();
    }
}