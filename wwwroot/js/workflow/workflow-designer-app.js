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
            $('#toolSection').hide();
            $('#executionSection').hide();
            $('#parametersSection').hide();
            $('#conditionSection').toggle(node.type === 'condition');
            if (node.type === 'condition') {
                $('#nodeCondition').val(node.condition || '');
            }
        } else if (node.type === 'InputAdapter' || node.type === 'OutputAdapter') {
            $('#toolSection').hide();
            $('#executionSection').hide();
            $('#parametersSection').hide();
            $('#conditionSection').hide();
            $('#adapterConfigSection').show();
            // Load adapter configuration
        } else if (node.type === 'tool' || node.type === 'task') {
            $('#toolSection').show();
            $('#executionSection').show();
            $('#parametersSection').show();
            $('#conditionSection').hide();
            $('#adapterConfigSection').hide();
            
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
            });
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
            this.renderToolParameters(parameters);
        } catch (error) {
            $('#dynamicParameters').html('<div class="alert alert-warning">Nepodařilo se načíst parametry nástroje</div>');
        }
    }
    
    /**
     * Render tool parameters
     */
    renderToolParameters(parameters) {
        if (!parameters || parameters.length === 0) {
            $('#dynamicParameters').html('<div class="text-muted">Tento nástroj nemá žádné parametry</div>');
            return;
        }
        
        let html = '';
        parameters.forEach(param => {
            html += this.renderParameter(param);
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
     * Test current tool
     */
    testCurrentTool() {
        // Implementation for tool testing
        toastr.info('Test nástroje - funkce bude implementována');
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