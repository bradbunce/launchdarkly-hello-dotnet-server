# Code Guide for Beginners

This document explains how the LaunchDarkly AI Config Demo works, section by section.

## Overview

This application is a web server that:
1. Connects to LaunchDarkly to get feature flags and AI configurations
2. Hosts a web UI with a chatbot
3. Routes chat messages to different AI providers (OpenRouter, Cohere, Mistral)
4. Tracks metrics (latency, tokens, user feedback) back to LaunchDarkly
5. Updates the UI in real-time when flags change
6. Exports comprehensive observability data via OpenTelemetry (traces, metrics, logs)

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

Each provider (OpenRouter, Cohere, Mistral) has its own method wrapped in an observability span:

```csharp
public static async Task<AiResponse> CallOpenRouter(...)
{
    // Start an activity span for distributed tracing
    using (var activity = Observe.StartActivity("openrouter.chat.completions", ActivityKind.Client,
        new Dictionary<string, object> 
        { 
            { "ai.provider", "openrouter" },
            { "ai.model", modelName },
            { "message.length", userMessage?.Length ?? 0 }
        }))
    {
        try
        {
            // 1. Build the request
            var requestBody = new { model = modelName, messages = [...] };
            
            // 2. Send HTTP POST to the provider's API
            var response = await httpClient.SendAsync(request);
            
            // 3. Parse the response
            var jsonResponse = JsonSerializer.Deserialize<JsonElement>(responseBody);
            
            // 4. Extract text and token usage
            var result = new AiResponse { ... };
            
            // 5. Add success tags to the span
            activity?.SetTag("ai.response.tokens", result.TotalTokens);
            activity?.SetTag("http.status_code", 200);
            
            return result;
        }
        catch (Exception ex)
        {
            // 6. Add error tags to the span
            activity?.SetTag("error", true);
            activity?.SetTag("error.message", ex.Message);
            throw;
        }
    }
}
```

**Why separate methods?**
Each AI provider has a slightly different API format:
- OpenRouter & Mistral use OpenAI-compatible format
- Cohere has its own format with different field names

**Observability features:**
- Each provider call is wrapped in a span for distributed tracing
- Spans include attributes (provider, model, message length)
- Success/error information is tagged on the span
- Token counts are recorded as span attributes

### 5. Main Method - Initialization

```csharp
static void Main(string[] args)
{
    // 1. Get SDK key and service metadata from environment variables
    string SdkKey = Environment.GetEnvironmentVariable("LAUNCHDARKLY_SDK_KEY");
    string serviceName = Environment.GetEnvironmentVariable("APPLICATION_ID") ?? "hello-dotnet-server";
    string serviceVersion = Environment.GetEnvironmentVariable("APPLICATION_VERSION") ?? "1.0.0";
    
    // 2. Create WebApplication builder (modern ASP.NET Core)
    var builder = WebApplication.CreateBuilder(args);
    
    // 3. Initialize LaunchDarkly with Observability plugin
    var ldConfig = Configuration.Builder(SdkKey)
        .StartWaitTime(TimeSpan.FromSeconds(5))
        .Plugins(new PluginConfigurationBuilder()
            .Add(ObservabilityPlugin.Builder(builder.Services)
                .WithServiceName(serviceName)
                .WithServiceVersion(serviceVersion)
                .Build()
            )
        ).Build();
    
    // 4. Create LaunchDarkly clients
    var client = new LdClient(ldConfig);              // For feature flags
    var aiClient = new LdAiClient(...);               // For AI Configs
    
    // 5. Create a context (identifies this server)
    var context = Context.Builder(ContextKind.Of("ld-sdk"), "dot-net-server")
        .Set("sdkVersion", sdkVersion)
        .Build();
    
    // 6. Evaluate a feature flag
    var flagValue = client.BoolVariation("sample-feature", context, false);
    
    // 7. Get AI Config
    var aiConfigTracker = aiClient.Config("sample-ai-config", context, ...);
    
    // 8. Set up flag change listeners
    client.FlagTracker.FlagChanged += ...
    
    // 9. Build and start web server
    var app = builder.Build();
    app.Run();
}
```

**Key changes for observability:**
- Uses `WebApplication.CreateBuilder` (modern ASP.NET Core pattern)
- Adds `ObservabilityPlugin` with service name and version
- Plugin automatically exports OpenTelemetry data (traces, metrics, logs)

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
- **Starts observability span** for the entire request
- **Records request counter** metric
- **10% chance of random error** for observability testing
- Gets current AI Config from LaunchDarkly
- Routes to appropriate provider based on model name:
  - Contains "command" or "cohere" → Cohere
  - Contains "mistral" → Mistral
  - Everything else → OpenRouter
- Tracks LaunchDarkly AI metrics:
  - `TrackDuration(latencyMs)` - How long it took
  - `TrackSuccess()` or `TrackError()` - Whether it worked
  - `TrackTokens(usage)` - Token usage
- **Records OpenTelemetry metrics**:
  - `ai.generation.latency` - Response time
  - `ai.generation.success/errors` - Success/failure counts
  - `ai.tokens.input/output/total` - Token usage
- **Logs structured events** (success/failure with attributes)
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

## OpenTelemetry Observability

The app exports comprehensive telemetry data using the LaunchDarkly Observability plugin.

### Distributed Tracing (Spans)

Spans create a trace showing the complete request flow:

```csharp
// Parent span for the entire chat request
using (var activity = Observe.StartActivity("chat.request", ActivityKind.Server, ...))
{
    // Child span for the AI provider call
    var aiResponse = await CallOpenRouter(...);  // Creates its own span
    
    // Add tags to the parent span
    activity?.SetTag("ai.model", modelName);
    activity?.SetTag("http.status_code", 200);
}
```

**Span hierarchy:**
```
chat.request (parent)
  └─ openrouter.chat.completions (child)
```

This shows:
- How long the entire request took
- How long just the AI provider call took
- Which model was used
- Whether it succeeded or failed

### Metrics

The app records various metrics using `Observe.RecordMetric()` and `Observe.RecordCount()`:

```csharp
// Counter: Increments by 1 each time
Observe.RecordCount("chat.requests", 1, new Dictionary<string, object>
{
    { "endpoint", "/chat" }
});

// Gauge: Records a specific value
Observe.RecordMetric("ai.generation.latency", latencyMs, new Dictionary<string, object>
{
    { "ai.model", modelName }
});
```

**Metrics tracked:**
- `sdk.initialization` - SDK startup success/failure
- `ai.config.evaluation` - AI Config enabled/disabled
- `chat.requests` - Total chat requests
- `chat.errors` - Error count (10% random errors)
- `ai.generation.success` - Successful generations by model
- `ai.generation.errors` - Failed generations by model
- `ai.generation.latency` - Response time in milliseconds
- `ai.tokens.input/output/total` - Token usage
- `feedback.received` - User feedback (positive/negative)

### Logs

The app exports structured logs using `Observe.RecordLog()`:

```csharp
Observe.RecordLog("AI generation succeeded", LogLevel.Information, new Dictionary<string, object>
{
    { "ai.model", modelName },
    { "latency.ms", latencyMs },
    { "tokens.total", aiResponse.TotalTokens }
});
```

**Log levels:**
- `LogLevel.Information` - Normal operations
- `LogLevel.Warning` - Potential issues (e.g., feedback tracker not found)
- `LogLevel.Error` - Errors (e.g., AI generation failed)

All logs include structured attributes for filtering and correlation.

### Random Error Generation

For observability testing, 10% of chat requests randomly fail:

```csharp
var random = new Random();
if (random.Next(100) < 10)
{
    var exception = new Exception("Randomly generated backend error for observability testing");
    
    // Record the exception with metadata
    Observe.RecordException(exception, new Dictionary<string, object>
    {
        { "endpoint", "/chat" },
        { "user_message", userMessage ?? "null" },
        { "error_type", "simulated" }
    });
    
    // Count the error
    Observe.RecordCount("chat.errors", 1, new Dictionary<string, object>
    {
        { "error_type", "simulated" }
    });
    
    return 500 error;
}
```

This helps demonstrate:
- Error tracking in your observability platform
- Alert configuration
- Error rate monitoring

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
- ✅ Comprehensive metrics tracking (LaunchDarkly AI metrics)
- ✅ User feedback collection
- ✅ Thread-safe concurrent request handling
- ✅ **OpenTelemetry observability** (distributed tracing, metrics, logs)
- ✅ **Structured logging** with attributes
- ✅ **Error tracking** with random error generation
- ✅ **Performance monitoring** with latency and token metrics

The key insights:
1. **LaunchDarkly acts as a control plane for your AI application**, letting you change models, providers, and configurations without deploying new code.
2. **OpenTelemetry provides deep visibility** into your application's behavior, performance, and errors across the entire request lifecycle.
