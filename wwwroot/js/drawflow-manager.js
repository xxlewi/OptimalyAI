/**
 * Drawflow Workflow Manager
 * Handles all workflow designer functionality
 */

const workflowManager = {
    editor: null,
    projectId: null,
    projectName: null,
    currentNode: null,
    nodeId: 1,
    
    /**
     * Initialize Drawflow
     */
    init: function(projectId, projectName) {
        this.projectId = projectId;
        this.projectName = projectName;
        
        // Initialize Drawflow
        const container = document.getElementById('drawflow');
        this.editor = new Drawflow(container);
        
        // Configure editor
        this.editor.reroute = true;
        this.editor.reroute_fix_curvature = true;
        this.editor.force_first_input = false;
        this.editor.line_path = 5;
        this.editor.curvature = 0.5;
        
        // Start editor
        this.editor.start();
        
        // Setup event handlers
        this.setupEventHandlers();
        
        // Setup drag and drop
        this.setupDragDrop();
        
        // Update stats
        this.updateStats();
    },
    
    /**
     * Setup event handlers
     */
    setupEventHandlers: function() {
        const self = this;
        
        // Node selected
        this.editor.on('nodeSelected', function(id) {
            self.currentNode = id;
            self.showNodeProperties(id);
        });
        
        // Node double click
        this.editor.on('nodeDblClick', function(id) {
            self.editNode(id);
        });
        
        // Node removed
        this.editor.on('nodeRemoved', function(id) {
            self.updateStats();
        });
        
        // Connection created
        this.editor.on('connectionCreated', function(connection) {
            self.updateStats();
        });
        
        // Connection removed
        this.editor.on('connectionRemoved', function(connection) {
            self.updateStats();
        });
        
        // Click on empty canvas
        container.addEventListener('click', function(e) {
            if (e.target.id === 'drawflow') {
                self.clearSelection();
            }
        });
        
        // Keyboard shortcuts
        document.addEventListener('keydown', function(e) {
            if (e.key === 'Delete' && self.currentNode) {
                self.editor.removeNodeId(`node-${self.currentNode}`);
            }
        });
    },
    
    /**
     * Setup drag and drop
     */
    setupDragDrop: function() {
        const self = this;
        
        // Node dragging
        document.querySelectorAll('.drag-item').forEach(item => {
            item.addEventListener('dragstart', function(e) {
                e.dataTransfer.setData('node-type', this.dataset.nodeType);
                e.dataTransfer.setData('node-inputs', this.dataset.nodeInputs);
                e.dataTransfer.setData('node-outputs', this.dataset.nodeOutputs);
            });
        });
        
        // Tool dragging
        document.querySelectorAll('.drag-tool').forEach(item => {
            item.addEventListener('dragstart', function(e) {
                e.dataTransfer.setData('tool-id', this.dataset.toolId);
                e.dataTransfer.setData('tool-name', this.dataset.toolName);
            });
        });
        
        // Canvas drop
        const container = document.getElementById('drawflow');
        container.addEventListener('drop', function(e) {
            e.preventDefault();
            
            const nodeType = e.dataTransfer.getData('node-type');
            const toolId = e.dataTransfer.getData('tool-id');
            
            if (nodeType) {
                // Add new node
                const inputs = parseInt(e.dataTransfer.getData('node-inputs'));
                const outputs = parseInt(e.dataTransfer.getData('node-outputs'));
                
                const pos_x = e.clientX * (self.editor.precanvas.clientWidth / (self.editor.precanvas.clientWidth * self.editor.zoom)) - (self.editor.precanvas.getBoundingClientRect().x * (self.editor.precanvas.clientWidth / (self.editor.precanvas.clientWidth * self.editor.zoom)));
                const pos_y = e.clientY * (self.editor.precanvas.clientHeight / (self.editor.precanvas.clientHeight * self.editor.zoom)) - (self.editor.precanvas.getBoundingClientRect().y * (self.editor.precanvas.clientHeight / (self.editor.precanvas.clientHeight * self.editor.zoom)));
                
                self.addNode(nodeType, pos_x, pos_y, inputs, outputs);
            } else if (toolId && self.currentNode) {
                // Add tool to selected node
                self.addToolToNode(self.currentNode, toolId, e.dataTransfer.getData('tool-name'));
            }
        });
        
        container.addEventListener('dragover', function(e) {
            e.preventDefault();
        });
    },
    
    /**
     * Add new node
     */
    addNode: function(type, pos_x, pos_y, inputs, outputs) {
        const nodeId = this.nodeId++;
        const data = {
            name: this.getNodeName(type),
            type: type,
            tools: [],
            description: '',
            config: {}
        };
        
        const html = this.createNodeHtml(nodeId, data);
        
        this.editor.addNode(
            type,
            inputs,
            outputs,
            pos_x,
            pos_y,
            type,
            data,
            html
        );
        
        this.updateStats();
    },
    
    /**
     * Create node HTML
     */
    createNodeHtml: function(id, data) {
        const icon = this.getNodeIcon(data.type);
        const typeClass = this.getNodeTypeClass(data.type);
        
        let toolsHtml = '';
        if (data.tools && data.tools.length > 0) {
            toolsHtml = '<div class="node-tools">' + 
                data.tools.map(tool => `<span class="node-tool-badge">${tool}</span>`).join('') +
                '</div>';
        }
        
        return `
            <div class="node-header ${typeClass}">
                <span><i class="${icon}"></i> ${data.name}</span>
                <span class="node-type-badge">${data.type}</span>
            </div>
            <div class="node-body">
                ${data.description ? `<small class="text-muted">${data.description}</small>` : ''}
                ${toolsHtml}
            </div>
        `;
    },
    
    /**
     * Get node name based on type
     */
    getNodeName: function(type) {
        const names = {
            'start': 'Start',
            'end': 'Konec',
            'task': 'Úloha',
            'condition': 'Podmínka',
            'parallel': 'Paralelní',
            'join': 'Spojení',
            'loop': 'Smyčka',
            'wait': 'Čekání',
            'orchestrator': 'Orchestrátor'
        };
        return names[type] || type;
    },
    
    /**
     * Get node icon
     */
    getNodeIcon: function(type) {
        const icons = {
            'start': 'fas fa-play-circle',
            'end': 'fas fa-stop-circle',
            'task': 'fas fa-cog',
            'condition': 'fas fa-code-branch',
            'parallel': 'fas fa-sitemap',
            'join': 'fas fa-compress-arrows-alt',
            'loop': 'fas fa-redo',
            'wait': 'fas fa-clock',
            'orchestrator': 'fas fa-robot'
        };
        return icons[type] || 'fas fa-circle';
    },
    
    /**
     * Get node type class
     */
    getNodeTypeClass: function(type) {
        return type;
    },
    
    /**
     * Show node properties
     */
    showNodeProperties: function(id) {
        const nodeInfo = this.editor.getNodeFromId(id);
        if (!nodeInfo) return;
        
        const data = nodeInfo.data;
        let html = `
            <h6>${data.name}</h6>
            <small class="text-muted">ID: ${id}</small>
            <hr>
            <div class="form-group">
                <label>Typ</label>
                <input type="text" class="form-control form-control-sm" value="${data.type}" readonly>
            </div>
        `;
        
        if (data.type === 'task' || data.type === 'orchestrator') {
            html += `
                <div class="form-group">
                    <label>Nástroje</label>
                    <div>${data.tools.length > 0 ? data.tools.join(', ') : '<em>Žádné</em>'}</div>
                </div>
            `;
            
            if (data.type === 'orchestrator') {
                $('#orchestratorCard').show();
                $('#orchestratorSelect').val(data.config.orchestrator || '');
            } else {
                $('#orchestratorCard').hide();
            }
        } else {
            $('#orchestratorCard').hide();
        }
        
        html += `
            <button class="btn btn-sm btn-primary btn-block" onclick="workflowManager.editNode(${id})">
                <i class="fas fa-edit"></i> Upravit
            </button>
        `;
        
        $('#propertiesPanel').html(html);
    },
    
    /**
     * Edit node
     */
    editNode: function(id) {
        const nodeInfo = this.editor.getNodeFromId(id);
        if (!nodeInfo) return;
        
        const data = nodeInfo.data;
        this.currentNode = id;
        
        // Fill form
        $('#nodeName').val(data.name);
        $('#nodeType').val(data.type);
        $('#nodeDescription').val(data.description || '');
        
        // Show/hide specific fields
        $('.modal-body > form > div[id$="Fields"]').hide();
        
        if (data.type === 'task') {
            $('#taskFields').show();
            this.updateAssignedTools(data.tools || []);
            $('#useReAct').prop('checked', data.config.useReAct || false);
        } else if (data.type === 'condition') {
            $('#conditionFields').show();
            $('#conditionExpression').val(data.config.expression || '');
        } else if (data.type === 'loop') {
            $('#loopFields').show();
            $('#loopCondition').val(data.config.condition || '');
            $('#maxIterations').val(data.config.maxIterations || 100);
        } else if (data.type === 'wait') {
            $('#waitFields').show();
            $('#waitTime').val(data.config.waitTime || 5);
        }
        
        // Advanced settings
        $('#nodeTimeout').val(data.config.timeout || 300);
        $('#nodeRetries').val(data.config.retries || 3);
        
        // Show/hide delete button
        $('#deleteNodeBtn').toggle(data.type !== 'start' && data.type !== 'end');
        
        $('#nodeEditModal').modal('show');
    },
    
    /**
     * Update assigned tools display
     */
    updateAssignedTools: function(tools) {
        const html = tools.map(tool => 
            `<span class="badge badge-primary">
                ${tool} 
                <i class="fas fa-times" onclick="workflowManager.removeTool('${tool}')" 
                   style="cursor:pointer"></i>
            </span>`
        ).join(' ');
        $('#assignedTools').html(html || '<em>Žádné nástroje</em>');
    },
    
    /**
     * Remove tool from node
     */
    removeTool: function(tool) {
        const nodeInfo = this.editor.getNodeFromId(this.currentNode);
        if (!nodeInfo) return;
        
        nodeInfo.data.tools = nodeInfo.data.tools.filter(t => t !== tool);
        this.updateAssignedTools(nodeInfo.data.tools);
    },
    
    /**
     * Add tool to node
     */
    addToolToNode: function(nodeId, toolId, toolName) {
        const nodeInfo = this.editor.getNodeFromId(nodeId);
        if (!nodeInfo || nodeInfo.data.type !== 'task') {
            toastr.warning('Nástroje lze přidat pouze k úlohám');
            return;
        }
        
        if (!nodeInfo.data.tools) nodeInfo.data.tools = [];
        
        if (!nodeInfo.data.tools.includes(toolName)) {
            nodeInfo.data.tools.push(toolName);
            this.updateNodeHtml(nodeId);
            toastr.success(`Nástroj ${toolName} přidán`);
        }
    },
    
    /**
     * Update node
     */
    updateNode: function() {
        const nodeInfo = this.editor.getNodeFromId(this.currentNode);
        if (!nodeInfo) return;
        
        // Update basic data
        nodeInfo.data.name = $('#nodeName').val();
        nodeInfo.data.description = $('#nodeDescription').val();
        
        // Update type-specific data
        if (nodeInfo.data.type === 'task') {
            nodeInfo.data.config.useReAct = $('#useReAct').is(':checked');
        } else if (nodeInfo.data.type === 'condition') {
            nodeInfo.data.config.expression = $('#conditionExpression').val();
        } else if (nodeInfo.data.type === 'loop') {
            nodeInfo.data.config.condition = $('#loopCondition').val();
            nodeInfo.data.config.maxIterations = parseInt($('#maxIterations').val());
        } else if (nodeInfo.data.type === 'wait') {
            nodeInfo.data.config.waitTime = parseInt($('#waitTime').val());
        } else if (nodeInfo.data.type === 'orchestrator') {
            nodeInfo.data.config.orchestrator = $('#orchestratorSelect').val();
        }
        
        // Update advanced settings
        nodeInfo.data.config.timeout = parseInt($('#nodeTimeout').val());
        nodeInfo.data.config.retries = parseInt($('#nodeRetries').val());
        
        // Update visual
        this.updateNodeHtml(this.currentNode);
        
        // Update properties panel
        this.showNodeProperties(this.currentNode);
        
        $('#nodeEditModal').modal('hide');
    },
    
    /**
     * Update node HTML
     */
    updateNodeHtml: function(nodeId) {
        const nodeInfo = this.editor.getNodeFromId(nodeId);
        if (!nodeInfo) return;
        
        const html = this.createNodeHtml(nodeId, nodeInfo.data);
        const nodeElement = document.querySelector(`#node-${nodeId} .drawflow_content_node`);
        if (nodeElement) {
            nodeElement.innerHTML = html;
        }
    },
    
    /**
     * Delete node
     */
    deleteNode: function() {
        if (!this.currentNode) return;
        
        if (confirm('Opravdu smazat tento uzel?')) {
            this.editor.removeNodeId(`node-${this.currentNode}`);
            $('#nodeEditModal').modal('hide');
            this.clearSelection();
        }
    },
    
    /**
     * Clear selection
     */
    clearSelection: function() {
        this.currentNode = null;
        $('#propertiesPanel').html(`
            <p class="text-muted text-center">
                <i class="fas fa-mouse-pointer"></i><br>
                Vyberte uzel pro zobrazení vlastností
            </p>
        `);
        $('#orchestratorCard').hide();
    },
    
    /**
     * Update statistics
     */
    updateStats: function() {
        const data = this.editor.export();
        const home = data.drawflow.Home.data;
        const nodeCount = Object.keys(home).length;
        
        let connectionCount = 0;
        Object.values(home).forEach(node => {
            Object.values(node.outputs).forEach(output => {
                connectionCount += output.connections.length;
            });
        });
        
        $('#nodeCount').text(nodeCount);
        $('#connectionCount').text(connectionCount);
    },
    
    /**
     * Save workflow
     */
    save: function() {
        const data = this.editor.export();
        const workflow = {
            projectId: this.projectId,
            projectName: this.projectName,
            drawflowData: JSON.stringify(data)
        };
        
        $.ajax({
            url: '/WorkflowPrototype/SaveWorkflow',
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(workflow),
            success: function(response) {
                if (response.success) {
                    toastr.success(response.message);
                } else {
                    toastr.error(response.message);
                }
            },
            error: function() {
                toastr.error('Chyba při ukládání workflow');
            }
        });
    },
    
    /**
     * Validate workflow
     */
    validate: function() {
        $.ajax({
            url: '/WorkflowPrototype/ValidateWorkflow',
            type: 'POST',
            data: { projectId: this.projectId },
            success: function(response) {
                if (response.success) {
                    toastr.success(response.message);
                } else {
                    let errorHtml = '<ul>' + response.errors.map(e => `<li>${e}</li>`).join('') + '</ul>';
                    toastr.error(errorHtml, 'Chyby ve workflow', {
                        timeOut: 0,
                        extendedTimeOut: 0,
                        closeButton: true,
                        progressBar: false
                    });
                }
            }
        });
    },
    
    /**
     * Export workflow
     */
    export: function() {
        window.location.href = `/WorkflowPrototype/ExportWorkflow?projectId=${this.projectId}`;
    },
    
    /**
     * Import workflow
     */
    import: function() {
        $('#importModal').modal('show');
    },
    
    /**
     * Do import
     */
    doImport: function() {
        const file = document.getElementById('importFile').files[0];
        if (!file) {
            toastr.warning('Vyberte soubor');
            return;
        }
        
        const formData = new FormData();
        formData.append('file', file);
        
        $.ajax({
            url: `/WorkflowPrototype/ImportWorkflow?projectId=${this.projectId}`,
            type: 'POST',
            data: formData,
            processData: false,
            contentType: false,
            success: (response) => {
                if (response.success) {
                    toastr.success(response.message);
                    location.reload();
                } else {
                    toastr.error(response.message);
                }
            }
        });
    },
    
    /**
     * Clear workflow
     */
    clear: function() {
        if (!confirm('Opravdu vymazat celý workflow?')) return;
        
        $.ajax({
            url: '/WorkflowPrototype/ClearWorkflow',
            type: 'POST',
            data: { projectId: this.projectId },
            success: (response) => {
                if (response.success) {
                    this.editor.clear();
                    this.updateStats();
                    toastr.success(response.message);
                }
            }
        });
    },
    
    /**
     * Load existing data
     */
    loadData: function(data) {
        try {
            this.editor.import(data);
            this.updateStats();
        } catch (e) {
            console.error('Error loading workflow data:', e);
        }
    }
};