using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OAI.Core.Attributes;
using OAI.Core.DTOs.Orchestration;
using OAI.Core.Interfaces.AI;
using OAI.Core.Interfaces.Orchestration;
using OAI.ServiceLayer.Services.Orchestration.Base;
using OAI.ServiceLayer.Interfaces;

namespace OAI.ServiceLayer.Services.Orchestration
{
    /// <summary>
    /// AI Coding Orchestrator - aktivní AI programátor asistent
    /// </summary>
    [OrchestratorMetadata(
        "coding_orchestrator",
        "AI Coding Orchestrator", 
        "Aktivní AI programátor asistent pro analýzu a úpravu kódu"
    )]
    public class CodingOrchestrator : BaseOrchestrator<CodingOrchestratorRequestDto, CodingOrchestratorResponseDto>
    {
        private readonly IAiServiceRouter _aiServiceRouter;
        private readonly IOrchestratorConfigurationService _configurationService;

        public override string Id => "coding_orchestrator";
        public override string Name => "AI Coding Orchestrator";
        public override string Description => "Aktivní AI programátor asistent pro analýzu a úpravu kódu";

        public CodingOrchestrator(
            IAiServiceRouter aiServiceRouter,
            ILogger<CodingOrchestrator> logger,
            IOrchestratorMetrics metrics,
            IServiceProvider serviceProvider) : base(logger, metrics, serviceProvider)
        {
            _aiServiceRouter = aiServiceRouter ?? throw new ArgumentNullException(nameof(aiServiceRouter));
            _configurationService = serviceProvider.GetService<IOrchestratorConfigurationService>() 
                ?? throw new InvalidOperationException("IOrchestratorConfigurationService is required");
        }

        protected override async Task<CodingOrchestratorResponseDto> ExecuteCoreAsync(
            CodingOrchestratorRequestDto request, 
            IOrchestratorContext context,
            OrchestratorResult<CodingOrchestratorResponseDto> result,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting AI Coding Orchestrator for task: {Task}", request.Task);

            var response = new CodingOrchestratorResponseDto
            {
                ExecutionId = context.ExecutionId,
                StartedAt = DateTime.UtcNow
            };

            try
            {
                // Načtení konfigurace orchestrátoru
                var configuration = await _configurationService.GetByOrchestratorIdAsync(Id);
                if (configuration == null)
                {
                    throw new InvalidOperationException($"Configuration not found for orchestrator {Id}");
                }
                
                // Extrakce uživatelského dotazu z kontextu
                var userQuery = ExtractUserQuery(request.Task);
                _logger.LogDebug("Extracted user query: {UserQuery}", userQuery);
                
                // Detekce typu dotazu
                var isCodingRequest = IsCodingRequest(userQuery);
                _logger.LogInformation("Query type detection - IsCoding: {IsCoding}, Query: {Query}", isCodingRequest, userQuery);
                
                // Určení modelu podle typu dotazu
                string? modelId;
                if (isCodingRequest)
                {
                    modelId = request.ModelId ?? configuration.DefaultModelId?.ToString();
                }
                else
                {
                    // Pro konverzaci použijeme ConversationModelId, nebo pokud není nastaven, tak DefaultModelId
                    modelId = configuration.ConversationModelId?.ToString() ?? configuration.DefaultModelId?.ToString();
                }
                
                if (string.IsNullOrWhiteSpace(modelId))
                {
                    throw new InvalidOperationException("No model configured for orchestrator");
                }
                
                _logger.LogInformation("Using model {ModelId} for {QueryType} query", modelId, isCodingRequest ? "coding" : "conversation");
                
                if (isCodingRequest)
                {
                    // Původní coding flow
                    // 1. Analýza projektového stromu
                    var projectAnalysis = await AnalyzeProjectStructure(request.ProjectPath, request.TargetFiles);
                    response.ProjectAnalysis = projectAnalysis;

                    // 2. Sestavení kontextu a promptu
                    var prompt = BuildCodingPrompt(request, projectAnalysis);

                    // 3. Volání AI modelu s coding parametry
                    var aiResponse = await CallAIModelForCoding(modelId, prompt, cancellationToken);
                    response.Explanation = aiResponse;

                    // 4. Parsování navrhovaných změn
                    var proposedChanges = ParseProposedChanges(aiResponse, request.ProjectPath);
                    response.ProposedChanges = proposedChanges;

                    // 5. Aplikace změn (pokud je autoApply = true)
                    if (request.AutoApply)
                    {
                        var appliedChanges = await ApplyChanges(proposedChanges);
                        response.AppliedChanges = appliedChanges;
                    }
                }
                else
                {
                    // Konverzační flow
                    var conversationPrompt = BuildConversationPrompt(request, userQuery);
                    var aiResponse = await CallAIModelForConversation(modelId, conversationPrompt, cancellationToken);
                    response.Explanation = aiResponse;
                    response.ProjectAnalysis = "Konverzační dotaz - analýza projektu není potřeba.";
                    response.ProposedChanges = new List<CodeChange>(); // Žádné změny kódu pro konverzaci
                }

                response.Success = true;
                response.CompletedAt = DateTime.UtcNow;

                _logger.LogInformation("AI Coding Orchestrator completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AI Coding Orchestrator");
                response.Success = false;
                response.ErrorMessage = ex.Message;
                response.Errors.Add(ex.Message);
                response.CompletedAt = DateTime.UtcNow;
            }

            return response;
        }

        /// <summary>
        /// Analyzuje strukturu projektu
        /// </summary>
        private async Task<string> AnalyzeProjectStructure(string projectPath, List<string> targetFiles)
        {
            var analysis = new List<string>();
            
            try
            {
                if (!Directory.Exists(projectPath))
                {
                    return $"Projektová cesta neexistuje: {projectPath}";
                }

                analysis.Add($"=== Analýza projektu: {projectPath} ===");
                
                // Základní struktura
                var directories = Directory.GetDirectories(projectPath, "*", SearchOption.TopDirectoryOnly)
                    .Take(10)
                    .Select(d => Path.GetFileName(d));
                analysis.Add($"Hlavní složky: {string.Join(", ", directories)}");

                // Relevantní soubory
                var relevantFiles = GetRelevantFiles(projectPath, targetFiles);
                analysis.Add($"Relevantní soubory ({relevantFiles.Count}): {string.Join(", ", relevantFiles.Take(10))}");

                // Obsah vybraných souborů
                if (targetFiles.Any())
                {
                    foreach (var file in targetFiles.Take(3)) // Maximálně 3 soubory
                    {
                        var fullPath = Path.IsPathRooted(file) ? file : Path.Combine(projectPath, file);
                        if (File.Exists(fullPath))
                        {
                            var content = await File.ReadAllTextAsync(fullPath);
                            analysis.Add($"\n--- Obsah {file} ---\n{content.Substring(0, Math.Min(content.Length, 2000))}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                analysis.Add($"Chyba při analýze: {ex.Message}");
            }

            return string.Join("\n", analysis);
        }

        /// <summary>
        /// Získá relevantní soubory projektu
        /// </summary>
        private List<string> GetRelevantFiles(string projectPath, List<string> targetFiles)
        {
            if (targetFiles.Any())
            {
                return targetFiles;
            }

            var extensions = new[] { ".cs", ".js", ".ts", ".jsx", ".tsx", ".html", ".css", ".json" };
            
            return Directory.GetFiles(projectPath, "*.*", SearchOption.AllDirectories)
                .Where(f => extensions.Contains(Path.GetExtension(f).ToLower()))
                .Where(f => !f.Contains("bin") && !f.Contains("obj") && !f.Contains("node_modules"))
                .Select(f => Path.GetRelativePath(projectPath, f))
                .Take(20)
                .ToList();
        }

        /// <summary>
        /// Sestaví prompt pro AI model
        /// </summary>
        private string BuildCodingPrompt(CodingOrchestratorRequestDto request, string projectAnalysis)
        {
            return $@"Jsi aktivní AI programátor asistent. Tvým úkolem je analyzovat projekt a provést požadované změny.

ÚKOL:
{request.Task}

KONTEXT:
{request.Context}

ANALÝZA PROJEKTU:
{projectAnalysis}

INSTRUKCE:
1. Analyzuj současný stav kódu
2. Navrhni konkrétní změny pro splnění úkolu
3. Vysvětli své rozhodnutí
4. Poskytni konkrétní kód pro implementaci

FORMÁT ODPOVĚDI:
```
ANALÝZA:
[Tvá analýza problému]

ŘEŠENÍ:
[Popis navrhovaného řešení]

ZMĚNY:
FILE: path/to/file.cs
ACTION: create|modify|delete
CONTENT:
[obsah souboru]
---

FILE: path/to/another/file.cs  
ACTION: modify
CONTENT:
[nový obsah]
---
```

Odpověz profesionálně a konkrétně.";
        }

        /// <summary>
        /// Extrahuje uživatelský dotaz z kontextu
        /// </summary>
        private string ExtractUserQuery(string task)
        {
            // Hledáme "Uživatelský dotaz:" v textu
            var userQueryMarker = "Uživatelský dotaz:";
            var index = task.IndexOf(userQueryMarker);
            
            if (index >= 0)
            {
                // Vrátíme část po "Uživatelský dotaz:"
                return task.Substring(index + userQueryMarker.Length).Trim();
            }
            
            // Pokud marker nenajdeme, vrátíme celý task
            return task;
        }

        /// <summary>
        /// Detekuje, zda jde o programovací dotaz
        /// </summary>
        private bool IsCodingRequest(string task)
        {
            var taskLower = task.ToLower();
            
            // Pokud obsahuje pozdrav nebo obecný dotaz, není to programovací úkol
            var conversationKeywords = new[] 
            { 
                "ahoj", "jak se", "dobrý den", "děkuji", "díky", "prosím",
                "co umíš", "pomoc", "nápověda", "vysvětli mi", "řekni mi"
            };
            
            if (conversationKeywords.Any(keyword => taskLower.Contains(keyword)))
            {
                return false;
            }
            
            // Programovací klíčová slova
            var codingKeywords = new[] 
            { 
                "unit test", "refactor", "implementuj", "vytvoř", "uprav", "oprav", 
                "analyzuj", "kód", "code", "function", "class", "method", "debug",
                "struktur", "projekt", "soubor", "třída", "metoda", "funkce",
                "přidej", "odstraň", "změň", "optimalizuj", "vylepši"
            };
            
            return codingKeywords.Any(keyword => taskLower.Contains(keyword));
        }

        /// <summary>
        /// Sestaví prompt pro konverzaci
        /// </summary>
        private string BuildConversationPrompt(CodingOrchestratorRequestDto request, string userQuery)
        {
            var contextParts = request.Context?.Split('\n')
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList() ?? new List<string>();

            return $@"Jsi přátelský AI asistent pro vývoj aplikací. Odpovídej stručně a přirozeně v češtině.

{string.Join("\n", contextParts)}

Uživatel: {userQuery}

Asistent:";
        }

        /// <summary>
        /// Volá AI model pro programování
        /// </summary>
        private async Task<string> CallAIModelForCoding(string modelId, string prompt, CancellationToken cancellationToken)
        {
            try
            {
                // Pro coding používáme nižší teplotu pro přesnější odpovědi
                var response = await _aiServiceRouter.GenerateResponseWithRoutingAsync(
                    modelId, 
                    prompt,
                    Guid.NewGuid().ToString(),
                    new Dictionary<string, object>
                    {
                        { "max_tokens", 4000 },
                        { "temperature", 0.1 }
                    },
                    cancellationToken);

                return response ?? "Prázdná odpověď z AI modelu";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Chyba při volání AI modelu pro coding {ModelId}", modelId);
                return $"Chyba při volání AI: {ex.Message}";
            }
        }

        /// <summary>
        /// Volá AI model pro konverzaci
        /// </summary>
        private async Task<string> CallAIModelForConversation(string modelId, string prompt, CancellationToken cancellationToken)
        {
            try
            {
                // Pro konverzaci používáme vyšší teplotu pro přirozenější odpovědi
                var response = await _aiServiceRouter.GenerateResponseWithRoutingAsync(
                    modelId, 
                    prompt,
                    Guid.NewGuid().ToString(),
                    new Dictionary<string, object>
                    {
                        { "max_tokens", 1000 },
                        { "temperature", 0.7 }
                    },
                    cancellationToken);

                return response ?? "Prázdná odpověď z AI modelu";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Chyba při volání AI modelu pro konverzaci {ModelId}", modelId);
                return $"Chyba při volání AI: {ex.Message}";
            }
        }

        /// <summary>
        /// Volá AI model (deprecated - použij CallAIModelForCoding nebo CallAIModelForConversation)
        /// </summary>
        [Obsolete("Use CallAIModelForCoding or CallAIModelForConversation instead")]
        private async Task<string> CallAIModel(string modelId, string prompt, CancellationToken cancellationToken)
        {
            try
            {
                // Používáme AiServiceRouter, který správně routuje na LM Studio nebo jiný nakonfigurovaný server
                var response = await _aiServiceRouter.GenerateResponseWithRoutingAsync(
                    modelId, 
                    prompt,
                    Guid.NewGuid().ToString(), // conversationId
                    new Dictionary<string, object>
                    {
                        { "max_tokens", 4000 },
                        { "temperature", 0.1 }
                    },
                    cancellationToken);

                return response ?? "Prázdná odpověď z AI modelu";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Chyba při volání AI modelu {ModelId}", modelId);
                return $"Chyba při volání AI: {ex.Message}";
            }
        }

        /// <summary>
        /// Parsuje navrhované změny z AI odpovědi
        /// </summary>
        private List<CodeChange> ParseProposedChanges(string aiResponse, string projectPath)
        {
            var changes = new List<CodeChange>();
            
            try
            {
                // Jednoduchý parser - hledá bloky FILE:, ACTION:, CONTENT:
                var lines = aiResponse.Split('\n');
                CodeChange? currentChange = null;
                bool inContent = false;
                var contentLines = new List<string>();

                foreach (var line in lines)
                {
                    if (line.StartsWith("FILE:"))
                    {
                        // Dokončení předchozí změny
                        if (currentChange != null)
                        {
                            currentChange.NewContent = string.Join("\n", contentLines);
                            changes.Add(currentChange);
                        }

                        // Nová změna
                        currentChange = new CodeChange
                        {
                            FilePath = line.Substring(5).Trim()
                        };
                        contentLines.Clear();
                        inContent = false;
                    }
                    else if (line.StartsWith("ACTION:") && currentChange != null)
                    {
                        currentChange.ChangeType = line.Substring(7).Trim().ToLower();
                    }
                    else if (line.StartsWith("CONTENT:"))
                    {
                        inContent = true;
                    }
                    else if (line.Trim() == "---")
                    {
                        inContent = false;
                        if (currentChange != null)
                        {
                            currentChange.NewContent = string.Join("\n", contentLines);
                            changes.Add(currentChange);
                            currentChange = null;
                            contentLines.Clear();
                        }
                    }
                    else if (inContent)
                    {
                        contentLines.Add(line);
                    }
                }

                // Dokončení poslední změny
                if (currentChange != null)
                {
                    currentChange.NewContent = string.Join("\n", contentLines);
                    changes.Add(currentChange);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Chyba při parsování změn");
            }

            return changes;
        }

        /// <summary>
        /// Aplikuje změny na disk
        /// </summary>
        private async Task<List<CodeChange>> ApplyChanges(List<CodeChange> proposedChanges)
        {
            var appliedChanges = new List<CodeChange>();

            foreach (var change in proposedChanges)
            {
                try
                {
                    switch (change.ChangeType?.ToLower())
                    {
                        case "create":
                            if (!string.IsNullOrEmpty(change.NewContent))
                            {
                                await File.WriteAllTextAsync(change.FilePath, change.NewContent);
                                change.Applied = true;
                                change.Description = "Soubor vytvořen";
                            }
                            break;

                        case "modify":
                            if (File.Exists(change.FilePath) && !string.IsNullOrEmpty(change.NewContent))
                            {
                                change.OriginalContent = await File.ReadAllTextAsync(change.FilePath);
                                await File.WriteAllTextAsync(change.FilePath, change.NewContent);
                                change.Applied = true;
                                change.Description = "Soubor upraven";
                            }
                            break;

                        case "delete":
                            if (File.Exists(change.FilePath))
                            {
                                change.OriginalContent = await File.ReadAllTextAsync(change.FilePath);
                                File.Delete(change.FilePath);
                                change.Applied = true;
                                change.Description = "Soubor smazán";
                            }
                            break;
                    }

                    if (change.Applied)
                    {
                        appliedChanges.Add(change);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Chyba při aplikaci změny pro soubor {FilePath}", change.FilePath);
                    change.Description = $"Chyba: {ex.Message}";
                }
            }

            return appliedChanges;
        }

        /// <summary>
        /// Validuje request
        /// </summary>
        public override async Task<OrchestratorValidationResult> ValidateAsync(CodingOrchestratorRequestDto request)
        {
            var result = new OrchestratorValidationResult { IsValid = true };

            if (string.IsNullOrWhiteSpace(request.Task))
            {
                result.IsValid = false;
                result.Errors.Add("Task je povinný");
            }

            if (string.IsNullOrWhiteSpace(request.ProjectPath))
            {
                result.IsValid = false;
                result.Errors.Add("ProjectPath je povinný");
            }
            else if (!Directory.Exists(request.ProjectPath))
            {
                result.IsValid = false;
                result.Errors.Add($"ProjectPath neexistuje: {request.ProjectPath}");
            }

            if (string.IsNullOrWhiteSpace(request.ModelId))
            {
                result.IsValid = false;
                result.Errors.Add("ModelId je povinný");
            }

            return await Task.FromResult(result);
        }

        /// <summary>
        /// Vrací capabilities orchestrátoru
        /// </summary>
        public override OrchestratorCapabilities GetCapabilities()
        {
            return new OrchestratorCapabilities
            {
                SupportsStreaming = false,
                SupportsParallelExecution = false,
                SupportsCancel = true,
                RequiresAuthentication = false,
                MaxConcurrentExecutions = 1,
                DefaultTimeout = TimeSpan.FromMinutes(10),
                SupportedToolCategories = new List<string> { "development", "code-analysis" },
                SupportedModels = new List<string> { "deepseek-coder:6.7b", "codellama:7b", "llama3.2:3b" },
                SupportsReActPattern = false,
                SupportsToolCalling = false,
                SupportsMultiModal = false,
                MaxIterations = 1,
                SupportedInputTypes = new[] { "text/plain", "application/json" },
                SupportedOutputTypes = new[] { "text/plain", "application/json" }
            };
        }
    }
}