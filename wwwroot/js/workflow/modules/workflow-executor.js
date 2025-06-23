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
    }
    
    /**
     * Run workflow
     */
    async runWorkflow() {
        try {
            // First save workflow
            await this.workflowManager.saveWorkflow();
            
            // Use the same beautiful test functionality but directly
            $('#executionModal').modal('show');
            this.resetExecutionModal();
            this.startExecutionWithOrchestrator();
            
        } catch (error) {
            toastr.error('Musíte nejdříve uložit workflow');
        }
    }

    async startExecutionWithOrchestrator() {
        // Use the same API and approach as project details test function
        const projectId = this.workflowManager.projectId;
        
        const testData = {
            projectId: projectId,
            runName: `Test Run - ${new Date().toLocaleString('cs-CZ')}`,
            mode: "test",
            priority: "normal",
            testItemLimit: 5,
            enableDebugLogging: true,
            startedBy: "workflow-designer",
            metadata: {
                inputParameters: {
                    testMode: true,
                    maxItems: 5,
                    debug: true
                },
                source: 'workflow-designer'
            }
        };
        
        try {
            const response = await fetch('/api/projects/execute', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(testData)
            });
            
            const result = await response.json();
            
            if (result.success && result.data) {
                const executionId = result.data.id;
                this.addLog('SUCCESS', `Projekt úspěšně spuštěn s orchestrátorem. Execution ID: ${executionId}`);
                this.startStatusMonitoring(executionId);
            } else {
                this.addLog('ERROR', result.message || 'Chyba při spouštění testu');
            }
        } catch (error) {
            this.addLog('ERROR', 'Nepodařilo se spustit test: ' + error.message);
        }
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
}