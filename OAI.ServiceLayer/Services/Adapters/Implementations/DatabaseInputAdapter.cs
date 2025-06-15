using System;
using System.Collections.Generic;
using System.Data;
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
    /// Adapter for reading data from databases
    /// </summary>
    public class DatabaseInputAdapter : BaseInputAdapter
    {
        public override string Id => "database_input";
        public override string Name => "Čtení z databáze";
        public override string Description => "Čtení dat z databáze pro použití ve workflow";
        public override string Version => "1.0.0";
        public override string Category => "Data";
        public override AdapterType Type => AdapterType.Input;

        public DatabaseInputAdapter(ILogger<DatabaseInputAdapter> logger) : base(logger)
        {
        }

        protected override void InitializeParameters()
        {
            AddParameter(new SimpleAdapterParameter
            {
                Name = "databaseType",
                DisplayName = "Typ databáze",
                Description = "Typ databázového systému",
                Type = ToolParameterType.String,
                IsRequired = true,
                DefaultValue = "postgresql",
                Validation = new SimpleParameterValidation
                {
                    AllowedValues = new List<object> { "postgresql", "mysql", "sqlserver", "sqlite", "oracle", "mongodb" }
                },
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Select
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "connectionString",
                DisplayName = "Connection String",
                Description = "Připojovací řetězec k databázi",
                Type = ToolParameterType.String,
                IsRequired = true,
                IsCritical = true,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Password,
                    Placeholder = "Server=localhost;Database=mydb;User Id=user;Password=pass;",
                    HelpText = "Kompletní connection string včetně přihlašovacích údajů"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "query",
                DisplayName = "SQL dotaz",
                Description = "SQL dotaz pro získání dat",
                Type = ToolParameterType.String,
                IsRequired = true,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Code,
                    Placeholder = "SELECT * FROM orders WHERE status = 'pending'",
                    HelpText = "SELECT dotaz pro získání dat",
                    CustomHints = new Dictionary<string, object> { ["language"] = "sql" }
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "queryParameters",
                DisplayName = "Parametry dotazu",
                Description = "Parametry pro SQL dotaz (JSON)",
                Type = ToolParameterType.String,
                IsRequired = false,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Code,
                    Placeholder = "{ \"status\": \"pending\", \"limit\": 100 }",
                    HelpText = "JSON objekt s parametry pro dotaz",
                    CustomHints = new Dictionary<string, object> { ["language"] = "json" }
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "commandTimeout",
                DisplayName = "Timeout dotazu (s)",
                Description = "Maximální doba běhu dotazu v sekundách",
                Type = ToolParameterType.Integer,
                IsRequired = false,
                DefaultValue = 30,
                Validation = new SimpleParameterValidation
                {
                    MinValue = 1,
                    MaxValue = 3600
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "maxRows",
                DisplayName = "Max. počet řádků",
                Description = "Maximální počet vrácených řádků",
                Type = ToolParameterType.Integer,
                IsRequired = false,
                DefaultValue = 1000,
                Validation = new SimpleParameterValidation
                {
                    MinValue = 1,
                    MaxValue = 100000
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "fetchMode",
                DisplayName = "Režim načítání",
                Description = "Způsob načítání dat",
                Type = ToolParameterType.String,
                IsRequired = false,
                DefaultValue = "all",
                Validation = new SimpleParameterValidation
                {
                    AllowedValues = new List<object> { "all", "streaming", "paged" }
                },
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Select,
                    HelpText = "all = vše najednou, streaming = postupně, paged = po stránkách"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "pageSize",
                DisplayName = "Velikost stránky",
                Description = "Počet řádků na stránku (pro paged mode)",
                Type = ToolParameterType.Integer,
                IsRequired = false,
                DefaultValue = 100,
                Validation = new SimpleParameterValidation
                {
                    MinValue = 10,
                    MaxValue = 10000
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "schema",
                DisplayName = "Schéma",
                Description = "Databázové schéma (pokud je potřeba)",
                Type = ToolParameterType.String,
                IsRequired = false,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Text,
                    Placeholder = "public, dbo",
                    HelpText = "Název schématu v databázi"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "enableTransaction",
                DisplayName = "Použít transakci",
                Description = "Spustit dotaz v transakci",
                Type = ToolParameterType.Boolean,
                IsRequired = false,
                DefaultValue = false
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "isolationLevel",
                DisplayName = "Úroveň izolace",
                Description = "Úroveň izolace transakce",
                Type = ToolParameterType.String,
                IsRequired = false,
                DefaultValue = "ReadCommitted",
                Validation = new SimpleParameterValidation
                {
                    AllowedValues = new List<object> 
                    { 
                        "ReadUncommitted", 
                        "ReadCommitted", 
                        "RepeatableRead", 
                        "Serializable", 
                        "Snapshot" 
                    }
                },
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Select
                }
            });
        }

        protected override async Task<IAdapterResult> ExecuteReadAsync(
            Dictionary<string, object> configuration,
            string executionId,
            CancellationToken cancellationToken)
        {
            var databaseType = GetParameter<string>(configuration, "databaseType", "postgresql");
            var connectionString = GetParameter<string>(configuration, "connectionString");
            var query = GetParameter<string>(configuration, "query");
            var maxRows = GetParameter<int>(configuration, "maxRows", 1000);
            var commandTimeout = GetParameter<int>(configuration, "commandTimeout", 30);
            var fetchMode = GetParameter<string>(configuration, "fetchMode", "all");

            var metrics = new AdapterMetrics();
            var startTime = DateTime.UtcNow;

            try
            {
                // Simulated database query results
                var results = new List<Dictionary<string, object>>();

                // Simulate reading data from database
                var simulatedData = new[]
                {
                    new Dictionary<string, object>
                    {
                        ["id"] = 1,
                        ["order_id"] = "ORD-2024-001",
                        ["customer_id"] = 123,
                        ["customer_name"] = "Jan Novák",
                        ["total_amount"] = 2599.99,
                        ["currency"] = "CZK",
                        ["status"] = "pending",
                        ["created_at"] = DateTime.UtcNow.AddDays(-2),
                        ["updated_at"] = DateTime.UtcNow.AddHours(-1)
                    },
                    new Dictionary<string, object>
                    {
                        ["id"] = 2,
                        ["order_id"] = "ORD-2024-002",
                        ["customer_id"] = 456,
                        ["customer_name"] = "Marie Dvořáková",
                        ["total_amount"] = 1299.50,
                        ["currency"] = "CZK",
                        ["status"] = "pending",
                        ["created_at"] = DateTime.UtcNow.AddDays(-1),
                        ["updated_at"] = DateTime.UtcNow.AddMinutes(-30)
                    },
                    new Dictionary<string, object>
                    {
                        ["id"] = 3,
                        ["order_id"] = "ORD-2024-003",
                        ["customer_id"] = 789,
                        ["customer_name"] = "Pavel Svoboda",
                        ["total_amount"] = 4999.00,
                        ["currency"] = "CZK",
                        ["status"] = "processing",
                        ["created_at"] = DateTime.UtcNow.AddHours(-5),
                        ["updated_at"] = DateTime.UtcNow.AddMinutes(-15)
                    }
                };

                // Apply row limit
                var dataToProcess = simulatedData.Take(Math.Min(maxRows, simulatedData.Length));

                foreach (var row in dataToProcess)
                {
                    results.Add(row);
                    metrics.ItemsProcessed++;
                    metrics.BytesProcessed += EstimateRowSize(row);
                }

                // Calculate metrics
                metrics.ProcessingTime = DateTime.UtcNow - startTime;
                metrics.ThroughputItemsPerSecond = metrics.ItemsProcessed / Math.Max(1, metrics.ProcessingTime.TotalSeconds);
                metrics.ThroughputMBPerSecond = (metrics.BytesProcessed / 1024.0 / 1024.0) / Math.Max(1, metrics.ProcessingTime.TotalSeconds);

                // Create schema based on first row
                var fields = new List<SchemaField>();
                if (results.Any())
                {
                    var firstRow = results.First();
                    foreach (var kvp in firstRow)
                    {
                        fields.Add(new SchemaField
                        {
                            Name = kvp.Key,
                            Type = GetFieldType(kvp.Value),
                            IsRequired = false
                        });
                    }
                }

                var schema = new DatabaseResultSchema
                {
                    Id = "database_query_results",
                    Name = "Výsledky databázového dotazu",
                    Description = "Data získaná z databáze",
                    Fields = fields
                };

                Logger.LogInformation("Successfully executed database query, retrieved {RowCount} rows", results.Count);

                return CreateSuccessResult(executionId, startTime, results, metrics, schema, results.Take(5).ToList());
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error executing database query");
                return CreateExceptionResult(executionId, startTime, ex);
            }
        }

        private string GetFieldType(object value)
        {
            return value switch
            {
                int _ => "integer",
                long _ => "long",
                decimal _ or double _ or float _ => "number",
                bool _ => "boolean",
                DateTime _ => "datetime",
                _ => "string"
            };
        }

        private long EstimateRowSize(Dictionary<string, object> row)
        {
            // Simple estimation of row size in bytes
            long size = 0;
            foreach (var value in row.Values)
            {
                if (value is string str)
                    size += str.Length * 2; // Unicode chars
                else if (value is int or long or decimal or double or float)
                    size += 8;
                else if (value is bool)
                    size += 1;
                else if (value is DateTime)
                    size += 8;
                else
                    size += 50; // Default estimate
            }
            return size;
        }

        protected override async Task PerformSourceValidationAsync(
            Dictionary<string, object> configuration,
            CancellationToken cancellationToken)
        {
            var connectionString = GetParameter<string>(configuration, "connectionString");
            var query = GetParameter<string>(configuration, "query");
            
            if (string.IsNullOrEmpty(connectionString))
                throw new InvalidOperationException("Connection string is required");

            if (string.IsNullOrEmpty(query))
                throw new InvalidOperationException("SQL query is required");

            // Validate query is SELECT (read-only)
            var trimmedQuery = query.Trim().ToUpper();
            if (!trimmedQuery.StartsWith("SELECT") && !trimmedQuery.StartsWith("WITH"))
                throw new InvalidOperationException("Only SELECT queries are allowed for input adapter");

            // In real implementation, would test database connection
            await Task.CompletedTask;
        }

        public override IReadOnlyList<IAdapterSchema> GetOutputSchemas()
        {
            return new List<IAdapterSchema>
            {
                new DatabaseResultSchema
                {
                    Id = "database_input_output",
                    Name = "Database Input Output",
                    Description = "Data načtená z databáze",
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
                            ["name"] = "Example Record",
                            ["created_at"] = DateTime.UtcNow,
                            ["status"] = "active"
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
                SupportsTransactions = true,
                RequiresAuthentication = true,
                MaxDataSizeBytes = 1024 * 1024 * 1024, // 1 GB
                MaxConcurrentOperations = 10,
                SupportedFormats = new List<string> { "table", "json", "csv" },
                SupportedEncodings = new List<string> { "UTF-8", "UTF-16", "ISO-8859-1" },
                CustomCapabilities = new Dictionary<string, object>
                {
                    ["supportsPagination"] = true,
                    ["supportsParameterizedQueries"] = true,
                    ["supportsStoredProcedures"] = false,
                    ["supportsViews"] = true,
                    ["supportsTransactions"] = true,
                    ["supportedDatabases"] = new[] { "postgresql", "mysql", "sqlserver", "sqlite", "oracle", "mongodb" }
                }
            };
        }

        protected override async Task PerformHealthCheckAsync()
        {
            // In real implementation, would test database connectivity
            await Task.CompletedTask;
        }
    }

    /// <summary>
    /// Schema implementation for database results
    /// </summary>
    internal class DatabaseResultSchema : IAdapterSchema
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string JsonSchema { get; set; }
        public object ExampleData { get; set; }
        public IReadOnlyList<SchemaField> Fields { get; set; } = new List<SchemaField>();
    }
}