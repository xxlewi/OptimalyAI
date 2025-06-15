using System;
using System.Collections.Generic;
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
    /// Adapter for receiving emails as workflow triggers
    /// </summary>
    public class EmailInputAdapter : BaseInputAdapter
    {
        public override string Id => "email_input";
        public override string Name => "Příjem emailů";
        public override string Description => "Spuštění workflow při přijetí emailu";
        public override string Version => "1.0.0";
        public override string Category => "Communication";
        public override AdapterType Type => AdapterType.Input;

        public EmailInputAdapter(ILogger<EmailInputAdapter> logger) : base(logger)
        {
        }

        protected override void InitializeParameters()
        {
            AddParameter(new SimpleAdapterParameter
            {
                Name = "mailServer",
                DisplayName = "Mail Server",
                Description = "IMAP/POP3 server pro příjem emailů",
                Type = ToolParameterType.String,
                IsRequired = true,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Text,
                    Placeholder = "imap.gmail.com",
                    HelpText = "Adresa IMAP nebo POP3 serveru"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "port",
                DisplayName = "Port",
                Description = "Port mail serveru",
                Type = ToolParameterType.Integer,
                IsRequired = true,
                DefaultValue = 993,
                Validation = new SimpleParameterValidation
                {
                    MinValue = 1,
                    MaxValue = 65535
                },
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Number,
                    HelpText = "993 pro IMAP SSL, 143 pro IMAP, 995 pro POP3 SSL"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "username",
                DisplayName = "Uživatelské jméno",
                Description = "Email nebo uživatelské jméno",
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
                Description = "Heslo k emailovému účtu",
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
                Name = "useSsl",
                DisplayName = "Použít SSL/TLS",
                Description = "Zabezpečené připojení",
                Type = ToolParameterType.Boolean,
                IsRequired = false,
                DefaultValue = true
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "folder",
                DisplayName = "Složka",
                Description = "Emailová složka ke sledování",
                Type = ToolParameterType.String,
                IsRequired = false,
                DefaultValue = "INBOX",
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Text,
                    HelpText = "INBOX, Sent, Drafts, atd."
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "filterSubject",
                DisplayName = "Filtr předmětu",
                Description = "Zpracovat pouze emaily s tímto textem v předmětu",
                Type = ToolParameterType.String,
                IsRequired = false,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Text,
                    Placeholder = "[WORKFLOW]",
                    HelpText = "Prázdné = všechny emaily"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "filterSender",
                DisplayName = "Filtr odesílatele",
                Description = "Zpracovat pouze emaily od těchto odesílatelů",
                Type = ToolParameterType.String,
                IsRequired = false,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Text,
                    Placeholder = "sender@example.com, *@company.com",
                    HelpText = "Oddělené čárkou, podporuje wildcards"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "markAsRead",
                DisplayName = "Označit jako přečtené",
                Description = "Po zpracování označit email jako přečtený",
                Type = ToolParameterType.Boolean,
                IsRequired = false,
                DefaultValue = true
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "deleteAfterProcess",
                DisplayName = "Smazat po zpracování",
                Description = "Po úspěšném zpracování email smazat",
                Type = ToolParameterType.Boolean,
                IsRequired = false,
                DefaultValue = false
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "processAttachments",
                DisplayName = "Zpracovat přílohy",
                Description = "Stáhnout a zpracovat přílohy emailu",
                Type = ToolParameterType.Boolean,
                IsRequired = false,
                DefaultValue = true
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "maxEmails",
                DisplayName = "Max. počet emailů",
                Description = "Maximální počet emailů ke zpracování najednou",
                Type = ToolParameterType.Integer,
                IsRequired = false,
                DefaultValue = 10,
                Validation = new SimpleParameterValidation
                {
                    MinValue = 1,
                    MaxValue = 100
                }
            });
        }

        protected override async Task<IAdapterResult> ExecuteReadAsync(
            Dictionary<string, object> configuration,
            string executionId,
            CancellationToken cancellationToken)
        {
            var mailServer = GetParameter<string>(configuration, "mailServer");
            var port = GetParameter<int>(configuration, "port");
            var username = GetParameter<string>(configuration, "username");
            var password = GetParameter<string>(configuration, "password");
            var useSsl = GetParameter<bool>(configuration, "useSsl", true);
            var folder = GetParameter<string>(configuration, "folder", "INBOX");
            var filterSubject = GetParameter<string>(configuration, "filterSubject", "");
            var filterSender = GetParameter<string>(configuration, "filterSender", "");
            var markAsRead = GetParameter<bool>(configuration, "markAsRead", true);
            var processAttachments = GetParameter<bool>(configuration, "processAttachments", true);
            var maxEmails = GetParameter<int>(configuration, "maxEmails", 10);

            var metrics = new AdapterMetrics();
            var startTime = DateTime.UtcNow;

            try
            {
                // Simulated email data (in real implementation, would connect to mail server)
                var emails = new List<Dictionary<string, object>>();

                // Simulate processing emails
                var simulatedEmails = new[]
                {
                    new
                    {
                        Subject = "[WORKFLOW] Process Invoice",
                        From = "accounting@company.com",
                        To = username,
                        Date = DateTime.UtcNow.AddHours(-1),
                        Body = "Please process the attached invoice.",
                        HasAttachments = true,
                        AttachmentCount = 1
                    },
                    new
                    {
                        Subject = "[WORKFLOW] Customer Request",
                        From = "customer@example.com",
                        To = username,
                        Date = DateTime.UtcNow.AddHours(-2),
                        Body = "I need assistance with my order #12345",
                        HasAttachments = false,
                        AttachmentCount = 0
                    }
                };

                foreach (var email in simulatedEmails.Take(maxEmails))
                {
                    // Apply filters
                    if (!string.IsNullOrEmpty(filterSubject) && !email.Subject.Contains(filterSubject))
                        continue;

                    if (!string.IsNullOrEmpty(filterSender) && !MatchesSenderFilter(email.From, filterSender))
                        continue;

                    var emailData = new Dictionary<string, object>
                    {
                        ["messageId"] = Guid.NewGuid().ToString(),
                        ["subject"] = email.Subject,
                        ["from"] = email.From,
                        ["to"] = email.To,
                        ["date"] = email.Date,
                        ["body"] = email.Body,
                        ["bodyHtml"] = $"<p>{email.Body}</p>",
                        ["hasAttachments"] = email.HasAttachments,
                        ["attachmentCount"] = email.AttachmentCount,
                        ["folder"] = folder,
                        ["isRead"] = false,
                        ["importance"] = "normal"
                    };

                    if (processAttachments && email.HasAttachments)
                    {
                        emailData["attachments"] = new List<Dictionary<string, object>>
                        {
                            new Dictionary<string, object>
                            {
                                ["fileName"] = "invoice.pdf",
                                ["fileSize"] = 524288,
                                ["contentType"] = "application/pdf",
                                ["contentId"] = Guid.NewGuid().ToString()
                            }
                        };
                    }

                    emails.Add(emailData);
                    metrics.ItemsProcessed++;
                    metrics.BytesProcessed += 1024; // Simulated email size
                }

                // Calculate metrics
                metrics.ProcessingTime = DateTime.UtcNow - startTime;
                metrics.ThroughputItemsPerSecond = metrics.ItemsProcessed / Math.Max(1, metrics.ProcessingTime.TotalSeconds);

                // Create schema
                var schema = new EmailSchema
                {
                    Id = "email_messages",
                    Name = "Emailové zprávy",
                    Description = "Přijaté emailové zprávy",
                    Fields = new List<SchemaField>
                    {
                        new SchemaField { Name = "messageId", Type = "string", IsRequired = true },
                        new SchemaField { Name = "subject", Type = "string", IsRequired = true },
                        new SchemaField { Name = "from", Type = "string", IsRequired = true },
                        new SchemaField { Name = "to", Type = "string", IsRequired = true },
                        new SchemaField { Name = "date", Type = "datetime", IsRequired = true },
                        new SchemaField { Name = "body", Type = "string", IsRequired = false },
                        new SchemaField { Name = "bodyHtml", Type = "string", IsRequired = false },
                        new SchemaField { Name = "hasAttachments", Type = "boolean", IsRequired = false },
                        new SchemaField { Name = "attachmentCount", Type = "integer", IsRequired = false },
                        new SchemaField { Name = "attachments", Type = "array", IsRequired = false }
                    }
                };

                Logger.LogInformation("Successfully retrieved {EmailCount} emails from {Server}", emails.Count, mailServer);

                return CreateSuccessResult(executionId, startTime, emails, metrics, schema, emails.Take(3).ToList());
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error retrieving emails from {Server}", mailServer);
                return CreateExceptionResult(executionId, startTime, ex);
            }
        }

        protected override async Task PerformSourceValidationAsync(
            Dictionary<string, object> configuration,
            CancellationToken cancellationToken)
        {
            var mailServer = GetParameter<string>(configuration, "mailServer");
            var username = GetParameter<string>(configuration, "username");
            var password = GetParameter<string>(configuration, "password");
            
            if (string.IsNullOrEmpty(mailServer))
                throw new InvalidOperationException("Mail server is required");

            if (string.IsNullOrEmpty(username))
                throw new InvalidOperationException("Username is required");

            if (string.IsNullOrEmpty(password))
                throw new InvalidOperationException("Password is required");

            // In real implementation, would test connection to mail server
            await Task.CompletedTask;
        }

        public override IReadOnlyList<IAdapterSchema> GetOutputSchemas()
        {
            return new List<IAdapterSchema>
            {
                new EmailSchema
                {
                    Id = "email_input_output",
                    Name = "Email Input Output",
                    Description = "Data o přijatých emailech",
                    JsonSchema = @"{
                        ""type"": ""array"",
                        ""items"": {
                            ""type"": ""object"",
                            ""properties"": {
                                ""messageId"": { ""type"": ""string"" },
                                ""subject"": { ""type"": ""string"" },
                                ""from"": { ""type"": ""string"" },
                                ""to"": { ""type"": ""string"" },
                                ""date"": { ""type"": ""string"", ""format"": ""date-time"" },
                                ""body"": { ""type"": ""string"" },
                                ""hasAttachments"": { ""type"": ""boolean"" },
                                ""attachments"": { 
                                    ""type"": ""array"",
                                    ""items"": {
                                        ""type"": ""object"",
                                        ""properties"": {
                                            ""fileName"": { ""type"": ""string"" },
                                            ""fileSize"": { ""type"": ""integer"" },
                                            ""contentType"": { ""type"": ""string"" }
                                        }
                                    }
                                }
                            }
                        }
                    }",
                    ExampleData = new[]
                    {
                        new Dictionary<string, object>
                        {
                            ["messageId"] = "msg-123",
                            ["subject"] = "[WORKFLOW] Process Invoice",
                            ["from"] = "sender@example.com",
                            ["to"] = "workflow@company.com",
                            ["date"] = DateTime.UtcNow,
                            ["body"] = "Please process the attached invoice.",
                            ["hasAttachments"] = true,
                            ["attachmentCount"] = 1
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
                RequiresAuthentication = true,
                MaxDataSizeBytes = 50 * 1024 * 1024, // 50 MB per email
                MaxConcurrentOperations = 1,
                SupportedFormats = new List<string> { "email", "eml", "msg" },
                SupportedEncodings = new List<string> { "UTF-8", "ISO-8859-1" },
                CustomCapabilities = new Dictionary<string, object>
                {
                    ["supportsIMAP"] = true,
                    ["supportsPOP3"] = true,
                    ["supportsFilters"] = true,
                    ["supportsAttachments"] = true,
                    ["supportsOAuth2"] = false
                }
            };
        }

        private bool MatchesSenderFilter(string sender, string filter)
        {
            if (string.IsNullOrEmpty(filter))
                return true;

            var filters = filter.Split(',').Select(f => f.Trim().ToLower());
            sender = sender.ToLower();

            foreach (var f in filters)
            {
                if (f.Contains("*"))
                {
                    var pattern = f.Replace("*", ".*");
                    if (System.Text.RegularExpressions.Regex.IsMatch(sender, pattern))
                        return true;
                }
                else if (sender.Contains(f))
                {
                    return true;
                }
            }

            return false;
        }

        protected override async Task PerformHealthCheckAsync()
        {
            // In real implementation, would test mail server connectivity
            await Task.CompletedTask;
        }
    }

    /// <summary>
    /// Schema implementation for email data
    /// </summary>
    internal class EmailSchema : IAdapterSchema
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string JsonSchema { get; set; }
        public object ExampleData { get; set; }
        public IReadOnlyList<SchemaField> Fields { get; set; } = new List<SchemaField>();
    }
}