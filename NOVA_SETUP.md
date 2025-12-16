# Setting Up Amazon Nova-micro with LaunchDarkly AI Config

This guide explains how to configure your application to use Amazon Bedrock's Nova-micro model through LaunchDarkly AI Config.

## What Was Added

The backend service now supports Amazon Bedrock models (including Nova-micro) alongside the existing OpenRouter, Cohere, and Mistral providers.

### Code Changes

1. **New Provider Method**: Added `CallBedrock()` method to handle Amazon Bedrock API calls
2. **Updated Routing**: The `/chat` endpoint now detects "nova" in the model name and routes to Bedrock
3. **Environment Variables**: Added AWS credential support in `.env.example`

### Routing Logic

The application automatically routes to the correct provider based on model name:
- Models containing "command" or "cohere" → Cohere API
- Models containing "mistral" → Mistral AI API
- Models containing "nova" → Amazon Bedrock API
- All other models → OpenRouter API (default)

## Configuration Steps

### 1. Set Up AWS Credentials

Add your AWS credentials to the `.env` file:

```bash
# AWS credentials for Amazon Bedrock (Nova models)
AWS_ACCESS_KEY_ID=your-aws-access-key-here
AWS_SECRET_ACCESS_KEY=your-aws-secret-key-here
AWS_REGION=us-east-1
```

**Important**: Make sure your AWS IAM user has permissions for Amazon Bedrock. You'll need the `bedrock:InvokeModel` permission.

### 2. Configure LaunchDarkly AI Config

In your LaunchDarkly dashboard:

1. Navigate to your **AI Configs**
2. Open or create the `sample-ai-config` configuration
3. Set the **Model** field to one of these Nova model IDs:
   - `us.amazon.nova-micro-v1:0` (recommended for this demo)
   - `us.amazon.nova-lite-v1:0`
   - `us.amazon.nova-pro-v1:0`

### 3. Restart the Application

If the app is already running, restart it to pick up the new environment variables:

```bash
docker stop LaunchDarkly-Hello-DotNet-Server
docker rm LaunchDarkly-Hello-DotNet-Server
docker run --name "LaunchDarkly-Hello-DotNet-Server" -p 5000:5000 --env-file .env -d launchdarkly-hello-dotnet-server
```

### 4. Test the Integration

1. Open http://localhost:5000 in your browser
2. The model badge should show your Nova model (e.g., "us.amazon.nova-micro-v1:0")
3. Send a message to the chatbot
4. The backend will route to Amazon Bedrock automatically

## Monitoring & Observability

All Nova model interactions are tracked with LaunchDarkly's AI Config metrics:

- **Duration**: Response latency in milliseconds
- **Tokens**: Input, output, and total token usage
- **Success Rate**: Percentage of successful generations
- **Feedback**: User satisfaction (thumbs up/down)

You can view these metrics in the LaunchDarkly dashboard under your AI Config's **Monitoring** tab.

The observability plugin also creates distributed traces with the span name `bedrock.converse`, allowing you to track:
- Request flow from `/chat` endpoint to Bedrock API
- Error rates and status codes
- Custom attributes (model, provider, region)

## Switching Between Models

One of the key benefits of using LaunchDarkly AI Config is the ability to switch models without code changes:

1. Go to your LaunchDarkly dashboard
2. Update the `sample-ai-config` model field
3. The change propagates in real-time via Server-Sent Events
4. The next chat message will use the new model

You can switch between:
- Nova models (Bedrock)
- GPT models (OpenRouter)
- Command models (Cohere)
- Mistral models (Mistral AI)

All without restarting the application!

## Troubleshooting

### "AWS credentials not configured" error
- Verify `AWS_ACCESS_KEY_ID` and `AWS_SECRET_ACCESS_KEY` are set in `.env`
- Restart the Docker container after updating `.env`

### "Access Denied" or 403 errors
- Check that your AWS IAM user has `bedrock:InvokeModel` permission
- Verify the model ID is correct for your AWS region
- Ensure Amazon Bedrock is available in your selected region

### Model not switching
- Changes take effect on the next chat message (not immediately)
- Check the model badge in the UI updates when you change the config
- Verify the flag change was saved in LaunchDarkly

## Model IDs Reference

Amazon Bedrock Nova models use specific model IDs:

| Model | Model ID | Use Case |
|-------|----------|----------|
| Nova Micro | `us.amazon.nova-micro-v1:0` | Fast, cost-effective responses |
| Nova Lite | `us.amazon.nova-lite-v1:0` | Balanced performance |
| Nova Pro | `us.amazon.nova-pro-v1:0` | Complex reasoning tasks |

Make sure to use the full model ID including the region prefix and version.
