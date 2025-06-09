# ReAct Debug UI Implementation Plan

## Overview

The ReAct Debug UI is a comprehensive development tool designed to provide deep visibility into the ReAct (Reasoning and Acting) agent's thought processes, decision-making, and execution flow. This tool will enable developers to:

- Test and debug ReAct agents interactively
- Visualize the reasoning chain and tool selection process
- Analyze performance bottlenecks and optimization opportunities
- Fine-tune prompts and agent behavior
- Export and share test scenarios for reproducibility

### Key Features
- **Interactive Playground** for testing ReAct agents
- **Real-time Execution Visualizer** with tree and timeline views
- **Prompt Engineering Lab** for template optimization
- **Performance Metrics Dashboard**
- **Debug Console** with detailed logging
- **Test Scenario Management** (save/load/share)

## UI Components

### 1. ReAct Playground (`/ReactDebug/Playground`)

**Purpose**: Interactive testing interface for ReAct agents

```
┌─────────────────────────────────────────────────────────────────┐
│ ReAct Playground                                    [Run] [Clear] │
├─────────────────────────────────────────────────────────────────┤
│ Configuration:                                                   │
│ ┌─────────────────────┐ ┌─────────────────────┐               │
│ │ Model: [llama3.2 ▼] │ │ Max Steps: [5    ] │               │
│ └─────────────────────┘ └─────────────────────┘               │
│                                                                 │
│ Available Tools: [✓] Web Search [ ] Code Analyzer [✓] Calculator│
│                                                                 │
│ User Query:                                                     │
│ ┌─────────────────────────────────────────────────────────────┐│
│ │ What is the weather in Prague and how many degrees is      ││
│ │ that in Fahrenheit?                                         ││
│ └─────────────────────────────────────────────────────────────┘│
│                                                                 │
│ Execution Controls:                                             │
│ [▶ Run] [⏸ Pause] [⏭ Step] [⏹ Stop] Speed: [Normal ▼]        │
└─────────────────────────────────────────────────────────────────┘
```

### 2. Execution Visualizer (`/ReactDebug/Visualizer`)

**Purpose**: Visual representation of ReAct execution flow

#### Tree View
```
┌─────────────────────────────────────────────────────────────────┐
│ Execution Tree                              [Tree] [Timeline]    │
├─────────────────────────────────────────────────────────────────┤
│ ▼ Initial Query: "Weather in Prague..."                         │
│   ├─▼ Thought 1: "I need to search for current weather"        │
│   │  ├─ Tool: web_search                                       │
│   │  ├─ Confidence: 0.95                                        │
│   │  └─ Result: "Prague: 18°C, partly cloudy"                  │
│   │                                                             │
│   ├─▼ Thought 2: "Now I need to convert to Fahrenheit"         │
│   │  ├─ Tool: calculator                                       │
│   │  ├─ Confidence: 0.98                                        │
│   │  └─ Result: "64.4°F"                                       │
│   │                                                             │
│   └─● Final Answer: "The weather in Prague is 18°C (64.4°F)..." │
└─────────────────────────────────────────────────────────────────┘
```

#### Timeline View
```
┌─────────────────────────────────────────────────────────────────┐
│ Execution Timeline                          [Tree] [Timeline]    │
├─────────────────────────────────────────────────────────────────┤
│ 0ms ─────── 500ms ────── 1000ms ────── 1500ms ────── 2000ms   │
│ │                                                               │
│ │ Query ═══╗                                                    │
│ │          ╚═ Thought 1 ══╗                                     │
│ │                         ╚═ web_search ════════╗               │
│ │                                               ╚═ Thought 2 ══╗ │
│ │                                                              ╚═│
│ │                                                               │
│ └───────────────────────────────────────────────────────────────│
│ Total: 1,847ms | Tools: 2 | Thoughts: 2 | Tokens: 487          │
└─────────────────────────────────────────────────────────────────┘
```

### 3. Prompt Engineering Lab (`/ReactDebug/PromptLab`)

**Purpose**: Fine-tune and test ReAct prompts

```
┌─────────────────────────────────────────────────────────────────┐
│ Prompt Engineering Lab                    [Save] [Load] [Reset]  │
├─────────────────────────────────────────────────────────────────┤
│ System Prompt:                                                   │
│ ┌─────────────────────────────────────────────────────────────┐│
│ │ You are a ReAct agent. Follow this format:                  ││
│ │ Thought: reason about what to do                            ││
│ │ Action: the action to take                                  ││
│ │ Observation: result of the action                           ││
│ └─────────────────────────────────────────────────────────────┘│
│                                                                 │
│ Variables:                                                      │
│ {tools} → Available tools list                                  │
│ {history} → Conversation history                                │
│ {query} → User query                                           │
│                                                                 │
│ Test Results:                                                   │
│ ┌─────────────────────────────────────────────────────────────┐│
│ │ ✓ Tool Selection Accuracy: 94%                             ││
│ │ ✓ Average Response Time: 1.2s                              ││
│ │ ⚠ Redundant Thoughts: 12%                                  ││
│ └─────────────────────────────────────────────────────────────┘│
└─────────────────────────────────────────────────────────────────┘
```

### 4. Metrics Dashboard (`/ReactDebug/Metrics`)

**Purpose**: Performance and quality metrics

```
┌─────────────────────────────────────────────────────────────────┐
│ ReAct Metrics Dashboard                        [Export] [Refresh]│
├─────────────────────────────────────────────────────────────────┤
│ ┌─────────────────┐ ┌─────────────────┐ ┌─────────────────┐   │
│ │ Avg Think Time  │ │ Tool Success    │ │ Token Usage     │   │
│ │    342ms        │ │    87%          │ │   523/query     │   │
│ └─────────────────┘ └─────────────────┘ └─────────────────┘   │
│                                                                 │
│ Tool Usage Distribution:          Reasoning Patterns:           │
│ ┌─────────────────────┐          ┌─────────────────────┐      │
│ │ Web Search    45%  │          │ Sequential    62%   │      │
│ │ Calculator    23%  │          │ Parallel      18%   │      │
│ │ Code Analyzer 18%  │          │ Retry         15%   │      │
│ │ Other         14%  │          │ Fallback       5%   │      │
│ └─────────────────────┘          └─────────────────────┘      │
└─────────────────────────────────────────────────────────────────┘
```

### 5. Debug Console (`/ReactDebug/Console`)

**Purpose**: Detailed logging and debugging information

```
┌─────────────────────────────────────────────────────────────────┐
│ Debug Console                      [Clear] [Export] [Filter ▼]  │
├─────────────────────────────────────────────────────────────────┤
│ [12:34:56.123] [INFO] ReAct execution started                   │
│ [12:34:56.234] [DEBUG] Thought generated: "I need to search..." │
│ [12:34:56.345] [DEBUG] Tool confidence scores:                  │
│                        - web_search: 0.95                       │
│                        - calculator: 0.12                       │
│                        - code_analyzer: 0.03                    │
│ [12:34:56.456] [INFO] Selected tool: web_search                │
│ [12:34:56.567] [DEBUG] Tool parameters: {"query": "Prague..."}  │
│ [12:34:57.678] [INFO] Tool execution completed (1,111ms)       │
│ [12:34:57.789] [DEBUG] Observation: "Prague: 18°C, partly..."   │
│ [12:34:57.890] [DEBUG] Generating next thought...              │
└─────────────────────────────────────────────────────────────────┘
```

## Technical Implementation

### New Controllers

#### 1. `ReactDebugController.cs`
```csharp
[Route("ReactDebug")]
public class ReactDebugController : Controller
{
    // View actions
    public IActionResult Playground() => View();
    public IActionResult Visualizer() => View();
    public IActionResult PromptLab() => View();
    public IActionResult Metrics() => View();
    public IActionResult Console() => View();
}
```

#### 2. `ReactDebugApiController.cs`
```csharp
[Route("api/react-debug")]
[ApiController]
public class ReactDebugApiController : BaseApiController
{
    [HttpPost("execute")]
    public async Task<IActionResult> ExecuteDebug([FromBody] ReactDebugRequestDto request);
    
    [HttpGet("metrics")]
    public async Task<IActionResult> GetMetrics([FromQuery] MetricsFilterDto filter);
    
    [HttpPost("scenarios")]
    public async Task<IActionResult> SaveScenario([FromBody] SaveScenarioDto scenario);
    
    [HttpGet("scenarios")]
    public async Task<IActionResult> GetScenarios();
}
```

### SignalR Events

#### `ReactDebugHub.cs`
```csharp
public class ReactDebugHub : Hub
{
    // Client → Server
    public async Task StartDebugSession(string sessionId);
    public async Task StepExecution(string sessionId);
    public async Task PauseExecution(string sessionId);
    
    // Server → Client events
    public async Task SendThought(ThoughtEventDto thought);
    public async Task SendToolSelection(ToolSelectionEventDto selection);
    public async Task SendObservation(ObservationEventDto observation);
    public async Task SendMetricsUpdate(MetricsUpdateDto metrics);
}
```

### View Models and DTOs

#### Request/Response DTOs
```csharp
public class ReactDebugRequestDto
{
    public string Query { get; set; }
    public string Model { get; set; }
    public int MaxSteps { get; set; }
    public List<string> EnabledTools { get; set; }
    public bool StepByStep { get; set; }
}

public class ThoughtEventDto
{
    public int Step { get; set; }
    public string Thought { get; set; }
    public DateTime Timestamp { get; set; }
    public long ElapsedMs { get; set; }
    public Dictionary<string, double> ToolConfidences { get; set; }
}

public class ToolSelectionEventDto
{
    public string ToolId { get; set; }
    public double Confidence { get; set; }
    public Dictionary<string, object> Parameters { get; set; }
    public string Reasoning { get; set; }
}
```

#### View Models
```csharp
public class ReactPlaygroundViewModel
{
    public List<string> AvailableModels { get; set; }
    public List<ToolDefinitionDto> AvailableTools { get; set; }
    public List<SavedScenarioDto> SavedScenarios { get; set; }
}

public class ExecutionVisualizerViewModel
{
    public string SessionId { get; set; }
    public ReactExecutionTreeDto ExecutionTree { get; set; }
    public List<TimelineEventDto> Timeline { get; set; }
}
```

### JavaScript/Frontend Requirements

#### 1. SignalR Connection Manager
```javascript
// react-debug-connection.js
class ReactDebugConnection {
    constructor() {
        this.connection = new signalR.HubConnectionBuilder()
            .withUrl("/hubs/react-debug")
            .build();
    }
    
    async startSession(sessionId) {
        await this.connection.invoke("StartDebugSession", sessionId);
    }
    
    onThought(callback) {
        this.connection.on("SendThought", callback);
    }
    
    onToolSelection(callback) {
        this.connection.on("SendToolSelection", callback);
    }
}
```

#### 2. Visualization Libraries
- **D3.js** for tree visualization
- **Chart.js** for metrics charts
- **Vis.js** for timeline visualization
- **Prism.js** for syntax highlighting
- **Monaco Editor** for prompt editing

#### 3. State Management
```javascript
// react-debug-state.js
class ReactDebugState {
    constructor() {
        this.currentSession = null;
        this.executionSteps = [];
        this.metrics = {};
        this.isPaused = false;
    }
    
    addStep(step) {
        this.executionSteps.push(step);
        this.updateVisualization();
    }
}
```

## Key Features

### 1. Step-by-Step Execution Control

- **Play/Pause/Step** buttons for controlling execution
- **Breakpoints** on specific thought patterns or tool calls
- **Speed control** (slow, normal, fast) for observation
- **Conditional pausing** based on confidence thresholds

### 2. Thought Process Visualization

- **Thought bubbles** showing reasoning text
- **Confidence meters** for each potential action
- **Decision trees** showing alternative paths considered
- **Highlighting** of key decision factors

### 3. Tool Selection Reasoning Display

- **Confidence scores** for each available tool
- **Parameter extraction** visualization
- **Matching patterns** that triggered tool selection
- **Alternative tools** that were considered

### 4. Performance Metrics

- **Response time** breakdown by component
- **Token usage** per thought/action
- **Tool success rates**
- **Reasoning efficiency** metrics
- **Cost estimation** based on model usage

### 5. Export/Import Test Scenarios

```json
{
  "scenario": {
    "name": "Weather Conversion Test",
    "description": "Test weather lookup and unit conversion",
    "query": "What is the weather in Prague?",
    "expectedTools": ["web_search", "calculator"],
    "configuration": {
      "model": "llama3.2",
      "maxSteps": 5,
      "enabledTools": ["web_search", "calculator"]
    },
    "assertions": [
      {
        "type": "tool_used",
        "tool": "web_search",
        "minConfidence": 0.8
      }
    ]
  }
}
```

## Integration Points

### 1. ReActAgent Integration

```csharp
public class DebugReActAgent : ReActAgent
{
    private readonly IDebugEventEmitter _debugEmitter;
    
    protected override async Task<string> GenerateThoughtAsync(
        string query, 
        List<ReActStep> history)
    {
        var thought = await base.GenerateThoughtAsync(query, history);
        
        // Emit debug event
        await _debugEmitter.EmitThoughtAsync(new ThoughtDebugInfo
        {
            Thought = thought,
            History = history,
            Timestamp = DateTime.UtcNow
        });
        
        return thought;
    }
}
```

### 2. Debug Data Capture

```csharp
public interface IReActDebugCapture
{
    Task CaptureThoughtAsync(string thought, Dictionary<string, double> toolConfidences);
    Task CaptureToolSelectionAsync(string toolId, Dictionary<string, object> parameters);
    Task CaptureObservationAsync(string observation, TimeSpan executionTime);
    Task CaptureMetricsAsync(ReActMetrics metrics);
}
```

### 3. Storage for Test Scenarios

```csharp
public class TestScenario : BaseEntity
{
    public string Name { get; set; }
    public string Description { get; set; }
    public string Query { get; set; }
    public string ConfigurationJson { get; set; }
    public string AssertionsJson { get; set; }
    public string ResultsJson { get; set; }
    public DateTime LastRun { get; set; }
}
```

### Database Schema
```sql
CREATE TABLE TestScenarios (
    Id INT PRIMARY KEY IDENTITY,
    Name NVARCHAR(200) NOT NULL,
    Description NVARCHAR(MAX),
    Query NVARCHAR(MAX) NOT NULL,
    ConfigurationJson NVARCHAR(MAX),
    AssertionsJson NVARCHAR(MAX),
    ResultsJson NVARCHAR(MAX),
    LastRun DATETIME2,
    CreatedAt DATETIME2 NOT NULL,
    UpdatedAt DATETIME2
);

CREATE TABLE DebugSessions (
    Id INT PRIMARY KEY IDENTITY,
    SessionId NVARCHAR(50) NOT NULL,
    Query NVARCHAR(MAX),
    Model NVARCHAR(100),
    ExecutionDataJson NVARCHAR(MAX),
    MetricsJson NVARCHAR(MAX),
    CreatedAt DATETIME2 NOT NULL
);
```

## Implementation Phases

### Phase 1: Core Infrastructure (Week 1)
- Controllers and API endpoints
- SignalR hub implementation
- Basic DTOs and view models
- Database schema

### Phase 2: Playground & Execution (Week 2)
- ReAct Playground UI
- Step-by-step execution
- Basic visualization
- Debug event capture

### Phase 3: Visualization (Week 3)
- Tree view implementation
- Timeline view
- Real-time updates
- Interactive controls

### Phase 4: Advanced Features (Week 4)
- Prompt Engineering Lab
- Metrics Dashboard
- Test scenario management
- Export/import functionality

### Phase 5: Polish & Integration (Week 5)
- Performance optimization
- UI polish
- Documentation
- Integration testing

## Success Metrics

1. **Developer Productivity**: 50% reduction in ReAct debugging time
2. **Issue Resolution**: 80% of ReAct issues identifiable through UI
3. **Test Coverage**: 100+ saved test scenarios
4. **Performance**: <100ms UI response time
5. **Adoption**: Used by all developers working on ReAct features