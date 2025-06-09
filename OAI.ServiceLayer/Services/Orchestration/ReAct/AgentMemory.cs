using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using OAI.Core.DTOs.Orchestration.ReAct;
using OAI.Core.Interfaces.Orchestration;
using System.Reflection;

namespace OAI.ServiceLayer.Services.Orchestration.ReAct;

public class AgentMemory : IAgentMemory
{
    private readonly ILogger<AgentMemory> _logger;
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _defaultExpiration = TimeSpan.FromHours(1);

    public AgentMemory(ILogger<AgentMemory> logger, IMemoryCache cache)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    public async Task StoreThoughtAsync(AgentThought thought, CancellationToken cancellationToken = default)
    {
        var key = GetThoughtsKey(thought.ExecutionId);
        var thoughts = _cache.Get<List<AgentThought>>(key) ?? new List<AgentThought>();
        thoughts.Add(thought);
        
        _cache.Set(key, thoughts, _defaultExpiration);
        
        _logger.LogDebug("Stored thought for execution {ExecutionId}: {ThoughtContent}", 
            thought.ExecutionId, thought.Content);
        
        await Task.CompletedTask;
    }

    public async Task StoreActionAsync(AgentAction action, CancellationToken cancellationToken = default)
    {
        var key = GetActionsKey(action.ExecutionId);
        var actions = _cache.Get<List<AgentAction>>(key) ?? new List<AgentAction>();
        actions.Add(action);
        
        _cache.Set(key, actions, _defaultExpiration);
        
        _logger.LogDebug("Stored action for execution {ExecutionId}: {ActionTool}", 
            action.ExecutionId, action.ToolName);
        
        await Task.CompletedTask;
    }

    public async Task StoreObservationAsync(AgentObservation observation, CancellationToken cancellationToken = default)
    {
        var key = GetObservationsKey(observation.ExecutionId);
        var observations = _cache.Get<List<AgentObservation>>(key) ?? new List<AgentObservation>();
        observations.Add(observation);
        
        _cache.Set(key, observations, _defaultExpiration);
        
        _logger.LogDebug("Stored observation for execution {ExecutionId}: {ObservationContent}", 
            observation.ExecutionId, observation.Content?.Take(100));
        
        await Task.CompletedTask;
    }

    public async Task<IReadOnlyList<AgentThought>> GetRecentThoughtsAsync(int count = 10, CancellationToken cancellationToken = default)
    {
        // For this implementation, we'll search across all cached thoughts
        // In a real implementation, this would query a database
        var allThoughts = new List<AgentThought>();
        
        // This is a simplified implementation - in production you'd want a better way to track all executions
        // We can't easily enumerate MemoryCache, so we'll skip this for now and return empty list
        // In a real implementation, we'd use a proper database or separate storage
        
        var recentThoughts = allThoughts
            .OrderByDescending(t => t.CreatedAt)
            .Take(count)
            .ToList();
            
        await Task.CompletedTask;
        return recentThoughts;
    }

    public async Task<AgentScratchpad> GetScratchpadAsync(string executionId, CancellationToken cancellationToken = default)
    {
        var thoughts = _cache.Get<List<AgentThought>>(GetThoughtsKey(executionId)) ?? new List<AgentThought>();
        var actions = _cache.Get<List<AgentAction>>(GetActionsKey(executionId)) ?? new List<AgentAction>();
        var observations = _cache.Get<List<AgentObservation>>(GetObservationsKey(executionId)) ?? new List<AgentObservation>();
        
        var scratchpad = new AgentScratchpad
        {
            ExecutionId = executionId,
            Thoughts = thoughts,
            Actions = actions,
            Observations = observations,
            CurrentStep = Math.Max(Math.Max(thoughts.Count, actions.Count), observations.Count)
        };
        
        // Try to restore original input and completion status from the first thought or action
        if (thoughts.Any())
        {
            scratchpad.StartedAt = thoughts.First().CreatedAt;
        }
        else if (actions.Any())
        {
            scratchpad.StartedAt = actions.First().CreatedAt;
        }
        
        var finalAction = actions.LastOrDefault(a => a.IsFinalAnswer);
        if (finalAction != null)
        {
            scratchpad.Complete(finalAction.FinalAnswer ?? "");
        }
        
        _logger.LogDebug("Retrieved scratchpad for execution {ExecutionId} with {ThoughtCount} thoughts, {ActionCount} actions, {ObservationCount} observations", 
            executionId, thoughts.Count, actions.Count, observations.Count);
        
        await Task.CompletedTask;
        return scratchpad;
    }

    public async Task ClearMemoryAsync(string executionId, CancellationToken cancellationToken = default)
    {
        _cache.Remove(GetThoughtsKey(executionId));
        _cache.Remove(GetActionsKey(executionId));
        _cache.Remove(GetObservationsKey(executionId));
        
        _logger.LogDebug("Cleared memory for execution {ExecutionId}", executionId);
        
        await Task.CompletedTask;
    }

    public async Task<bool> HasSimilarThoughtAsync(string thoughtText, double similarityThreshold = 0.8, CancellationToken cancellationToken = default)
    {
        var recentThoughts = await GetRecentThoughtsAsync(50, cancellationToken);
        
        foreach (var thought in recentThoughts)
        {
            var similarity = CalculateTextSimilarity(thoughtText, thought.Content);
            if (similarity >= similarityThreshold)
            {
                _logger.LogDebug("Found similar thought with similarity {Similarity}: {ThoughtContent}", 
                    similarity, thought.Content);
                return true;
            }
        }
        
        return false;
    }

    private string GetThoughtsKey(string executionId) => $"thoughts_{executionId}";
    private string GetActionsKey(string executionId) => $"actions_{executionId}";
    private string GetObservationsKey(string executionId) => $"observations_{executionId}";

    private static double CalculateTextSimilarity(string text1, string text2)
    {
        if (string.IsNullOrEmpty(text1) || string.IsNullOrEmpty(text2))
            return 0.0;

        // Simple Jaccard similarity based on words
        var words1 = text1.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var words2 = text2.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        
        var intersection = words1.Intersect(words2).Count();
        var union = words1.Union(words2).Count();
        
        return union == 0 ? 0.0 : (double)intersection / union;
    }
}