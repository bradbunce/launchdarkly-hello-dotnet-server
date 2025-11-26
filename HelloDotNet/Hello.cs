using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LaunchDarkly.Sdk;
using LaunchDarkly.Sdk.Server;
using LaunchDarkly.Sdk.Server.Ai;
using LaunchDarkly.Sdk.Server.Ai.Adapters;
using LaunchDarkly.Sdk.Server.Ai.Config;
using LaunchDarkly.Sdk.Server.Ai.Interfaces;
using LaunchDarkly.Sdk.Server.Ai.Tracking;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace HelloDotNet
{
    class AiResponse
    {
        public string Text { get; set; }
        public int InputTokens { get; set; }
        public int OutputTokens { get; set; }
        public int TotalTokens { get; set; }
        public bool IsError { get; set; }
    }

    class Hello
    {
        private static ConcurrentQueue<string> logMessages = new ConcurrentQueue<string>();
        private static ConcurrentBag<HttpResponse> activeConnections = new ConcurrentBag<HttpResponse>();
        private static ConcurrentBag<HttpResponse> aiConfigConnections = new ConcurrentBag<HttpResponse>();
        private static ConcurrentBag<HttpResponse> consoleVisibilityConnections = new ConcurrentBag<HttpResponse>();
        private static ConcurrentDictionary<int, ILdAiConfigTracker> trackerCache = new ConcurrentDictionary<int, ILdAiConfigTracker>();
        private static int messageCounter = 0;
        private static object logLock = new object();
        private static readonly HttpClient httpClient = new HttpClient();
        
        public static void Log(string message)
        {
            lock (logLock)
            {
                Console.Write(message);
                logMessages.Enqueue(message);
                
                // Send to all active SSE connections
                // Escape newlines for SSE format
                var escapedMessage = message.Replace("\n", "\\n").Replace("\r", "");
                foreach (var response in activeConnections)
                {
                    try
                    {
                        response.WriteAsync($"data: {escapedMessage}\n\n").GetAwaiter().GetResult();
                        response.Body.FlushAsync().GetAwaiter().GetResult();
                    }
                    catch { }
                }
            }
        }

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
            bool CI = Environment.GetEnvironmentVariable("CI") != null;

            string SdkKey = Environment.GetEnvironmentVariable("LAUNCHDARKLY_SDK_KEY");
            // string SdkKey = "";

            // Set FeatureFlagKey to the feature flag key you want to evaluate.
            string FeatureFlagKey = "sample-feature";

            if (string.IsNullOrEmpty(SdkKey))
            {
                Console.WriteLine("*** Please set LAUNCHDARKLY_SDK_KEY environment variable to your LaunchDarkly SDK key first\n");
                Environment.Exit(1);
            }

            var ldConfig = Configuration.Default(SdkKey);

            var client = new LdClient(ldConfig);
            var aiClient = new LdAiClient(new LdClientAdapter(client));

            if (client.Initialized)
            {
                Log("*** SDK successfully initialized!\n");
            }
            else
            {
                Log("*** SDK failed to initialize\n");
                Environment.Exit(1);
            }

            // Set up the evaluation context. This context should appear on your LaunchDarkly contexts
            // dashboard soon after you run the demo.
            var sdkVersion = typeof(LdClient).Assembly.GetName().Version.ToString();
            var context = Context.Builder(ContextKind.Of("ld-sdk"), "dot-net-server")
                .Set("sdkVersion", sdkVersion)
                .Build();

            if (Environment.GetEnvironmentVariable("LAUNCHDARKLY_FLAG_KEY") != null)
            {
                FeatureFlagKey = Environment.GetEnvironmentVariable("LAUNCHDARKLY_FLAG_KEY");
            }

            var flagValue = client.BoolVariation(FeatureFlagKey, context, false);

            Log(string.Format("*** The {0} feature flag evaluates to {1}.\n",
                FeatureFlagKey, flagValue));

            if (flagValue)
            {
                ShowBanner();
            }

            // AI SDK - get AI config
            var aiConfigTracker = aiClient.Config("sample-ai-config", context, LdAiConfig.Disabled);
            if (aiConfigTracker.Config.Enabled)
            {
                Log(string.Format("*** AI Config enabled with model: {0}\n", aiConfigTracker.Config.Model?.Name ?? "unknown"));
            }
            else
            {
                Log("*** AI Config is disabled\n");
            }

            // Track console visibility flag changes
            client.FlagTracker.FlagChanged += client.FlagTracker.FlagValueChangeHandler(
                "show-console",
                context,
                (sender, changeArgs) => {
                    var visible = changeArgs.NewValue.AsBool;
                    Log($"*** Console visibility changed: {visible}\n");
                    
                    var data = JsonSerializer.Serialize(new { visible = visible });
                    foreach (var response in consoleVisibilityConnections)
                    {
                        try
                        {
                            response.WriteAsync($"data: {data}\n\n").GetAwaiter().GetResult();
                            response.Body.FlushAsync().GetAwaiter().GetResult();
                        }
                        catch { }
                    }
                }
            );

            // Track AI Config changes via FlagTracker
            client.FlagTracker.FlagChanged += (sender, changeArgs) => {
                if (changeArgs.Key == "sample-ai-config")
                {
                    Log($"*** AI Config '{changeArgs.Key}' has changed!\n");
                    
                    // Re-evaluate the AI config
                    var updatedConfig = aiClient.Config("sample-ai-config", context, LdAiConfig.Disabled);
                    
                    if (updatedConfig.Config.Enabled)
                    {
                        Log($"*** New AI Config: model {updatedConfig.Config.Model?.Name}\n");
                    }
                    else
                    {
                        Log("*** AI Config is now disabled\n");
                    }
                    
                    // Broadcast to all AI Config SSE connections
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
                        catch { }
                    }
                }
            };

            client.FlagTracker.FlagChanged += client.FlagTracker.FlagValueChangeHandler(
                FeatureFlagKey,
                context,
                (sender, changeArgs) => {
                    Log(string.Format("*** The {0} feature flag evaluates to {1}.\n",
                    FeatureFlagKey, changeArgs.NewValue));

                    if (changeArgs.NewValue.AsBool) ShowBanner();
                }
            );

            if(CI) Environment.Exit(0);

            Log("*** Waiting for changes \n");
            Log("*** Web interface available at http://localhost:5000\n");

            // Start web server
            var host = WebHost.CreateDefaultBuilder(args)
                .UseUrls("http://0.0.0.0:5000")
                .UseWebRoot("wwwroot")
                .Configure(app =>
                {
                    app.UseDefaultFiles();
                    app.UseStaticFiles();
                    
                    app.Map("/console-visibility-stream", consoleStreamApp =>
                    {
                        consoleStreamApp.Run(async httpContext =>
                        {
                            httpContext.Response.ContentType = "text/event-stream";
                            httpContext.Response.Headers["Cache-Control"] = "no-cache";
                            httpContext.Response.Headers["Connection"] = "keep-alive";
                            
                            // Send initial state
                            var initialVisible = client.BoolVariation("show-console", context, true);
                            var initialData = JsonSerializer.Serialize(new { visible = initialVisible });
                            await httpContext.Response.WriteAsync($"data: {initialData}\n\n");
                            await httpContext.Response.Body.FlushAsync();
                            
                            // Register this connection
                            consoleVisibilityConnections.Add(httpContext.Response);
                            
                            // Keep connection alive
                            try
                            {
                                while (!httpContext.RequestAborted.IsCancellationRequested)
                                {
                                    await Task.Delay(30000);
                                    await httpContext.Response.WriteAsync(":keepalive\n\n");
                                    await httpContext.Response.Body.FlushAsync();
                                }
                            }
                            catch { }
                        });
                    });
                    
                    app.Map("/ai-config-stream", aiConfigStreamApp =>
                    {
                        aiConfigStreamApp.Run(async httpContext =>
                        {
                            httpContext.Response.ContentType = "text/event-stream";
                            httpContext.Response.Headers["Cache-Control"] = "no-cache";
                            httpContext.Response.Headers["Connection"] = "keep-alive";
                            
                            // Send initial AI config
                            var initialData = JsonSerializer.Serialize(new {
                                model = aiConfigTracker.Config.Model?.Name ?? "Disabled",
                                enabled = aiConfigTracker.Config.Enabled
                            });
                            await httpContext.Response.WriteAsync($"data: {initialData}\n\n");
                            await httpContext.Response.Body.FlushAsync();
                            
                            // Register this connection
                            aiConfigConnections.Add(httpContext.Response);
                            
                            // Keep connection alive - updates pushed via FlagTracker
                            try
                            {
                                while (!httpContext.RequestAborted.IsCancellationRequested)
                                {
                                    await Task.Delay(30000);
                                    await httpContext.Response.WriteAsync(":keepalive\n\n");
                                    await httpContext.Response.Body.FlushAsync();
                                }
                            }
                            catch { }
                        });
                    });
                    
                    app.Map("/feedback", feedbackApp =>
                    {
                        feedbackApp.Run(async httpContext =>
                        {
                            if (httpContext.Request.Method != "POST")
                            {
                                httpContext.Response.StatusCode = 405;
                                return;
                            }

                            using var reader = new StreamReader(httpContext.Request.Body);
                            var body = await reader.ReadToEndAsync();
                            var request = JsonSerializer.Deserialize<JsonElement>(body);
                            var messageId = request.GetProperty("messageId").GetInt32();
                            var isPositive = request.GetProperty("positive").GetBoolean();

                            // Track feedback using cached tracker
                            if (trackerCache.TryRemove(messageId, out var tracker))
                            {
                                var feedback = isPositive ? Feedback.Positive : Feedback.Negative;
                                tracker.TrackFeedback(feedback);
                                Log($"[Feedback] Message {messageId}: {(isPositive ? "👍" : "👎")}\n");
                            }
                            else
                            {
                                Log($"[Feedback] Message {messageId} not found in cache\n");
                            }

                            httpContext.Response.StatusCode = 200;
                            await httpContext.Response.WriteAsync("{}");
                        });
                    });
                    
                    app.Map("/chat", chatApp =>
                    {
                        chatApp.Run(async httpContext =>
                        {
                            if (httpContext.Request.Method != "POST")
                            {
                                httpContext.Response.StatusCode = 405;
                                return;
                            }

                            using var reader = new StreamReader(httpContext.Request.Body);
                            var body = await reader.ReadToEndAsync();
                            var request = JsonSerializer.Deserialize<JsonElement>(body);
                            var userMessage = request.GetProperty("message").GetString();

                            Log($"[Chat] User: {userMessage}\n");

                            // Get AI config from LaunchDarkly
                            var aiConfigTracker = aiClient.Config("sample-ai-config", context, LdAiConfig.Disabled);
                            
                            string response;
                            string modelName = "Disabled";
                            bool enabled = aiConfigTracker.Config.Enabled;
                            
                            if (!enabled)
                            {
                                response = "AI Config is disabled. Please enable 'sample-ai-config' in LaunchDarkly.";
                                Log($"[Chat] AI Config disabled\n");
                            }
                            else
                            {
                                modelName = aiConfigTracker.Config.Model?.Name ?? "unknown";
                                Log($"[Chat] Using model: {modelName}\n");
                                
                                var startTime = DateTime.UtcNow;
                                AiResponse aiResponse;
                                
                                // Determine which provider to use based on model name
                                if (modelName.ToLower().Contains("command") || modelName.ToLower().Contains("cohere"))
                                {
                                    var cohereKey = Environment.GetEnvironmentVariable("COHERE_API_KEY");
                                    aiResponse = !string.IsNullOrEmpty(cohereKey)
                                        ? await CallCohere(userMessage, modelName, cohereKey)
                                        : new AiResponse { Text = "Cohere API key not configured.", IsError = true };
                                }
                                else if (modelName.ToLower().Contains("mistral"))
                                {
                                    var mistralKey = Environment.GetEnvironmentVariable("MISTRAL_API_KEY");
                                    aiResponse = !string.IsNullOrEmpty(mistralKey)
                                        ? await CallMistral(userMessage, modelName, mistralKey)
                                        : new AiResponse { Text = "Mistral API key not configured.", IsError = true };
                                }
                                else
                                {
                                    var openRouterKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");
                                    aiResponse = !string.IsNullOrEmpty(openRouterKey)
                                        ? await CallOpenRouter(userMessage, modelName, openRouterKey)
                                        : new AiResponse { Text = "OpenRouter API key not configured.", IsError = true };
                                }
                                
                                response = aiResponse.Text;
                                
                                // Track metrics
                                var latencyMs = (float)(DateTime.UtcNow - startTime).TotalMilliseconds;
                                aiConfigTracker.TrackDuration(latencyMs);
                                
                                if (aiResponse.IsError)
                                {
                                    aiConfigTracker.TrackError();
                                    Log($"[Error] Generation failed\n");
                                }
                                else
                                {
                                    aiConfigTracker.TrackSuccess();
                                    
                                    // Track token usage only on success
                                    if (aiResponse.TotalTokens > 0)
                                    {
                                        try
                                        {
                                            var usage = new Usage 
                                            { 
                                                Total = aiResponse.TotalTokens,
                                                Input = aiResponse.InputTokens, 
                                                Output = aiResponse.OutputTokens
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
                            
                            // Store tracker for feedback
                            var messageId = Interlocked.Increment(ref messageCounter);
                            trackerCache[messageId] = aiConfigTracker;
                            
                            httpContext.Response.ContentType = "application/json";
                            await httpContext.Response.WriteAsync(JsonSerializer.Serialize(new { 
                                response = response,
                                model = modelName,
                                enabled = enabled,
                                messageId = messageId
                            }));
                        });
                    });
                    
                    app.Map("/stream", streamApp =>
                    {
                        streamApp.Run(async context =>
                        {
                            context.Response.ContentType = "text/event-stream";
                            context.Response.Headers["Cache-Control"] = "no-cache";
                            context.Response.Headers["Connection"] = "keep-alive";
                            
                            // Send existing messages
                            foreach (var msg in logMessages)
                            {
                                var escapedMsg = msg.Replace("\n", "\\n").Replace("\r", "");
                                await context.Response.WriteAsync($"data: {escapedMsg}\n\n");
                                await context.Response.Body.FlushAsync();
                            }
                            
                            // Register this connection
                            activeConnections.Add(context.Response);
                            
                            // Keep connection alive
                            try
                            {
                                while (!context.RequestAborted.IsCancellationRequested)
                                {
                                    await Task.Delay(1000);
                                    await context.Response.WriteAsync(":keepalive\n\n");
                                    await context.Response.Body.FlushAsync();
                                }
                            }
                            catch { }
                        });
                    });
                })
                .Build();

            host.Run();
        }
    }
}