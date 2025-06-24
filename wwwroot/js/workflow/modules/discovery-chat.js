/**
 * Discovery Chat Module
 * Provides AI-powered workflow building from natural language
 * Uses SignalR DiscoveryHub for real-time communication
 */

export class DiscoveryChat {
    constructor(options) {
        this.options = options || {};
        this.projectId = options.projectId;
        this.workflowManager = options.workflowManager;
        this.canvasRenderer = options.canvasRenderer;
        this.containerSelector = options.containerSelector || '#discovery-chat-container';
        
        // SignalR connection
        this.connection = null;
        this.sessionId = null;
        this.isConnected = false;
        this.isProcessing = false;
        
        // UI elements
        this.container = null;
        this.chatMessages = null;
        this.inputField = null;
        this.sendButton = null;
        this.stopButton = null;
        this.statusIndicator = null;
        
        // Chat history
        this.messages = [];
        this.currentDiscovery = null;
        
        // Callbacks
        this.onWorkflowSuggested = options.onWorkflowSuggested || null;
        this.onWorkflowStepAdded = options.onWorkflowStepAdded || null;
        
        // Store the init promise so callers can await it
        this.initPromise = this.init();
    }
    
    async init() {
        try {
            console.log('DiscoveryChat init() started');
            this.createUI();
            console.log('UI created');
            await this.initializeSignalR();
            console.log('SignalR initialized');
            this.bindEvents();
            console.log('Events bound');
            
            console.log('Discovery Chat initialized successfully');
        } catch (error) {
            console.error('Failed to initialize Discovery Chat:', error);
            this.showError('Failed to initialize Discovery Chat: ' + error.message);
        }
    }
    
    createUI() {
        const container = document.querySelector(this.containerSelector);
        if (!container) {
            throw new Error(`Container not found: ${this.containerSelector}`);
        }
        
        container.innerHTML = `
            <div class="discovery-chat-wrapper">
                <div class="chat-header">
                    <h5 class="mb-0">
                        <i class="fas fa-brain text-primary me-2"></i>
                        AI Workflow Discovery
                    </h5>
                    <div class="status-indicator">
                        <span class="status-text">Ready</span>
                        <div class="status-dot bg-success"></div>
                    </div>
                </div>
                
                <div class="chat-messages" id="discovery-chat-messages">
                    <div class="welcome-message">
                        <div class="alert alert-info">
                            <i class="fas fa-info-circle me-2"></i>
                            <strong>Welcome to AI Workflow Discovery!</strong><br>
                            Describe what you want to accomplish in natural language, and I'll help you build a workflow.
                            <br><br>
                            <small><strong>Examples:</strong><br>
                            • "I need to scrape product data from a website"<br>
                            • "Send daily reports via email"<br>
                            • "Process CSV files and save to database"</small>
                        </div>
                    </div>
                </div>
                
                <div class="chat-input-wrapper">
                    <div class="input-group">
                        <textarea class="form-control" id="discovery-input" 
                                placeholder="Describe what workflow you want to create..." 
                                rows="2"></textarea>
                        <button class="btn btn-primary" id="discovery-send" type="button">
                            <i class="fas fa-paper-plane"></i>
                            Send
                        </button>
                        <button class="btn btn-danger d-none" id="discovery-stop" type="button">
                            <i class="fas fa-stop"></i>
                            Stop
                        </button>
                    </div>
                    <div class="input-actions mt-2">
                        <button class="btn btn-sm btn-outline-secondary" id="clear-chat">
                            <i class="fas fa-trash me-1"></i>Clear Chat
                        </button>
                        <button class="btn btn-sm btn-outline-info" id="get-components">
                            <i class="fas fa-puzzle-piece me-1"></i>Available Components
                        </button>
                    </div>
                </div>
            </div>
            
            <style>
                .discovery-chat-wrapper {
                    height: 100%;
                    display: flex;
                    flex-direction: column;
                    border: 1px solid #dee2e6;
                    border-radius: 0.375rem;
                    background: white;
                }
                
                .chat-header {
                    padding: 1rem;
                    border-bottom: 1px solid #dee2e6;
                    display: flex;
                    justify-content: space-between;
                    align-items: center;
                    background: #f8f9fa;
                }
                
                .status-indicator {
                    display: flex;
                    align-items: center;
                    gap: 0.5rem;
                }
                
                .status-dot {
                    width: 8px;
                    height: 8px;
                    border-radius: 50%;
                    animation: pulse 2s infinite;
                }
                
                .chat-messages {
                    flex: 1;
                    overflow-y: auto;
                    padding: 1rem;
                    max-height: 400px;
                    min-height: 200px;
                }
                
                .chat-input-wrapper {
                    padding: 1rem;
                    border-top: 1px solid #dee2e6;
                    background: #f8f9fa;
                }
                
                .message {
                    margin-bottom: 1rem;
                    display: flex;
                    gap: 0.75rem;
                }
                
                .message.user {
                    justify-content: flex-end;
                }
                
                .message.assistant {
                    justify-content: flex-start;
                }
                
                .message-content {
                    max-width: 80%;
                    padding: 0.75rem 1rem;
                    border-radius: 1rem;
                    word-wrap: break-word;
                }
                
                .message.user .message-content {
                    background: #007bff;
                    color: white;
                    border-bottom-right-radius: 0.25rem;
                }
                
                .message.assistant .message-content {
                    background: #e9ecef;
                    color: #212529;
                    border-bottom-left-radius: 0.25rem;
                }
                
                .message-timestamp {
                    font-size: 0.75rem;
                    color: #6c757d;
                    margin-top: 0.25rem;
                }
                
                .workflow-suggestion {
                    border: 1px solid #28a745;
                    border-radius: 0.5rem;
                    padding: 1rem;
                    margin: 1rem 0;
                    background: #f8fff9;
                }
                
                .workflow-step {
                    padding: 0.5rem;
                    margin: 0.25rem 0;
                    border-left: 3px solid #007bff;
                    background: #f8f9fa;
                    border-radius: 0.25rem;
                    animation: slideIn 0.3s ease-out;
                }
                
                .progress-indicator {
                    display: flex;
                    align-items: center;
                    gap: 0.5rem;
                    font-size: 0.875rem;
                    color: #6c757d;
                }
                
                .spinner-border-sm {
                    width: 1rem;
                    height: 1rem;
                }
                
                @keyframes pulse {
                    0% { opacity: 1; }
                    50% { opacity: 0.5; }
                    100% { opacity: 1; }
                }
                
                @keyframes slideIn {
                    from {
                        opacity: 0;
                        transform: translateX(-20px);
                    }
                    to {
                        opacity: 1;
                        transform: translateX(0);
                    }
                }
                
                .bg-processing { background-color: #ffc107 !important; }
                .bg-error { background-color: #dc3545 !important; }
            </style>
        `;
        
        // Cache UI elements
        this.container = container.querySelector('.discovery-chat-wrapper');
        this.chatMessages = container.querySelector('#discovery-chat-messages');
        this.inputField = container.querySelector('#discovery-input');
        this.sendButton = container.querySelector('#discovery-send');
        this.stopButton = container.querySelector('#discovery-stop');
        this.statusIndicator = container.querySelector('.status-indicator');
    }
    
    async initializeSignalR() {
        try {
            // Initialize SignalR connection
            this.connection = new signalR.HubConnectionBuilder()
                .withUrl('/discoveryHub')
                .withAutomaticReconnect()
                .build();
            
            // Set up event handlers
            this.setupSignalREvents();
            
            // Start connection
            await this.connection.start();
            this.isConnected = true;
            this.updateStatus('Connected', 'success');
            
            // Join discovery session
            if (this.projectId) {
                await this.connection.invoke('JoinDiscoverySession', this.projectId);
            }
            
        } catch (error) {
            console.error('SignalR connection failed:', error);
            this.updateStatus('Connection Failed', 'error');
            throw error;
        }
    }
    
    setupSignalREvents() {
        // Connection events
        this.connection.onreconnecting(() => {
            this.updateStatus('Reconnecting...', 'processing');
        });
        
        this.connection.onreconnected(() => {
            this.updateStatus('Connected', 'success');
        });
        
        this.connection.onclose(() => {
            this.isConnected = false;
            this.updateStatus('Disconnected', 'error');
        });
        
        // Discovery events
        this.connection.on('DiscoverySessionJoined', (data) => {
            this.sessionId = data.sessionId;
            console.log('Joined discovery session:', data);
        });
        
        this.connection.on('DiscoveryProcessingStarted', (data) => {
            this.isProcessing = true;
            this.updateStatus('Processing...', 'processing');
            this.toggleInputs(false);
            this.addProgressMessage('Starting discovery process...');
        });
        
        this.connection.on('IntentAnalysisStarted', (data) => {
            this.addProgressMessage('Analyzing your request...');
        });
        
        this.connection.on('ComponentMatchingStarted', (data) => {
            this.addProgressMessage('Finding matching tools and adapters...');
        });
        
        this.connection.on('WorkflowBuildingStarted', (data) => {
            this.addProgressMessage('Building workflow structure...');
        });
        
        this.connection.on('WorkflowSuggestionReceived', (data) => {
            this.handleWorkflowSuggestion(data);
        });
        
        this.connection.on('WorkflowStepAdded', (data) => {
            this.handleWorkflowStepAdded(data);
        });
        
        this.connection.on('DiscoveryCompleted', (data) => {
            this.isProcessing = false;
            this.updateStatus('Ready', 'success');
            this.toggleInputs(true);
            this.addProgressMessage(`Discovery completed! Created workflow with ${data.stepsCount} steps (${(data.confidence * 100).toFixed(1)}% confidence)`, 'success');
        });
        
        this.connection.on('DiscoveryError', (data) => {
            this.isProcessing = false;
            this.updateStatus('Error', 'error');
            this.toggleInputs(true);
            this.addMessage('assistant', `Error: ${data.error}`, new Date());
        });
        
        this.connection.on('DiscoveryCancelled', (data) => {
            this.isProcessing = false;
            this.updateStatus('Ready', 'success');
            this.toggleInputs(true);
            this.addProgressMessage('Discovery cancelled by user', 'warning');
        });
        
        this.connection.on('AvailableComponentsReceived', (data) => {
            this.showAvailableComponents(data);
        });

        // Step testing events
        this.connection.on('StepTestStarted', (data) => {
            this.addProgressMessage(`Testing step ${data.stepId} (${data.stepType})...`);
        });

        this.connection.on('StepTestCompleted', (data) => {
            this.handleStepTestResult(data);
        });

        this.connection.on('StepTestError', (data) => {
            this.addProgressMessage(`Step test failed: ${data.error}`, 'error');
        });

        this.connection.on('StepPerformanceMetrics', (data) => {
            this.showPerformanceMetrics(data);
        });

        this.connection.on('StepValidationResults', (data) => {
            this.showValidationResults(data);
        });

        this.connection.on('StepSuggestions', (data) => {
            this.showStepSuggestions(data);
        });

        this.connection.on('StepTestingStatusReceived', (data) => {
            console.log('Step testing status:', data);
        });
    }
    
    bindEvents() {
        console.log('bindEvents called');
        console.log('sendButton:', this.sendButton);
        console.log('inputField:', this.inputField);
        
        if (!this.sendButton || !this.inputField) {
            console.error('Critical UI elements not found!');
            return;
        }
        
        // Send button
        this.sendButton.addEventListener('click', () => {
            console.log('Send button clicked');
            this.sendMessage();
        });
        
        // Stop button
        if (this.stopButton) {
            this.stopButton.addEventListener('click', () => {
                this.stopDiscovery();
            });
        }
        
        // Input field - Enter to send
        this.inputField.addEventListener('keydown', (e) => {
            if (e.key === 'Enter' && !e.shiftKey) {
                e.preventDefault();
                this.sendMessage();
            }
        });
        
        // Clear chat
        const clearButton = document.getElementById('clear-chat');
        console.log('clearButton:', clearButton);
        if (clearButton) {
            clearButton.addEventListener('click', () => {
                console.log('Clear button clicked');
                this.clearChat();
            });
        }
        
        // Get components
        const componentsButton = document.getElementById('get-components');
        console.log('componentsButton:', componentsButton);
        if (componentsButton) {
            componentsButton.addEventListener('click', () => {
                console.log('Components button clicked');
                this.requestAvailableComponents();
            });
        }
        
        console.log('Event binding completed');
    }
    
    async sendMessage() {
        console.log('sendMessage called');
        const message = this.inputField.value.trim();
        console.log('Message:', message, 'isProcessing:', this.isProcessing, 'isConnected:', this.isConnected);
        if (!message || this.isProcessing || !this.isConnected) {
            console.log('Message blocked - empty message, processing, or not connected');
            return;
        }
        
        try {
            // Add user message to chat
            this.addMessage('user', message, new Date());
            
            // Clear input
            this.inputField.value = '';
            
            // Send to discovery orchestrator
            const currentWorkflow = this.workflowManager ? 
                JSON.stringify(this.workflowManager.getWorkflowDefinition()) : null;
            
            await this.connection.invoke('DiscoverWorkflow', 
                this.projectId, 
                message, 
                currentWorkflow, 
                { source: 'discovery-chat' }
            );
            
        } catch (error) {
            console.error('Failed to send message:', error);
            this.addMessage('assistant', `Error sending message: ${error.message}`, new Date());
        }
    }
    
    async stopDiscovery() {
        if (!this.isConnected || !this.isProcessing) {
            return;
        }
        
        try {
            await this.connection.invoke('StopDiscovery');
        } catch (error) {
            console.error('Failed to stop discovery:', error);
        }
    }
    
    async requestAvailableComponents() {
        console.log('requestAvailableComponents called');
        console.log('isConnected:', this.isConnected);
        if (!this.isConnected) {
            console.log('Not connected, aborting');
            return;
        }
        
        try {
            console.log('Invoking RequestAvailableComponents');
            await this.connection.invoke('RequestAvailableComponents');
        } catch (error) {
            console.error('Failed to request components:', error);
        }
    }
    
    addMessage(role, content, timestamp) {
        const messageEl = document.createElement('div');
        messageEl.className = `message ${role}`;
        
        const messageContent = document.createElement('div');
        messageContent.className = 'message-content';
        messageContent.textContent = content;
        
        const messageTime = document.createElement('div');
        messageTime.className = 'message-timestamp';
        messageTime.textContent = timestamp.toLocaleTimeString();
        
        messageContent.appendChild(messageTime);
        messageEl.appendChild(messageContent);
        
        this.chatMessages.appendChild(messageEl);
        this.scrollToBottom();
        
        // Store in history
        this.messages.push({ role, content, timestamp });
    }
    
    addProgressMessage(text, type = 'info') {
        const messageEl = document.createElement('div');
        messageEl.className = 'progress-indicator mb-2';
        
        const icon = type === 'success' ? 'fa-check text-success' : 
                    type === 'warning' ? 'fa-exclamation-triangle text-warning' : 
                    type === 'error' ? 'fa-times text-danger' : 'fa-spinner fa-spin text-primary';
        
        messageEl.innerHTML = `
            <i class="fas ${icon}"></i>
            <span>${text}</span>
        `;
        
        this.chatMessages.appendChild(messageEl);
        this.scrollToBottom();
    }
    
    handleWorkflowSuggestion(data) {
        const suggestion = data.response.workflowSuggestion;
        if (!suggestion) return;
        
        this.currentDiscovery = data.response;
        
        const suggestionEl = document.createElement('div');
        suggestionEl.className = 'workflow-suggestion';
        
        suggestionEl.innerHTML = `
            <h6 class="mb-3">
                <i class="fas fa-lightbulb text-warning me-2"></i>
                Workflow Suggestion: ${suggestion.name}
            </h6>
            <p class="mb-2">${suggestion.description}</p>
            <div class="mb-3">
                <small class="text-muted">
                    Confidence: <span class="badge bg-primary">${(suggestion.confidence * 100).toFixed(1)}%</span>
                    Steps: <span class="badge bg-info">${suggestion.workflowDefinition?.steps?.length || 0}</span>
                    Tools: <span class="badge bg-success">${suggestion.requiredTools?.length || 0}</span>
                </small>
            </div>
            <div class="workflow-steps" id="workflow-steps-${data.sessionId}">
                <!-- Steps will be added here -->
            </div>
            <div class="mt-3">
                <button class="btn btn-success btn-sm me-2" onclick="discoveryChat.applyWorkflow()">
                    <i class="fas fa-check me-1"></i>Apply to Designer
                </button>
                <button class="btn btn-outline-secondary btn-sm me-2" onclick="discoveryChat.saveWorkflow()">
                    <i class="fas fa-save me-1"></i>Save Workflow
                </button>
                <button class="btn btn-outline-info btn-sm" onclick="discoveryChat.testWorkflowSteps('${data.sessionId}')">
                    <i class="fas fa-flask me-1"></i>Test Steps
                </button>
            </div>
        `;
        
        this.chatMessages.appendChild(suggestionEl);
        this.scrollToBottom();
        
        // Call callback if provided
        if (this.onWorkflowSuggested) {
            this.onWorkflowSuggested(suggestion);
        }
    }
    
    handleWorkflowStepAdded(data) {
        const stepsContainer = document.getElementById(`workflow-steps-${data.sessionId}`);
        if (!stepsContainer) return;
        
        const step = data.update.step;
        const stepEl = document.createElement('div');
        stepEl.className = 'workflow-step';
        
        const stepType = step.type === 'tool' ? 'wrench' : 
                        step.type === 'adapter' ? 'plug' : 'cogs';
        const stepColor = step.type === 'tool' ? 'primary' : 
                         step.type === 'adapter' ? 'success' : 'info';
        
        stepEl.innerHTML = `
            <i class="fas fa-${stepType} text-${stepColor} me-2"></i>
            <strong>${step.name}</strong>
            <small class="text-muted ms-2">(${step.type})</small>
        `;
        
        stepsContainer.appendChild(stepEl);
        
        // Call callback if provided
        if (this.onWorkflowStepAdded) {
            this.onWorkflowStepAdded(step);
        }
    }
    
    showAvailableComponents(data) {
        const componentsEl = document.createElement('div');
        componentsEl.className = 'message assistant';
        
        const content = document.createElement('div');
        content.className = 'message-content';
        
        content.innerHTML = `
            <h6><i class="fas fa-puzzle-piece me-2"></i>Available Components</h6>
            
            <div class="mb-3">
                <strong class="text-primary">Tools (${data.tools.length}):</strong><br>
                <small>${data.tools.join(', ')}</small>
            </div>
            
            <div class="mb-3">
                <strong class="text-success">Adapters (${data.adapters.length}):</strong><br>
                <small>${data.adapters.join(', ')}</small>
            </div>
            
            <div class="mb-2">
                <strong class="text-info">Orchestrators (${data.orchestrators.length}):</strong><br>
                <small>${data.orchestrators.join(', ')}</small>
            </div>
            
            <div class="message-timestamp">
                ${new Date().toLocaleTimeString()}
            </div>
        `;
        
        componentsEl.appendChild(content);
        this.chatMessages.appendChild(componentsEl);
        this.scrollToBottom();
    }
    
    applyWorkflow() {
        if (!this.currentDiscovery || !this.workflowManager) {
            console.warn('No workflow to apply or workflow manager not available');
            return;
        }
        
        try {
            const workflowDef = this.currentDiscovery.workflowSuggestion.workflowDefinition;
            
            // Apply to workflow manager
            this.workflowManager.loadWorkflowDefinition(workflowDef);
            
            // Render on canvas
            if (this.canvasRenderer) {
                this.canvasRenderer.render();
            }
            
            this.addProgressMessage('Workflow applied to designer successfully!', 'success');
            
        } catch (error) {
            console.error('Failed to apply workflow:', error);
            this.addProgressMessage(`Failed to apply workflow: ${error.message}`, 'error');
        }
    }
    
    async saveWorkflow() {
        if (!this.currentDiscovery || !this.isConnected) {
            return;
        }
        
        try {
            const workflowDef = this.currentDiscovery.workflowSuggestion.workflowDefinition;
            const workflowName = this.currentDiscovery.workflowSuggestion.name;
            
            await this.connection.invoke('SaveDiscoveredWorkflow', 
                this.projectId, 
                JSON.stringify(workflowDef), 
                workflowName
            );
            
            this.addProgressMessage('Workflow saved successfully!', 'success');
            
        } catch (error) {
            console.error('Failed to save workflow:', error);
            this.addProgressMessage(`Failed to save workflow: ${error.message}`, 'error');
        }
    }
    
    clearChat() {
        this.chatMessages.innerHTML = `
            <div class="welcome-message">
                <div class="alert alert-info">
                    <i class="fas fa-info-circle me-2"></i>
                    <strong>Chat cleared!</strong> Start a new conversation.
                </div>
            </div>
        `;
        this.messages = [];
        this.currentDiscovery = null;
    }
    
    updateStatus(text, type) {
        const statusText = this.statusIndicator.querySelector('.status-text');
        const statusDot = this.statusIndicator.querySelector('.status-dot');
        
        statusText.textContent = text;
        
        // Remove all status classes
        statusDot.classList.remove('bg-success', 'bg-processing', 'bg-error');
        
        // Add appropriate class
        switch (type) {
            case 'success':
                statusDot.classList.add('bg-success');
                break;
            case 'processing':
                statusDot.classList.add('bg-processing');
                break;
            case 'error':
                statusDot.classList.add('bg-error');
                break;
        }
    }
    
    toggleInputs(enabled) {
        this.inputField.disabled = !enabled;
        this.sendButton.disabled = !enabled;
        
        if (enabled) {
            this.sendButton.classList.remove('d-none');
            this.stopButton.classList.add('d-none');
        } else {
            this.sendButton.classList.add('d-none');
            this.stopButton.classList.remove('d-none');
        }
    }
    
    scrollToBottom() {
        this.chatMessages.scrollTop = this.chatMessages.scrollHeight;
    }
    
    showError(message) {
        this.addMessage('assistant', `Error: ${message}`, new Date());
    }
    
    // Public methods for external integration
    setProjectId(projectId) {
        this.projectId = projectId;
    }
    
    setWorkflowManager(workflowManager) {
        this.workflowManager = workflowManager;
    }
    
    setCanvasRenderer(canvasRenderer) {
        this.canvasRenderer = canvasRenderer;
    }
    
    // Cleanup
    async destroy() {
        if (this.connection) {
            await this.connection.stop();
        }
        
        if (this.container) {
            this.container.remove();
        }
    }

    // Step testing methods
    async testWorkflowSteps(sessionId) {
        if (!this.currentDiscovery || !this.isConnected) {
            return;
        }

        const workflowDef = this.currentDiscovery.workflowSuggestion.workflowDefinition;
        if (!workflowDef || !workflowDef.steps) {
            this.addProgressMessage('No workflow steps to test', 'warning');
            return;
        }

        this.addProgressMessage(`Testing ${workflowDef.steps.length} workflow steps...`);

        for (const step of workflowDef.steps) {
            try {
                const testRequest = {
                    stepId: step.id || step.name,
                    stepType: step.type || 'tool',
                    configuration: step.configuration || {},
                    sampleData: { test: 'sample data' },
                    projectId: this.projectId,
                    sessionId: sessionId,
                    timeoutSeconds: 30
                };

                await this.connection.invoke('TestWorkflowStep', testRequest);
                
                // Small delay between tests
                await new Promise(resolve => setTimeout(resolve, 500));
                
            } catch (error) {
                console.error('Failed to test step:', step, error);
                this.addProgressMessage(`Failed to test step ${step.name}: ${error.message}`, 'error');
            }
        }
    }

    handleStepTestResult(data) {
        const result = data.result;
        const statusIcon = result.isSuccess ? 'fa-check text-success' : 'fa-times text-danger';
        const statusText = result.isSuccess ? 'passed' : 'failed';
        
        const resultEl = document.createElement('div');
        resultEl.className = 'step-test-result mt-2 p-2 border rounded';
        
        resultEl.innerHTML = `
            <div class="d-flex justify-content-between align-items-center">
                <span>
                    <i class="fas ${statusIcon} me-2"></i>
                    <strong>${data.stepId}</strong> (${data.stepType}) - ${statusText}
                </span>
                <small class="text-muted">${result.executionTimeMs.toFixed(0)}ms</small>
            </div>
            ${result.errorMessage ? `<small class="text-danger mt-1">${result.errorMessage}</small>` : ''}
        `;
        
        this.chatMessages.appendChild(resultEl);
        this.scrollToBottom();
    }

    showPerformanceMetrics(data) {
        const perf = data.performance;
        const metricsEl = document.createElement('div');
        metricsEl.className = 'performance-metrics mt-2 p-2 bg-light rounded';
        
        metricsEl.innerHTML = `
            <h6><i class="fas fa-tachometer-alt me-2"></i>Performance Metrics - ${data.stepId}</h6>
            <div class="row">
                <div class="col-sm-6">
                    <small><strong>Memory:</strong> ${perf.memoryUsageMB.toFixed(2)} MB</small>
                </div>
                <div class="col-sm-6">
                    <small><strong>Rating:</strong> ${perf.performanceRating}/5</small>
                </div>
            </div>
        `;
        
        this.chatMessages.appendChild(metricsEl);
        this.scrollToBottom();
    }

    showValidationResults(data) {
        const validation = data.validation;
        const validationEl = document.createElement('div');
        validationEl.className = 'validation-results mt-2 p-2 bg-light rounded';
        
        const issues = [...(validation.errors || []), ...(validation.warnings || [])];
        
        validationEl.innerHTML = `
            <h6><i class="fas fa-shield-alt me-2"></i>Validation - ${data.stepId}</h6>
            <div class="row">
                <div class="col-sm-4">
                    <small><strong>Execution:</strong> ${validation.isExecutionValid ? '✓' : '✗'}</small>
                </div>
                <div class="col-sm-4">
                    <small><strong>Output:</strong> ${validation.isOutputFormatValid ? '✓' : '✗'}</small>
                </div>
                <div class="col-sm-4">
                    <small><strong>Config:</strong> ${validation.isConfigurationValid ? '✓' : '✗'}</small>
                </div>
            </div>
            ${issues.length > 0 ? `<div class="mt-2"><small class="text-warning">${issues.join(', ')}</small></div>` : ''}
        `;
        
        this.chatMessages.appendChild(validationEl);
        this.scrollToBottom();
    }

    showStepSuggestions(data) {
        if (!data.suggestions || data.suggestions.length === 0) {
            return;
        }

        const suggestionsEl = document.createElement('div');
        suggestionsEl.className = 'step-suggestions mt-2 p-2 border-left border-info';
        
        suggestionsEl.innerHTML = `
            <h6><i class="fas fa-lightbulb text-warning me-2"></i>Suggestions for ${data.stepId}</h6>
            <ul class="mb-0">
                ${data.suggestions.map(suggestion => `<li><small>${suggestion}</small></li>`).join('')}
            </ul>
        `;
        
        this.chatMessages.appendChild(suggestionsEl);
        this.scrollToBottom();
    }

    async getStepTestingStatus() {
        if (!this.isConnected) {
            return;
        }

        try {
            await this.connection.invoke('GetStepTestingStatus');
        } catch (error) {
            console.error('Failed to get step testing status:', error);
        }
    }
}

// Global instance for button callbacks
window.discoveryChat = null;