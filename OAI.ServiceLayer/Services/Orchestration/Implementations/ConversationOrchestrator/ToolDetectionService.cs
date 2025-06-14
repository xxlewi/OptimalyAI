using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OAI.Core.Interfaces.Tools;

namespace OAI.ServiceLayer.Services.Orchestration.Implementations.ConversationOrchestrator
{
    /// <summary>
    /// Service responsible for detecting tools in user messages
    /// </summary>
    public class ToolDetectionService
    {
        private readonly IToolRegistry _toolRegistry;
        private readonly ILogger<ToolDetectionService> _logger;

        // Tool detection keywords
        private readonly HashSet<string> _toolKeywords;
        private readonly Dictionary<string, string> _toolPatterns;
        private readonly Dictionary<string, Regex> _compiledPatterns;

        public ToolDetectionService(
            IToolRegistry toolRegistry,
            ILogger<ToolDetectionService> logger)
        {
            _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _toolKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _toolPatterns = new Dictionary<string, string>();
            InitializeDetectionPatterns();
            _compiledPatterns = CompilePatterns();
        }

        private void InitializeDetectionPatterns()
        {
            // Initialize tool detection keywords
            _toolKeywords.Clear();
            var keywords = new[]
            {
                // English keywords
                "search", "find", "lookup", "look up", "google",
                "what is", "who is", "where is", "when is",
                "analyze", "compare", "summarize", "translate", "generate", "create",
                "calculate", "compute", "solve",
                "weather", "forecast",
                
                // Czech keywords
                "vyhledej", "najdi", "hledej", "vyhledat", "najít",
                "co je", "kdo je", "kde je", "kdy je",
                "analyzuj", "porovnej", "shrň", "přelož", "vygeneruj", "vytvoř",
                "spočítej", "vypočítej",
                "počasí", "předpověď"
            };
            
            foreach (var keyword in keywords)
                _toolKeywords.Add(keyword);

            // Initialize tool patterns
            _toolPatterns.Clear();
            _toolPatterns.Add("web_search", @"(search|find|lookup|vyhled|najdi|hled).*?(for|about|na|pro|o)?\s+(.+)");
            _toolPatterns.Add("calculator", @"(calculate|compute|solve|spočítej|vypočítej)\s+(.+)");
            _toolPatterns.Add("llm_tornado", @"(analyze|analyzuj|compare|porovnej|summarize|shrň|translate|přelož|generate|vygeneruj|create|vytvoř)\s+(.+)");
            _toolPatterns.Add("weather", @"(weather|počasí|forecast|předpověď)\s+(in|for|v|pro)?\s*(.+)");
        }

        private Dictionary<string, Regex> CompilePatterns()
        {
            var compiled = new Dictionary<string, Regex>();
            foreach (var pattern in _toolPatterns)
            {
                try
                {
                    compiled[pattern.Key] = new Regex(pattern.Value, 
                        RegexOptions.IgnoreCase | RegexOptions.Compiled);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to compile regex pattern for tool {Tool}", pattern.Key);
                }
            }
            return compiled;
        }

        /// <summary>
        /// Detects if the message contains any tool keywords
        /// </summary>
        public bool ContainsToolKeywords(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return false;

            var lowerMessage = message.ToLowerInvariant();
            return _toolKeywords.Any(keyword => lowerMessage.Contains(keyword));
        }

        /// <summary>
        /// Detects tools that should be used based on the message
        /// </summary>
        public async Task<ToolDetectionResult> DetectToolsAsync(string message)
        {
            var result = new ToolDetectionResult
            {
                DetectedTools = new List<DetectedTool>(),
                Confidence = 0.0
            };

            if (string.IsNullOrWhiteSpace(message))
                return result;

            // First, check for pattern matches
            foreach (var pattern in _compiledPatterns)
            {
                var match = pattern.Value.Match(message);
                if (match.Success)
                {
                    var tool = await _toolRegistry.GetToolAsync(pattern.Key);
                    if (tool != null && tool.IsEnabled)
                    {
                        result.DetectedTools.Add(new DetectedTool
                        {
                            ToolId = pattern.Key,
                            ToolName = tool.Name,
                            Confidence = 0.9,
                            ExtractedQuery = match.Groups.Count > 2 ? match.Groups[match.Groups.Count - 1].Value : "",
                            MatchedPattern = pattern.Key
                        });
                    }
                }
            }

            // If no pattern matches, check for keyword matches
            if (!result.DetectedTools.Any() && ContainsToolKeywords(message))
            {
                // Default to web search for general queries
                var webSearchTool = await _toolRegistry.GetToolAsync("web_search");
                if (webSearchTool != null && webSearchTool.IsEnabled)
                {
                    result.DetectedTools.Add(new DetectedTool
                    {
                        ToolId = "web_search",
                        ToolName = webSearchTool.Name,
                        Confidence = 0.6,
                        ExtractedQuery = ExtractSearchQuery(message),
                        MatchedPattern = "keyword"
                    });
                }
            }

            // Calculate overall confidence
            if (result.DetectedTools.Any())
            {
                result.Confidence = result.DetectedTools.Max(t => t.Confidence);
                result.PrimaryTool = result.DetectedTools.OrderByDescending(t => t.Confidence).First();
            }

            _logger.LogDebug("Tool detection complete. Found {Count} tools with confidence {Confidence}", 
                result.DetectedTools.Count, result.Confidence);

            return result;
        }

        /// <summary>
        /// Extracts the search query from a message
        /// </summary>
        private string ExtractSearchQuery(string message)
        {
            // Remove common prefixes
            var prefixes = new[] 
            { 
                "search for", "find", "lookup", "look up", "google",
                "vyhledej", "najdi", "hledej", "what is", "who is",
                "co je", "kdo je"
            };

            var query = message;
            foreach (var prefix in prefixes)
            {
                if (query.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Substring(prefix.Length).Trim();
                    break;
                }
            }

            return query;
        }

        /// <summary>
        /// Builds tool parameters based on the detected tool and message
        /// </summary>
        public Dictionary<string, object> BuildToolParameters(DetectedTool tool, string originalMessage)
        {
            var parameters = new Dictionary<string, object>();

            switch (tool.ToolId)
            {
                case "web_search":
                    parameters["query"] = tool.ExtractedQuery ?? originalMessage;
                    parameters["limit"] = 5;
                    break;

                case "llm_tornado":
                    parameters["prompt"] = originalMessage;
                    parameters["model"] = "default";
                    break;

                case "calculator":
                    parameters["expression"] = tool.ExtractedQuery ?? originalMessage;
                    break;

                case "weather":
                    parameters["location"] = tool.ExtractedQuery ?? "current";
                    break;

                default:
                    parameters["input"] = originalMessage;
                    break;
            }

            return parameters;
        }
    }

    /// <summary>
    /// Result of tool detection
    /// </summary>
    public class ToolDetectionResult
    {
        public List<DetectedTool> DetectedTools { get; set; } = new();
        public DetectedTool? PrimaryTool { get; set; }
        public double Confidence { get; set; }
    }

    /// <summary>
    /// Information about a detected tool
    /// </summary>
    public class DetectedTool
    {
        public string ToolId { get; set; } = "";
        public string ToolName { get; set; } = "";
        public double Confidence { get; set; }
        public string? ExtractedQuery { get; set; }
        public string? MatchedPattern { get; set; }
    }
}