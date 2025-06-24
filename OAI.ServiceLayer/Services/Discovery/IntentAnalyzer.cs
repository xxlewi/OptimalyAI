using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OAI.Core.Interfaces.AI;
using OAI.Core.Interfaces.Discovery;
using OAI.ServiceLayer.Services.AI;

namespace OAI.ServiceLayer.Services.Discovery
{
    /// <summary>
    /// Analyzes user messages to understand workflow intent using AI
    /// </summary>
    public class IntentAnalyzer : IIntentAnalyzer
    {
        private readonly IOllamaService _ollamaService;
        private readonly ILogger<IntentAnalyzer> _logger;

        public IntentAnalyzer(
            IOllamaService ollamaService,
            ILogger<IntentAnalyzer> logger)
        {
            _ollamaService = ollamaService ?? throw new ArgumentNullException(nameof(ollamaService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<WorkflowIntent> AnalyzeIntentAsync(string userMessage, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Analyzing intent for message: {Message}", userMessage);

            var prompt = $@"Analyzuj následující požadavek uživatele na vytvoření workflow:
""{userMessage}""

Identifikuj:
1. Trigger (kdy/jak se má workflow spustit) - manual/scheduled/webhook/file-upload
2. Zdroje dat (odkud data získat) - web/api/file/database
3. Zpracování (co s daty udělat) - filter/transform/analyze/summarize
4. Výstup (kam data poslat) - file/api/database/email
5. Podmínky a omezení

Vrať odpověď ve formátu JSON:
{{
  ""trigger"": {{
    ""type"": ""manual|scheduled|webhook|file-upload"",
    ""configuration"": {{}}
  }},
  ""dataSources"": [
    {{
      ""type"": ""web|api|file|database"",
      ""url"": ""pokud je uvedena"",
      ""description"": ""popis zdroje""
    }}
  ],
  ""processing"": {{
    ""operations"": [""filter"", ""transform"", ""analyze"", ""summarize""],
    ""parameters"": {{}}
  }},
  ""outputs"": [
    {{
      ""type"": ""file|api|database|email"",
      ""format"": ""json|csv|xml|pdf""
    }}
  ],
  ""conditions"": [""seznam podmínek""],
  ""requiresWebScraping"": true/false,
  ""requiresProcessing"": true/false,
  ""requiresDataTransformation"": true/false
}}";

            try
            {
                // Send to AI for analysis
                var response = await _ollamaService.GenerateAsync(
                    "llama3.2", // Default model
                    prompt,
                    cancellationToken);

                if (string.IsNullOrEmpty(response))
                {
                    _logger.LogWarning("Empty response from AI service, using fallback analysis");
                    return FallbackAnalysis(userMessage);
                }

                // Try to parse JSON response
                try
                {
                    // Extract JSON from response (AI might include additional text)
                    var jsonStart = response.IndexOf('{');
                    var jsonEnd = response.LastIndexOf('}') + 1;
                    
                    if (jsonStart >= 0 && jsonEnd > jsonStart)
                    {
                        var jsonResponse = response.Substring(jsonStart, jsonEnd - jsonStart);
                        var analysisResult = JsonSerializer.Deserialize<IntentAnalysisResult>(jsonResponse, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                        if (analysisResult != null)
                        {
                            return ConvertToWorkflowIntent(userMessage, analysisResult);
                        }
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse AI response as JSON, using text analysis");
                }

                // If JSON parsing fails, do text-based analysis
                return TextBasedAnalysis(userMessage, response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing intent");
                return FallbackAnalysis(userMessage);
            }
        }

        private WorkflowIntent ConvertToWorkflowIntent(string userMessage, IntentAnalysisResult analysis)
        {
            var intent = new WorkflowIntent
            {
                UserMessage = userMessage,
                RequiresWebScraping = analysis.RequiresWebScraping,
                RequiresProcessing = analysis.RequiresProcessing,
                RequiresDataTransformation = analysis.RequiresDataTransformation,
                Conditions = analysis.Conditions ?? new List<string>()
            };

            if (analysis.Trigger != null)
            {
                intent.Trigger = new WorkflowTrigger
                {
                    Type = analysis.Trigger.Type ?? "manual",
                    Configuration = analysis.Trigger.Configuration
                };
            }

            if (analysis.DataSources != null)
            {
                intent.DataSources = analysis.DataSources.Select(ds => new DataSource
                {
                    Type = ds.Type ?? "web",
                    Url = ds.Url,
                    Description = ds.Description,
                    Configuration = new Dictionary<string, object>()
                }).ToList();
            }

            if (analysis.Processing != null)
            {
                intent.ProcessingRequirements = new ProcessingRequirements
                {
                    Operations = analysis.Processing.Operations ?? new List<string>(),
                    Parameters = analysis.Processing.Parameters
                };
            }

            if (analysis.Outputs != null)
            {
                intent.Outputs = analysis.Outputs.Select(o => new OutputTarget
                {
                    Type = o.Type ?? "file",
                    Format = o.Format,
                    Configuration = new Dictionary<string, object>()
                }).ToList();
            }

            return intent;
        }

        private WorkflowIntent TextBasedAnalysis(string userMessage, string aiResponse)
        {
            var intent = new WorkflowIntent
            {
                UserMessage = userMessage,
                Trigger = new WorkflowTrigger { Type = "manual" }
            };

            var lowerMessage = userMessage.ToLower();
            var lowerResponse = aiResponse.ToLower();

            // Detect web scraping needs
            if (lowerMessage.Contains("web") || lowerMessage.Contains("stáhnout") || 
                lowerMessage.Contains("scrape") || lowerMessage.Contains("url") ||
                lowerResponse.Contains("web scraping") || lowerResponse.Contains("firecrawl"))
            {
                intent.RequiresWebScraping = true;
                intent.DataSources.Add(new DataSource { Type = "web" });
            }

            // Detect processing needs
            if (lowerMessage.Contains("zpracovat") || lowerMessage.Contains("analyzovat") ||
                lowerMessage.Contains("filtrovat") || lowerMessage.Contains("transformovat") ||
                lowerResponse.Contains("processing") || lowerResponse.Contains("analysis"))
            {
                intent.RequiresProcessing = true;
                intent.ProcessingRequirements.Operations.Add("analyze");
            }

            // Detect output needs
            if (lowerMessage.Contains("uložit") || lowerMessage.Contains("export") ||
                lowerMessage.Contains("soubor") || lowerMessage.Contains("csv"))
            {
                intent.Outputs.Add(new OutputTarget 
                { 
                    Type = "file",
                    Format = lowerMessage.Contains("csv") ? "csv" : "json"
                });
            }

            return intent;
        }

        private WorkflowIntent FallbackAnalysis(string userMessage)
        {
            _logger.LogDebug("Using fallback analysis");

            var intent = new WorkflowIntent
            {
                UserMessage = userMessage,
                Trigger = new WorkflowTrigger { Type = "manual" }
            };

            var lowerMessage = userMessage.ToLower();

            // Simple keyword-based analysis
            if (lowerMessage.Contains("web") || lowerMessage.Contains("url") || 
                lowerMessage.Contains("stáhnout") || lowerMessage.Contains("scrape"))
            {
                intent.RequiresWebScraping = true;
                intent.DataSources.Add(new DataSource 
                { 
                    Type = "web",
                    Description = "Web data source"
                });
            }

            if (lowerMessage.Contains("soubor") || lowerMessage.Contains("file"))
            {
                intent.DataSources.Add(new DataSource 
                { 
                    Type = "file",
                    Description = "File data source"
                });
            }

            if (lowerMessage.Contains("zpracovat") || lowerMessage.Contains("analyzovat") ||
                lowerMessage.Contains("process") || lowerMessage.Contains("analyze"))
            {
                intent.RequiresProcessing = true;
                intent.ProcessingRequirements.Operations.Add("analyze");
            }

            if (lowerMessage.Contains("filtrovat") || lowerMessage.Contains("filter"))
            {
                intent.RequiresProcessing = true;
                intent.ProcessingRequirements.Operations.Add("filter");
            }

            if (lowerMessage.Contains("transformovat") || lowerMessage.Contains("transform"))
            {
                intent.RequiresDataTransformation = true;
                intent.ProcessingRequirements.Operations.Add("transform");
            }

            // Default output
            if (!intent.Outputs.Any())
            {
                intent.Outputs.Add(new OutputTarget 
                { 
                    Type = "file",
                    Format = "json"
                });
            }

            return intent;
        }

        // Helper class for JSON deserialization
        private class IntentAnalysisResult
        {
            public TriggerInfo? Trigger { get; set; }
            public List<DataSourceInfo>? DataSources { get; set; }
            public ProcessingInfo? Processing { get; set; }
            public List<OutputInfo>? Outputs { get; set; }
            public List<string>? Conditions { get; set; }
            public bool RequiresWebScraping { get; set; }
            public bool RequiresProcessing { get; set; }
            public bool RequiresDataTransformation { get; set; }
        }

        private class TriggerInfo
        {
            public string? Type { get; set; }
            public Dictionary<string, object>? Configuration { get; set; }
        }

        private class DataSourceInfo
        {
            public string? Type { get; set; }
            public string? Url { get; set; }
            public string? Description { get; set; }
        }

        private class ProcessingInfo
        {
            public List<string>? Operations { get; set; }
            public Dictionary<string, object>? Parameters { get; set; }
        }

        private class OutputInfo
        {
            public string? Type { get; set; }
            public string? Format { get; set; }
        }
    }
}