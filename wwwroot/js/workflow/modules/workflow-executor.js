/**
 * Workflow Executor Module
 * Modul pro spouštění workflow
 */
export class WorkflowExecutor {
    constructor(workflowManager) {
        this.workflowManager = workflowManager;
        this.currentExecutionId = null;
        this.executionCheckInterval = null;
    }
    
    /**
     * Run workflow
     */
    async runWorkflow() {
        try {
            // First save workflow
            await this.workflowManager.saveWorkflow();
            
            // Show execution modal
            $('#executionModal').modal('show');
            this.resetExecutionModal();
        } catch (error) {
            toastr.error('Musíte nejdříve uložit workflow');
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
            toastr.error('Workflow musí být nejdříve uloženo');
            this.resetExecutionModal();
            return;
        }
        
        // Start execution via API
        const requestData = {
            inputParameters: parameters,
            enableDebugLogging: enableDebug,
            initiatedBy: "user"
        };
        
        try {
            const response = await fetch(`/api/workflow/execute/${workflowId}`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify(requestData)
            });
            
            const result = await response.json();
            
            if (result.success && result.data) {
                this.currentExecutionId = result.data.executionId;
                this.addLogEntry('Workflow spuštěno s ID: ' + this.currentExecutionId, 'info');
                
                // Start polling for status
                this.executionCheckInterval = setInterval(() => this.checkExecutionStatus(), 2000);
            } else {
                toastr.error(result.message || 'Chyba při spouštění workflow');
                this.showExecutionError(result.message || 'Neznámá chyba');
            }
        } catch (error) {
            console.error('Execution failed:', error);
            toastr.error('Chyba při spouštění workflow');
            this.showExecutionError(error.message);
        }
    }
    
    /**
     * Check execution status
     */
    async checkExecutionStatus() {
        if (!this.currentExecutionId) return;
        
        try {
            const response = await fetch(`/api/workflow/status/${this.currentExecutionId}`);
            const result = await response.json();
            
            if (result.success && result.data) {
                this.updateExecutionProgress(result.data);
                
                // Check if execution is complete
                if (result.data.status !== 'Running' && result.data.status !== 'Pending') {
                    clearInterval(this.executionCheckInterval);
                    this.executionCheckInterval = null;
                    
                    if (result.data.status === 'Completed') {
                        this.showExecutionSuccess();
                    } else {
                        this.showExecutionError(result.data.message || 'Workflow selhalo');
                    }
                }
            }
        } catch (error) {
            console.error('Error checking execution status:', error);
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
        
        if (this.executionCheckInterval) {
            clearInterval(this.executionCheckInterval);
            this.executionCheckInterval = null;
        }
    }
    
    /**
     * Update execution progress
     */
    updateExecutionProgress(status) {
        // Update progress bar
        const progress = Math.round(status.progressPercentage || 0);
        $('#progressBar').css('width', progress + '%').text(progress + '%');
        
        // Update current step
        if (status.currentStepName) {
            const stepExists = $(`#step-${status.currentStepId}`).length > 0;
            if (!stepExists) {
                this.addExecutionStep(status.currentStepId, status.currentStepName, 'running');
            }
        }
        
        // Add log message
        if (status.message) {
            this.addLogEntry(status.message, 'info');
        }
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
        $('#executionProgress').hide();
        $('#executionResults').show();
        $('#cancelExecutionBtn').hide();
        
        $('#executionResultAlert')
            .removeClass('alert-danger')
            .addClass('alert-success')
            .html('<i class="fas fa-check-circle"></i> Workflow bylo úspěšně dokončeno!');
    }
    
    /**
     * Show execution error
     */
    showExecutionError(message) {
        $('#executionProgress').hide();
        $('#executionResults').show();
        $('#cancelExecutionBtn').hide();
        
        $('#executionResultAlert')
            .removeClass('alert-success')
            .addClass('alert-danger')
            .html(`<i class="fas fa-exclamation-circle"></i> ${message}`);
    }
}