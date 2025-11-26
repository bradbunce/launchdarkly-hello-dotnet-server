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

### Metrics & Analytics
- **Duration Tracking**: Measures AI response latency in milliseconds
- **Token Tracking**: Monitors input, output, and total token usage
- **Success Tracking**: Records successful AI generations
- **Feedback Tracking**: Captures user satisfaction via thumbs up/down

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

3. **Configure your environment variables**
   
   Edit `.env` and add your keys:
   ```bash
   # Required: LaunchDarkly SDK key
   LAUNCHDARKLY_SDK_KEY=sdk-your-key-here
   
   # Optional: Feature flag key (defaults to "sample-feature")
   LAUNCHDARKLY_FLAG_KEY=sample-feature
   
   # AI Provider API Keys (at least one required)
   OPENROUTER_API_KEY=sk-or-v1-your-key-here
   COHERE_API_KEY=your-cohere-key-here
   MISTRAL_API_KEY=your-mistral-key-here
   ```

4. **Build and run with Docker**
   ```bash
   docker build -t launchdarkly-hello-dotnet-server .
   docker run --name "LaunchDarkly-Hello-DotNet-Server" -p 5000:5000 --env-file .env launchdarkly-hello-dotnet-server
   ```

5. **Access the application**
   
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

## 📊 Viewing Metrics

All AI interactions are tracked and sent to LaunchDarkly. View your metrics in the LaunchDarkly dashboard:

1. Navigate to your AI Config (`sample-ai-config`)
2. Click on the **Monitoring** tab
3. View metrics across all variations:
   - **Duration**: Average response latency
   - **Tokens**: Token usage (input/output/total)
   - **Success Rate**: Percentage of successful generations
   - **Feedback**: User satisfaction (thumbs up/down ratio)

## 🏗️ Architecture

### Technology Stack
- **.NET 8.0**: Modern web framework
- **LaunchDarkly Server SDK 8.10.4**: Feature flag management
- **LaunchDarkly AI SDK 0.9.1**: AI Config and metrics tracking
- **Bootstrap 5.3**: UI framework
- **Server-Sent Events (SSE)**: Real-time updates

### Context Configuration
The app uses a custom LaunchDarkly context:
- **Kind**: `ld-sdk`
- **Key**: `dot-net-server`
- **Attributes**: `sdkVersion` (tracks the SDK version in use)

This allows you to target specific SDK versions or server instances with your feature flags.

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
| `LAUNCHDARKLY_SDK_KEY` | Yes | - | Your LaunchDarkly SDK key |
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

## 📚 Learn More

- [LaunchDarkly AI Configs Documentation](https://docs.launchdarkly.com/home/ai-configs)
- [LaunchDarkly .NET Server SDK](https://docs.launchdarkly.com/sdk/server-side/dotnet)
- [LaunchDarkly .NET AI SDK](https://docs.launchdarkly.com/sdk/ai/dotnet)

## 🤝 Contributing

This is a demonstration application. Feel free to fork and modify for your own use cases!

## 📄 License

See LICENSE.txt for details.

---

**Built with ❤️ using LaunchDarkly AI Configs**
