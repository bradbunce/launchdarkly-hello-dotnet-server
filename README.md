# LaunchDarkly .NET Server-Side Demo with AI Configs and Observability

A hands-on demonstration for developers showing how to integrate and use LaunchDarkly's .NET server-side SDK with AI Configs and Observability. Built as an AI chatbot, this demo showcases practical implementations of feature flags, AI configuration management, and comprehensive observability.

## 🎯 What You'll Learn

This demo teaches you how to integrate and use LaunchDarkly's SDKs in a real application. Through a working AI chatbot, you'll see practical implementations of:

### 1. 🤖 AI Configs SDK
**Learn how to:**
- Initialize the LaunchDarkly AI SDK (`LdAiClient`)
- Fetch AI Configs with `aiClient.Config()`
- Track AI-specific metrics: `TrackDuration()`, `TrackTokens()`, `TrackSuccess()`, `TrackFeedback()`
- Route to different AI providers based on config
- Cache trackers for feedback association
- Handle AI Config changes in real-time

**Code examples:** See `CallOpenRouter()`, `CallCohere()`, `CallMistral()` methods and the `/chat` endpoint

### 2. 🚩 Feature Flags SDK
**Learn how to:**
- Initialize the LaunchDarkly Server SDK (`LdClient`)
- Create and use contexts for targeting
- Evaluate boolean flags with `BoolVariation()`
- Listen for flag changes with `FlagTracker.FlagChanged`
- Broadcast flag updates to connected clients via SSE
- Use flags as kill switches and feature toggles

**Code examples:** See SDK initialization in `Main()` and flag change listeners

### 3. 📊 Observability Plugin
**Learn how to:**
- Add the Observability plugin to your SDK configuration
- Create distributed tracing spans with `Observe.StartActivity()`
- Record metrics with `Observe.RecordMetric()` and `Observe.RecordCount()`
- Log structured events with `Observe.RecordLog()`
- Track exceptions with `Observe.RecordException()`
- Tag spans with custom attributes
- Export telemetry to your observability platform

**Code examples:** See plugin initialization in `Main()`, spans in AI provider methods, and metrics throughout `/chat` endpoint

### 4. 🎨 Frontend SDK Integration
**Learn how to:**
- Initialize the JavaScript SDK with Session Replay and Observability plugins
- Create user contexts with random GUIDs
- Evaluate flags client-side
- Record custom errors for testing
- Capture network requests and user sessions

**Code examples:** See `src/index.js`

---

**For Developers:** This demo is designed to be read and understood. Check out `CODE_GUIDE.md` for a detailed walkthrough of how everything works, or dive into the code to see real-world SDK usage patterns.

## ✨ SDK Features Demonstrated

### AI Config SDK Usage
- `aiClient.Config()` - Fetch AI configurations
- `tracker.TrackDuration()` - Measure response latency
- `tracker.TrackTokens()` - Monitor token usage
- `tracker.TrackSuccess()` / `tracker.TrackError()` - Track outcomes
- `tracker.TrackFeedback()` - Capture user satisfaction
- Real-time config updates via flag change listeners

### Metrics & Analytics (Backend)
- **Duration Tracking**: Measures AI response latency in milliseconds
- **Token Tracking**: Monitors input, output, and total token usage
- **Success Tracking**: Records successful AI generations
- **Feedback Tracking**: Captures user satisfaction via thumbs up/down

### Observability Plugin SDK Usage
The backend demonstrates comprehensive observability instrumentation:

#### Distributed Tracing API
```csharp
// Create spans to track operations
using (var activity = Observe.StartActivity("operation-name", ActivityKind.Client, attributes))
{
    // Your code here
    activity?.SetTag("custom-tag", value);
}
```
- Parent/child span relationships
- Automatic error recording
- Custom span attributes

#### Metrics API
```csharp
// Record counter metrics
Observe.RecordCount("metric.name", 1, attributes);

// Record gauge metrics
Observe.RecordMetric("metric.name", value, attributes);
```
- Track request counts, error rates, success rates
- Measure latency, token usage, and custom metrics
- All metrics include structured attributes

#### Logging API
```csharp
// Structured logging with attributes
Observe.RecordLog("message", LogLevel.Information, attributes);
```
- Information, Warning, and Error levels
- Structured attributes for filtering
- Automatic correlation with traces

#### Exception Tracking API
```csharp
// Record exceptions with context
Observe.RecordException(exception, attributes);
```
- Automatic error capture
- Custom metadata for debugging
- 10% random error generation for demo purposes

### Frontend SDK Usage (JavaScript)
```javascript
// Initialize with plugins
const client = LDClient.initialize(clientSideId, context, {
  bootstrap: 'localStorage',
  inspectors: [
    sessionReplayInspector(),
    observabilityInspector()
  ]
});
```
- Session Replay plugin captures user interactions
- Observability plugin records network requests and errors
- Custom error generation for testing
- Flag evaluation tracking
- Random user contexts for testing targeting rules

### Real-Time Flag Updates
```csharp
// Listen for flag changes
client.FlagTracker.FlagChanged += (sender, changeArgs) => {
    // Re-evaluate and broadcast to clients
};
```
- Server-Sent Events (SSE) for real-time updates
- Flag change listeners with `FlagTracker`
- Broadcast updates to connected clients
- No polling required

### Context Targeting
```csharp
// Create contexts for targeting
var context = Context.Builder(ContextKind.Of("ld-sdk"), "dot-net-server")
    .Set("sdkVersion", sdkVersion)
    .Build();
```
- Custom context kinds
- Context attributes for targeting rules
- Server-side and client-side contexts

## 🚀 Getting Started

### Prerequisites

- Docker (for containerized deployment)
- LaunchDarkly account with SDK key
- API keys for at least one AI provider:
  - OpenRouter API key
  - Cohere API key
  - Mistral AI API key

### Installation

1. **Clone the repository**
   ```bash
   git clone <repository-url>
   cd launchdarkly-hello-dotnet-server
   ```

2. **Create your `.env` file**
   ```bash
   cp .env.example .env
   ```

3. **Build the frontend bundle**
   ```bash
   npm install
   npm run build
   ```

4. **Configure your environment variables**
   
   Edit `.env` and add your keys:
   ```bash
   # Required: LaunchDarkly Server SDK key
   LAUNCHDARKLY_SDK_KEY=sdk-your-key-here
   
   # Required: LaunchDarkly Client-Side ID (for frontend observability)
   LAUNCHDARKLY_CLIENT_SIDE_ID=your-client-side-id-here
   
   # Optional: Application metadata
   APPLICATION_ID=hello-dotnet-server
   APPLICATION_VERSION=1.0.0
   
   # Optional: Feature flag key (defaults to "sample-feature")
   LAUNCHDARKLY_FLAG_KEY=sample-feature
   
   # AI Provider API Keys (at least one required)
   OPENROUTER_API_KEY=sk-or-v1-your-key-here
   COHERE_API_KEY=your-cohere-key-here
   MISTRAL_API_KEY=your-mistral-key-here
   ```

5. **Build and run with Docker**
   ```bash
   docker build -t launchdarkly-hello-dotnet-server .
   docker run --name "LaunchDarkly-Hello-DotNet-Server" -p 5000:5000 --env-file .env launchdarkly-hello-dotnet-server
   ```

6. **Access the application**
   
   Open your browser to: `http://localhost:5000`

## 🎮 LaunchDarkly Configuration

### Required Feature Flags

Create these feature flags in your LaunchDarkly project:

#### 1. `sample-feature` (Boolean)
- **Purpose**: Demonstrates basic feature flag functionality
- **Effect**: Shows/hides the LaunchDarkly banner in console output
- **Default**: `false`

#### 2. `show-console` (Boolean)
- **Purpose**: Controls console output visibility in the UI
- **Effect**: Shows/hides the debug console panel
- **Default**: `true`

### Required AI Config

Create an AI Config in LaunchDarkly:

#### `sample-ai-config`
- **Purpose**: Controls which AI model the chatbot uses
- **Configuration**:
  - **Enabled**: Toggle to enable/disable the chatbot
  - **Model**: Select your AI model (e.g., `gpt-4`, `command-r-plus`, `mistral-large`)
  - **Provider**: Automatically detected based on model name

**Model Name Patterns**:
- Models containing `command` or `cohere` → Routes to Cohere
- Models containing `mistral` → Routes to Mistral AI
- All other models → Routes to OpenRouter (default)

## 📊 Viewing Metrics & Observability

### Backend Metrics (AI Config)

All AI interactions are tracked and sent to LaunchDarkly. View your metrics in the LaunchDarkly dashboard:

1. Navigate to your AI Config (`sample-ai-config`)
2. Click on the **Monitoring** tab
3. View metrics across all variations:
   - **Duration**: Average response latency
   - **Tokens**: Token usage (input/output/total)
   - **Success Rate**: Percentage of successful generations
   - **Feedback**: User satisfaction (thumbs up/down ratio)

### LaunchDarkly Observability (Backend)

LaunchDarkly provides comprehensive backend observability out of the box:

#### Distributed Tracing
View distributed traces showing the complete request flow:
- Parent span: `chat.request` (the HTTP endpoint handler)
- Child spans: `openrouter.chat.completions`, `cohere.chat`, or `mistral.chat.completions` (AI provider calls)
- Each span includes:
  - Duration (how long each operation took)
  - Attributes (model, provider, tokens, status codes)
  - Error information (if the operation failed)

#### Metrics Dashboard
LaunchDarkly automatically collects and visualizes metrics. You can also export to your preferred observability platform (Datadog, New Relic, Honeycomb, etc.) to create custom dashboards:
- **Request Rate**: `chat.requests` counter
- **Error Rate**: `chat.errors` and `ai.generation.errors` counters
- **Latency**: `ai.generation.latency` gauge (p50, p95, p99)
- **Token Usage**: `ai.tokens.total` gauge (track costs over time)
- **Success Rate**: Ratio of `ai.generation.success` to total requests
- **User Satisfaction**: Ratio of positive to negative feedback

#### Logs
All application logs are exported with structured attributes:
- Filter by `ai.model` to see logs for specific models
- Filter by `endpoint` to see logs for specific API routes
- Filter by log level (Information, Warning, Error)
- Correlate logs with traces using trace IDs

#### Alerts
Set up alerts based on metrics:
- Alert when error rate exceeds 5%
- Alert when p95 latency exceeds 5 seconds
- Alert when token usage spikes unexpectedly
- Alert when negative feedback exceeds 20%

### Frontend Observability

The frontend uses LaunchDarkly's JavaScript SDK with Observability and Session Replay plugins:

#### Session Replay
1. Navigate to **Observability** → **Session Replay** in LaunchDarkly
2. Filter by application: `hello-dotnet-server`
3. View recorded user sessions with:
   - Mouse movements and clicks
   - Form inputs (masked for privacy)
   - Network requests
   - Console logs
   - Errors and exceptions

#### Error Tracking
- Errors appear in **Observability** → **Errors**
- Demo errors are generated randomly (25% chance per message)
- Error types include:
  - NetworkLatencyError
  - TokenLimitError
  - RateLimitError
  - CacheMissError
  - ValidationError

#### Network Recording
- All HTTP requests are captured with headers and body
- View in session replay timeline
- Includes chat API calls, config fetches, and feedback submissions

### Random User Sessions

Each page load creates a new user with a unique GUID (e.g., `a3f2b8c1-4d5e-4f6a-9b8c-7d6e5f4a3b2c`), allowing you to:
- Track individual user journeys
- See separate session replays for each visit
- Analyze behavior across different sessions
- Test targeting rules with fresh contexts

## 🏗️ Architecture

### Technology Stack

**Backend:**
- **.NET 8.0**: Modern web framework
- **LaunchDarkly Server SDK 8.10.4**: Feature flag management
- **LaunchDarkly AI SDK 0.9.1**: AI Config and metrics tracking
- **LaunchDarkly Observability Plugin 0.3.0**: Backend observability (traces, metrics, logs)
- **Server-Sent Events (SSE)**: Real-time updates

**Frontend:**
- **LaunchDarkly JavaScript SDK 3.9.0**: Client-side feature flags
- **@launchdarkly/observability 0.4.9**: Network recording and error tracking
- **@launchdarkly/session-replay 0.4.9**: User session recording
- **Bootstrap 5.3**: UI framework
- **Webpack 5**: Module bundler (no CDN dependencies)

### Context Configuration

**Backend Context:**
- **Kind**: `ld-sdk`
- **Key**: `dot-net-server`
- **Attributes**: `sdkVersion` (tracks the SDK version in use)

**Frontend Context:**
- **Kind**: `user`
- **Key**: Random GUID (generated on each page load)
- **Anonymous**: `false` (treated as identified users)

This allows you to:
- Target specific SDK versions or server instances
- Track individual user sessions
- Test targeting rules with fresh user contexts

## 🔧 How It Works

### AI Model Selection Flow

1. User sends a message to the chatbot
2. App fetches the current AI Config from LaunchDarkly
3. Based on the model name, routes to the appropriate provider:
   - Cohere models → `https://api.cohere.ai/v1/chat`
   - Mistral models → `https://api.mistral.ai/v1/chat/completions`
   - Other models → `https://openrouter.ai/api/v1/chat/completions`
4. Tracks metrics (duration, tokens, success)
5. Returns response to user
6. User can provide feedback (👍/👎)

### Real-Time Updates

The app maintains three SSE connections:

1. **Console Stream** (`/stream`): Live console output
2. **AI Config Stream** (`/ai-config-stream`): Model configuration updates
3. **Console Visibility Stream** (`/console-visibility-stream`): Feature flag updates

When you change a flag or AI Config in LaunchDarkly, the SDK receives the update via streaming and broadcasts it to all connected clients.

## 🎨 User Interface

### Chat Interface
- Real-time AI responses
- Model badge showing current AI model
- Thumbs up/down feedback buttons
- Automatically hides when AI Config is disabled

### Console Output
- Live server logs
- AI Config changes
- Feature flag evaluations
- Token usage information
- Controlled by `show-console` feature flag

## 🧪 SDK Patterns to Explore

### 1. AI Config Variations
Create multiple variations in your `sample-ai-config` to see how the SDK handles different configurations:
```
Variation A: gpt-4 (OpenRouter)
Variation B: command-r-plus (Cohere)
Variation C: mistral-large (Mistral AI)
```

### 2. Targeting Rules
Use the SDK's context to implement targeting:
```csharp
// Target by SDK version
.Set("sdkVersion", sdkVersion)

// Target by environment
.Set("environment", "production")
```

### 3. Metrics Tracking
See how metrics flow from SDK to dashboard:
- Duration tracking with `TrackDuration()`
- Token tracking with `TrackTokens()`
- Feedback tracking with `TrackFeedback()`
- Compare metrics across variations

### 4. Real-Time Updates
Test the flag change listener:
- Change a flag in LaunchDarkly dashboard
- Watch the SDK receive the update via streaming
- See the UI update without page refresh

## 🐛 Troubleshooting

### Chatbot doesn't appear
- Check that `sample-ai-config` is enabled in LaunchDarkly
- Verify your AI provider API key is set in `.env`
- Check console logs for errors

### Metrics not showing in LaunchDarkly
- Ensure your LaunchDarkly SDK key is correct
- Check that the AI Config name matches (`sample-ai-config`)
- Verify the app can reach LaunchDarkly's servers

### Model not switching
- Changes take effect on the next chat message
- Check the model badge updates (should change immediately)
- Verify the flag change was saved in LaunchDarkly

### Console output not visible
- Check the `show-console` feature flag is enabled
- Refresh the page to reconnect SSE streams

## 📝 Environment Variables Reference

| Variable | Required | Default | Description |
|----------|----------|---------|-------------|
| `LAUNCHDARKLY_SDK_KEY` | Yes | - | Your LaunchDarkly Server SDK key |
| `LAUNCHDARKLY_CLIENT_SIDE_ID` | Yes | - | Your LaunchDarkly Client-Side ID (for frontend) |
| `APPLICATION_ID` | No | `hello-dotnet-server` | Application identifier for observability |
| `APPLICATION_VERSION` | No | `1.0.0` | Application version for observability |
| `LAUNCHDARKLY_FLAG_KEY` | No | `sample-feature` | Feature flag key to evaluate |
| `OPENROUTER_API_KEY` | No* | - | OpenRouter API key |
| `COHERE_API_KEY` | No* | - | Cohere API key |
| `MISTRAL_API_KEY` | No* | - | Mistral AI API key |
| `CI` | No | - | Set to any value to exit after initialization |

*At least one AI provider API key is required for the chatbot to function.

## 🚢 Deployment

### Docker
```bash
docker build -t launchdarkly-hello-dotnet-server .
docker run -d -p 5000:5000 --env-file .env --name ld-ai-demo launchdarkly-hello-dotnet-server
```

### Docker Compose
```yaml
version: '3.8'
services:
  app:
    build: .
    ports:
      - "5000:5000"
    env_file:
      - .env
    restart: unless-stopped
```

## � Connectineg to Your Observability Platform

LaunchDarkly's Observability plugin provides built-in telemetry collection and can export data to your preferred observability platform.

### Supported Platforms
- **Datadog** - Full-stack monitoring and APM
- **New Relic** - Application performance monitoring
- **Honeycomb** - Observability for complex systems
- **Grafana Cloud** - Open-source observability stack
- **AWS X-Ray** - Distributed tracing for AWS
- **Google Cloud Trace** - Tracing for GCP
- **Azure Monitor** - Microsoft Azure monitoring
- **Any OpenTelemetry-compatible platform**

### Configuration
Configure environment variables to export data to your platform:
```bash
OTEL_EXPORTER_OTLP_ENDPOINT=https://your-platform.com
OTEL_EXPORTER_OTLP_HEADERS=api-key=your-api-key
```

The plugin handles all the telemetry collection and export automatically.

## 📚 Learn More

- [LaunchDarkly AI Configs Documentation](https://docs.launchdarkly.com/home/ai-configs)
- [LaunchDarkly .NET Server SDK](https://docs.launchdarkly.com/sdk/server-side/dotnet)
- [LaunchDarkly .NET AI SDK](https://docs.launchdarkly.com/sdk/ai/dotnet)
- [LaunchDarkly Observability](https://docs.launchdarkly.com/home/observability)
- [LaunchDarkly Observability Plugin](https://docs.launchdarkly.com/sdk/features/observability)
- [LaunchDarkly Session Replay](https://docs.launchdarkly.com/home/observability/session-replay)
- [LaunchDarkly JavaScript SDK](https://docs.launchdarkly.com/sdk/client-side/javascript)

## 🤝 Contributing

This is a demonstration application. Feel free to fork and modify for your own use cases!

## 📄 License

See LICENSE.txt for details.

---

**Built with ❤️ using LaunchDarkly AI Configs**
