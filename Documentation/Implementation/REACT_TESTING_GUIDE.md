# 🧪 ReAct Pattern Testing Guide

## Rychlý start

### 1. Spuštění aplikace
```bash
cd /Users/lewi/Documents/Vyvoj/OptimalyAI
python run-dev.py
```

### 2. Testování pomocí skriptů

#### Jednoduchý test
```bash
./test-react-simple.sh
```

#### Komplexní test suite
```bash
./test-react.sh
```

## Manuální testování

### Test 1: Základní ReAct s vyhledáváním
```bash
curl -k -X POST "https://localhost:5005/api/orchestrators/conversation/execute" \
  -H "Content-Type: application/json" \
  -d '{
    "message": "Jaké je počasí v Praze?",
    "modelId": "llama3.2",
    "enableTools": true,
    "metadata": {
      "enable_react": true
    }
  }' | jq '.'
```

**Očekávaný výsledek:**
- Response obsahuje `metadata.react_mode: true`
- `metadata.react_steps` > 0
- `metadata.react_thoughts` > 0
- Tool web_search byl použit

### Test 2: Multi-step reasoning
```bash
curl -k -X POST "https://localhost:5005/api/orchestrators/conversation/execute" \
  -H "Content-Type: application/json" \
  -d '{
    "message": "Porovnej počasí v Praze a New Yorku",
    "modelId": "llama3.2",
    "enableTools": true,
    "metadata": {
      "enable_react": true
    }
  }' | jq '.'
```

**Očekávaný výsledek:**
- Minimálně 2 tool cally (jeden pro každé město)
- `metadata.react_actions` >= 2
- Finální odpověď obsahuje srovnání obou měst

### Test 3: Auto-aktivace ReAct
```bash
# Komplexní dotaz bez explicit enable_react
curl -k -X POST "https://localhost:5005/api/orchestrators/conversation/execute" \
  -H "Content-Type: application/json" \
  -d '{
    "message": "Najdi informace o Pythonu a vysvětli proč je populární pro AI",
    "modelId": "llama3.2",
    "enableTools": true
  }' | jq '.'
```

**Očekávaný výsledek:**
- ReAct se automaticky aktivuje (komplexní dotaz)
- `metadata.react_mode: true`

### Test 4: Srovnání s/bez ReAct

#### Bez ReAct:
```bash
curl -k -X POST "https://localhost:5005/api/orchestrators/conversation/execute" \
  -H "Content-Type: application/json" \
  -d '{
    "message": "Co je hlavní město Francie?",
    "modelId": "llama3.2",
    "enableTools": true,
    "metadata": {
      "enable_react": false
    }
  }' | jq '.'
```

#### S ReAct:
```bash
curl -k -X POST "https://localhost:5005/api/orchestrators/conversation/execute" \
  -H "Content-Type: application/json" \
  -d '{
    "message": "Co je hlavní město Francie?",
    "modelId": "llama3.2",
    "enableTools": true,
    "metadata": {
      "enable_react": true
    }
  }' | jq '.'
```

## UI Testování

1. Otevřete https://localhost:5005/Chat
2. Vytvořte novou konverzaci
3. Zadejte dotaz: "Vyhledej informace o počasí v Praze"
4. Sledujte:
   - Tool usage indicators
   - Progress během ReAct execution
   - Finální odpověď

## Kontrola logů

```bash
# Sledování logů v reálném čase
tail -f logs/optimaly-ai-*.log | grep -i react
```

## Očekávané ReAct chování

### 1. Reasoning Loop
```
User: "Jaké je počasí v Praze?"
→ Thought: Potřebuji vyhledat aktuální počasí v Praze
→ Action: web_search
→ Action Input: {"query": "počasí Praha aktuální"}
→ Observation: V Praze je 18°C, polojasno...
→ Thought: Mám potřebné informace
→ Final Answer: V Praze je aktuálně 18°C...
```

### 2. Multi-tool usage
```
User: "Porovnej počasí v Praze a Londýně"
→ Thought: Potřebuji data pro obě města
→ Action: web_search (Praha)
→ Observation: Praha data...
→ Thought: Teď potřebuji Londýn
→ Action: web_search (Londýn)
→ Observation: Londýn data...
→ Final Answer: Srovnání...
```

## Troubleshooting

### ReAct se neaktivuje
1. Zkontrolujte `appsettings.Development.json` - má být `"Enabled": true`
2. Přidejte explicit `"enable_react": true` do metadata
3. Zkontrolujte, že `enableTools: true`

### Chybí metadata v response
- Zkontrolujte, že používáte správný endpoint: `/api/orchestrators/conversation/execute`
- Ne `/api/chat` nebo jiný endpoint

### Tool se nevykoná
1. Zkontrolujte, že tool je registrovaný: https://localhost:5005/Tools
2. Zkontrolujte logy pro chyby
3. Ověřte, že Ollama běží na portu 11434

## Performance metriky

Typické časy pro ReAct execution:
- Simple query (1 tool): 2-5 sekund
- Complex query (2-3 tools): 5-10 sekund
- Max iterations (5): do 15 sekund

## Debug mode

Pro detailní debugging přidejte do request:
```json
{
  "metadata": {
    "enable_react": true,
    "debug_mode": true,
    "log_thoughts": true
  }
}
```

## Automatizované testy

### Unit testy
```bash
# Spustit unit testy
dotnet test Tests/Unit/Orchestration/ReAct/ConversationReActAgentTests.cs
```

### Integrační testy
```bash
# Spustit integrační testy (vyžaduje běžící aplikaci)
./test-react-integration.sh
```

### Všechny testy najednou
```bash
# 1. Spustit aplikaci
python run-dev.py

# 2. V novém terminálu spustit testy
./test-react.sh && ./test-react-integration.sh
```

## Struktura testů

```
Tests/
├── Unit/
│   └── Orchestration/
│       └── ReAct/
│           └── ConversationReActAgentTests.cs  # Unit testy
├── Integration/
│   ├── TestWebApplicationFactory.cs           # Test factory
│   └── Orchestration/
│       └── ReAct/
│           └── ConversationOrchestratorReActTests.cs  # Integrační testy
```