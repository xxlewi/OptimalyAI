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
    /// Adapter for writing data to databases
    /// </summary>
    public class DatabaseOutputAdapter : BaseOutputAdapter
    {
        public override string Id => "database_output";
        public override string Name => "Zápis do databáze";
        public override string Description => "Ukládání dat z workflow do databáze";
        public override string Version => "1.0.0";
        public override string Category => "Data";
        public override AdapterType Type => AdapterType.Output;

        public DatabaseOutputAdapter(ILogger<DatabaseOutputAdapter> logger) : base(logger)
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
                Name = "tableName",
                DisplayName = "Název tabulky",
                Description = "Cílová tabulka pro zápis dat",
                Type = ToolParameterType.String,
                IsRequired = true,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Text,
                    Placeholder = "orders, customers, workflow_results",
                    HelpText = "Název existující tabulky v databázi"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "operationType",
                DisplayName = "Typ operace",
                Description = "Způsob zápisu dat",
                Type = ToolParameterType.String,
                IsRequired = true,
                DefaultValue = "insert",
                Validation = new SimpleParameterValidation
                {
                    AllowedValues = new List<object> { "insert", "update", "upsert", "replace", "merge" }
                },
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Select,
                    HelpText = "insert = nové záznamy, update = aktualizace, upsert = insert nebo update"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "keyColumns",
                DisplayName = "Klíčové sloupce",
                Description = "Sloupce pro identifikaci záznamů (pro update/upsert)",
                Type = ToolParameterType.String,
                IsRequired = false,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Text,
                    Placeholder = "id, order_id, customer_id",
                    HelpText = "Oddělené čárkou, vyžadováno pro update/upsert"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "columnMapping",
                DisplayName = "Mapování sloupců",
                Description = "Mapování dat na sloupce tabulky (JSON)",
                Type = ToolParameterType.String,
                IsRequired = false,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Code,
                    Placeholder = "{ \"sourceField\": \"targetColumn\" }",
                    HelpText = "Prázdné = automatické mapování podle názvů",
                    CustomHints = new Dictionary<string, object> { ["language"] = "json" }
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "batchSize",
                DisplayName = "Velikost dávky",
                Description = "Počet záznamů v jedné dávce",
                Type = ToolParameterType.Integer,
                IsRequired = false,
                DefaultValue = 100,
                Validation = new SimpleParameterValidation
                {
                    MinValue = 1,
                    MaxValue = 10000
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "commandTimeout",
                DisplayName = "Timeout příkazu (s)",
                Description = "Maximální doba běhu příkazu v sekundách",
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
                Name = "enableTransaction",
                DisplayName = "Použít transakci",
                Description = "Spustit operace v transakci",
                Type = ToolParameterType.Boolean,
                IsRequired = false,
                DefaultValue = true
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "onError",
                DisplayName = "Při chybě",
                Description = "Chování při chybě",
                Type = ToolParameterType.String,
                IsRequired = false,
                DefaultValue = "rollback",
                Validation = new SimpleParameterValidation
                {
                    AllowedValues = new List<object> { "rollback", "continue", "stop" }
                },
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Select,
                    HelpText = "rollback = vrátit vše, continue = pokračovat, stop = zastavit"
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
                Name = "createTableIfNotExists",
                DisplayName = "Vytvořit tabulku",
                Description = "Automaticky vytvořit tabulku pokud neexistuje",
                Type = ToolParameterType.Boolean,
                IsRequired = false,
                DefaultValue = false
            });
        }

        protected override async Task<IAdapterResult> ExecuteWriteAsync(
            object data,
            Dictionary<string, object> configuration,
            string executionId,
            CancellationToken cancellationToken)
        {
            var databaseType = GetParameter<string>(configuration, "databaseType", "postgresql");
            var connectionString = GetParameter<string>(configuration, "connectionString");
            var tableName = GetParameter<string>(configuration, "tableName");
            var operationType = GetParameter<string>(configuration, "operationType", "insert");
            var batchSize = GetParameter<int>(configuration, "batchSize", 100);
            var enableTransaction = GetParameter<bool>(configuration, "enableTransaction", true);

            var metrics = new AdapterMetrics();
            var startTime = DateTime.UtcNow;

            try
            {
                // Parse input data
                var records = ParseDatabaseRecords(data);
                var results = new List<Dictionary<string, object>>();

                // Process records in batches
                var batches = records.Select((record, index) => new { record, index })
                    .GroupBy(x => x.index / batchSize)
                    .Select(g => g.Select(x => x.record).ToList());

                var totalBatches = batches.Count();
                var currentBatch = 0;

                foreach (var batch in batches)
                {
                    currentBatch++;
                    
                    try
                    {
                        // Simulate database write operation
                        var batchResult = new Dictionary<string, object>
                        {
                            ["batchNumber"] = currentBatch,
                            ["totalBatches"] = totalBatches,
                            ["recordsProcessed"] = batch.Count,
                            ["operationType"] = operationType,
                            ["tableName"] = tableName,
                            ["startTime"] = DateTime.UtcNow,
                            ["endTime"] = DateTime.UtcNow.AddMilliseconds(50),
                            ["status"] = "success",
                            ["affectedRows"] = batch.Count
                        };

                        // Simulate different operation types
                        switch (operationType)
                        {
                            case "insert":
                                batchResult["insertedIds"] = batch.Select((_, i) => Guid.NewGuid().ToString()).ToList();
                                break;
                            case "update":
                                batchResult["updatedRows"] = batch.Count;
                                break;
                            case "upsert":
                                batchResult["insertedRows"] = batch.Count / 2;
                                batchResult["updatedRows"] = batch.Count - (batch.Count / 2);
                                break;
                        }

                        results.Add(batchResult);
                        metrics.ItemsProcessed += batch.Count;
                        metrics.BytesProcessed += batch.Sum(r => EstimateRecordSize(r));

                        Logger.LogInformation("Processed batch {Batch}/{Total} with {Records} records", 
                            currentBatch, totalBatches, batch.Count);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "Failed to process batch {Batch}", currentBatch);
                        
                        var failedBatch = new Dictionary<string, object>
                        {
                            ["batchNumber"] = currentBatch,
                            ["status"] = "failed",
                            ["error"] = ex.Message,
                            ["recordsAffected"] = 0
                        };
                        
                        results.Add(failedBatch);
                        
                        if (enableTransaction)
                        {
                            throw; // Rollback transaction
                        }
                    }
                }

                // Calculate metrics
                metrics.ProcessingTime = DateTime.UtcNow - startTime;
                metrics.ThroughputItemsPerSecond = metrics.ItemsProcessed / Math.Max(1, metrics.ProcessingTime.TotalSeconds);
                metrics.ThroughputMBPerSecond = (metrics.BytesProcessed / 1024.0 / 1024.0) / Math.Max(1, metrics.ProcessingTime.TotalSeconds);

                // Create schema
                var schema = new DatabaseWriteResultSchema
                {
                    Id = "database_write_results",
                    Name = "Výsledky zápisu do databáze",
                    Description = "Informace o zapsaných datech",
                    Fields = new List<SchemaField>
                    {
                        new SchemaField { Name = "batchNumber", Type = "integer", IsRequired = true },
                        new SchemaField { Name = "totalBatches", Type = "integer", IsRequired = true },
                        new SchemaField { Name = "recordsProcessed", Type = "integer", IsRequired = true },
                        new SchemaField { Name = "status", Type = "string", IsRequired = true },
                        new SchemaField { Name = "affectedRows", Type = "integer", IsRequired = false },
                        new SchemaField { Name = "error", Type = "string", IsRequired = false }
                    }
                };

                return CreateSuccessResult(executionId, startTime, results, metrics, schema, results.Take(3).ToList());
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error in database output adapter");
                return CreateExceptionResult(executionId, startTime, ex);
            }
        }

        private List<Dictionary<string, object>> ParseDatabaseRecords(object data)
        {
            var records = new List<Dictionary<string, object>>();

            if (data is Dictionary<string, object> singleRecord)
            {
                records.Add(singleRecord);
            }
            else if (data is List<Dictionary<string, object>> recordList)
            {
                records.AddRange(recordList);
            }
            else if (data is IEnumerable<object> enumerable)
            {
                foreach (var item in enumerable)
                {
                    if (item is Dictionary<string, object> record)
                    {
                        records.Add(record);
                    }
                    else
                    {
                        // Convert to dictionary
                        records.Add(new Dictionary<string, object>
                        {
                            ["data"] = item
                        });
                    }
                }
            }

            // If no valid records found, create default
            if (!records.Any())
            {
                records.Add(new Dictionary<string, object>
                {
                    ["workflow_id"] = Guid.NewGuid().ToString(),
                    ["data"] = data?.ToString() ?? "No data",
                    ["created_at"] = DateTime.UtcNow,
                    ["status"] = "processed"
                });
            }

            return records;
        }

        private long EstimateRecordSize(Dictionary<string, object> record)
        {
            long size = 0;
            foreach (var value in record.Values)
            {
                if (value is string str)
                    size += str.Length * 2;
                else if (value is int or long or decimal or double or float)
                    size += 8;
                else if (value is bool)
                    size += 1;
                else if (value is DateTime)
                    size += 8;
                else
                    size += 50;
            }
            return size;
        }

        protected override async Task PerformDestinationValidationAsync(
            Dictionary<string, object> configuration,
            CancellationToken cancellationToken)
        {
            var connectionString = GetParameter<string>(configuration, "connectionString");
            var tableName = GetParameter<string>(configuration, "tableName");
            var operationType = GetParameter<string>(configuration, "operationType", "insert");
            
            if (string.IsNullOrEmpty(connectionString))
                throw new InvalidOperationException("Connection string is required");

            if (string.IsNullOrEmpty(tableName))
                throw new InvalidOperationException("Table name is required");

            if ((operationType == "update" || operationType == "upsert") && 
                string.IsNullOrEmpty(GetParameter<string>(configuration, "keyColumns", "")))
            {
                throw new InvalidOperationException($"Key columns are required for {operationType} operation");
            }

            // In real implementation, would test database connection and table existence
            await Task.CompletedTask;
        }

        public override IReadOnlyList<IAdapterSchema> GetInputSchemas()
        {
            return new List<IAdapterSchema>
            {
                new DatabaseRecordSchema
                {
                    Id = "database_record",
                    Name = "Database Record",
                    Description = "Data pro zápis do databáze",
                    JsonSchema = @"{
                        ""type"": ""object"",
                        ""additionalProperties"": true
                    }",
                    ExampleData = new Dictionary<string, object>
                    {
                        ["id"] = 123,
                        ["name"] = "Example Record",
                        ["value"] = 99.99,
                        ["created_at"] = DateTime.UtcNow,
                        ["status"] = "active"
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
                SupportsTransactions = true,
                RequiresAuthentication = true,
                MaxDataSizeBytes = 100 * 1024 * 1024, // 100 MB
                MaxConcurrentOperations = 5,
                SupportedFormats = new List<string> { "table", "json", "csv" },
                SupportedEncodings = new List<string> { "UTF-8", "UTF-16", "ISO-8859-1" },
                CustomCapabilities = new Dictionary<string, object>
                {
                    ["supportsBulkInsert"] = true,
                    ["supportsBulkUpdate"] = true,
                    ["supportsUpsert"] = true,
                    ["supportsTransactions"] = true,
                    ["supportsStoredProcedures"] = false,
                    ["supportsTriggers"] = false,
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
    /// Schema for database record input
    /// </summary>
    internal class DatabaseRecordSchema : IAdapterSchema
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string JsonSchema { get; set; }
        public object ExampleData { get; set; }
        public IReadOnlyList<SchemaField> Fields { get; set; } = new List<SchemaField>();
    }

    /// <summary>
    /// Schema for database write results
    /// </summary>
    internal class DatabaseWriteResultSchema : IAdapterSchema
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string JsonSchema { get; set; }
        public object ExampleData { get; set; }
        public IReadOnlyList<SchemaField> Fields { get; set; } = new List<SchemaField>();
    }
}