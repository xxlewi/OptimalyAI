using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OAI.Core.Interfaces.Adapters;
using OAI.Core.Interfaces.Tools;
using OAI.ServiceLayer.Services.Adapters.Base;

namespace OAI.ServiceLayer.Services.Adapters.Workflow
{
    /// <summary>
    /// Output adapter for sending emails from workflow
    /// </summary>
    public class EmailOutputAdapter : BaseOutputAdapter
    {
        public override string Id => "email-output";
        public override string Name => "Email Output";
        public override string Description => "Sends workflow results via email";
        public override string Version => "1.0.0";
        public override string Category => "Communication";

        public EmailOutputAdapter(ILogger<EmailOutputAdapter> logger) : base(logger)
        {
            InitializeParameters();
        }

        private void InitializeParameters()
        {
            AddParameter(new AdapterParameter
            {
                Name = "smtpHost",
                DisplayName = "SMTP Host",
                Description = "SMTP server hostname",
                Type = ToolParameterType.String,
                IsRequired = true
            });

            AddParameter(new AdapterParameter
            {
                Name = "smtpPort",
                DisplayName = "SMTP Port",
                Description = "SMTP server port",
                Type = ToolParameterType.Integer,
                IsRequired = false,
                DefaultValue = 587,
                Validation = new ParameterValidation
                {
                    MinValue = 1,
                    MaxValue = 65535
                }
            });

            AddParameter(new AdapterParameter
            {
                Name = "username",
                DisplayName = "Username",
                Description = "SMTP authentication username",
                Type = ToolParameterType.String,
                IsRequired = true
            });

            AddParameter(new AdapterParameter
            {
                Name = "password",
                DisplayName = "Password",
                Description = "SMTP authentication password",
                Type = ToolParameterType.String,
                IsRequired = true,
                UIHints = new ParameterUIHints
                {
                    InputType = UIInputType.Password,
                    IsSecret = true
                }
            });

            AddParameter(new AdapterParameter
            {
                Name = "from",
                DisplayName = "From Address",
                Description = "Sender email address",
                Type = ToolParameterType.String,
                IsRequired = true,
                UIHints = new ParameterUIHints
                {
                    InputType = UIInputType.Email
                }
            });

            AddParameter(new AdapterParameter
            {
                Name = "to",
                DisplayName = "To Address",
                Description = "Recipient email address (comma-separated for multiple)",
                Type = ToolParameterType.String,
                IsRequired = true
            });

            AddParameter(new AdapterParameter
            {
                Name = "subject",
                DisplayName = "Subject",
                Description = "Email subject",
                Type = ToolParameterType.String,
                IsRequired = true,
                DefaultValue = "Workflow Execution Results"
            });

            AddParameter(new AdapterParameter
            {
                Name = "enableSsl",
                DisplayName = "Enable SSL",
                Description = "Use SSL/TLS for SMTP connection",
                Type = ToolParameterType.Boolean,
                IsRequired = false,
                DefaultValue = true
            });
        }

        protected override async Task<bool> ValidateDestinationAsync(
            Dictionary<string, object> configuration,
            CancellationToken cancellationToken)
        {
            try
            {
                var smtpHost = GetParameter<string>(configuration, "smtpHost");
                var smtpPort = GetParameter<int>(configuration, "smtpPort", 587);

                // Try to resolve the SMTP host
                var hostEntry = await Dns.GetHostEntryAsync(smtpHost);
                return hostEntry != null;
            }
            catch
            {
                return false;
            }
        }

        protected override IEnumerable<IDataSchema> GetSupportedOutputSchemas()
        {
            yield return new DataSchema
            {
                Id = "email-content",
                Name = "Email Content",
                Description = "Email message content",
                Fields = new List<SchemaField>
                {
                    new SchemaField { Name = "body", Type = "string", IsRequired = true },
                    new SchemaField { Name = "isHtml", Type = "boolean", IsRequired = false, DefaultValue = false },
                    new SchemaField { Name = "attachments", Type = "array", IsRequired = false }
                }
            };
        }

        protected override async Task<object> ProcessOutputAsync(
            object data,
            Dictionary<string, object> configuration,
            CancellationToken cancellationToken)
        {
            var smtpHost = GetParameter<string>(configuration, "smtpHost");
            var smtpPort = GetParameter<int>(configuration, "smtpPort", 587);
            var username = GetParameter<string>(configuration, "username");
            var password = GetParameter<string>(configuration, "password");
            var from = GetParameter<string>(configuration, "from");
            var to = GetParameter<string>(configuration, "to");
            var subject = GetParameter<string>(configuration, "subject");
            var enableSsl = GetParameter<bool>(configuration, "enableSsl", true);

            // Format the data as email body
            var body = FormatDataAsEmailBody(data);

            using var client = new SmtpClient(smtpHost, smtpPort)
            {
                EnableSsl = enableSsl,
                Credentials = new NetworkCredential(username, password),
                Timeout = 30000 // 30 seconds
            };

            var message = new MailMessage
            {
                From = new MailAddress(from),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };

            // Add recipients
            foreach (var recipient in to.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                message.To.Add(new MailAddress(recipient.Trim()));
            }

            await client.SendMailAsync(message);

            return new Dictionary<string, object>
            {
                ["messageId"] = message.Headers["Message-ID"] ?? Guid.NewGuid().ToString(),
                ["sentAt"] = DateTime.UtcNow,
                ["recipients"] = message.To.Select(r => r.Address).ToList()
            };
        }

        private string FormatDataAsEmailBody(object data)
        {
            var html = "<html><body>";
            html += "<h2>Workflow Execution Results</h2>";
            html += "<pre>" + System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions 
            { 
                WriteIndented = true 
            }) + "</pre>";
            html += "</body></html>";
            return html;
        }

        public override AdapterCapabilities GetCapabilities()
        {
            return new AdapterCapabilities
            {
                SupportsBatch = false,
                SupportsStreaming = false,
                SupportsTransactions = false,
                MaxBatchSize = 1,
                SupportedOperations = new[] { "write" }
            };
        }

        public override AdapterType Type => AdapterType.Output;
    }
}