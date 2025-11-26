// System namespaces for core functionality
using System;
using System.Collections.Concurrent;  // Thread-safe collections
using System.IO;
using System.Net.Http;  // For making HTTP requests to AI providers
using System.Text;
using System.Text.Json;  // For JSON serialization/deserialization
using System.Threading;  // For thread-safe operations like Interlocked
using System.Threading.Tasks;  // For async/await support

// LaunchDarkly SDK namespaces
using LaunchDarkly.Sdk;  // Core SDK types
using LaunchDarkly.Sdk.Server;  // Server-side SDK for feature flags
using LaunchDarkly.Sdk.Server.Ai;  // AI Config support
using LaunchDarkly.Sdk.Server.Ai.Adapters;  // Adapters for AI SDK
using LaunchDarkly.Sdk.Server.Ai.Config;  // AI Config types
using LaunchDarkly.Sdk.Server.Ai.Interfaces;  // AI SDK interfaces
using LaunchDarkly.Sdk.Server.Ai.Tracking;  // Metrics tracking (duration, tokens, feedback)

// ASP.NET Core namespaces for web server
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace HelloDotNet
{
    /// <summary>
    /// Represents a response from an AI provider, including the text and token usage information.
    /// </summary>
    class AiResponse
    {
        public string Text { get; set; }  // The AI-generated response text
        public int InputTokens { get; set; }  // Number of tokens in the user's prompt
        public int OutputTokens { get; set; }  // Number of tokens in the AI's response
        public int TotalTokens { get; set; }  // Total tokens used (input + output)
        public bool IsError { get; set; }  // Whether this response represents an error
    }

    class Hello
    {
        // Thread-safe collections for managing state across multiple concurrent requests
        
        /// <summary>Queue of log messages to be sent to connected clients via Server-Sent Events (SSE)</summary>
        private static ConcurrentQueue<string> logMessages = new ConcurrentQueue<string>();
        
        /// <summary>Active SSE connections for console output streaming</summary>
        private static ConcurrentBag<HttpResponse> activeConnections = new ConcurrentBag<HttpResponse>();
        
        /// <summary>Active SSE connections for AI Config updates</summary>
        private static ConcurrentBag<HttpResponse> aiConfigConnections = new ConcurrentBag<HttpResponse>();
        
        /// <summary>Active SSE connections for console visibility flag updates</summary>
        private static ConcurrentBag<HttpResponse> consoleVisibilityConnections = new ConcurrentBag<HttpResponse>();
        
        /// <summary>
        /// Cache of AI Config trackers by message ID. This allows us to associate user feedback
        /// (thumbs up/down) with the specific AI Config that generated the response.
        /// </summary>
        private static ConcurrentDictionary<int, ILdAiConfigTracker> trackerCache = new ConcurrentDictionary<int, ILdAiConfigTracker>();
        
        /// <summary>Thread-safe counter for generating unique message IDs</summary>
        private static int messageCounter = 0;
        
        /// <summary>Lock object to ensure thread-safe logging</summary>
        private static object logLock = new object();
        
        /// <summary>Shared HTTP client for making requests to AI providers (reusing connections improves performance)</summary>
        private static readonly HttpClient httpClient = new HttpClient();
        
        /// <summary>
        /// Logs a message to the console and broadcasts it to all connected web clients via Server-Sent Events (SSE).
        /// This allows real-time console output to be displayed in the web UI.
        /// </summary>
        /// <param name="message">The message to log</param>
        public static void Log(string message)
        {
            // Use a lock to ensure thread-safe logging (multiple requests could log simultaneously)
            lock (logLock)
            {
                // Write to the server console
                Console.Write(message);
                
                // Add to the message queue so new SSE connections can see historical messages
                logMessages.Enqueue(message);
                
                // Broadcast to all active SSE connections in real-time
                // SSE format requires newlines to be escaped as \n
                var escapedMessage = message.Replace("\n", "\\n").Replace("\r", "");
                
                foreach (var response in activeConnections)
                {
                    try
                    {
                        // Send the message in SSE format: "data: <message>\n\n"
                        response.WriteAsync($"data: {escapedMessage}\n\n").GetAwaiter().GetResult();
                        response.Body.FlushAsync().GetAwaiter().GetResult();
                    }
                    catch 
                    { 
                        // Ignore errors (connection may have closed)
                    }
                }
            }
        }

        /// <summary>
        /// Displays the LaunchDarkly ASCII art banner.
        /// This is controlled by the "sample-feature" feature flag.
        /// </summary>
        public static void ShowBanner(){
            string banner = @"        ██
          ██
      ████████
         ███████
██ LAUNCHDARKLY █
         ███████
      ████████
          ██
        ██
";
            Log(banner);
        }

        /// <summary>
        /// Calls the OpenRouter API to generate an AI response.
        /// OpenRouter provides access to multiple AI models through a single API.
        /// </summary>
        /// <param name="userMessage">The user's input message</param>
        /// <param name="modelName">The AI model to use (e.g., "gpt-4", "claude-3")</param>
        /// <param name="apiKey">OpenRouter API key</param>
        /// <returns>AI response with text and token usage</returns>
        public static async Task<AiResponse> CallOpenRouter(string userMessage, string modelName, string apiKey)
        {
            try
            {
                var requestBody = new
                {
                    model = modelName,
                    messages = new[]
                    {
                        new { role = "user", content = userMessage }
                    }
                };

                var request = new HttpRequestMessage(HttpMethod.Post, "https://openrouter.ai/api/v1/chat/completions");
                request.Headers.Add("Authorization", $"Bearer {apiKey}");
                request.Content = new StringContent(
                    JsonSerializer.Serialize(requestBody),
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await httpClient.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = JsonSerializer.Deserialize<JsonElement>(responseBody);
                    var text = jsonResponse.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "No response";
                    
                    var usage = jsonResponse.GetProperty("usage");
                    return new AiResponse
                    {
                        Text = text,
                        InputTokens = usage.GetProperty("prompt_tokens").GetInt32(),
                        OutputTokens = usage.GetProperty("completion_tokens").GetInt32(),
                        TotalTokens = usage.GetProperty("total_tokens").GetInt32(),
                        IsError = false
                    };
                }
                else
                {
                    Log($"[OpenRouter Error] {response.StatusCode}: {responseBody}\n");
                    return new AiResponse 
                    { 
                        Text = $"Error calling AI model: {response.StatusCode}",
                        IsError = true
                    };
                }
            }
            catch (Exception ex)
            {
                Log($"[OpenRouter Exception] {ex.Message}\n");
                return new AiResponse 
                { 
                    Text = $"Error: {ex.Message}",
                    IsError = true
                };
            }
        }

        /// <summary>
        /// Calls the Cohere API to generate an AI response.
        /// Cohere specializes in enterprise AI models like Command R+.
        /// </summary>
        /// <param name="userMessage">The user's input message</param>
        /// <param name="modelName">The Cohere model to use (e.g., "command-r-plus")</param>
        /// <param name="apiKey">Cohere API key</param>
        /// <returns>AI response with text and token usage</returns>
        public static async Task<AiResponse> CallCohere(string userMessage, string modelName, string apiKey)
        {
            try
            {
                var requestBody = new
                {
                    model = modelName,
                    message = userMessage
                };

                var request = new HttpRequestMessage(HttpMethod.Post, "https://api.cohere.ai/v1/chat");
                request.Headers.Add("Authorization", $"Bearer {apiKey}");
                request.Content = new StringContent(
                    JsonSerializer.Serialize(requestBody),
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await httpClient.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = JsonSerializer.Deserialize<JsonElement>(responseBody);
                    var text = jsonResponse.GetProperty("text").GetString() ?? "No response";
                    
                    // Cohere returns token usage in meta
                    var tokens = jsonResponse.GetProperty("meta").GetProperty("tokens");
                    return new AiResponse
                    {
                        Text = text,
                        InputTokens = tokens.GetProperty("input_tokens").GetInt32(),
                        OutputTokens = tokens.GetProperty("output_tokens").GetInt32(),
                        TotalTokens = tokens.GetProperty("input_tokens").GetInt32() + tokens.GetProperty("output_tokens").GetInt32(),
                        IsError = false
                    };
                }
                else
                {
                    Log($"[Cohere Error] {response.StatusCode}: {responseBody}\n");
                    return new AiResponse 
                    { 
                        Text = $"Error calling AI model: {response.StatusCode}",
                        IsError = true
                    };
                }
            }
            catch (Exception ex)
            {
                Log($"[Cohere Exception] {ex.Message}\n");
                return new AiResponse 
                { 
                    Text = $"Error: {ex.Message}",
                    IsError = true
                };
            }
        }

        /// <summary>
        /// Calls the Mistral AI API to generate an AI response.
        /// Mistral AI provides open-source and commercial AI models.
        /// </summary>
        /// <param name="userMessage">The user's input message</param>
        /// <param name="modelName">The Mistral model to use (e.g., "mistral-large")</param>
        /// <param name="apiKey">Mistral AI API key</param>
        /// <returns>AI response with text and token usage</returns>
        public static async Task<AiResponse> CallMistral(string userMessage, string modelName, string apiKey)
        {
            try
            {
                var requestBody = new
                {
                    model = modelName,
                    messages = new[]
                    {
                        new { role = "user", content = userMessage }
                    }
                };

                var request = new HttpRequestMessage(HttpMethod.Post, "https://api.mistral.ai/v1/chat/completions");
                request.Headers.Add("Authorization", $"Bearer {apiKey}");
                request.Content = new StringContent(
                    JsonSerializer.Serialize(requestBody),
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await httpClient.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = JsonSerializer.Deserialize<JsonElement>(responseBody);
                    var text = jsonResponse.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "No response";
                    
                    var usage = jsonResponse.GetProperty("usage");
                    return new AiResponse
                    {
                        Text = text,
                        InputTokens = usage.GetProperty("prompt_tokens").GetInt32(),
                        OutputTokens = usage.GetProperty("completion_tokens").GetInt32(),
                        TotalTokens = usage.GetProperty("total_tokens").GetInt32(),
                        IsError = false
                    };
                }
                else
                {
                    Log($"[Mistral Error] {response.StatusCode}: {responseBody}\n");
                    return new AiResponse 
                    { 
                        Text = $"Error calling AI model: {response.StatusCode}",
                        IsError = true
                    };
                }
            }
            catch (Exception ex)
            {
                Log($"[Mistral Exception] {ex.Message}\n");
                return new AiResponse 
                { 
                    Text = $"Error: {ex.Message}",
                    IsError = true
                };
            }
        }

        static void Main(string[] args)
        {
            // Check if running in CI environment (exits after initialization if true)
            bool CI = Environment.GetEnvironmentVariable("CI") != null;

            // Get LaunchDarkly SDK key from environment variable
            // This key authorizes the app to connect to your LaunchDarkly project
            string SdkKey = Environment.GetEnvironmentVariable("LAUNCHDARKLY_SDK_KEY");

            // The feature flag key to evaluate (can be overridden via LAUNCHDARKLY_FLAG_KEY env var)
            string FeatureFlagKey = "sample-feature";

            // Validate that SDK key is provided
            if (string.IsNullOrEmpty(SdkKey))
            {
                Console.WriteLine("*** Please set LAUNCHDARKLY_SDK_KEY environment variable to your LaunchDarkly SDK key first\n");
                Environment.Exit(1);
            }

            // Initialize LaunchDarkly configuration with the SDK key
            var ldConfig = Configuration.Default(SdkKey);

            // Create the LaunchDarkly client for feature flags
            var client = new LdClient(ldConfig);
            
            // Create the LaunchDarkly AI client for AI Configs
            // The LdClientAdapter wraps the base client to provide AI-specific functionality
            var aiClient = new LdAiClient(new LdClientAdapter(client));

            // Wait for SDK to initialize (connects to LaunchDarkly and downloads flags)
            if (client.Initialized)
            {
                Log("*** SDK successfully initialized!\n");
            }
            else
            {
                Log("*** SDK failed to initialize\n");
                Environment.Exit(1);
            }

            // Create a LaunchDarkly context to identify this server
            // Contexts are used for targeting - you can target specific servers, SDK versions, etc.
            // This context will appear in your LaunchDarkly dashboard under "Contexts"
            var sdkVersion = typeof(LdClient).Assembly.GetName().Version.ToString();
            var context = Context.Builder(ContextKind.Of("ld-sdk"), "dot-net-server")
                .Set("sdkVersion", sdkVersion)  // Add SDK version as an attribute
                .Build();

            // Allow overriding the feature flag key via environment variable
            if (Environment.GetEnvironmentVariable("LAUNCHDARKLY_FLAG_KEY") != null)
            {
                FeatureFlagKey = Environment.GetEnvironmentVariable("LAUNCHDARKLY_FLAG_KEY");
            }

            // Evaluate the feature flag
            // Parameters: flag key, context, default value (used if flag doesn't exist)
            var flagValue = client.BoolVariation(FeatureFlagKey, context, false);

            Log(string.Format("*** The {0} feature flag evaluates to {1}.\n",
                FeatureFlagKey, flagValue));

            // If flag is true, show the LaunchDarkly banner
            if (flagValue)
            {
                ShowBanner();
            }

            // Get the AI Config from LaunchDarkly
            // This determines which AI model to use for the chatbot
            // Parameters: config key, context, default value (disabled if config doesn't exist)
            var aiConfigTracker = aiClient.Config("sample-ai-config", context, LdAiConfig.Disabled);
            if (aiConfigTracker.Config.Enabled)
            {
                Log(string.Format("*** AI Config enabled with model: {0}\n", aiConfigTracker.Config.Model?.Name ?? "unknown"));
            }
            else
            {
                Log("*** AI Config is disabled\n");
            }

            // Set up real-time tracking for the "show-console" feature flag
            // When this flag changes in LaunchDarkly, this handler broadcasts the update to all connected web clients
            client.FlagTracker.FlagChanged += client.FlagTracker.FlagValueChangeHandler(
                "show-console",  // The flag key to monitor
                context,  // The context to evaluate the flag for
                (sender, changeArgs) => {
                    // Get the new flag value
                    var visible = changeArgs.NewValue.AsBool;
                    Log($"*** Console visibility changed: {visible}\n");
                    
                    // Broadcast the change to all connected SSE clients
                    var data = JsonSerializer.Serialize(new { visible = visible });
                    foreach (var response in consoleVisibilityConnections)
                    {
                        try
                        {
                            response.WriteAsync($"data: {data}\n\n").GetAwaiter().GetResult();
                            response.Body.FlushAsync().GetAwaiter().GetResult();
                        }
                        catch { /* Ignore errors from closed connections */ }
                    }
                }
            );

            // Set up real-time tracking for AI Config changes
            // When the "sample-ai-config" changes in LaunchDarkly, this handler updates all connected web clients
            client.FlagTracker.FlagChanged += (sender, changeArgs) => {
                if (changeArgs.Key == "sample-ai-config")
                {
                    Log($"*** AI Config '{changeArgs.Key}' has changed!\n");
                    
                    // Re-evaluate the AI Config to get the new model configuration
                    var updatedConfig = aiClient.Config("sample-ai-config", context, LdAiConfig.Disabled);
                    
                    if (updatedConfig.Config.Enabled)
                    {
                        Log($"*** New AI Config: model {updatedConfig.Config.Model?.Name}\n");
                    }
                    else
                    {
                        Log("*** AI Config is now disabled\n");
                    }
                    
                    // Broadcast the updated config to all connected SSE clients
                    // This updates the model badge in the web UI in real-time
                    var data = JsonSerializer.Serialize(new {
                        model = updatedConfig.Config.Model?.Name ?? "Disabled",
                        enabled = updatedConfig.Config.Enabled
                    });
                    
                    foreach (var response in aiConfigConnections)
                    {
                        try
                        {
                            response.WriteAsync($"data: {data}\n\n").GetAwaiter().GetResult();
                            response.Body.FlushAsync().GetAwaiter().GetResult();
                        }
                        catch { /* Ignore errors from closed connections */ }
                    }
                }
            };

            // Set up real-time tracking for the sample feature flag
            // When this flag changes in LaunchDarkly, this handler logs the change and shows the banner if enabled
            client.FlagTracker.FlagChanged += client.FlagTracker.FlagValueChangeHandler(
                FeatureFlagKey,  // The flag key to monitor (default: "sample-feature")
                context,  // The context to evaluate the flag for
                (sender, changeArgs) => {
                    Log(string.Format("*** The {0} feature flag evaluates to {1}.\n",
                    FeatureFlagKey, changeArgs.NewValue));

                    // If the flag is now true, show the LaunchDarkly banner
                    if (changeArgs.NewValue.AsBool) ShowBanner();
                }
            );

            // Exit early if running in CI (Continuous Integration) environment
            // This allows automated tests to verify initialization without starting the web server
            if(CI) Environment.Exit(0);

            Log("*** Waiting for changes \n");
            Log("*** Web interface available at http://localhost:5000\n");

            // Start the ASP.NET Core web server
            var host = WebHost.CreateDefaultBuilder(args)
                .UseUrls("http://0.0.0.0:5000")  // Listen on all network interfaces, port 5000
                .UseWebRoot("wwwroot")  // Serve static files from wwwroot directory
                .Configure(app =>
                {
                    // Serve index.html by default when accessing root URL
                    app.UseDefaultFiles();
                    // Enable serving static files (HTML, CSS, JS)
                    app.UseStaticFiles();
                    
                    // SSE endpoint for streaming console visibility flag changes
                    // This allows the web UI to show/hide the console output in real-time
                    app.Map("/console-visibility-stream", consoleStreamApp =>
                    {
                        consoleStreamApp.Run(async httpContext =>
                        {
                            // Set SSE headers
                            httpContext.Response.ContentType = "text/event-stream";
                            httpContext.Response.Headers["Cache-Control"] = "no-cache";
                            httpContext.Response.Headers["Connection"] = "keep-alive";
                            
                            // Send the current state of the "show-console" flag immediately
                            // This ensures the UI starts with the correct visibility state
                            var initialVisible = client.BoolVariation("show-console", context, true);
                            var initialData = JsonSerializer.Serialize(new { visible = initialVisible });
                            await httpContext.Response.WriteAsync($"data: {initialData}\n\n");
                            await httpContext.Response.Body.FlushAsync();
                            
                            // Register this connection to receive future flag updates
                            consoleVisibilityConnections.Add(httpContext.Response);
                            
                            // Keep the connection alive with periodic keepalive messages
                            try
                            {
                                while (!httpContext.RequestAborted.IsCancellationRequested)
                                {
                                    await Task.Delay(30000);  // Wait 30 seconds
                                    await httpContext.Response.WriteAsync(":keepalive\n\n");
                                    await httpContext.Response.Body.FlushAsync();
                                }
                            }
                            catch { /* Connection closed */ }
                        });
                    });
                    
                    // SSE endpoint for streaming AI Config changes
                    // This updates the model badge in the web UI when the AI Config changes in LaunchDarkly
                    app.Map("/ai-config-stream", aiConfigStreamApp =>
                    {
                        aiConfigStreamApp.Run(async httpContext =>
                        {
                            // Set SSE headers
                            httpContext.Response.ContentType = "text/event-stream";
                            httpContext.Response.Headers["Cache-Control"] = "no-cache";
                            httpContext.Response.Headers["Connection"] = "keep-alive";
                            
                            // Send the current AI Config state immediately
                            // This ensures the UI displays the correct model from the start
                            var initialData = JsonSerializer.Serialize(new {
                                model = aiConfigTracker.Config.Model?.Name ?? "Disabled",
                                enabled = aiConfigTracker.Config.Enabled
                            });
                            await httpContext.Response.WriteAsync($"data: {initialData}\n\n");
                            await httpContext.Response.Body.FlushAsync();
                            
                            // Register this connection to receive future AI Config updates
                            // Updates are pushed via the FlagTracker event handler above
                            aiConfigConnections.Add(httpContext.Response);
                            
                            // Keep connection alive with periodic keepalive messages
                            try
                            {
                                while (!httpContext.RequestAborted.IsCancellationRequested)
                                {
                                    await Task.Delay(30000);  // Wait 30 seconds
                                    await httpContext.Response.WriteAsync(":keepalive\n\n");
                                    await httpContext.Response.Body.FlushAsync();
                                }
                            }
                            catch { /* Connection closed */ }
                        });
                    });
                    
                    // Feedback endpoint for tracking user satisfaction (thumbs up/down)
                    // This associates user feedback with the AI Config that generated the response
                    app.Map("/feedback", feedbackApp =>
                    {
                        feedbackApp.Run(async httpContext =>
                        {
                            // Only accept POST requests
                            if (httpContext.Request.Method != "POST")
                            {
                                httpContext.Response.StatusCode = 405;  // Method Not Allowed
                                return;
                            }

                            // Parse the JSON request body
                            using var reader = new StreamReader(httpContext.Request.Body);
                            var body = await reader.ReadToEndAsync();
                            var request = JsonSerializer.Deserialize<JsonElement>(body);
                            var messageId = request.GetProperty("messageId").GetInt32();
                            var isPositive = request.GetProperty("positive").GetBoolean();

                            // Look up the tracker from cache and track the feedback
                            // TryRemove is thread-safe and removes the entry after retrieving it
                            if (trackerCache.TryRemove(messageId, out var tracker))
                            {
                                var feedback = isPositive ? Feedback.Positive : Feedback.Negative;
                                tracker.TrackFeedback(feedback);  // Send feedback to LaunchDarkly
                                Log($"[Feedback] Message {messageId}: {(isPositive ? "👍" : "👎")}\n");
                            }
                            else
                            {
                                // Tracker not found (may have expired or already been used)
                                Log($"[Feedback] Message {messageId} not found in cache\n");
                            }

                            // Return success response
                            httpContext.Response.StatusCode = 200;
                            await httpContext.Response.WriteAsync("{}");
                        });
                    });
                    
                    // Chat endpoint for handling user messages and generating AI responses
                    // This is the main endpoint that ties together LaunchDarkly AI Config with AI providers
                    app.Map("/chat", chatApp =>
                    {
                        chatApp.Run(async httpContext =>
                        {
                            // Only accept POST requests
                            if (httpContext.Request.Method != "POST")
                            {
                                httpContext.Response.StatusCode = 405;  // Method Not Allowed
                                return;
                            }

                            // Parse the user's message from the request body
                            using var reader = new StreamReader(httpContext.Request.Body);
                            var body = await reader.ReadToEndAsync();
                            var request = JsonSerializer.Deserialize<JsonElement>(body);
                            var userMessage = request.GetProperty("message").GetString();

                            Log($"[Chat] User: {userMessage}\n");

                            // Get the current AI Config from LaunchDarkly
                            // This determines which AI model to use for this request
                            var aiConfigTracker = aiClient.Config("sample-ai-config", context, LdAiConfig.Disabled);
                            
                            string response;
                            string modelName = "Disabled";
                            bool enabled = aiConfigTracker.Config.Enabled;
                            
                            // Check if AI Config is enabled
                            if (!enabled)
                            {
                                response = "AI Config is disabled. Please enable 'sample-ai-config' in LaunchDarkly.";
                                Log($"[Chat] AI Config disabled\n");
                            }
                            else
                            {
                                modelName = aiConfigTracker.Config.Model?.Name ?? "unknown";
                                Log($"[Chat] Using model: {modelName}\n");
                                
                                var startTime = DateTime.UtcNow;  // Start timing for latency tracking
                                AiResponse aiResponse;
                                
                                // Route to the appropriate AI provider based on model name
                                // This allows switching between providers via LaunchDarkly without code changes
                                
                                if (modelName.ToLower().Contains("command") || modelName.ToLower().Contains("cohere"))
                                {
                                    // Use Cohere for Command models
                                    var cohereKey = Environment.GetEnvironmentVariable("COHERE_API_KEY");
                                    aiResponse = !string.IsNullOrEmpty(cohereKey)
                                        ? await CallCohere(userMessage, modelName, cohereKey)
                                        : new AiResponse { Text = "Cohere API key not configured.", IsError = true };
                                }
                                else if (modelName.ToLower().Contains("mistral"))
                                {
                                    // Use Mistral AI for Mistral models
                                    var mistralKey = Environment.GetEnvironmentVariable("MISTRAL_API_KEY");
                                    aiResponse = !string.IsNullOrEmpty(mistralKey)
                                        ? await CallMistral(userMessage, modelName, mistralKey)
                                        : new AiResponse { Text = "Mistral API key not configured.", IsError = true };
                                }
                                else
                                {
                                    // Default to OpenRouter for all other models
                                    var openRouterKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");
                                    aiResponse = !string.IsNullOrEmpty(openRouterKey)
                                        ? await CallOpenRouter(userMessage, modelName, openRouterKey)
                                        : new AiResponse { Text = "OpenRouter API key not configured.", IsError = true };
                                }
                                
                                response = aiResponse.Text;
                                
                                // Track metrics with LaunchDarkly AI SDK
                                // This sends telemetry data to LaunchDarkly for monitoring AI performance
                                
                                // 1. Track how long the AI request took (latency)
                                var latencyMs = (float)(DateTime.UtcNow - startTime).TotalMilliseconds;
                                aiConfigTracker.TrackDuration(latencyMs);
                                
                                if (aiResponse.IsError)
                                {
                                    // 2. Track that this request failed
                                    aiConfigTracker.TrackError();
                                    Log($"[Error] Generation failed\n");
                                }
                                else
                                {
                                    // 3. Track that this request succeeded
                                    aiConfigTracker.TrackSuccess();
                                    
                                    // 4. Track token usage (cost tracking)
                                    // Only track tokens on successful responses
                                    if (aiResponse.TotalTokens > 0)
                                    {
                                        try
                                        {
                                            // Create a Usage object with token counts
                                            var usage = new Usage 
                                            { 
                                                Total = aiResponse.TotalTokens,
                                                Input = aiResponse.InputTokens,  // Tokens in the user's prompt
                                                Output = aiResponse.OutputTokens  // Tokens in the AI's response
                                            };
                                            aiConfigTracker.TrackTokens(usage);
                                            Log($"[Tokens] Input: {aiResponse.InputTokens}, Output: {aiResponse.OutputTokens}, Total: {aiResponse.TotalTokens}\n");
                                        }
                                        catch (Exception ex)
                                        {
                                            Log($"[Token Tracking Error] {ex.Message}\n");
                                        }
                                    }
                                }
                            }
                            
                            // Store the tracker in cache so we can associate feedback later
                            // When the user clicks thumbs up/down, we'll look up this tracker by messageId
                            var messageId = Interlocked.Increment(ref messageCounter);  // Thread-safe counter increment
                            trackerCache[messageId] = aiConfigTracker;
                            
                            // Send JSON response back to the web client
                            httpContext.Response.ContentType = "application/json";
                            await httpContext.Response.WriteAsync(JsonSerializer.Serialize(new { 
                                response = response,  // The AI-generated text
                                model = modelName,  // Which model was used
                                enabled = enabled,  // Whether AI Config is enabled
                                messageId = messageId  // Unique ID for feedback tracking
                            }));
                        });
                    });
                    
                    // Server-Sent Events (SSE) endpoint for streaming console output to the web UI
                    // This allows the web page to display real-time server logs
                    app.Map("/stream", streamApp =>
                    {
                        streamApp.Run(async context =>
                        {
                            // Set SSE headers
                            context.Response.ContentType = "text/event-stream";  // SSE MIME type
                            context.Response.Headers["Cache-Control"] = "no-cache";  // Don't cache events
                            context.Response.Headers["Connection"] = "keep-alive";  // Keep connection open
                            
                            // Send all existing log messages to this new connection
                            // This ensures new clients see the full console history
                            foreach (var msg in logMessages)
                            {
                                // SSE format requires newlines to be escaped
                                var escapedMsg = msg.Replace("\n", "\\n").Replace("\r", "");
                                await context.Response.WriteAsync($"data: {escapedMsg}\n\n");
                                await context.Response.Body.FlushAsync();
                            }
                            
                            // Register this connection to receive future log messages
                            activeConnections.Add(context.Response);
                            
                            // Keep the connection alive by sending periodic keepalive messages
                            // Without this, proxies or browsers might close the connection
                            try
                            {
                                while (!context.RequestAborted.IsCancellationRequested)
                                {
                                    await Task.Delay(1000);  // Wait 1 second
                                    await context.Response.WriteAsync(":keepalive\n\n");  // SSE comment (ignored by client)
                                    await context.Response.Body.FlushAsync();
                                }
                            }
                            catch { /* Connection closed */ }
                        });
                    });
                })
                .Build();

            // Start the web server and block until shutdown
            // This keeps the application running and serving requests
            host.Run();
        }
    }
}