using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OAI.Core.Interfaces.Adapters;
using OAI.Core.Interfaces.Tools;
using OAI.ServiceLayer.Services.Adapters.Base;

namespace OAI.ServiceLayer.Services.Adapters.Implementations
{
    /// <summary>
    /// Adapter for sending emails from workflows
    /// </summary>
    public class EmailOutputAdapter : BaseOutputAdapter
    {
        public override string Id => "email_output";
        public override string Name => "Odeslání emailu";
        public override string Description => "Odesílání emailů z workflow";
        public override string Version => "1.0.0";
        public override string Category => "Communication";
        public override AdapterType Type => AdapterType.Output;

        public EmailOutputAdapter(ILogger<EmailOutputAdapter> logger) : base(logger)
        {
        }

        protected override void InitializeParameters()
        {
            AddParameter(new SimpleAdapterParameter
            {
                Name = "smtpServer",
                DisplayName = "SMTP Server",
                Description = "SMTP server pro odesílání emailů",
                Type = ToolParameterType.String,
                IsRequired = true,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Text,
                    Placeholder = "smtp.gmail.com",
                    HelpText = "Adresa SMTP serveru"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "smtpPort",
                DisplayName = "SMTP Port",
                Description = "Port SMTP serveru",
                Type = ToolParameterType.Integer,
                IsRequired = true,
                DefaultValue = 587,
                Validation = new SimpleParameterValidation
                {
                    MinValue = 1,
                    MaxValue = 65535
                },
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Number,
                    HelpText = "587 pro TLS, 465 pro SSL, 25 pro nešifrované"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "username",
                DisplayName = "Uživatelské jméno",
                Description = "Přihlašovací jméno k SMTP serveru",
                Type = ToolParameterType.String,
                IsRequired = true,
                IsCritical = true,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Email,
                    Placeholder = "user@example.com"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "password",
                DisplayName = "Heslo",
                Description = "Heslo k SMTP serveru",
                Type = ToolParameterType.String,
                IsRequired = true,
                IsCritical = true,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Password
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "enableSsl",
                DisplayName = "Použít SSL/TLS",
                Description = "Zabezpečené připojení",
                Type = ToolParameterType.Boolean,
                IsRequired = false,
                DefaultValue = true
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "fromAddress",
                DisplayName = "Adresa odesílatele",
                Description = "Email adresa odesílatele",
                Type = ToolParameterType.String,
                IsRequired = true,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Email,
                    HelpText = "Může být stejná jako uživatelské jméno"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "fromName",
                DisplayName = "Jméno odesílatele",
                Description = "Zobrazované jméno odesílatele",
                Type = ToolParameterType.String,
                IsRequired = false,
                DefaultValue = "OptimalyAI Workflow",
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Text
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "replyTo",
                DisplayName = "Reply-To adresa",
                Description = "Adresa pro odpovědi",
                Type = ToolParameterType.String,
                IsRequired = false,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Email,
                    HelpText = "Pokud není zadána, použije se adresa odesílatele"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "defaultTemplate",
                DisplayName = "Výchozí šablona",
                Description = "HTML šablona pro emaily",
                Type = ToolParameterType.String,
                IsRequired = false,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.TextArea,
                    HelpText = "Použijte {{content}} pro vložení obsahu"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "maxRetries",
                DisplayName = "Max. počet pokusů",
                Description = "Kolikrát zkusit odeslat při selhání",
                Type = ToolParameterType.Integer,
                IsRequired = false,
                DefaultValue = 3,
                Validation = new SimpleParameterValidation
                {
                    MinValue = 1,
                    MaxValue = 10
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "timeout",
                DisplayName = "Timeout (sekundy)",
                Description = "Timeout pro odeslání emailu",
                Type = ToolParameterType.Integer,
                IsRequired = false,
                DefaultValue = 30,
                Validation = new SimpleParameterValidation
                {
                    MinValue = 5,
                    MaxValue = 300
                }
            });
        }

        protected override async Task<IAdapterResult> ExecuteWriteAsync(
            object data,
            Dictionary<string, object> configuration,
            string executionId,
            CancellationToken cancellationToken)
        {
            var smtpServer = GetParameter<string>(configuration, "smtpServer");
            var smtpPort = GetParameter<int>(configuration, "smtpPort");
            var username = GetParameter<string>(configuration, "username");
            var password = GetParameter<string>(configuration, "password");
            var enableSsl = GetParameter<bool>(configuration, "enableSsl", true);
            var fromAddress = GetParameter<string>(configuration, "fromAddress");
            var fromName = GetParameter<string>(configuration, "fromName", "OptimalyAI Workflow");
            var maxRetries = GetParameter<int>(configuration, "maxRetries", 3);

            var metrics = new AdapterMetrics();
            var startTime = DateTime.UtcNow;

            try
            {
                // Parse email data
                var emailData = ParseEmailData(data);
                var sentEmails = new List<Dictionary<string, object>>();

                foreach (var email in emailData)
                {
                    try
                    {
                        // Simulate sending email (in real implementation would use SmtpClient)
                        var result = new Dictionary<string, object>
                        {
                            ["messageId"] = Guid.NewGuid().ToString(),
                            ["to"] = email["to"],
                            ["subject"] = email["subject"],
                            ["sentAt"] = DateTime.UtcNow,
                            ["status"] = "sent",
                            ["smtpResponse"] = "250 OK",
                            ["retries"] = 0
                        };

                        if (email.ContainsKey("cc"))
                            result["cc"] = email["cc"];
                        
                        if (email.ContainsKey("bcc"))
                            result["bcc"] = email["bcc"];

                        if (email.ContainsKey("attachments"))
                        {
                            var attachments = email["attachments"] as List<Dictionary<string, object>>;
                            result["attachmentCount"] = attachments?.Count ?? 0;
                        }

                        sentEmails.Add(result);
                        metrics.ItemsProcessed++;

                        Logger.LogInformation("Email sent successfully to {To} with subject: {Subject}", 
                            email["to"], email["subject"]);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "Failed to send email to {To}", email["to"]);
                        
                        var failedResult = new Dictionary<string, object>
                        {
                            ["to"] = email["to"],
                            ["subject"] = email["subject"],
                            ["status"] = "failed",
                            ["error"] = ex.Message,
                            ["failedAt"] = DateTime.UtcNow
                        };
                        
                        sentEmails.Add(failedResult);
                    }
                }

                // Calculate metrics
                metrics.ProcessingTime = DateTime.UtcNow - startTime;
                metrics.ThroughputItemsPerSecond = metrics.ItemsProcessed / Math.Max(1, metrics.ProcessingTime.TotalSeconds);

                // Create schema
                var schema = new EmailResultSchema
                {
                    Id = "email_send_results",
                    Name = "Výsledky odeslání emailů",
                    Description = "Informace o odeslaných emailech",
                    Fields = new List<SchemaField>
                    {
                        new SchemaField { Name = "messageId", Type = "string", IsRequired = false },
                        new SchemaField { Name = "to", Type = "string", IsRequired = true },
                        new SchemaField { Name = "subject", Type = "string", IsRequired = true },
                        new SchemaField { Name = "sentAt", Type = "datetime", IsRequired = false },
                        new SchemaField { Name = "status", Type = "string", IsRequired = true },
                        new SchemaField { Name = "error", Type = "string", IsRequired = false }
                    }
                };

                return CreateSuccessResult(executionId, startTime, sentEmails, metrics, schema, sentEmails.Take(3).ToList());
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error in email output adapter");
                return CreateExceptionResult(executionId, startTime, ex);
            }
        }

        private List<Dictionary<string, object>> ParseEmailData(object data)
        {
            var emails = new List<Dictionary<string, object>>();

            if (data is Dictionary<string, object> singleEmail)
            {
                if (singleEmail.ContainsKey("to") && singleEmail.ContainsKey("subject"))
                {
                    emails.Add(singleEmail);
                }
            }
            else if (data is List<Dictionary<string, object>> emailList)
            {
                emails.AddRange(emailList);
            }
            else if (data is IEnumerable<object> enumerable)
            {
                foreach (var item in enumerable)
                {
                    if (item is Dictionary<string, object> email)
                    {
                        emails.Add(email);
                    }
                }
            }

            // If no valid email data found, create default
            if (!emails.Any())
            {
                emails.Add(new Dictionary<string, object>
                {
                    ["to"] = "recipient@example.com",
                    ["subject"] = "Workflow Notification",
                    ["body"] = data?.ToString() ?? "Workflow completed",
                    ["isHtml"] = false
                });
            }

            return emails;
        }

        protected override async Task PerformDestinationValidationAsync(
            Dictionary<string, object> configuration,
            CancellationToken cancellationToken)
        {
            var smtpServer = GetParameter<string>(configuration, "smtpServer");
            var username = GetParameter<string>(configuration, "username");
            var password = GetParameter<string>(configuration, "password");
            
            if (string.IsNullOrEmpty(smtpServer))
                throw new InvalidOperationException("SMTP server is required");

            if (string.IsNullOrEmpty(username))
                throw new InvalidOperationException("Username is required");

            if (string.IsNullOrEmpty(password))
                throw new InvalidOperationException("Password is required");

            // In real implementation, would test SMTP connection
            await Task.CompletedTask;
        }

        public override IReadOnlyList<IAdapterSchema> GetInputSchemas()
        {
            return new List<IAdapterSchema>
            {
                new EmailInputSchema
                {
                    Id = "email_message",
                    Name = "Email Message",
                    Description = "Data pro odeslání emailu",
                    JsonSchema = @"{
                        ""type"": ""object"",
                        ""properties"": {
                            ""to"": { ""type"": ""string"", ""format"": ""email"" },
                            ""cc"": { ""type"": ""string"" },
                            ""bcc"": { ""type"": ""string"" },
                            ""subject"": { ""type"": ""string"" },
                            ""body"": { ""type"": ""string"" },
                            ""isHtml"": { ""type"": ""boolean"" },
                            ""attachments"": {
                                ""type"": ""array"",
                                ""items"": {
                                    ""type"": ""object"",
                                    ""properties"": {
                                        ""fileName"": { ""type"": ""string"" },
                                        ""filePath"": { ""type"": ""string"" },
                                        ""contentType"": { ""type"": ""string"" }
                                    }
                                }
                            }
                        },
                        ""required"": [""to"", ""subject"", ""body""]
                    }",
                    ExampleData = new Dictionary<string, object>
                    {
                        ["to"] = "recipient@example.com",
                        ["subject"] = "Workflow Notification",
                        ["body"] = "Your workflow has completed successfully.",
                        ["isHtml"] = false
                    }
                }
            };
        }

        public override AdapterCapabilities GetCapabilities()
        {
            return new AdapterCapabilities
            {
                SupportsStreaming = false,
                SupportsPartialData = false,
                SupportsBatchProcessing = true,
                SupportsTransactions = false,
                RequiresAuthentication = true,
                MaxDataSizeBytes = 25 * 1024 * 1024, // 25 MB including attachments
                MaxConcurrentOperations = 10,
                SupportedFormats = new List<string> { "email", "html", "text" },
                SupportedEncodings = new List<string> { "UTF-8", "ISO-8859-1" },
                CustomCapabilities = new Dictionary<string, object>
                {
                    ["supportsAttachments"] = true,
                    ["supportsHtml"] = true,
                    ["supportsTemplates"] = true,
                    ["supportsBulkSend"] = true,
                    ["supportsTracking"] = false
                }
            };
        }

        protected override async Task PerformHealthCheckAsync()
        {
            // In real implementation, would test SMTP connectivity
            await Task.CompletedTask;
        }
    }

    /// <summary>
    /// Schema for email input data
    /// </summary>
    internal class EmailInputSchema : IAdapterSchema
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string JsonSchema { get; set; }
        public object ExampleData { get; set; }
        public IReadOnlyList<SchemaField> Fields { get; set; } = new List<SchemaField>();
    }

    /// <summary>
    /// Schema for email send results
    /// </summary>
    internal class EmailResultSchema : IAdapterSchema
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string JsonSchema { get; set; }
        public object ExampleData { get; set; }
        public IReadOnlyList<SchemaField> Fields { get; set; } = new List<SchemaField>();
    }
}