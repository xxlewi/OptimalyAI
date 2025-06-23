/**
 * Shared Project Test Functionality
 * Used by both Project Details and Workflow Designer
 */

// Global variables for test monitoring
window.testMonitoringInterval = null;
window.lastStepName = null;
window.processedLogs = [];
window.processedLogIds = [];

/**
 * Main test function - shared between project details and workflow designer
 */
function testWorkflow() {
    // Get project data from either page
    const projectId = getProjectId();
    const workflowData = getWorkflowData();
    
    if (!projectId) {
        toastr.error('Nelze najít ID projektu');
        return;
    }
    
    // Ensure the test modal HTML is loaded - returns false if redirect happens
    if (!ensureTestModalExists()) {
        return;
    }
    
    // Show test modal
    $('#workflowTestModal').modal('show');
    
    // Reset test state
    resetTestState();
    
    // Start the test
    startProjectTest(projectId, workflowData);
}

/**
 * Get project ID from current page context
 */
function getProjectId() {
    // Try workflow designer first
    if (window.workflowDesignerApp && window.workflowDesignerApp.config) {
        console.log('Getting project ID from workflow designer config:', window.workflowDesignerApp.config);
        return window.workflowDesignerApp.config.projectId;
    }
    
    // Try project details page
    if (window.projectId) {
        return window.projectId;
    }
    
    // Try to extract from URL query parameter (workflow designer)
    const urlParams = new URLSearchParams(window.location.search);
    const projectIdFromUrl = urlParams.get('projectId');
    if (projectIdFromUrl) {
        console.log('Getting project ID from URL parameter:', projectIdFromUrl);
        return projectIdFromUrl;
    }
    
    // Try to extract from URL path (project details)
    const urlMatch = window.location.pathname.match(/\/Projects\/([a-f0-9-]+)/i);
    if (urlMatch) {
        console.log('Getting project ID from URL path:', urlMatch[1]);
        return urlMatch[1];
    }
    
    console.error('Could not find project ID anywhere');
    return null;
}

/**
 * Get workflow data if available
 */
function getWorkflowData() {
    if (window.workflowDesignerApp && window.workflowDesignerApp.workflowManager) {
        // Try to get workflow data from the manager
        try {
            return window.workflowDesignerApp.workflowManager.exportData();
        } catch (e) {
            console.log('Could not get workflow data:', e);
        }
    }
    return null;
}

/**
 * Ensure test modal HTML exists on the page
 */
function ensureTestModalExists() {
    if ($('#workflowTestModal').length === 0) {
        toastr.error('Test modal není k dispozici na této stránce');
        return false;
    }
    return true;
}

/**
 * Reset test state
 */
function resetTestState() {
    // Clear intervals
    if (window.testMonitoringInterval) {
        clearInterval(window.testMonitoringInterval);
        window.testMonitoringInterval = null;
    }
    
    // Reset variables
    window.lastStepName = null;
    window.processedLogs = [];
    window.processedLogIds = [];
    
    // Reset UI
    $('#testProgressBar').css('width', '0%').text('0%');
    $('#testStatus').text('Připravuji test...');
    $('#testStepsCount').text('0 / 0');
    $('#testDuration').text('0s');
    $('#testLogs').html('<div class="text-warning">[SYSTEM] Inicializuji test workflow...</div>');
    $('#cancelTestBtn').show();
    $('#closeTestBtn').hide();
    
    // Reset styling
    $('.info-box-icon').removeClass('bg-success bg-danger').addClass('bg-warning');
    $('#testStatusIcon').removeClass('fa-check fa-times').addClass('fa-cog fa-spin');
    $('#testProgressBar').removeClass('bg-success bg-danger').addClass('bg-warning progress-bar-animated');
}

/**
 * Start project test execution
 */
function startProjectTest(projectId, workflowData) {
    addTestLog('INFO', 'Připravuji testovací data...');
    addTestLog('DEBUG', `Workflow má ${workflowData?.steps?.length || 0} kroků`);
    
    // Execute workflow in test mode
    const testData = {
        projectId: projectId,
        runName: `Test Run - ${new Date().toLocaleString('cs-CZ')}`,
        mode: "test",
        priority: "normal",
        testItemLimit: 5,
        enableDebugLogging: true,
        startedBy: "shared-test-function",
        metadata: {
            inputParameters: {
                testMode: true,
                maxItems: 5,
                debug: true
            },
            source: 'shared-test',
            workflowId: workflowData?.id || null
        }
    };
    
    addTestLog('INFO', `Spouštím projekt ${projectId} v testovacím režimu s orchestrátorem...`);
    addTestLog('DEBUG', `Testovací parametry: ${JSON.stringify(testData.metadata.inputParameters)}`);
    
    $.ajax({
        url: `/api/projects/execute`,
        type: 'POST',
        contentType: 'application/json',
        data: JSON.stringify(testData),
        success: function(response) {
            if (response.success && response.data) {
                const executionId = response.data.id;
                addTestLog('SUCCESS', `Projekt úspěšně spuštěn s orchestrátorem. Execution ID: ${executionId}`);
                $('#testProgressBar').css('width', '10%').text('10%');
                
                // Start monitoring execution
                monitorTestExecution(executionId);
            } else {
                showTestError(response.message || 'Chyba při spouštění testu');
            }
        },
        error: function(xhr) {
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
            showTestError(errorMsg);
        }
    });
}

/**
 * Add log entry to test logs
 */
function addTestLog(level, message) {
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
function monitorTestExecution(executionId) {
    let startTime = Date.now();
    
    window.testMonitoringInterval = setInterval(async function() {
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
                if (execution.currentStage && execution.currentStage !== window.lastStepName) {
                    addTestLog('INFO', `Aktuální fáze: ${execution.currentStage}`);
                    window.lastStepName = execution.currentStage;
                }
                
                // Add execution logs
                if (execution.executionLog) {
                    const logs = execution.executionLog.split('\n');
                    logs.forEach(log => {
                        if (log.trim() && !window.processedLogs.includes(log)) {
                            addTestLog('INFO', log);
                            window.processedLogs.push(log);
                        }
                    });
                }
                
                // Check if completed
                if (execution.status === 'Completed' || execution.status === 'Success') {
                    showTestSuccess(execution);
                } else if (execution.status === 'Failed') {
                    showTestError(execution.errorMessage || 'Test selhal');
                } else if (!['Running', 'InProgress', 'Pending'].includes(execution.status)) {
                    showTestError(`Neočekávaný stav: ${execution.status}`);
                }
            }
        } catch (error) {
            console.error('Error monitoring test:', error);
        }
    }, 1000);
}

/**
 * Show test success
 */
function showTestSuccess(execution) {
    $('#testProgressBar')
        .removeClass('progress-bar-animated bg-warning')
        .addClass('bg-success')
        .css('width', '100%')
        .text('Dokončeno');
    $('#testStatus').text('Test úspěšný');
    $('#testStatusIcon').removeClass('fa-cog fa-spin').addClass('fa-check');
    $('.info-box-icon.bg-warning').removeClass('bg-warning').addClass('bg-success');
    
    addTestLog('SUCCESS', '=== TEST ÚSPĚŠNÝ ===');
    if (execution.outputData) {
        addTestLog('SUCCESS', `Výsledek: ${JSON.stringify(execution.outputData)}`);
    }
    
    $('#cancelTestBtn').hide();
    $('#closeTestBtn').show();
    
    // Clear interval
    if (window.testMonitoringInterval) {
        clearInterval(window.testMonitoringInterval);
        window.testMonitoringInterval = null;
    }
}

/**
 * Show test error
 */
function showTestError(message) {
    $('#testProgressBar')
        .removeClass('progress-bar-animated bg-warning')
        .addClass('bg-danger')
        .css('width', '100%')
        .text('Chyba');
    $('#testStatus').text('Test selhal');
    $('#testStatusIcon').removeClass('fa-cog fa-spin').addClass('fa-times');
    $('.info-box-icon.bg-warning').removeClass('bg-warning').addClass('bg-danger');
    
    addTestLog('ERROR', '=== TEST SELHAL ===');
    addTestLog('ERROR', message);
    
    $('#cancelTestBtn').hide();
    $('#closeTestBtn').show();
    
    // Clear interval
    if (window.testMonitoringInterval) {
        clearInterval(window.testMonitoringInterval);
        window.testMonitoringInterval = null;
    }
}

/**
 * Cancel test
 */
function cancelTest() {
    addTestLog('WARNING', 'Test byl zrušen uživatelem');
    
    // Clear intervals
    if (window.testMonitoringInterval) {
        clearInterval(window.testMonitoringInterval);
        window.testMonitoringInterval = null;
    }
    
    // Reset state
    window.lastStepName = null;
    window.processedLogs = [];
    window.processedLogIds = [];
    
    $('#workflowTestModal').modal('hide');
}


/**
 * Copy test logs to clipboard (needs to be defined per page)
 */
function copyTestLogs() {
    if (typeof window.copyTestLogs_impl === 'function') {
        window.copyTestLogs_impl();
    } else {
        toastr.error('Funkce kopírování logů není k dispozici');
    }
}

// Export function globally
window.testWorkflow = testWorkflow;
window.cancelTest = cancelTest;
window.copyTestLogs = copyTestLogs;

// Debug log
console.log('Shared project test loaded - testWorkflow function available');