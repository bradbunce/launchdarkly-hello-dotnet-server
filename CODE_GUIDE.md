# Code Guide for Beginners

This document explains how the LaunchDarkly AI Config Demo works, section by section.

## Overview

This application is a web server that:
1. Connects to LaunchDarkly to get feature flags and AI configurations
2. Hosts a web UI with a chatbot
3. Routes chat messages to different AI providers (OpenRouter, Cohere, Mistral)
4. Tracks metrics (latency, tokens, user feedback) back to LaunchDarkly
5. Updates the UI in real-time when flags change

## Key Concepts

### Server-Sent Events (SSE)
SSE allows the server to push updates to the browser in real-time without the browser constantly asking for updates. We use three SSE streams:
- `/stream` - Console output
- `/ai-config-stream` - AI model changes
- `/console-visibility-stream` - Show/hide console flag

### LaunchDarkly Context
A "context" identifies who is using your app. We use:
- **Kind**: `ld-sdk` (custom context type)
- **Key**: `dot-net-server` (identifies this server)
- **Attributes**: `sdkVersion` (tracks which SDK version)

This lets you target specific servers or SDK versions with your flags.

### AI Config Tracker
When you get an AI Config from LaunchDarkly, it returns a "tracker" object. This tracker:
- Holds the configuration (which model to use)
- Tracks metrics (duration, tokens, success/failure)
- Associates feedback with the specific config variation

## Code Structure

### 1. AiResponse Class
```csharp
class AiResponse
{
    public string Text { get; set; }          // The AI's response
    public int InputTokens { get; set; }      // Tokens in user's message
    public int OutputTokens { get; set; }     // Tokens in AI's response
    public int TotalTokens { get; set; }      // Total tokens used
    public bool IsError { get; set; }         // Whether an error occurred
}
```

This simple class holds everything we need from an AI provider's response.

### 2. Static Fields (Shared State)

```csharp
private static ConcurrentQueue<string> logMessages
```
- Stores all log messages
- Thread-safe queue (multiple requests can log simultaneously)
- New SSE connections get historical messages

```csharp
private static ConcurrentBag<HttpResponse> activeConnections
```
- Stores all active SSE connections for console output
- When we log something, we send it to all these connections
- Thread-safe collection

```csharp
private static ConcurrentDictionary<int, ILdAiConfigTracker> trackerCache
```
- Maps message IDs to their AI Config trackers
- When user clicks thumbs up/down, we look up the tracker by message ID
- This ensures feedback is associated with the correct AI Config variation

```csharp
private static readonly HttpClient httpClient
```
- Reused for all HTTP requests to AI providers
- Reusing improves performance (connection pooling)

### 3. Log Method

```csharp
public static void Log(string message)
{
    lock (logLock)  // Prevent multiple threads from logging simultaneously
    {
        Console.Write(message);           // Write to server console
        logMessages.Enqueue(message);     // Store for new SSE connections
        
        // Broadcast to all connected browsers
        foreach (var response in activeConnections)
        {
            response.WriteAsync($"data: {escapedMessage}\n\n");
        }
    }
}
```

This method does three things:
1. Logs to the server console
2. Stores the message for later
3. Broadcasts to all connected web clients in real-time

### 4. AI Provider Methods

Each provider (OpenRouter, Cohere, Mistral) has its own method:

```csharp
public static async Task<AiResponse> CallOpenRouter(...)
{
    // 1. Build the request
    var requestBody = new { model = modelName, messages = [...] };
    
    // 2. Send HTTP POST to the provider's API
    var response = await httpClient.SendAsync(request);
    
    // 3. Parse the response
    var jsonResponse = JsonSerializer.Deserialize<JsonElement>(responseBody);
    
    // 4. Extract text and token usage
    return new AiResponse
    {
        Text = jsonResponse.GetProperty("choices")[0]...GetString(),
        InputTokens = usage.GetProperty("prompt_tokens").GetInt32(),
        ...
    };
}
```

**Why separate methods?**
Each AI provider has a slightly different API format:
- OpenRouter & Mistral use OpenAI-compatible format
- Cohere has its own format with different field names

### 5. Main Method - Initialization

```csharp
static void Main(string[] args)
{
    // 1. Get SDK key from environment variable
    string SdkKey = Environment.GetEnvironmentVariable("LAUNCHDARKLY_SDK_KEY");
    
    // 2. Initialize LaunchDarkly clients
    var client = new LdClient(ldConfig);              // For feature flags
    var aiClient = new LdAiClient(...);               // For AI Configs
    
    // 3. Create a context (identifies this server)
    var context = Context.Builder(ContextKind.Of("ld-sdk"), "dot-net-server")
        .Set("sdkVersion", sdkVersion)
        .Build();
    
    // 4. Evaluate a feature flag
    var flagValue = client.BoolVariation("sample-feature", context, false);
    
    // 5. Get AI Config
    var aiConfigTracker = aiClient.Config("sample-ai-config", context, ...);
    
    // 6. Set up flag change listeners
    client.FlagTracker.FlagChanged += ...
    
    // 7. Start web server
    var host = WebHost.CreateDefaultBuilder(args)...
}
```

### 6. Flag Change Listeners

```csharp
client.FlagTracker.FlagChanged += (sender, changeArgs) => {
    if (changeArgs.Key == "sample-ai-config")
    {
        // Re-evaluate the AI config
        var updatedConfig = aiClient.Config("sample-ai-config", context, ...);
        
        // Broadcast to all connected browsers
        foreach (var response in aiConfigConnections)
        {
            response.WriteAsync($"data: {data}\n\n");
        }
    }
};
```

**How it works:**
1. LaunchDarkly SDK maintains a streaming connection to LaunchDarkly servers
2. When you change a flag in the dashboard, LaunchDarkly pushes the update
3. The SDK fires the `FlagChanged` event
4. We re-evaluate the config and broadcast to all connected browsers
5. The UI updates instantly without page refresh

### 7. Web Server Endpoints

#### `/console-visibility-stream` (SSE)
- Sends the current value of the `show-console` flag
- Keeps connection open for real-time updates
- When flag changes, broadcasts new value to all connected clients

#### `/ai-config-stream` (SSE)
- Sends the current AI model name and enabled status
- Updates when AI Config changes in LaunchDarkly
- Controls the model badge in the UI

#### `/feedback` (POST)
- Receives thumbs up/down from user
- Looks up the AI Config tracker by message ID
- Calls `tracker.TrackFeedback(Feedback.Positive/Negative)`
- Sends feedback metric to LaunchDarkly

#### `/chat` (POST)
- Receives user's message
- Gets current AI Config from LaunchDarkly
- Routes to appropriate provider based on model name:
  - Contains "command" or "cohere" → Cohere
  - Contains "mistral" → Mistral
  - Everything else → OpenRouter
- Tracks metrics:
  - `TrackDuration(latencyMs)` - How long it took
  - `TrackSuccess()` or `TrackError()` - Whether it worked
  - `TrackTokens(usage)` - Token usage
- Caches the tracker with a message ID
- Returns response to user

#### `/stream` (SSE)
- Sends all historical log messages
- Keeps connection open
- Broadcasts new log messages in real-time

## Request Flow Example

Let's trace what happens when a user sends a chat message:

1. **User types "Hello" and clicks Send**
   - Browser sends POST to `/chat` with `{ message: "Hello" }`

2. **Server receives request**
   ```csharp
   var userMessage = request.GetProperty("message").GetString();
   Log($"[Chat] User: {userMessage}\n");  // Logs to console and UI
   ```

3. **Get AI Config from LaunchDarkly**
   ```csharp
   var aiConfigTracker = aiClient.Config("sample-ai-config", context, ...);
   var modelName = aiConfigTracker.Config.Model?.Name;  // e.g., "gpt-4"
   ```

4. **Route to appropriate provider**
   ```csharp
   if (modelName.Contains("mistral"))
       aiResponse = await CallMistral(userMessage, modelName, apiKey);
   else
       aiResponse = await CallOpenRouter(userMessage, modelName, apiKey);
   ```

5. **Track metrics**
   ```csharp
   var latencyMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
   aiConfigTracker.TrackDuration(latencyMs);
   aiConfigTracker.TrackSuccess();
   aiConfigTracker.TrackTokens(usage);
   ```

6. **Cache tracker for feedback**
   ```csharp
   var messageId = Interlocked.Increment(ref messageCounter);  // Thread-safe counter
   trackerCache[messageId] = aiConfigTracker;
   ```

7. **Return response**
   ```csharp
   return { 
       response: "Hello! How can I help?",
       model: "gpt-4",
       enabled: true,
       messageId: 42
   };
   ```

8. **User clicks thumbs up**
   - Browser sends POST to `/feedback` with `{ messageId: 42, positive: true }`
   - Server looks up tracker: `trackerCache.TryRemove(42, out var tracker)`
   - Tracks feedback: `tracker.TrackFeedback(Feedback.Positive)`
   - LaunchDarkly receives the feedback metric

## Metrics in LaunchDarkly

All these metrics appear in the LaunchDarkly dashboard under your AI Config's Monitoring tab:

- **Duration**: Average response time in milliseconds
- **Tokens**: Total tokens used (helps estimate costs)
- **Success Rate**: Percentage of successful generations
- **Feedback**: Ratio of thumbs up to thumbs down

You can compare these metrics across different AI Config variations to see which model performs best.

## Thread Safety

This app handles multiple concurrent requests. Thread-safe patterns used:

1. **ConcurrentQueue/Bag/Dictionary** - Thread-safe collections
2. **lock (logLock)** - Prevents simultaneous logging
3. **Interlocked.Increment** - Thread-safe counter increment
4. **HttpClient reuse** - Single shared instance (thread-safe)

## Environment Variables

The app reads configuration from environment variables:

```bash
LAUNCHDARKLY_SDK_KEY=sdk-xxx        # Required: Your LaunchDarkly SDK key
LAUNCHDARKLY_FLAG_KEY=sample-feature # Optional: Which flag to evaluate
OPENROUTER_API_KEY=sk-or-xxx        # Optional: OpenRouter API key
COHERE_API_KEY=xxx                   # Optional: Cohere API key
MISTRAL_API_KEY=xxx                  # Optional: Mistral API key
```

These are loaded via `Environment.GetEnvironmentVariable("KEY_NAME")`.

## Summary

This app demonstrates:
- ✅ Feature flag evaluation
- ✅ AI Config management
- ✅ Real-time updates via SSE
- ✅ Multi-provider AI routing
- ✅ Comprehensive metrics tracking
- ✅ User feedback collection
- ✅ Thread-safe concurrent request handling

The key insight: **LaunchDarkly acts as a control plane for your AI application**, letting you change models, providers, and configurations without deploying new code.
