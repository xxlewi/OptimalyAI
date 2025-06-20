/**
 * Workflow Designer Application
 * Hlavní aplikace která integruje všechny moduly
 */

// Import modules
import { WorkflowManager } from './modules/workflow-manager.js';
import { CanvasRenderer } from './modules/canvas-renderer.js';
import { DragDropHandler } from './modules/drag-drop-handler.js';
import { ConnectionManager } from './modules/connection-manager.js';
import { AutoLayout } from './modules/auto-layout.js';
import { WorkflowExecutor } from './modules/workflow-executor.js';

export class WorkflowDesignerApp {
    constructor(options) {
        this.options = options;
        this.projectId = options.projectId;
        
        // Initialize modules
        this.workflowManager = new WorkflowManager({
            projectId: this.projectId,
            workflowId: options.workflowId
        });
        
        this.canvasRenderer = new CanvasRenderer('workflow-canvas', this.workflowManager);
        
        // Set callback for when workflow is loaded
        this.workflowManager.onWorkflowLoaded = () => {
            this.canvasRenderer.render();
        };
        this.dragDropHandler = new DragDropHandler('workflow-canvas', this.workflowManager, this.canvasRenderer);
        this.connectionManager = new ConnectionManager('workflow-canvas', this.workflowManager, this.canvasRenderer);
        this.autoLayout = new AutoLayout(this.workflowManager, this.canvasRenderer);
        this.workflowExecutor = new WorkflowExecutor(this.workflowManager);
        
        // Bind UI events
        this.bindUIEvents();
        
        // Setup module events
        this.setupModuleEvents();
    }
    
    /**
     * Initialize application
     */
    async initialize() {
        // Initialize workflow manager
        await this.workflowManager.initialize();
        
        // Initialize canvas
        this.canvasRenderer.initialize();
        
        // Initialize drag & drop
        this.dragDropHandler.initialize();
        
        // Initialize connections
        this.connectionManager.initialize();
        
        // Setup keyboard shortcuts
        this.setupKeyboardShortcuts();
        
        // Setup tool parameter loading
        this.setupToolParameterLoading();
        
        // Initialize zoom
        this.applyZoom();
        
        // Modal test removed - double-click debugging
    }
    
    /**
     * Bind UI events
     */
    bindUIEvents() {
        // Toolbar buttons
        $('#saveWorkflowBtn, button[onclick*="saveWorkflow"]').off('click').on('click', (e) => {
            e.preventDefault();
            this.saveWorkflow();
        });
        
        $('#validateWorkflowBtn, button[onclick*="validateWorkflow"]').off('click').on('click', (e) => {
            e.preventDefault();
            this.validateWorkflow();
        });
        
        $('#exportWorkflowBtn, button[onclick*="testWorkflow"]').off('click').on('click', (e) => {
            e.preventDefault();
            this.exportWorkflow();
        });
        
        $('#runWorkflowBtn, button[onclick*="runWorkflow"]').off('click').on('click', (e) => {
            e.preventDefault();
            this.workflowExecutor.runWorkflow();
        });
        
        $('#clearWorkflowBtn, button[onclick*="clearWorkflow"]').off('click').on('click', (e) => {
            e.preventDefault();
            this.clearWorkflow();
        });
        
        $('#autoLayoutBtn, button[onclick*="autoLayout"]').off('click').on('click', (e) => {
            e.preventDefault();
            this.autoLayout.performLayout();
        });
        
        // Zoom controls
        $('button[onclick*="zoomIn"]').off('click').on('click', (e) => {
            e.preventDefault();
            this.zoomIn();
        });
        
        $('button[onclick*="zoomOut"]').off('click').on('click', (e) => {
            e.preventDefault();
            this.zoomOut();
        });
        
        $('button[onclick*="zoomReset"]').off('click').on('click', (e) => {
            e.preventDefault();
            this.zoomReset();
        });
        
        // Execution modal buttons
        $('#startExecutionBtn').off('click').on('click', (e) => {
            e.preventDefault();
            this.workflowExecutor.startExecution();
        });
        
        $('#cancelExecutionBtn').off('click').on('click', (e) => {
            e.preventDefault();
            this.workflowExecutor.cancelExecution();
        });
        
        // Node edit modal buttons
        $('button[onclick*="updateCurrentNode"]').off('click').on('click', (e) => {
            e.preventDefault();
            this.updateCurrentNode();
        });
        
        $('button[onclick*="deleteCurrentNode"]').off('click').on('click', (e) => {
            e.preventDefault();
            this.deleteCurrentNode();
        });
        
        $('button[onclick*="testCurrentTool"]').off('click').on('click', (e) => {
            e.preventDefault();
            this.testCurrentTool();
        });
    }
    
    /**
     * Setup module events
     */
    setupModuleEvents() {
        console.log('Setting up module events...');
        
        // Canvas renderer events
        this.canvasRenderer.onNodeSelect = (node) => {
            console.log('Node selected:', node);
        };
        
        this.canvasRenderer.onNodeEdit = (node) => {
            console.log('onNodeEdit callback triggered for node:', node);
            this.editNode(node);
        };
        
        console.log('onNodeEdit handler set:', !!this.canvasRenderer.onNodeEdit);
        
        // Connection manager events
        this.connectionManager.onConnectionAdded = (from, to, branch) => {
            console.log('Connection added:', from, to, branch);
        };
    }
    
    /**
     * Setup keyboard shortcuts
     */
    setupKeyboardShortcuts() {
        $(document).on('keydown', (e) => {
            // Delete or Backspace key
            if (e.key === 'Delete' || e.key === 'Backspace') {
                if (!$(e.target).is('input, textarea')) {
                    e.preventDefault();
                    
                    if (this.workflowManager.selectedNode) {
                        this.workflowManager.removeNode(this.workflowManager.selectedNode);
                        this.canvasRenderer.render();
                    } else if (this.workflowManager.selectedConnection) {
                        const conn = this.workflowManager.selectedConnection;
                        this.workflowManager.removeConnection(conn.from, conn.to, conn.branch);
                        this.workflowManager.deselectAll();
                        this.canvasRenderer.render();
                    }
                }
            }
            
            // Ctrl+S - Save
            if (e.ctrlKey && e.key === 's') {
                e.preventDefault();
                this.saveWorkflow();
            }
        });
    }
    
    /**
     * Setup tool parameter loading
     */
    setupToolParameterLoading() {
        $('#nodeToolSelect').on('change', (e) => {
            const selectedTool = $(e.target).val();
            if (selectedTool) {
                const toolName = $(e.target).find('option:selected').text();
                $('#nodeName').val(toolName);
                this.loadToolParameters(selectedTool);
                $('#testToolBtn').show();
            } else {
                $('#dynamicParameters').html(`
                    <div class="text-muted text-center py-3">
                        <i class="fas fa-info-circle"></i> Vyberte nástroj pro zobrazení parametrů
                    </div>
                `);
                $('#testToolBtn').hide();
            }
        });
    }
    
    /**
     * Save workflow
     */
    async saveWorkflow() {
        try {
            const result = await this.workflowManager.saveWorkflow();
            toastr.success('Workflow uloženo');
        } catch (error) {
            toastr.error(error.message || 'Chyba při ukládání');
        }
    }
    
    /**
     * Validate workflow
     */
    async validateWorkflow() {
        const result = await this.workflowManager.validateWorkflow();
        
        if (result.isValid) {
            toastr.success('Workflow je validní');
        } else {
            toastr.error(result.errors.join('<br>'), 'Chyby ve workflow');
        }
    }
    
    /**
     * Export workflow
     */
    exportWorkflow() {
        this.workflowManager.exportWorkflow();
        toastr.info('Export pro orchestrátor byl stažen');
    }
    
    /**
     * Clear workflow
     */
    clearWorkflow() {
        if (confirm('Opravdu vymazat workflow?')) {
            this.workflowManager.clearWorkflow();
            this.canvasRenderer.render();
        }
    }
    
    /**
     * Edit node
     */
    editNode(node) {
        console.log('Editing node:', node);
        
        // Check if modal exists
        if ($('#nodeEditModal').length === 0) {
            console.error('nodeEditModal not found!');
            return;
        }
        
        $('#editingNodeId').val(node.id);
        $('#nodeName').val(node.name);
        $('#nodeDescription').val(node.description || '');
        
        // Show/hide sections based on type
        if (node.type === 'condition' || node.type === 'parallel') {
            $('#nodeEditTabs').hide();
            $('#toolSection').hide();
            $('#executionSection').hide();
            $('#parametersSection').hide();
            $('#conditionSection').toggle(node.type === 'condition');
            if (node.type === 'condition') {
                $('#nodeCondition').val(node.condition || '');
            }
        } else if (node.type === 'InputAdapter' || node.type === 'OutputAdapter') {
            $('#nodeEditTabs').hide();
            $('#toolSection').hide();
            $('#executionSection').hide();
            $('#parametersSection').hide();
            $('#conditionSection').hide();
            $('#adapterConfigSection').show();
            console.log('Editing adapter node, config:', node.adapterConfiguration);
            // Load adapter configuration
            this.loadAdapterConfiguration(node);
        } else if (node.type === 'tool' || node.type === 'task') {
            $('#toolSection').show();
            $('#executionSection').show();
            $('#parametersSection').show();
            $('#conditionSection').hide();
            $('#adapterConfigSection').hide();
            
            // Show tabs for tool nodes
            $('#nodeEditTabs').show();
            
            $('#nodeToolSelect').val(node.tool || '');
            $('#nodeUseReAct').prop('checked', node.useReAct || false);
            $('#nodeTimeout').val(node.timeoutSeconds || 300);
            $('#nodeRetryCount').val(node.retryCount || 3);
            
            if (node.tool) {
                this.loadToolParameters(node.tool);
            }
        } else {
            $('#toolSection').hide();
            $('#executionSection').show();
            $('#parametersSection').hide();
            $('#conditionSection').hide();
            $('#adapterConfigSection').hide();
        }
        
        console.log('About to show modal...');
        console.log('Modal element exists:', $('#nodeEditModal').length > 0);
        console.log('Bootstrap modal function available:', typeof $.fn.modal);
        
        try {
            // Reset to config tab
            $('#nodeEditTabs a[href="#configTab"]').tab('show');
            $('#testResults').hide();
            $('#testResultContent').empty();
            
            // Bootstrap 4 modal show
            $('#nodeEditModal').modal('show');
            console.log('Modal show command sent');
        } catch (error) {
            console.error('Error showing modal:', error);
        }
    }
    
    /**
     * Update current node
     */
    updateCurrentNode() {
        const nodeId = $('#editingNodeId').val();
        const updates = {
            name: $('#nodeName').val(),
            description: $('#nodeDescription').val()
        };
        
        const node = this.workflowManager.getNode(nodeId);
        
        if (node.type === 'condition') {
            updates.condition = $('#nodeCondition').val();
        } else if (node.type === 'tool' || node.type === 'task') {
            updates.tool = $('#nodeToolSelect').val() || null;
            updates.useReAct = $('#nodeUseReAct').is(':checked');
            updates.timeoutSeconds = parseInt($('#nodeTimeout').val()) || 300;
            updates.retryCount = parseInt($('#nodeRetryCount').val()) || 3;
            
            // Collect parameters
            updates.configuration = {};
            console.log('Collecting tool parameters...');
            $('#dynamicParameters [data-param]').each(function() {
                const paramName = $(this).data('param');
                let value;
                
                if ($(this).attr('type') === 'checkbox') {
                    value = $(this).is(':checked');
                } else if ($(this).attr('type') === 'number') {
                    value = parseFloat($(this).val()) || 0;
                } else {
                    value = $(this).val();
                }
                
                updates.configuration[paramName] = value;
                console.log(`Tool param ${paramName}: ${value}`);
            });
            console.log('Final tool configuration:', updates.configuration);
            console.log('All updates being saved:', updates);
        } else if (node.type === 'InputAdapter' || node.type === 'OutputAdapter') {
            // Save adapter selection
            updates.selectedAdapter = $('#nodeAdapterSelect').val();
            
            // Collect adapter parameters
            updates.adapterConfiguration = {};
            $('#nodeAdapterParameters [data-param]').each(function() {
                const paramName = $(this).data('param');
                let value;
                
                if ($(this).attr('type') === 'checkbox') {
                    value = $(this).is(':checked');
                } else if ($(this).attr('type') === 'number') {
                    value = parseFloat($(this).val()) || 0;
                } else if ($(this).is('textarea')) {
                    const textValue = $(this).val().trim();
                    // Try to parse JSON
                    if (textValue) {
                        try {
                            value = JSON.parse(textValue);
                        } catch {
                            value = textValue;
                        }
                    } else {
                        value = null;
                    }
                } else {
                    value = $(this).val();
                }
                
                if (value !== null && value !== '') {
                    updates.adapterConfiguration[paramName] = value;
                }
            });
            
            console.log('Saving adapter configuration:', updates.adapterConfiguration);
        }
        
        this.workflowManager.updateNode(nodeId, updates);
        this.canvasRenderer.render();
        $('#nodeEditModal').modal('hide');
    }
    
    /**
     * Delete current node
     */
    deleteCurrentNode() {
        const nodeId = $('#editingNodeId').val();
        this.workflowManager.removeNode(nodeId);
        this.canvasRenderer.render();
        $('#nodeEditModal').modal('hide');
    }
    
    /**
     * Load tool parameters
     */
    async loadToolParameters(toolId) {
        $('#dynamicParameters').html('<div class="text-center"><i class="fas fa-spinner fa-spin"></i> Načítám parametry...</div>');
        
        try {
            const response = await fetch(`/api/tools/${toolId}/parameters`);
            const parameters = await response.json();
            
            // Get current node to access existing configuration
            const nodeId = $('#editingNodeId').val();
            const node = this.workflowManager.getNode(nodeId);
            
            this.renderToolParameters(parameters, node);
        } catch (error) {
            $('#dynamicParameters').html('<div class="alert alert-warning">Nepodařilo se načíst parametry nástroje</div>');
        }
    }
    
    /**
     * Render tool parameters
     */
    renderToolParameters(parameters, node) {
        if (!parameters || parameters.length === 0) {
            $('#dynamicParameters').html('<div class="text-muted">Tento nástroj nemá žádné parametry</div>');
            return;
        }
        
        let html = '';
        parameters.forEach(param => {
            // Create a copy of param to avoid modifying the original
            const paramCopy = {...param};
            
            // Set current value if exists
            if (node && node.configuration && node.configuration[param.name] !== undefined) {
                paramCopy.defaultValue = node.configuration[param.name];
                console.log(`Loading tool param ${param.name}: ${paramCopy.defaultValue}`);
            }
            
            html += this.renderParameter(paramCopy);
        });
        
        $('#dynamicParameters').html(html);
    }
    
    /**
     * Render single parameter
     */
    renderParameter(param, prefix = '') {
        const idPrefix = prefix || 'param_';
        let html = '<div class="form-group">';
        html += `<label for="${idPrefix}${param.name}">${param.displayName || param.name}`;
        if (param.isRequired) html += ' <span class="text-danger">*</span>';
        html += '</label>';
        if (param.description) {
            html += `<small class="form-text text-muted">${param.description}</small>`;
        }
        
        // According to parameter type render the correct input
        const paramType = param.type || 'String';
        
        switch(paramType) {
            case 'String':
                if (param.validation?.allowedValues && param.validation.allowedValues.length > 0) {
                    // Select for allowed values
                    const selectParamInfo = btoa(JSON.stringify(param));
                    html += `<select class="form-control" id="${idPrefix}${param.name}" data-param="${param.name}" data-param-info="${selectParamInfo}">`;
                    param.validation.allowedValues.forEach(val => {
                        html += `<option value="${val}"${val === param.defaultValue ? ' selected' : ''}>${val}</option>`;
                    });
                    html += '</select>';
                } else {
                    // Text input
                    const paramInfo = btoa(JSON.stringify(param)); // Base64 encode to avoid quote issues
                    html += `<input type="text" class="form-control" id="${idPrefix}${param.name}"
                             data-param="${param.name}" 
                             data-param-info="${paramInfo}"
                             value="${param.defaultValue || ''}" 
                             placeholder="${param.example || ''}" />`;
                }
                break;
                
            case 'Boolean':
                html += `<div class="custom-control custom-switch">`;
                const boolParamInfo = btoa(JSON.stringify(param));
                html += `<input type="checkbox" class="custom-control-input" id="${idPrefix}${param.name}" 
                         data-param="${param.name}" data-param-info="${boolParamInfo}"
                         ${param.defaultValue ? ' checked' : ''}>`;
                html += `<label class="custom-control-label" for="${idPrefix}${param.name}">Ano</label>`;
                html += '</div>';
                break;
                
            case 'Integer':
                const intParamInfo = btoa(JSON.stringify(param));
                html += `<input type="number" class="form-control" id="${idPrefix}${param.name}"
                         data-param="${param.name}" 
                         data-param-info="${intParamInfo}"
                         value="${param.defaultValue || ''}" 
                         ${param.validation?.min ? 'min="' + param.validation.min + '"' : ''}
                         ${param.validation?.max ? 'max="' + param.validation.max + '"' : ''}/>`;
                break;
                
            case 'Decimal':
                const decimalParamInfo = btoa(JSON.stringify(param));
                html += `<input type="number" step="0.01" class="form-control" id="${idPrefix}${param.name}"
                         data-param="${param.name}" 
                         data-param-info="${decimalParamInfo}"
                         value="${param.defaultValue || ''}"/>`;
                break;
                
            case 'Json':
                const jsonParamInfo = btoa(JSON.stringify(param));
                html += `<textarea class="form-control json-input" id="${idPrefix}${param.name}"
                         data-param="${param.name}" 
                         data-param-info="${jsonParamInfo}"
                         rows="4">${param.defaultValue ? JSON.stringify(param.defaultValue, null, 2) : '{}'}</textarea>`;
                break;
                
            default:
                const defaultParamInfo = btoa(JSON.stringify(param));
                html += `<input type="text" class="form-control" id="${idPrefix}${param.name}"
                         data-param="${param.name}" 
                         data-param-info="${defaultParamInfo}"
                         value="${param.defaultValue || ''}"/>`;
        }
        
        if (param.description) {
            html += `<small class="form-text text-muted">${param.description}</small>`;
        }
        
        // Div for validation errors
        html += `<div class="invalid-feedback" id="error_${idPrefix}${param.name}"></div>`;
        
        html += '</div>';
        return html;
    }
    
    /**
     * Load adapter configuration for node
     */
    async loadAdapterConfiguration(node) {
        const $container = $('#adapterConfigContainer');
        const adapterType = node.type === 'InputAdapter' ? 'Input' : 'Output';
        
        // Show loading
        $container.html(`
            <div class="text-center">
                <i class="fas fa-spinner fa-spin"></i> Načítám adaptéry...
            </div>
        `);
        
        try {
            // Load adapters
            const response = await fetch(`/api/adapters?type=${adapterType}`);
            const data = await response.json();
            
            if (data.success) {
                // Create select for adapters
                let html = `
                    <div class="form-group">
                        <label>Vyberte ${adapterType === 'Input' ? 'vstupní' : 'výstupní'} adaptér</label>
                        <select class="form-control" id="nodeAdapterSelect">
                            <option value="">-- Vyberte adaptér --</option>
                `;
                
                // Group by category
                const categories = {};
                data.data.forEach(adapter => {
                    const category = adapter.category || 'Ostatní';
                    if (!categories[category]) {
                        categories[category] = [];
                    }
                    categories[category].push(adapter);
                });
                
                // Add options
                Object.entries(categories).forEach(([category, adapters]) => {
                    html += `<optgroup label="${category}">`;
                    adapters.forEach(adapter => {
                        const selected = node.selectedAdapter === adapter.id ? 'selected' : '';
                        html += `<option value="${adapter.id}" ${selected}>${adapter.name}</option>`;
                    });
                    html += `</optgroup>`;
                });
                
                html += `</select></div>`;
                html += `<div id="nodeAdapterParameters"></div>`;
                
                $container.html(html);
                
                // Store adapters for later use
                this.currentAdapters = data.data;
                
                // Handle adapter selection
                $('#nodeAdapterSelect').on('change', (e) => {
                    const adapterId = $(e.target).val();
                    this.loadAdapterParameters(adapterId, node);
                });
                
                // If adapter is already selected, load its parameters
                if (node.selectedAdapter) {
                    this.loadAdapterParameters(node.selectedAdapter, node);
                }
            } else {
                $container.html(`
                    <div class="alert alert-danger">
                        <i class="fas fa-exclamation-triangle"></i> Chyba při načítání adaptérů
                    </div>
                `);
            }
        } catch (error) {
            console.error('Error loading adapters:', error);
            $container.html(`
                <div class="alert alert-danger">
                    <i class="fas fa-exclamation-triangle"></i> Chyba při načítání adaptérů
                </div>
            `);
        }
    }
    
    /**
     * Load adapter parameters
     */
    loadAdapterParameters(adapterId, node) {
        const $container = $('#nodeAdapterParameters');
        
        if (!adapterId) {
            $container.empty();
            return;
        }
        
        const adapter = this.currentAdapters.find(a => a.id === adapterId);
        if (!adapter) return;
        
        let html = '';
        
        if (!adapter.parameters || adapter.parameters.length === 0) {
            html = `
                <div class="alert alert-info">
                    <i class="fas fa-info-circle"></i> Tento adaptér nemá žádné parametry
                </div>
            `;
        } else {
            html = '<h6>Parametry adaptéru</h6>';
            adapter.parameters.forEach(param => {
                // Create a copy of param to avoid modifying the original
                const paramCopy = {...param};
                
                // Set current value if exists
                if (node.adapterConfiguration && node.adapterConfiguration[param.name] !== undefined) {
                    paramCopy.defaultValue = node.adapterConfiguration[param.name];
                    console.log(`Loading adapter param ${param.name}: ${paramCopy.defaultValue}`);
                }
                html += this.renderParameter(paramCopy, 'nodeAdapter_');
            });
        }
        
        $container.html(html);
    }
    
    /**
     * Run tool test from test tab
     */
    async runToolTest() {
        const nodeId = $('#editingNodeId').val();
        const node = this.workflowManager.getNode(nodeId);
        
        if (!node || !node.tool) {
            toastr.error('Nejprve vyberte nástroj');
            return;
        }
        
        // Collect current parameters from config tab
        const configuration = {};
        $('#dynamicParameters [data-param]').each(function() {
            const paramName = $(this).data('param');
            let value;
            
            if ($(this).attr('type') === 'checkbox') {
                value = $(this).is(':checked');
            } else if ($(this).attr('type') === 'number') {
                value = parseFloat($(this).val()) || 0;
            } else {
                value = $(this).val();
            }
            
            configuration[paramName] = value;
        });
        
        // Show parameters in test tab
        let paramsHtml = '<h6>Testovací parametry:</h6><pre class="bg-light p-3">' + 
                        JSON.stringify(configuration, null, 2) + '</pre>';
        $('#testParameters').html(paramsHtml);
        
        // Show loading
        $('#testResults').show();
        $('#testResultContent').html('<div class="text-center"><i class="fas fa-spinner fa-spin"></i> Spouštím test...</div>');
        
        try {
            const response = await fetch(`/api/tools/${node.tool}/execute`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify(configuration)
            });
            
            const result = await response.json();
            
            let resultHtml = '';
            if (result.success) {
                resultHtml = `
                    <div class="alert alert-success">
                        <i class="fas fa-check-circle"></i> Test proběhl úspěšně
                    </div>
                    <h6>Výstup:</h6>
                    <pre class="bg-light p-3" style="max-height: 400px; overflow-y: auto;">`;
                
                const output = result.data?.output || result.data || 'Test proběhl úspěšně';
                resultHtml += typeof output === 'string' ? output : JSON.stringify(output, null, 2);
                resultHtml += '</pre>';
                
                if (result.duration) {
                    resultHtml += `<small class="text-muted">Doba zpracování: ${result.duration}ms</small>`;
                }
            } else {
                // Parse error message for better display
                const errorMessage = result.error || result.message || 'Neznámá chyba';
                const errors = errorMessage.split(';').map(e => e.trim());
                
                resultHtml = `
                    <div class="alert alert-danger">
                        <i class="fas fa-exclamation-triangle"></i> Test selhal
                    </div>`;
                
                if (errors.length > 1) {
                    resultHtml += '<h6>Chyby:</h6><ul class="text-danger">';
                    errors.forEach(error => {
                        if (error.includes('Extraction Instruction is required')) {
                            resultHtml += '<li><strong>Instruction je povinný parametr</strong> - zadejte, co chcete z webu extrahovat (např. "Získej všechny ceny produktů")</li>';
                        } else if (error.includes('command injection')) {
                            resultHtml += '<li><strong>URL obsahuje nepovolené znaky</strong> - zkuste jednodušší URL bez query parametrů (?, &, =)</li>';
                        } else {
                            resultHtml += `<li>${error}</li>`;
                        }
                    });
                    resultHtml += '</ul>';
                } else {
                    resultHtml += `<p><strong>Chyba:</strong> ${errorMessage}</p>`;
                }
                
                // Add helpful hints
                resultHtml += `
                    <div class="alert alert-info mt-3">
                        <h6><i class="fas fa-lightbulb"></i> Tipy:</h6>
                        <ul class="mb-0">
                            <li>Pro <strong>instruction</strong> zadejte např: "Extrahuj názvy a ceny produktů"</li>
                            <li>Pro <strong>URL</strong> zkuste např: https://example.com nebo https://www.google.com</li>
                            <li>Query parametry v URL (?, &) mohou být blokovány bezpečnostní kontrolou</li>
                        </ul>
                    </div>`;
                
                if (result.data) {
                    resultHtml += '<details class="mt-3"><summary>Debug informace</summary>';
                    resultHtml += '<pre class="bg-light p-3 mt-2">' + JSON.stringify(result.data, null, 2) + '</pre>';
                    resultHtml += '</details>';
                }
            }
            
            $('#testResultContent').html(resultHtml);
            
        } catch (error) {
            console.error('Error testing tool:', error);
            $('#testResultContent').html(`
                <div class="alert alert-danger">
                    <i class="fas fa-exclamation-triangle"></i> Chyba při komunikaci se serverem
                </div>
                <p>${error.message}</p>
            `);
        }
    }
    
    /**
     * Test current tool (old version - kept for compatibility)
     */
    async testCurrentTool() {
        const nodeId = $('#editingNodeId').val();
        const node = this.workflowManager.getNode(nodeId);
        
        if (!node || !node.tool) {
            toastr.error('Nejprve vyberte nástroj');
            return;
        }
        
        // Collect current parameters
        const configuration = {};
        $('#dynamicParameters [data-param]').each(function() {
            const paramName = $(this).data('param');
            let value;
            
            if ($(this).attr('type') === 'checkbox') {
                value = $(this).is(':checked');
            } else if ($(this).attr('type') === 'number') {
                value = parseFloat($(this).val()) || 0;
            } else {
                value = $(this).val();
            }
            
            configuration[paramName] = value;
        });
        
        // Show loading
        const $btn = $('#testToolBtn');
        const originalText = $btn.html();
        $btn.prop('disabled', true).html('<i class="fas fa-spinner fa-spin"></i> Testuji...');
        
        try {
            const response = await fetch(`/api/tools/${node.tool}/execute`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify(configuration)
            });
            
            const result = await response.json();
            
            if (result.success) {
                // Show result in a modal or alert
                const output = result.data?.output || result.data || 'Test proběhl úspěšně';
                
                // Create result modal
                const resultHtml = `
                    <div class="modal fade" id="testResultModal" tabindex="-1">
                        <div class="modal-dialog modal-lg">
                            <div class="modal-content">
                                <div class="modal-header">
                                    <h5 class="modal-title">
                                        <i class="fas fa-flask"></i> Výsledek testu nástroje
                                    </h5>
                                    <button type="button" class="close" data-dismiss="modal">
                                        <span>&times;</span>
                                    </button>
                                </div>
                                <div class="modal-body">
                                    <pre style="max-height: 400px; overflow-y: auto;">${typeof output === 'string' ? output : JSON.stringify(output, null, 2)}</pre>
                                </div>
                                <div class="modal-footer">
                                    <button type="button" class="btn btn-secondary" data-dismiss="modal">Zavřít</button>
                                </div>
                            </div>
                        </div>
                    </div>
                `;
                
                // Remove existing modal if any
                $('#testResultModal').remove();
                
                // Add and show modal
                $('body').append(resultHtml);
                $('#testResultModal').modal('show');
                
                // Clean up modal after close
                $('#testResultModal').on('hidden.bs.modal', function() {
                    $(this).remove();
                });
                
                toastr.success('Test nástroje proběhl úspěšně');
            } else {
                toastr.error(result.message || 'Test nástroje selhal');
            }
        } catch (error) {
            console.error('Error testing tool:', error);
            toastr.error('Chyba při testování nástroje');
        } finally {
            $btn.prop('disabled', false).html(originalText);
        }
    }
    
    /**
     * Zoom functions
     */
    zoomIn() {
        this.workflowManager.currentZoom = Math.min(this.workflowManager.currentZoom + 0.1, 2.0);
        this.applyZoom();
    }
    
    zoomOut() {
        this.workflowManager.currentZoom = Math.max(this.workflowManager.currentZoom - 0.1, 0.5);
        this.applyZoom();
    }
    
    zoomReset() {
        this.workflowManager.currentZoom = 1.0;
        this.applyZoom();
    }
    
    applyZoom() {
        this.canvasRenderer.applyZoom(this.workflowManager.currentZoom);
        $('.zoom-level').text(Math.round(this.workflowManager.currentZoom * 100) + '%');
    }
}

// Export for global use
window.WorkflowDesignerApp = WorkflowDesignerApp;