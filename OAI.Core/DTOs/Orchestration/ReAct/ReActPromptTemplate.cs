namespace OAI.Core.DTOs.Orchestration.ReAct;

public class ReActPromptTemplate
{
    public string SystemPrompt { get; set; } = string.Empty;
    public string UserPromptTemplate { get; set; } = string.Empty;
    public string ToolDescriptionTemplate { get; set; } = string.Empty;
    public string ThoughtTemplate { get; set; } = string.Empty;
    public string ActionTemplate { get; set; } = string.Empty;
    public string ObservationTemplate { get; set; } = string.Empty;
    public string FinalAnswerTemplate { get; set; } = string.Empty;
    public List<string> FewShotExamples { get; set; } = new();
    public string Language { get; set; } = "cs";
    public Dictionary<string, string> LanguageSpecificPhrases { get; set; } = new();
    
    public static ReActPromptTemplate CreateCzechTemplate()
    {
        return new ReActPromptTemplate
        {
            Language = "cs",
            SystemPrompt = @"Jsi inteligentní asistent, který může používat různé nástroje k zodpovězení otázek. 
Postupuj podle ReAct patternu: Thought (myšlenka) → Action (akce) → Observation (pozorování).

Formát odpovědi:
Thought: Zde napiš svou myšlenku o tom, co potřebuješ udělat
Action: název_nástroje
Action Input: {""parametr"": ""hodnota""}
Observation: Výsledek použití nástroje

Opakuj tento cyklus dokud nemáš dostatek informací pro finální odpověď.
Když máš všechny potřebné informace, ukonči pomocí:
Thought: Mám všechny potřebné informace
Final Answer: Zde napiš svou finální odpověď",

            UserPromptTemplate = @"Otázka: {input}

Dostupné nástroje:
{tools}

Začni svojí analýzou:",

            ToolDescriptionTemplate = "- {name}: {description}",
            
            ThoughtTemplate = "Thought: {content}",
            ActionTemplate = "Action: {tool_name}\nAction Input: {parameters}",
            ObservationTemplate = "Observation: {content}",
            FinalAnswerTemplate = "Final Answer: {answer}",
            
            FewShotExamples = new List<string>
            {
                @"Otázka: Jaké je počasí v Praze?

Thought: Potřebujem zjistit aktuální počasí v Praze. Použiju vyhledávací nástroj.
Action: web_search
Action Input: {""query"": ""počasí Praha aktuální""}
Observation: V Praze je aktuálně 18°C, polojasno, vlhkost 65%

Thought: Mám aktuální informace o počasí v Praze.
Final Answer: V Praze je aktuálně 18°C s polojasnem a vlhkostí 65%."
            },
            
            LanguageSpecificPhrases = new Dictionary<string, string>
            {
                { "thought", "Thought" },
                { "action", "Action" },
                { "action_input", "Action Input" },
                { "observation", "Observation" },
                { "final_answer", "Final Answer" },
                { "need_more_info", "Potřebuji více informací" },
                { "have_enough_info", "Mám dostatek informací" },
                { "error_occurred", "Došlo k chybě" }
            }
        };
    }
    
    public static ReActPromptTemplate CreateEnglishTemplate()
    {
        return new ReActPromptTemplate
        {
            Language = "en",
            SystemPrompt = @"You are an intelligent assistant that can use various tools to answer questions.
Follow the ReAct pattern: Thought → Action → Observation.

Response format:
Thought: Write your thinking about what you need to do
Action: tool_name
Action Input: {""parameter"": ""value""}
Observation: Result from using the tool

Repeat this cycle until you have enough information for a final answer.
When you have all necessary information, finish with:
Thought: I have all the necessary information
Final Answer: Write your final answer here",

            UserPromptTemplate = @"Question: {input}

Available tools:
{tools}

Begin your analysis:",

            ToolDescriptionTemplate = "- {name}: {description}",
            
            ThoughtTemplate = "Thought: {content}",
            ActionTemplate = "Action: {tool_name}\nAction Input: {parameters}",
            ObservationTemplate = "Observation: {content}",
            FinalAnswerTemplate = "Final Answer: {answer}",
            
            FewShotExamples = new List<string>
            {
                @"Question: What is the weather in Prague?

Thought: I need to find the current weather in Prague. I'll use the search tool.
Action: web_search
Action Input: {""query"": ""weather Prague current""}
Observation: Prague currently has 18°C, partly cloudy, humidity 65%

Thought: I have current weather information for Prague.
Final Answer: Prague currently has 18°C with partly cloudy skies and 65% humidity."
            },
            
            LanguageSpecificPhrases = new Dictionary<string, string>
            {
                { "thought", "Thought" },
                { "action", "Action" },
                { "action_input", "Action Input" },
                { "observation", "Observation" },
                { "final_answer", "Final Answer" },
                { "need_more_info", "I need more information" },
                { "have_enough_info", "I have enough information" },
                { "error_occurred", "An error occurred" }
            }
        };
    }
    
    public string BuildPrompt(string input, string toolDescriptions, string scratchpad = "")
    {
        var prompt = UserPromptTemplate
            .Replace("{input}", input)
            .Replace("{tools}", toolDescriptions);
        
        if (!string.IsNullOrEmpty(scratchpad))
        {
            prompt += "\n\n" + scratchpad + "\n";
        }
        
        return prompt;
    }
}