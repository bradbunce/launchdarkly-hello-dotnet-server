# LaunchDarkly AI Config Demo - .NET Server

A comprehensive demonstration of LaunchDarkly's AI Config capabilities, showcasing how to dynamically control AI model selection, track performance metrics, and gather user feedback—all through feature flags.

## 🎯 What This App Does

This application demonstrates a production-ready AI chatbot that uses LaunchDarkly to:

- **Dynamically switch between AI providers** (OpenRouter, Cohere, Mistral AI) without code changes
- **A/B test different AI models** to find the best performance and user satisfaction
- **Track comprehensive metrics** including latency, token usage, success rates, and user feedback
- **Control feature visibility** with real-time feature flag updates
- **Monitor AI performance** across different models and configurations

## ✨ Key Features

### AI Config Management
- Switch AI models in real-time through LaunchDarkly dashboard
- Support for multiple AI providers (OpenRouter, Cohere, Mistral AI)
- Automatic provider detection based on model name
- Real-time model badge updates when configuration changes

### Metrics & Analytics (Backend)
- **Duration Tracking**: Measures AI response latency in milliseconds
- **Token Tracking**: Monitors input, output, and total token usage
- **Success Tracking**: Records successful AI generations
- **Feedback Tracking**: Captures user satisfaction via thumbs up/down

### OpenTelemetry Observability (Backend)
The backend uses LaunchDarkly's Observability plugin to export comprehensive telemetry:

#### Distributed Tracing (Spans)
- **Chat Request Spans**: Tracks the full lifecycle of each chat request
- **AI Provider Spans**: Separate spans for OpenRouter, Cohere, and Mistral API calls
- **Span Attributes**: Includes AI model, provider, message length, token counts, HTTP status codes
- **Error Tracking**: Automatically records exceptions with error tags

#### Metrics
- **Counters**:
  - `sdk.initialization` - SDK startup success/failure
  - `ai.config.evaluation` - AI Config enabled/disabled counts
  - `chat.requests` - Total chat requests
  - `chat.errors` - Simulated error count (10% random errors)
  - `ai.generation.success` - Successful AI generations by model
  - `ai.generation.errors` - Failed AI generations by model
  - `feedback.received` - User feedback (positive/negative)
  - `feedback.tracker_not_found` - Missing feedback trackers

- **Gauges**:
  - `ai.generation.latency` - AI request latency in milliseconds
  - `ai.tokens.input` - Input tokens used per request
  - `ai.tokens.output` - Output tokens generated per request
  - `ai.tokens.total` - Total tokens per request

#### Logs
- **Structured Logging**: All logs include attributes (model, latency, tokens, endpoints)
- **Log Levels**: Information, Warning, Error
- **Key Events**:
  - SDK initialization
  - AI Config changes
  - Chat requests and responses
  - User feedback
  - Errors and exceptions

#### Random Error Generation
- 10% of chat requests randomly fail for observability testing
- Errors are recorded via `Observe.RecordException()` with metadata
- Helps demonstrate error tracking and alerting capabilities

### Observability (Frontend)
- **Session Replay**: Records user interactions for debugging and analysis
- **Network Recording**: Captures all HTTP requests with headers and body
- **Error Tracking**: Automatically captures and reports errors (25% demo error rate)
- **Application Metadata**: Tracks application ID and version
- **Random User Sessions**: Each page load creates a new user with unique GUID

### Real-Time Updates
- Server-Sent Events (SSE) for live console output
- Streaming AI Config updates
- Feature flag changes reflected instantly in the UI
- No page refresh required

### Feature Flags
- **AI Config**: Enable/disable the chatbot dynamically
- **Console Visibility**: Show/hide debug console output
- **Sample Feature**: Demonstrates feature flag evaluation with visual banner

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

### OpenTelemetry Observability (Backend)

The backend exports comprehensive telemetry data via OpenTelemetry:

#### Distributed Tracing
View distributed traces showing the complete request flow:
- Parent span: `chat.request` (the HTTP endpoint handler)
- Child spans: `openrouter.chat.completions`, `cohere.chat`, or `mistral.chat.completions` (AI provider calls)
- Each span includes:
  - Duration (how long each operation took)
  - Attributes (model, provider, tokens, status codes)
  - Error information (if the operation failed)

#### Metrics Dashboard
Create dashboards in your observability platform (Datadog, New Relic, Honeycomb, etc.) to visualize:
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
- **LaunchDarkly Observability Plugin 0.3.0**: OpenTelemetry integration
- **Server-Sent Events (SSE)**: Real-time updates
- **OpenTelemetry**: Distributed tracing, metrics, and logs

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

## 🧪 Testing Different Models

1. **Create variations** in your `sample-ai-config`:
   - Variation A: `gpt-4` (via OpenRouter)
   - Variation B: `command-r-plus` (via Cohere)
   - Variation C: `mistral-large` (via Mistral AI)

2. **Set up targeting rules** to A/B test:
   - 33% of users get Variation A
   - 33% of users get Variation B
   - 34% of users get Variation C

3. **Monitor metrics** to see which model performs best:
   - Fastest response time?
   - Best user satisfaction?
   - Most cost-effective token usage?

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

The LaunchDarkly Observability plugin exports OpenTelemetry data. To send this data to your observability platform:

### Option 1: OpenTelemetry Collector
1. Set up an [OpenTelemetry Collector](https://opentelemetry.io/docs/collector/)
2. Configure exporters for your platform (Datadog, New Relic, Honeycomb, etc.)
3. The plugin automatically exports to the collector

### Option 2: Direct Export
Configure environment variables for direct export:
```bash
OTEL_EXPORTER_OTLP_ENDPOINT=https://your-platform.com
OTEL_EXPORTER_OTLP_HEADERS=api-key=your-api-key
```

### Supported Platforms
- Datadog
- New Relic
- Honeycomb
- Grafana Cloud
- AWS X-Ray
- Google Cloud Trace
- Azure Monitor
- Any OpenTelemetry-compatible platform

## 📚 Learn More

- [LaunchDarkly AI Configs Documentation](https://docs.launchdarkly.com/home/ai-configs)
- [LaunchDarkly .NET Server SDK](https://docs.launchdarkly.com/sdk/server-side/dotnet)
- [LaunchDarkly .NET AI SDK](https://docs.launchdarkly.com/sdk/ai/dotnet)
- [LaunchDarkly Observability Plugin](https://docs.launchdarkly.com/sdk/features/observability)
- [LaunchDarkly JavaScript SDK](https://docs.launchdarkly.com/sdk/client-side/javascript)
- [LaunchDarkly Observability](https://docs.launchdarkly.com/home/observability)
- [LaunchDarkly Session Replay](https://docs.launchdarkly.com/home/observability/session-replay)
- [OpenTelemetry Documentation](https://opentelemetry.io/docs/)

## 🤝 Contributing

This is a demonstration application. Feel free to fork and modify for your own use cases!

## 📄 License

See LICENSE.txt for details.

---

**Built with ❤️ using LaunchDarkly AI Configs**
