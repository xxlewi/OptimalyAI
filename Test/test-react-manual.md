# Manual ReAct Pattern Testing Guide

## Test Steps

1. **Open Chat Interface**
   - Navigate to: https://localhost:5005/Chat
   - Create a new conversation

2. **Test Messages to Send**
   
   ### Simple Search (Should trigger ReAct)
   ```
   search what is OptimalyAI
   ```
   
   ### Complex Query (Should auto-enable ReAct)
   ```
   Compare the weather in Prague and Brno and tell me which city is better for outdoor activities today
   ```
   
   ### Tool Chain Test
   ```
   First search for information about ASP.NET Core, then summarize the key features
   ```

3. **Monitor Logs**
   
   Open a terminal and run:
   ```bash
   cd /Users/lewi/Documents/Vyvoj/OptimalyAI
   tail -f logs/optimaly-ai-$(date +%Y%m%d).log | grep -E "(ReAct|react|enable_react|GetReActMode|ExecuteWithReActAsync|Thought|Action|Observation)"
   ```

4. **Expected Log Entries**
   
   You should see:
   - `ReAct mode from request metadata: True`
   - `ReAct mode from configuration: True`
   - `Final ReAct mode decision: True`
   - `Executing with ReAct pattern for message:`
   - `ReAct execution completed:`

## Verification Checklist

- [ ] Chat interface loads successfully
- [ ] New conversation can be created
- [ ] Messages are sent without errors
- [ ] ReAct logs appear in console
- [ ] Tools are executed (web_search)
- [ ] Response includes tool results

## Troubleshooting

If ReAct is not working:

1. Check configuration in `appsettings.Development.json`:
   ```json
   "ReActSettings": {
     "Enabled": true
   }
   ```

2. Verify ChatController has metadata:
   ```csharp
   Metadata = new Dictionary<string, object>
   {
       ["enable_react"] = true
   }
   ```

3. Check logs for errors:
   ```bash
   tail -100 logs/optimaly-ai-$(date +%Y%m%d).log | grep -i error
   ```