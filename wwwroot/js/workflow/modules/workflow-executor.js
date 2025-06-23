/**
 * Workflow Executor Module
 * Modul pro spouštění workflow
 */
export class WorkflowExecutor {
    constructor(workflowManager) {
        this.workflowManager = workflowManager;
        this.currentExecutionId = null;
        this.executionCheckInterval = null;
        this.logEntries = []; // Store all log entries for saving to file
        
        // Test-related properties
        this.testMonitoringInterval = null;
        this.lastStepName = null;
        this.processedLogs = [];
        this.processedLogIds = [];
    }
    
    /**
     * Run workflow - test mode
     */
    async runWorkflow() {
        try {
            // First save workflow
            await this.workflowManager.saveWorkflow();
            
            // Show test modal instead of execution modal
            this.testWorkflow();
            
        } catch (error) {
            toastr.error('Musíte nejdříve uložit workflow');
        }
    }

    /**
     * Test workflow functionality
     */
    testWorkflow() {
        const projectId = this.workflowManager.projectId;
        
        if (!projectId) {
            toastr.error('Nelze najít ID projektu');
            return;
        }
        
        // Show test modal
        $('#workflowTestModal').modal('show');
        
        // Reset test state
        this.resetTestState();
        
        // Start the test
        this.startProjectTest(projectId);
    }
    
    /**
     * Reset test state
     */
    resetTestState() {
        // Clear intervals
        if (this.testMonitoringInterval) {
            clearInterval(this.testMonitoringInterval);
            this.testMonitoringInterval = null;
        }
        
        // Reset variables
        this.lastStepName = null;
        this.processedLogs = [];
        this.processedLogIds = [];
        
        // Reset UI
        $('#testProgressBar').css('width', '0%').text('0%');
        $('#testStatus').text('Připravuji test...');
        $('#testStepsCount').text('0 / 0');
        $('#testDuration').text('0s');
        $('#testLogs').html('<div class="text-warning">[SYSTEM] Inicializuji test workflow...</div>');
        $('#cancelTestBtn').show();
        $('#repeatTestBtn').hide();
        $('#closeTestBtn').hide();
        
        // Reset styling
        $('.info-box-icon').removeClass('bg-success bg-danger').addClass('bg-warning');
        $('#testStatusIcon').removeClass('fa-check fa-times').addClass('fa-cog fa-spin');
        $('#testProgressBar').removeClass('bg-success bg-danger').addClass('bg-warning progress-bar-animated');
    }
    
    /**
     * Start project test execution
     */
    async startProjectTest(projectId) {
        this.addTestLog('INFO', 'Zahájení validace před spuštěním testu...');
        
        // Phase 1: Check for default workflow orchestrator
        this.addTestLog('INFO', '🔍 Kontroluji výchozí workflow orchestrátor...');
        $('#testProgressBar').css('width', '10%').text('Orchestrátor...');
        const orchestratorCheck = await this.validateDefaultWorkflowOrchestrator();
        if (!orchestratorCheck.valid) {
            this.showTestError(orchestratorCheck.error);
            return;
        }
        
        const orchestrator = orchestratorCheck.orchestrator;
        const config = orchestratorCheck.config;
        this.addTestLog('SUCCESS', `✅ Nalezen orchestrátor: ${orchestrator.name} (ID: ${orchestrator.id})`);
        
        // Phase 2: Check AI server configuration and status
        this.addTestLog('INFO', '🔍 Kontroluji AI server...');
        $('#testProgressBar').css('width', '30%').text('AI Server...');
        const serverCheck = await this.validateAiServer(config.aiServerId);
        if (!serverCheck.valid) {
            this.showTestError(serverCheck.error);
            return;
        }
        
        const server = serverCheck.server;
        this.addTestLog('SUCCESS', `✅ AI server: ${server.name} (${server.type || 'neznámý typ'}) - ${server.status}`);
        
        // Phase 3: Check model availability
        this.addTestLog('INFO', '🔍 Kontroluji dostupnost modelu...');
        $('#testProgressBar').css('width', '50%').text('Model...');
        const modelCheck = await this.validateModel(config.aiServerId, config.defaultModelId);
        if (!modelCheck.valid) {
            this.showTestError(modelCheck.error);
            return;
        }
        
        this.addTestLog('SUCCESS', `✅ Model: ${config.defaultModelId} - ${modelCheck.status}`);
        
        // Phase 4: All validations passed, start the test
        this.addTestLog('INFO', '🚀 Všechny kontroly prošly, spouštím test...');
        $('#testProgressBar').css('width', '70%').text('Spouštím...');
        
        // Execute workflow in test mode
        const testData = {
            projectId: projectId,
            runName: `Test Run - ${new Date().toLocaleString('cs-CZ')}`,
            mode: "test",
            priority: "normal",
            testItemLimit: 5,
            enableDebugLogging: true,
            startedBy: "workflow-designer-test",
            metadata: {
                inputParameters: {
                    testMode: true,
                    maxItems: 5,
                    debug: true
                },
                source: 'workflow-designer',
                workflowId: this.workflowManager.workflowId || null
            }
        };
        
        this.addTestLog('INFO', `Spouštím projekt ${projectId} v testovacím režimu s orchestrátorem...`);
        
        $.ajax({
            url: `/api/projects/execute`,
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(testData),
            success: (response) => {
                if (response.success && response.data) {
                    const executionId = response.data.id;
                    this.addTestLog('SUCCESS', `Projekt úspěšně spuštěn s orchestrátorem. Execution ID: ${executionId}`);
                    $('#testProgressBar').css('width', '10%').text('10%');
                    
                    // Start monitoring execution
                    this.monitorTestExecution(executionId);
                } else {
                    this.showTestError(response.message || 'Chyba při spouštění testu');
                }
            },
            error: (xhr) => {
                let errorMsg = 'Nepodařilo se spustit test';
                try {
                    const errorResponse = JSON.parse(xhr.responseText);
                    if (errorResponse.message) {
                        errorMsg = errorResponse.message;
                    }
                    if (errorResponse.errors) {
                        errorMsg += ': ' + errorResponse.errors.join(', ');
                    }
                } catch (e) {}
                this.showTestError(errorMsg);
            }
        });
    }
    
    /**
     * Start execution
     */
    async startExecution() {
        const executionName = $('#executionName').val();
        const parametersText = $('#executionParameters').val();
        const enableDebug = $('#enableDebugLogging').is(':checked');
        
        // Validate JSON parameters
        let parameters = {};
        try {
            parameters = JSON.parse(parametersText);
        } catch (e) {
            toastr.error('Neplatné JSON parametry');
            return;
        }
        
        // Switch to progress view
        $('#executionSetup').hide();
        $('#executionProgress').show();
        $('#startExecutionBtn').hide();
        $('#cancelExecutionBtn').show();
        
        const workflowId = this.workflowManager.workflowId;
        
        if (!workflowId || workflowId === '00000000-0000-0000-0000-000000000000') {
            this.addLogEntry('❌ Workflow musí být nejdříve uloženo', 'error');
            toastr.error('Workflow musí být nejdříve uloženo');
            this.resetExecutionModal();
            return;
        }
        
        // For project workflow, we don't need to check nodes here
        // The project workflow is defined differently and validated on the server side
        
        // Start execution via API - using project execution format
        const requestData = {
            projectId: this.workflowManager.projectId,
            runName: executionName || `Test execution ${new Date().toLocaleString()}`,
            mode: "test",
            priority: "normal",
            testItemLimit: null,
            enableDebugLogging: enableDebug,
            startedBy: "user",
            metadata: {
                inputParameters: parameters,
                source: "workflow-designer"
            }
        };
        
        try {
            this.addLogEntry('📤 Odesílám požadavek na spuštění project workflow...', 'info');
            
            // Use project execution API instead of generic workflow API
            const response = await fetch('/api/projects/execute', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify(requestData)
            });
            
            this.addLogEntry(`📥 Odpověď serveru: ${response.status} ${response.statusText}`, 'info');
            
            if (!response.ok) {
                const errorText = await response.text();
                throw new Error(`Server error: ${response.status} - ${errorText}`);
            }
            
            const result = await response.json();
            
            if (result.id) {
                // Project execution returns the execution object directly
                this.currentExecutionId = result.id;
                this.addLogEntry('✅ Project workflow spuštěno s ID: ' + this.currentExecutionId, 'success');
                this.addLogEntry('🔄 Začínám sledovat průběh...', 'info');
                this.addLogEntry(`📊 Status: ${result.status}`, 'info');
                
                // Start polling for status - faster rate for better debugging
                this.executionCheckInterval = setInterval(() => this.checkExecutionStatus(), 1000);
            } else if (result.success && result.data) {
                // Handle old format response
                this.currentExecutionId = result.data.executionId || result.data.id;
                this.addLogEntry('✅ Workflow spuštěno s ID: ' + this.currentExecutionId, 'success');
                this.addLogEntry('🔄 Začínám sledovat průběh...', 'info');
                
                // Start polling for status - faster rate for better debugging
                this.executionCheckInterval = setInterval(() => this.checkExecutionStatus(), 1000);
            } else {
                const errorMsg = result.message || 'Neznámá chyba při spouštění workflow';
                this.addLogEntry(`❌ Chyba: ${errorMsg}`, 'error');
                if (result.errors && result.errors.length > 0) {
                    result.errors.forEach(err => this.addLogEntry(`  • ${err}`, 'error'));
                }
                toastr.error(errorMsg);
                this.showExecutionError(errorMsg);
            }
        } catch (error) {
            console.error('Execution failed:', error);
            const errorMsg = `Chyba při komunikaci se serverem: ${error.message}`;
            this.addLogEntry(`❌ ${errorMsg}`, 'error');
            this.addLogEntry(`  Stack: ${error.stack}`, 'error');
            toastr.error(errorMsg);
            this.showExecutionError(errorMsg);
        }
    }
    
    /**
     * Check execution status
     */
    async checkExecutionStatus() {
        if (!this.currentExecutionId) return;
        
        try {
            // Use project execution API for status check
            const response = await fetch(`/api/projects/execution/${this.currentExecutionId}`);
            
            if (!response.ok) {
                this.addLogEntry(`⚠️ Chyba při kontrole stavu: ${response.status}`, 'warning');
                return;
            }
            
            const result = await response.json();
            
            // Project execution returns the execution object directly
            if (result.id) {
                this.updateExecutionProgress(result);
                
                // Check if execution is complete
                if (result.status !== 'Running' && result.status !== 'Pending' && result.status !== 'InProgress') {
                    clearInterval(this.executionCheckInterval);
                    this.executionCheckInterval = null;
                    
                    this.addLogEntry(`🏁 Project workflow dokončeno se stavem: ${result.status}`, 'info');
                    
                    if (result.status === 'Completed' || result.status === 'Success') {
                        this.showExecutionSuccess();
                        // Show execution details
                        if (result.outputData) {
                            this.addLogEntry('📊 Výstupní data:', 'info');
                            this.addLogEntry(JSON.stringify(result.outputData, null, 2), 'code');
                        }
                    } else {
                        const errorMsg = result.errorMessage || 'Workflow selhalo bez uvedení důvodu';
                        this.showExecutionError(errorMsg);
                        if (result.errorStackTrace) {
                            this.addLogEntry('📋 Stack trace:', 'error');
                            this.addLogEntry(result.errorStackTrace, 'code');
                        }
                    }
                }
            } else if (result.success && result.data) {
                // Handle old format response
                this.updateExecutionProgress(result.data);
                
                // Check if execution is complete
                if (result.data.status !== 'Running' && result.data.status !== 'Pending') {
                    clearInterval(this.executionCheckInterval);
                    this.executionCheckInterval = null;
                    
                    this.addLogEntry(`🏁 Workflow dokončeno se stavem: ${result.data.status}`, 'info');
                    
                    if (result.data.status === 'Completed') {
                        this.showExecutionSuccess();
                        // Get detailed results
                        await this.checkExecutionDetails();
                    } else {
                        const errorMsg = result.data.message || 'Workflow selhalo bez uvedení důvodu';
                        this.showExecutionError(errorMsg);
                    }
                }
            } else {
                this.addLogEntry('⚠️ Server vrátil neplatnou odpověď', 'warning');
            }
        } catch (error) {
            console.error('Error checking execution status:', error);
            this.addLogEntry(`❌ Chyba při kontrole stavu: ${error.message}`, 'error');
        }
    }
    
    /**
     * Cancel execution
     */
    async cancelExecution() {
        if (!this.currentExecutionId) return;
        
        if (confirm('Opravdu chcete zrušit probíhající workflow?')) {
            try {
                const response = await fetch(`/api/workflow/cancel/${this.currentExecutionId}`, {
                    method: 'POST'
                });
                
                const result = await response.json();
                
                if (result.success) {
                    toastr.info('Workflow bylo zrušeno');
                    clearInterval(this.executionCheckInterval);
                    this.showExecutionError('Workflow bylo zrušeno uživatelem');
                }
            } catch (error) {
                toastr.error('Nepodařilo se zrušit workflow');
            }
        }
    }
    
    /**
     * Reset execution modal
     */
    resetExecutionModal() {
        $('#executionSetup').show();
        $('#executionProgress').hide();
        $('#executionResults').hide();
        $('#startExecutionBtn').show();
        $('#cancelExecutionBtn').hide();
        $('#executionParameters').val('{}');
        $('#executionSteps').empty();
        $('#executionLog').empty();
        this.currentExecutionId = null;
        this.logEntries = []; // Clear log entries
        
        if (this.executionCheckInterval) {
            clearInterval(this.executionCheckInterval);
            this.executionCheckInterval = null;
        }
    }
    
    /**
     * Update execution progress
     */
    updateExecutionProgress(execution) {
        // Handle project execution format
        if (execution.id) {
            // Calculate progress based on items processed
            const progress = execution.itemsProcessedCount && execution.totalItemsCount 
                ? Math.round((execution.itemsProcessedCount / execution.totalItemsCount) * 100)
                : 0;
            $('#progressBar').css('width', progress + '%').text(progress + '%');
            
            // Add status info
            this.addLogEntry(`📊 Status: ${execution.status}`, 'info');
            if (execution.itemsProcessedCount !== undefined) {
                this.addLogEntry(`📈 Zpracováno položek: ${execution.itemsProcessedCount}/${execution.totalItemsCount || '?'}`, 'info');
            }
            
            // Show execution log if available
            if (execution.executionLog && execution.executionLog !== this.lastExecutionLog) {
                this.lastExecutionLog = execution.executionLog;
                const logEntries = execution.executionLog.split('\n');
                logEntries.forEach(entry => {
                    if (entry.trim()) {
                        this.addLogEntry(entry, 'system');
                    }
                });
            }
            
            // Show current stage info
            if (execution.currentStage) {
                this.addLogEntry(`🎯 Aktuální fáze: ${execution.currentStage}`, 'info');
            }
            
            // Show duration
            if (execution.startedAt) {
                const duration = execution.durationSeconds || 
                    Math.round((new Date() - new Date(execution.startedAt)) / 1000);
                this.addLogEntry(`⏱️ Doba běhu: ${duration}s`, 'info');
            }
        } else {
            // Handle old workflow format
            const progress = Math.round(execution.progressPercentage || 0);
            $('#progressBar').css('width', progress + '%').text(progress + '%');
            
            // Add status info
            this.addLogEntry(`Status: ${execution.status}`, 'info');
            this.addLogEntry(`Krok ${execution.completedSteps}/${execution.totalSteps}: ${execution.currentStepName || 'Příprava'}`, 'info');
            
            // Update current step
            if (execution.currentStepId && execution.currentStepName) {
                // Mark previous steps as completed
                $('.execution-step.running').removeClass('running').addClass('completed');
                $('.execution-step.running i').removeClass('fa-spinner fa-spin').addClass('fa-check-circle');
                
                const stepExists = $(`#step-${execution.currentStepId}`).length > 0;
                if (!stepExists) {
                    this.addExecutionStep(execution.currentStepId, execution.currentStepName, 'running');
                    this.addLogEntry(`🚀 Spouštím: ${execution.currentStepName}`, 'info');
                }
            }
            
            // Add detailed message
            if (execution.message && execution.message !== 'Running') {
                this.addLogEntry(execution.message, 'info');
            }
        }
        
        // Show step details panel
        this.updateStepDetails(execution);
    }
    
    /**
     * Add execution step
     */
    addExecutionStep(stepId, stepName, status) {
        const stepHtml = `
            <div id="step-${stepId}" class="execution-step ${status}">
                <i class="fas fa-${status === 'running' ? 'spinner fa-spin' : 
                                 status === 'completed' ? 'check-circle' : 
                                 'times-circle'}"></i>
                ${stepName}
            </div>
        `;
        $('#executionSteps').append(stepHtml);
    }
    
    /**
     * Add log entry
     */
    addLogEntry(message, type = 'info') {
        const timestamp = new Date().toLocaleTimeString();
        const fullTimestamp = new Date().toISOString();
        
        // Store log entry for file saving
        this.logEntries.push({
            timestamp: fullTimestamp,
            type: type,
            message: message
        });
        
        const logHtml = `
            <div class="log-entry ${type}">
                [${timestamp}] ${message}
            </div>
        `;
        $('#executionLog').append(logHtml);
        $('#executionLog').scrollTop($('#executionLog')[0].scrollHeight);
    }
    
    /**
     * Show execution success
     */
    showExecutionSuccess() {
        // Keep progress visible but disable cancel button
        $('#cancelExecutionBtn').hide();
        $('#startExecutionBtn').show();
        
        // Add success message to log
        this.addLogEntry('✅ Workflow bylo úspěšně dokončeno!', 'success');
        
        // Save logs to file
        this.saveLogsToFile();
        
        // Show results section below progress
        $('#executionResults').show();
        $('#executionResultAlert')
            .removeClass('alert-danger')
            .addClass('alert-success')
            .html('<i class="fas fa-check-circle"></i> Workflow bylo úspěšně dokončeno!');
    }
    
    /**
     * Show execution error
     */
    showExecutionError(message) {
        // Keep progress visible but disable cancel button
        $('#cancelExecutionBtn').hide();
        $('#startExecutionBtn').show();
        
        // Add error to log
        this.addLogEntry(`❌ Chyba: ${message}`, 'error');
        
        // Save logs to file even on error
        this.saveLogsToFile();
        
        // Show error in results section
        $('#executionResults').show();
        $('#executionResultAlert')
            .removeClass('alert-success')
            .addClass('alert-danger')
            .html(`<i class="fas fa-exclamation-circle"></i> ${message}`);
    }
    
    /**
     * Update step details panel
     */
    updateStepDetails(status) {
        // Create or update step details section
        let $detailsSection = $('#stepDetailsSection');
        if ($detailsSection.length === 0) {
            const detailsHtml = `
                <div id="stepDetailsSection" class="mt-3 border rounded p-3 bg-light">
                    <h6 class="mb-2"><i class="fas fa-info-circle"></i> Detaily aktuálního kroku</h6>
                    <div id="stepDetailsContent"></div>
                </div>
            `;
            $('#executionSteps').after(detailsHtml);
            $detailsSection = $('#stepDetailsSection');
        }
        
        const detailsContent = `
            <div class="row">
                <div class="col-md-6">
                    <strong>Krok:</strong> ${status.currentStepName || 'N/A'}<br>
                    <strong>ID:</strong> ${status.currentStepId || 'N/A'}<br>
                    <strong>Stav:</strong> <span class="badge badge-${this.getStatusBadgeClass(status.status)}">${status.status}</span>
                </div>
                <div class="col-md-6">
                    <strong>Dokončeno:</strong> ${status.completedSteps}/${status.totalSteps}<br>
                    <strong>Čas spuštění:</strong> ${new Date(status.startedAt).toLocaleTimeString()}<br>
                    <strong>Odhad dokončení:</strong> ${status.estimatedCompletionTime ? new Date(status.estimatedCompletionTime).toLocaleTimeString() : 'N/A'}
                </div>
            </div>
        `;
        
        $('#stepDetailsContent').html(detailsContent);
    }
    
    /**
     * Get status badge class
     */
    getStatusBadgeClass(status) {
        switch (status) {
            case 'Running':
                return 'primary';
            case 'Completed':
                return 'success';
            case 'Failed':
                return 'danger';
            case 'Cancelled':
                return 'warning';
            default:
                return 'secondary';
        }
    }
    
    /**
     * Check execution details
     */
    async checkExecutionDetails() {
        if (!this.currentExecutionId) return;
        
        try {
            // Get detailed execution results
            const response = await fetch(`/api/workflow/execution/${this.currentExecutionId}/details`);
            if (response.ok) {
                const result = await response.json();
                if (result.success && result.data) {
                    this.showExecutionDetails(result.data);
                    this.addLogEntry('✅ Workflow dokončeno úspěšně!', 'success');
                }
            } else {
                this.addLogEntry('⚠️ Nepodařilo se načíst detaily výsledků', 'warning');
            }
        } catch (error) {
            console.error('Error getting execution details:', error);
            this.addLogEntry('❌ Chyba při načítání detailů: ' + error.message, 'error');
        }
    }
    
    /**
     * Show execution details
     */
    showExecutionDetails(details) {
        // Add step results to the output
        if (details.stepResults && details.stepResults.length > 0) {
            let outputHtml = '<h6 class="mt-3">Výsledky jednotlivých kroků:</h6>';
            outputHtml += '<div class="accordion" id="stepResultsAccordion">';
            
            details.stepResults.forEach((step, index) => {
                const collapseId = `collapse${index}`;
                outputHtml += `
                    <div class="card">
                        <div class="card-header" id="heading${index}">
                            <h2 class="mb-0">
                                <button class="btn btn-link btn-block text-left ${index > 0 ? 'collapsed' : ''}" 
                                        type="button" data-toggle="collapse" data-target="#${collapseId}">
                                    <i class="fas fa-${step.success ? 'check-circle text-success' : 'times-circle text-danger'}"></i>
                                    ${step.stepName} (${step.durationSeconds.toFixed(2)}s)
                                </button>
                            </h2>
                        </div>
                        <div id="${collapseId}" class="collapse ${index === 0 ? 'show' : ''}" 
                             data-parent="#stepResultsAccordion">
                            <div class="card-body">
                                <p><strong>Tool:</strong> ${step.toolId || 'N/A'}</p>
                                <p><strong>Čas:</strong> ${new Date(step.startedAt).toLocaleTimeString()} - ${new Date(step.completedAt).toLocaleTimeString()}</p>
                                ${step.errorMessage ? `<p class="text-danger"><strong>Chyba:</strong> ${step.errorMessage}</p>` : ''}
                                <div class="mt-2">
                                    <strong>Výstup:</strong>
                                    <pre class="bg-light p-2 rounded" style="max-height: 200px; overflow-y: auto;">${JSON.stringify(step.output, null, 2)}</pre>
                                </div>
                            </div>
                        </div>
                    </div>
                `;
            });
            
            outputHtml += '</div>';
            $('#executionOutput').html(outputHtml);
        }
    }
    
    /**
     * Save logs to file
     */
    async saveLogsToFile() {
        if (!this.currentExecutionId || this.logEntries.length === 0) {
            return;
        }
        
        try {
            // Format log content
            const logContent = this.formatLogsForFile();
            
            // Get project name from workflow manager
            const projectName = this.workflowManager.projectName || 'unknown-project';
            
            // Send request to save log
            const response = await fetch(`/api/workflow/execution/${this.currentExecutionId}/log`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({
                    logContent: logContent,
                    projectName: projectName
                })
            });
            
            if (response.ok) {
                const result = await response.json();
                if (result.success && result.data) {
                    this.addLogEntry(`📁 Log uložen do souboru: ${result.data.filePath}`, 'success');
                }
            } else {
                this.addLogEntry('⚠️ Nepodařilo se uložit log do souboru', 'warning');
            }
        } catch (error) {
            console.error('Error saving log file:', error);
            this.addLogEntry('⚠️ Chyba při ukládání logu: ' + error.message, 'warning');
        }
    }
    
    /**
     * Format logs for file output
     */
    formatLogsForFile() {
        const header = [
            '========================================',
            `WORKFLOW EXECUTION LOG`,
            `Execution ID: ${this.currentExecutionId}`,
            `Workflow ID: ${this.workflowManager.workflowId}`,
            `Project: ${this.workflowManager.projectName || 'N/A'}`,
            `Started: ${this.logEntries.length > 0 ? this.logEntries[0].timestamp : 'N/A'}`,
            `Completed: ${new Date().toISOString()}`,
            '========================================',
            ''
        ].join('\n');
        
        const logLines = this.logEntries.map(entry => {
            const typePrefix = {
                'error': '[ERROR]',
                'warning': '[WARN] ',
                'info': '[INFO] ',
                'success': '[OK]   '
            }[entry.type] || '[LOG]  ';
            
            return `${entry.timestamp} ${typePrefix} ${entry.message}`;
        });
        
        return header + logLines.join('\n');
    }
    
    /**
     * Copy execution logs to clipboard
     */
    copyExecutionLogs() {
        const logText = this.formatLogsForFile();
        
        // Create temporary textarea to copy text
        const textarea = document.createElement('textarea');
        textarea.value = logText;
        textarea.style.position = 'fixed';
        textarea.style.opacity = '0';
        document.body.appendChild(textarea);
        
        // Select and copy text
        textarea.select();
        textarea.setSelectionRange(0, 99999); // For mobile devices
        
        try {
            document.execCommand('copy');
            toastr.success('Logy byly zkopírovány do schránky');
        } catch (err) {
            toastr.error('Nepodařilo se zkopírovat logy');
            console.error('Copy failed:', err);
        }
        
        // Remove temporary textarea
        document.body.removeChild(textarea);
    }
    
    /**
     * Add log entry to test logs
     */
    addTestLog(level, message) {
        const timestamp = new Date().toLocaleTimeString('cs-CZ');
        const levelColors = {
            'INFO': 'text-info',
            'SUCCESS': 'text-success', 
            'WARNING': 'text-warning',
            'ERROR': 'text-danger',
            'DEBUG': 'text-muted'
        };
        
        const colorClass = levelColors[level] || 'text-light';
        const logEntry = `<div class="${colorClass}">[${timestamp}] ${level}: ${message}</div>`;
        
        $('#testLogs').append(logEntry);
        
        // Auto-scroll if enabled
        if ($('#autoScrollLogs').is(':checked')) {
            $('#testLogs').scrollTop($('#testLogs')[0].scrollHeight);
        }
    }
    
    /**
     * Monitor test execution progress
     */
    monitorTestExecution(executionId) {
        let startTime = Date.now();
        
        this.testMonitoringInterval = setInterval(async () => {
            try {
                const response = await fetch(`/api/projects/execution/${executionId}`);
                const result = await response.json();
                
                if (result.success && result.data) {
                    const execution = result.data;
                    
                    // Update duration
                    const duration = Math.floor((Date.now() - startTime) / 1000);
                    $('#testDuration').text(`${duration}s`);
                    
                    // Update progress
                    const progress = execution.totalItemsCount > 0 
                        ? Math.round((execution.itemsProcessedCount / execution.totalItemsCount) * 100)
                        : 0;
                    $('#testProgressBar').css('width', `${Math.max(10, progress)}%`).text(`${progress}%`);
                    
                    // Update status
                    $('#testStatus').text(execution.status);
                    $('#testStepsCount').text(`${execution.itemsProcessedCount} / ${execution.totalItemsCount || 0}`);
                    
                    // Update current stage
                    if (execution.currentStage && execution.currentStage !== this.lastStepName) {
                        this.addTestLog('INFO', `Aktuální fáze: ${execution.currentStage}`);
                        this.lastStepName = execution.currentStage;
                    }
                    
                    // Add execution logs - parse JSON logs
                    if (execution.executionLog) {
                        try {
                            const logEntries = JSON.parse(execution.executionLog);
                            if (Array.isArray(logEntries)) {
                                logEntries.forEach(entry => {
                                    const logId = `${entry.timestamp}-${entry.message}`;
                                    if (!this.processedLogIds.includes(logId)) {
                                        // First show the step message
                                        this.addTestLog(entry.level === 'Error' ? 'ERROR' : 'INFO', entry.message);
                                        
                                        // Show model responses if available
                                        if (entry.output) {
                                            // Check for direct response text
                                            if (typeof entry.output === 'string' && entry.output.trim()) {
                                                this.addTestLog('SUCCESS', `💬 Model odpověděl: ${entry.output}`);
                                            } 
                                            // Check for structured response
                                            else if (typeof entry.output === 'object') {
                                                // Look for response in various possible fields
                                                if (entry.output.response) {
                                                    this.addTestLog('SUCCESS', `💬 Model odpověděl: ${entry.output.response}`);
                                                } else if (entry.output.result) {
                                                    this.addTestLog('SUCCESS', `📊 Výsledek: ${JSON.stringify(entry.output.result, null, 2)}`);
                                                } else if (entry.output.content) {
                                                    this.addTestLog('SUCCESS', `💬 Model odpověděl: ${entry.output.content}`);
                                                } else if (entry.output.message) {
                                                    this.addTestLog('SUCCESS', `💬 Model odpověděl: ${entry.output.message}`);
                                                } else if (Object.keys(entry.output).length > 0) {
                                                    // Show any non-empty object
                                                    this.addTestLog('SUCCESS', `📊 Data: ${JSON.stringify(entry.output, null, 2)}`);
                                                }
                                            }
                                        }
                                        
                                        // Also check for error details
                                        if (entry.error && entry.error.trim()) {
                                            this.addTestLog('ERROR', `❌ Chyba: ${entry.error}`);
                                        }
                                        
                                        this.processedLogIds.push(logId);
                                    }
                                });
                            }
                        } catch (e) {
                            // Fallback to plain text logs
                            const logs = execution.executionLog.split('\\n');
                            logs.forEach(log => {
                                if (log.trim() && !this.processedLogs.includes(log)) {
                                    this.addTestLog('INFO', log);
                                    this.processedLogs.push(log);
                                }
                            });
                        }
                    }
                    
                    // Check if completed
                    if (execution.status === 'Completed' || execution.status === 'Success') {
                        this.showTestSuccess(execution);
                    } else if (execution.status === 'Failed') {
                        this.showTestError(execution.errorMessage || 'Test selhal');
                    } else if (!['Running', 'InProgress', 'Pending'].includes(execution.status)) {
                        this.showTestError(`Neočekávaný stav: ${execution.status}`);
                    }
                }
            } catch (error) {
                console.error('Error checking test status:', error);
            }
        }, 1000);
    }
    
    /**
     * Show test success
     */
    showTestSuccess(execution) {
        $('#testProgressBar')
            .removeClass('progress-bar-animated bg-warning')
            .addClass('bg-success')
            .css('width', '100%')
            .text('Dokončeno');
        $('#testStatus').text('Test úspěšný');
        $('#testStatusIcon').removeClass('fa-cog fa-spin').addClass('fa-check');
        $('.info-box-icon.bg-warning').removeClass('bg-warning').addClass('bg-success');
        
        this.addTestLog('SUCCESS', '=== TEST ÚSPĚŠNÝ ===');
        
        // Show execution stats
        if (execution.itemsProcessedCount !== undefined) {
            this.addTestLog('INFO', `📈 Zpracováno položek: ${execution.itemsProcessedCount}`);
        }
        if (execution.durationSeconds !== undefined) {
            this.addTestLog('INFO', `⏱️ Doba trvání: ${execution.durationSeconds}s`);
        }
        
        // Show output data if available
        if (execution.outputData) {
            try {
                const output = JSON.parse(execution.outputData);
                if (output && Object.keys(output).length > 0) {
                    this.addTestLog('SUCCESS', `📊 Výstupní data workflow:`);
                    this.addTestLog('SUCCESS', JSON.stringify(output, null, 2));
                } else {
                    this.addTestLog('INFO', `✅ Workflow dokončeno bez výstupních dat`);
                }
            } catch (e) {
                // If not JSON, show as plain text
                if (execution.outputData.trim()) {
                    this.addTestLog('SUCCESS', `📊 Výstup: ${execution.outputData}`);
                }
            }
        } else {
            this.addTestLog('INFO', `✅ Workflow dokončeno bez výstupních dat`);
        }
        
        $('#cancelTestBtn').hide();
        $('#repeatTestBtn').show();
        $('#closeTestBtn').show();
        
        // Clear interval
        if (this.testMonitoringInterval) {
            clearInterval(this.testMonitoringInterval);
            this.testMonitoringInterval = null;
        }
    }
    
    /**
     * Show test error
     */
    showTestError(message) {
        $('#testProgressBar')
            .removeClass('progress-bar-animated bg-warning')
            .addClass('bg-danger')
            .css('width', '100%')
            .text('Chyba');
        $('#testStatus').text('Test selhal');
        $('#testStatusIcon').removeClass('fa-cog fa-spin').addClass('fa-times');
        $('.info-box-icon.bg-warning').removeClass('bg-warning').addClass('bg-danger');
        
        this.addTestLog('ERROR', '=== TEST SELHAL ===');
        this.addTestLog('ERROR', message);
        
        $('#cancelTestBtn').hide();
        $('#repeatTestBtn').show();
        $('#closeTestBtn').show();
        
        // Clear interval
        if (this.testMonitoringInterval) {
            clearInterval(this.testMonitoringInterval);
            this.testMonitoringInterval = null;
        }
    }
    
    /**
     * Validate default workflow orchestrator
     */
    async validateDefaultWorkflowOrchestrator() {
        try {
            const response = await fetch('/api/orchestrators');
            const result = await response.json();
            
            if (!result.success || !result.data) {
                return { valid: false, error: 'Nelze načíst seznam orchestrátorů' };
            }
            
            const orchestrators = result.data;
            
            if (orchestrators.length === 0) {
                return { valid: false, error: 'Žádné workflow orchestrátory nejsou k dispozici' };
            }
            
            // Find default workflow orchestrator
            let defaultOrchestrator = null;
            let defaultConfig = null;
            
            for (const orchestrator of orchestrators) {
                try {
                    const configResponse = await fetch(`/Orchestrators/GetConfiguration/${orchestrator.id}`);
                    const configResult = await configResponse.json();
                    
                    if (configResult.success && configResult.data?.isDefaultWorkflowOrchestrator) {
                        defaultOrchestrator = orchestrator;
                        defaultConfig = configResult.data;
                        break;
                    }
                } catch (error) {
                    console.error('Error getting orchestrator config:', error);
                }
            }
            
            if (!defaultOrchestrator || !defaultConfig) {
                return { 
                    valid: false, 
                    error: 'Žádný orchestrátor není nastaven jako výchozí workflow orchestrátor. Nakonfigurujte prosím výchozí orchestrátor.' 
                };
            }
            
            return {
                valid: true,
                orchestrator: defaultOrchestrator,
                config: defaultConfig
            };
        } catch (error) {
            return { valid: false, error: 'Chyba při kontrole orchestrátor: ' + error.message };
        }
    }
    
    /**
     * Validate AI server status
     */
    async validateAiServer(aiServerId) {
        try {
            // Get server information
            const serverResponse = await fetch(`/api/ai-servers/${aiServerId}`);
            
            if (!serverResponse.ok) {
                const errorText = await serverResponse.text();
                return { valid: false, error: `AI server API chyba: ${serverResponse.status} - ${errorText}` };
            }
            
            const responseText = await serverResponse.text();
            
            let serverResult;
            try {
                serverResult = JSON.parse(responseText);
            } catch (jsonError) {
                return { valid: false, error: `AI server API vrací nevalidní JSON: ${jsonError.message}` };
            }
            
            if (!serverResult.success || !serverResult.data) {
                return { valid: false, error: `AI server s ID ${aiServerId} nebyl nalezen` };
            }
            
            const server = serverResult.data;
            
            // Check server health
            const healthResponse = await fetch(`/api/ai-servers/${aiServerId}/health`);
            const healthResult = await healthResponse.json();
            
            if (!healthResult.success) {
                return { 
                    valid: false, 
                    error: `AI server "${server.name}" není dostupný: ${healthResult.message || 'Server neodpovídá'}` 
                };
            }
            
            return {
                valid: true,
                server: {
                    ...server,
                    status: healthResult.data?.status || 'online'
                }
            };
        } catch (error) {
            return { valid: false, error: 'Chyba při kontrole AI serveru: ' + error.message };
        }
    }
    
    /**
     * Validate model availability
     */
    async validateModel(aiServerId, modelId) {
        try {
            // Get loaded models for the server
            const modelsResponse = await fetch(`/api/ai-servers/${aiServerId}/models`);
            const modelsResult = await modelsResponse.json();
            
            if (!modelsResult.success) {
                return { 
                    valid: false, 
                    error: `Nelze načíst seznam modelů ze serveru: ${modelsResult.message}` 
                };
            }
            
            const models = modelsResult.data || [];
            const targetModel = models.find(m => m.id === modelId || m.name === modelId);
            
            if (!targetModel) {
                return { 
                    valid: false, 
                    error: `Model "${modelId}" není dostupný na serveru. Dostupné modely: ${models.map(m => m.name || m.id).join(', ')}` 
                };
            }
            
            // Check if model is loaded (if status information is available)
            const status = targetModel.loaded === true ? 'načten' : 
                         targetModel.loaded === false ? 'dostupný (nenačten)' : 
                         'dostupný';
            
            return {
                valid: true,
                model: targetModel,
                status: status
            };
        } catch (error) {
            console.error('Error validating model:', error);
            return { valid: false, error: 'Chyba při kontrole modelu: ' + error.message };
        }
    }
    
    /**
     * Cancel test
     */
    cancelTest() {
        this.addTestLog('WARNING', 'Test byl zrušen uživatelem');
        
        // Clear intervals
        if (this.testMonitoringInterval) {
            clearInterval(this.testMonitoringInterval);
            this.testMonitoringInterval = null;
        }
        
        // Reset state
        this.lastStepName = null;
        this.processedLogs = [];
        this.processedLogIds = [];
        
        $('#workflowTestModal').modal('hide');
    }
    
    /**
     * Repeat test - restart the test with same parameters
     */
    repeatTest() {
        const projectId = this.workflowManager.projectId;
        
        if (!projectId) {
            toastr.error('Nelze najít ID projektu pro opakování testu');
            return;
        }
        
        this.addTestLog('INFO', '--- OPAKOVÁNÍ TESTU ---');
        
        // Reset test state
        this.resetTestState();
        
        // Start the test again
        this.startProjectTest(projectId);
    }
    
    /**
     * Copy test logs to clipboard from the modal
     */
    copyTestLogs() {
        try {
            // Get logs from the modal
            const logsContainer = document.getElementById('testLogs');
            if (!logsContainer) {
                toastr.error('Logy nejsou k dispozici');
                return;
            }
            
            // Extract text content from all log entries
            const logEntries = logsContainer.querySelectorAll('div');
            const logLines = [];
            
            logEntries.forEach(entry => {
                const text = entry.textContent || entry.innerText;
                if (text && text.trim()) {
                    logLines.push(text.trim());
                }
            });
            
            if (logLines.length === 0) {
                toastr.warning('Žádné logy k kopírování');
                return;
            }
            
            // Create formatted log text
            const timestamp = new Date().toLocaleString('cs-CZ');
            const logText = [
                `=== WORKFLOW TEST LOGY ===`,
                `Čas exportu: ${timestamp}`,
                `Počet záznamů: ${logLines.length}`,
                ``,
                ...logLines,
                ``,
                `=== KONEC LOGŮ ===`
            ].join('\n');
            
            // Copy to clipboard using the Clipboard API
            if (navigator.clipboard && window.isSecureContext) {
                navigator.clipboard.writeText(logText).then(() => {
                    toastr.success(`Logy zkopírovány do schránky (${logLines.length} záznamů)`);
                }).catch(err => {
                    console.error('Clipboard API failed:', err);
                    this.fallbackCopyTextToClipboard(logText, logLines.length);
                });
            } else {
                // Fallback for older browsers or non-secure contexts
                this.fallbackCopyTextToClipboard(logText, logLines.length);
            }
        } catch (error) {
            console.error('Error copying test logs:', error);
            toastr.error('Chyba při kopírování logů');
        }
    }
    
    fallbackCopyTextToClipboard(text, count) {
        const textArea = document.createElement('textarea');
        textArea.value = text;
        textArea.style.position = 'fixed';
        textArea.style.opacity = '0';
        textArea.style.left = '-999999px';
        textArea.style.top = '-999999px';
        document.body.appendChild(textArea);
        
        textArea.focus();
        textArea.select();
        
        try {
            const successful = document.execCommand('copy');
            if (successful) {
                toastr.success(`Logy zkopírovány do schránky (${count} záznamů)`);
            } else {
                toastr.error('Nepodařilo se zkopírovat logy');
            }
        } catch (err) {
            console.error('Copy command failed:', err);
            toastr.error('Kopírování do schránky není podporováno');
        }
        
        document.body.removeChild(textArea);
    }
}