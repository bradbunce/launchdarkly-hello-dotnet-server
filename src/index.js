import * as LDClient from 'launchdarkly-js-client-sdk';
import 'bootstrap/dist/css/bootstrap.min.css';
import 'bootstrap/dist/js/bootstrap.bundle.min.js';
import Observe from '@launchdarkly/observability';
import SessionReplay from '@launchdarkly/session-replay';

// DOM elements
const consoleDiv = document.getElementById('console');
const chatMessages = document.getElementById('chatMessages');
const chatInput = document.getElementById('chatInput');
const sendBtn = document.getElementById('sendBtn');
const modelBadge = document.getElementById('modelBadge');
const chatCard = document.getElementById('chatCard');
const consoleCard = document.getElementById('consoleCard');

// Initialize LaunchDarkly JavaScript SDK
let ldClient = null;

fetch('/config')
    .then(response => response.json())
    .then(config => {
        if (config.clientSideId && config.clientSideId !== '') {
            // Generate a unique GUID for each session
            const generateGuid = () => {
                return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function(c) {
                    const r = Math.random() * 16 | 0;
                    const v = c === 'x' ? r : (r & 0x3 | 0x8);
                    return v.toString(16);
                });
            };
            
            const ldContext = {
                kind: 'user',
                key: generateGuid()
            };
            
            const observePlugin = new Observe({
                networkRecording: {
                    enabled: true,
                    recordHeadersAndBody: true
                },
                environment: 'production',
                captureWebVitals: true
            });
            
            const sessionReplayPlugin = new SessionReplay({
                privacySetting: 'default'
            });
            
            ldClient = LDClient.initialize(config.clientSideId, ldContext, {
                application: {
                    id: config.applicationId,
                    version: config.applicationVersion
                },
                plugins: [observePlugin, sessionReplayPlugin]
            });
            
            ldClient.on('ready', function() {
                console.log('LaunchDarkly JavaScript SDK initialized');
                const showConsole = ldClient.variation('show-console', true);
                console.log('show-console flag:', showConsole);
                
                ldClient.on('change:show-console', function(newValue, oldValue) {
                    console.log('show-console changed:', oldValue, '->', newValue);
                });
            });
            
            ldClient.on('failed', function() {
                console.error('LaunchDarkly SDK failed to initialize');
            });
        } else {
            console.log('LaunchDarkly client-side ID not configured');
        }
    })
    .catch(error => {
        console.error('Failed to fetch config:', error);
    });

// Console SSE
const eventSource = new EventSource('/stream');
eventSource.onmessage = function(event) {
    const message = event.data.replace(/\\n/g, '\n');
    consoleDiv.textContent += message;
    consoleDiv.scrollTop = consoleDiv.scrollHeight;
};
eventSource.onerror = function() {
    consoleDiv.textContent += '\n[Connection lost]\n';
};

// AI Config SSE
const aiConfigStream = new EventSource('/ai-config-stream');
aiConfigStream.onmessage = function(event) {
    const data = JSON.parse(event.data);
    modelBadge.textContent = data.model;
    modelBadge.className = data.enabled ? 'badge bg-success' : 'badge bg-danger';
    chatCard.style.display = data.enabled ? 'block' : 'none';
};

// Console visibility SSE
const consoleStream = new EventSource('/console-visibility-stream');
consoleStream.onmessage = function(event) {
    const data = JSON.parse(event.data);
    consoleCard.style.display = data.visible ? 'block' : 'none';
};

// Chat functionality
let messageCounter = 0;

function addMessage(text, isUser, messageId) {
    const messageDiv = document.createElement('div');
    messageDiv.className = `message ${isUser ? 'user' : 'assistant'}`;
    
    const bubble = document.createElement('div');
    bubble.className = 'bubble';
    bubble.textContent = text;
    
    if (!isUser && messageId) {
        const container = document.createElement('div');
        container.appendChild(bubble);
        
        const feedbackDiv = document.createElement('div');
        feedbackDiv.className = 'feedback-buttons';
        
        const thumbsUp = document.createElement('button');
        thumbsUp.className = 'feedback-btn';
        thumbsUp.innerHTML = '👍';
        thumbsUp.onclick = () => sendFeedback(messageId, true, thumbsUp, thumbsDown);
        
        const thumbsDown = document.createElement('button');
        thumbsDown.className = 'feedback-btn';
        thumbsDown.innerHTML = '👎';
        thumbsDown.onclick = () => sendFeedback(messageId, false, thumbsUp, thumbsDown);
        
        feedbackDiv.appendChild(thumbsUp);
        feedbackDiv.appendChild(thumbsDown);
        container.appendChild(feedbackDiv);
        
        messageDiv.appendChild(container);
    } else {
        messageDiv.appendChild(bubble);
    }
    
    chatMessages.appendChild(messageDiv);
    chatMessages.scrollTop = chatMessages.scrollHeight;
}

async function sendFeedback(messageId, isPositive, thumbsUpBtn, thumbsDownBtn) {
    try {
        await fetch('/feedback', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ 
                messageId: messageId,
                positive: isPositive
            })
        });
        
        thumbsUpBtn.classList.toggle('active', isPositive);
        thumbsDownBtn.classList.toggle('active', !isPositive);
        thumbsUpBtn.disabled = true;
        thumbsDownBtn.disabled = true;
    } catch (error) {
        console.error('Failed to send feedback:', error);
    }
}

async function sendMessage() {
    const message = chatInput.value.trim();
    if (!message) return;
    
    addMessage(message, true);
    chatInput.value = '';
    sendBtn.disabled = true;
    
    // Randomly generate demo errors for observability (25% chance)
    if (Math.random() < 0.25 && ldClient) {
        const demoErrors = [
            { name: 'NetworkLatencyError', message: 'Response time exceeded 2000ms threshold' },
            { name: 'TokenLimitError', message: 'Message approaching token limit (90% of max)' },
            { name: 'RateLimitError', message: 'API rate limit at 80% capacity' },
            { name: 'CacheMissError', message: 'Cache miss - fetching from origin' },
            { name: 'ValidationError', message: 'Input contains special characters that may affect processing' }
        ];
        
        const randomError = demoErrors[Math.floor(Math.random() * demoErrors.length)];
        const error = new Error(randomError.message);
        error.name = randomError.name;
        
        console.error(`[Demo Error] ${randomError.name}:`, error);
        
        // Throw uncaught error asynchronously so it's captured by observability
        setTimeout(() => {
            throw error;
        }, 0);
    }
    
    try {
        const response = await fetch('/chat', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ message: message })
        });
        
        const data = await response.json();
        const msgId = ++messageCounter;
        addMessage(data.response, false, msgId);
    } catch (error) {
        console.error('Chat error:', error);
        addMessage('Error: Could not get response', false);
        
        // Send real errors to observability
        if (window.Observe && window.Observe.recordError) {
            window.Observe.recordError(error, 'Failed to send chat message', {
                component: 'Chat'
            });
        }
    } finally {
        sendBtn.disabled = false;
        chatInput.focus();
    }
}

sendBtn.addEventListener('click', sendMessage);
chatInput.addEventListener('keypress', (e) => {
    if (e.key === 'Enter') sendMessage();
});
