/**
 * Workflow Manager Module
 * Hlavní modul pro správu workflow designeru
 */
export class WorkflowManager {
    constructor(options = {}) {
        this.projectId = options.projectId;
        this.workflowId = options.workflowId || null;
        this.nodes = {};
        this.connections = [];
        this.selectedNode = null;
        this.selectedConnection = null;
        this.nodeIdCounter = 1;
        this.currentZoom = 1.0;
        
        // Configuration
        this.workflowIOConfig = {
            input: {
                type: 'images',
                config: {
                    formats: ['jpg', 'png', 'webp'],
                    maxSize: '10MB',
                    source: 'upload'
                }
            },
            output: {
                type: 'excel',
                config: {
                    format: 'xlsx',
                    includeImages: true,
                    fields: ['url', 'price', 'availability', 'similarity']
                }
            }
        };
        
        // API endpoints
        this.apiEndpoints = {
            get: `/api/workflow-designer/${this.projectId}`,
            save: `/api/workflow-designer/${this.projectId}`,
            validate: '/api/workflow-designer/validate',
            export: `/api/workflow-designer/${this.projectId}/export`,
            import: `/api/workflow-designer/${this.projectId}/import`
        };
    }
    
    /**
     * Initialize workflow manager
     */
    async initialize() {
        await this.loadWorkflow();
    }
    
    /**
     * Load workflow from server
     */
    async loadWorkflow() {
        try {
            const response = await fetch(this.apiEndpoints.get);
            const result = await response.json();
            
            if (result.success && result.data) {
                this.loadFromOrchestratorFormat(result.data);
            }
        } catch (error) {
            console.error('Error loading workflow:', error);
        }
    }
    
    /**
     * Save workflow to server
     */
    async saveWorkflow() {
        // Validate tools before saving
        const validationResult = this.validateTools();
        if (!validationResult.isValid) {
            throw new Error(validationResult.errors.join(', '));
        }
        
        const workflowData = this.convertToOrchestratorFormat();
        
        try {
            const response = await fetch(this.apiEndpoints.save, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({
                    WorkflowData: JSON.stringify(workflowData)
                })
            });
            
            if (!response.ok) {
                const errorText = await response.text();
                console.error('Save failed with status:', response.status);
                console.error('Error response:', errorText);
                throw new Error(`HTTP ${response.status}: ${response.statusText}`);
            }
            
            const result = await response.json();
            
            if (result.success && result.data && result.data.id) {
                this.workflowId = result.data.id;
                return result;
            } else {
                throw new Error(result.message || 'Save failed');
            }
        } catch (error) {
            console.error('Error saving workflow:', error);
            throw error;
        }
    }
    
    /**
     * Validate workflow
     */
    async validateWorkflow() {
        const workflowData = this.convertToOrchestratorFormat();
        
        try {
            const response = await fetch(this.apiEndpoints.validate, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify(workflowData)
            });
            
            const result = await response.json();
            return result.data || { isValid: false, errors: ['Validation failed'] };
        } catch (error) {
            console.error('Error validating workflow:', error);
            return { isValid: false, errors: [error.message] };
        }
    }
    
    /**
     * Export workflow
     */
    async exportWorkflow(format = 'json') {
        const workflowData = this.convertToOrchestratorFormat();
        const jsonStr = JSON.stringify(workflowData, null, 2);
        
        const blob = new Blob([jsonStr], { type: 'application/json' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `workflow-${this.projectId || 'export'}-orchestrator.json`;
        a.click();
        URL.revokeObjectURL(url);
    }
    
    /**
     * Clear workflow
     */
    clearWorkflow() {
        this.nodes = {};
        this.connections = [];
        this.selectedNode = null;
        this.nodeIdCounter = 1;
    }
    
    /**
     * Update node ID counter based on existing nodes
     */
    updateNodeIdCounter() {
        let maxId = 0;
        Object.keys(this.nodes).forEach(nodeId => {
            const match = nodeId.match(/node_(\d+)/);
            if (match) {
                const id = parseInt(match[1]);
                if (id > maxId) {
                    maxId = id;
                }
            }
        });
        this.nodeIdCounter = maxId + 1;
    }
    
    /**
     * Add node
     */
    addNode(type, x, y, tool = null) {
        const nodeId = 'node_' + this.nodeIdCounter++;
        
        const node = {
            id: nodeId,
            type: type,
            name: this.getNodeTypeName(type),
            x: x,
            y: y,
            tool: tool || null,
            description: '',
            configuration: {}
        };
        
        this.nodes[nodeId] = node;
        return node;
    }
    
    /**
     * Remove node
     */
    removeNode(nodeId) {
        delete this.nodes[nodeId];
        this.connections = this.connections.filter(c => c.from !== nodeId && c.to !== nodeId);
        
        if (this.selectedNode === nodeId) {
            this.selectedNode = null;
        }
    }
    
    /**
     * Add connection
     */
    addConnection(from, to, branch = null) {
        // Check if connection already exists
        const exists = this.connections.some(c => 
            c.from === from && c.to === to && c.branch === branch
        );
        
        if (!exists) {
            const connection = { from, to };
            if (branch) {
                connection.branch = branch;
            }
            this.connections.push(connection);
            return true;
        }
        
        return false;
    }
    
    /**
     * Remove connection
     */
    removeConnection(from, to, branch = null) {
        console.log('Removing connection:', from, '->', to, 'branch:', branch);
        console.log('Connections before:', this.connections.length);
        this.connections = this.connections.filter(c => {
            // Handle both null and undefined for branch comparison
            const branchMatch = (c.branch === branch) || (c.branch == null && branch == null);
            const isMatch = c.from === from && c.to === to && branchMatch;
            if (isMatch) {
                console.log('Found matching connection to remove');
            }
            return !isMatch;
        });
        console.log('Connections after:', this.connections.length);
    }
    
    /**
     * Remove connection between two nodes (alias for removeConnection)
     */
    removeConnectionBetween(fromId, toId, branch = null) {
        this.removeConnection(fromId, toId, branch);
    }
    
    /**
     * Select node
     */
    selectNode(nodeId) {
        this.selectedNode = nodeId;
        this.selectedConnection = null;
    }
    
    /**
     * Select connection
     */
    selectConnection(from, to, branch = null) {
        console.log('Selecting connection:', from, '->', to, 'branch:', branch);
        this.selectedNode = null;
        this.selectedConnection = { from, to, branch };
        console.log('Selected connection set to:', this.selectedConnection);
    }
    
    /**
     * Deselect all
     */
    deselectAll() {
        this.selectedNode = null;
        this.selectedConnection = null;
    }
    
    /**
     * Is connection selected
     */
    isConnectionSelected(from, to, branch = null) {
        if (!this.selectedConnection) return false;
        // Handle both null and undefined for branch comparison
        const branchMatch = (this.selectedConnection.branch === branch) || 
                          (this.selectedConnection.branch == null && branch == null);
        return this.selectedConnection.from === from && 
               this.selectedConnection.to === to && 
               branchMatch;
    }
    
    /**
     * Get node by ID
     */
    getNode(nodeId) {
        return this.nodes[nodeId];
    }
    
    /**
     * Mark workflow as modified
     */
    markAsModified() {
        // Emit event or update UI to indicate changes
        console.log('Workflow marked as modified');
    }
    
    /**
     * Update node
     */
    updateNode(nodeId, updates) {
        if (this.nodes[nodeId]) {
            Object.assign(this.nodes[nodeId], updates);
        }
    }
    
    /**
     * Get node type display name
     */
    getNodeTypeName(type) {
        const names = {
            'task': 'Proces',
            'InputAdapter': 'Input Adaptér',
            'OutputAdapter': 'Output Adaptér',
            'condition': 'Rozhodnutí',
            'parallel': 'Paralelní brána',
            'tool': 'AI Tool'
        };
        return names[type] || type;
    }
    
    /**
     * Validate tools
     */
    validateTools() {
        const toolNodes = Object.values(this.nodes).filter(n => n.type === 'tool');
        const errors = [];
        
        toolNodes.forEach(node => {
            if (!node.tool) {
                errors.push(`Uzel '${node.name}' nemá přiřazený nástroj`);
            }
        });
        
        return {
            isValid: errors.length === 0,
            errors: errors
        };
    }
    
    /**
     * Convert to orchestrator format
     */
    convertToOrchestratorFormat() {
        const steps = [];
        const nodeMap = {};
        
        // First pass - create steps
        Object.values(this.nodes).forEach((node, index) => {
            const step = {
                id: node.id,
                name: node.name,
                type: this.mapNodeTypeToStepType(node.type),
                description: node.description || '',
                position: index
            };
            
            // Add type-specific properties
            if (node.type === 'tool') {
                step.tool = node.tool || null;
                step.useReAct = node.useReAct || false;
                step.timeoutSeconds = node.timeoutSeconds || 300;
                step.retryCount = node.retryCount || 3;
                step.configuration = node.configuration || {};
            } else if (node.type === 'InputAdapter' || node.type === 'OutputAdapter') {
                step.adapterId = node.selectedAdapter || null;
                step.adapterConfiguration = node.adapterConfiguration || {};
                step.adapterType = node.type === 'InputAdapter' ? 'Input' : 'Output';
            } else if (node.type === 'condition') {
                step.condition = node.condition || 'true';
                step.branches = {
                    true: [],
                    false: []
                };
            } else if (node.type === 'parallel') {
                step.branches = [];
            } else if (node.type === 'orchestrator') {
                step.orchestratorType = node.orchestratorType || null;
                step.useReAct = node.useReAct || false;
                step.timeoutSeconds = node.timeoutSeconds || 600;
                step.retryCount = node.retryCount || 2;
                step.configuration = node.configuration || {};
            }
            
            steps.push(step);
            nodeMap[node.id] = step;
        });
        
        // Second pass - build connections
        const nodesWithIncoming = new Set(this.connections.map(c => c.to));
        const entryNodes = Object.keys(nodeMap).filter(id => !nodesWithIncoming.has(id));
        const firstStepId = entryNodes[0] || Object.keys(nodeMap)[0];
        
        const nodesWithOutgoing = new Set(this.connections.map(c => c.from));
        const lastStepIds = Object.keys(nodeMap).filter(id => !nodesWithOutgoing.has(id));
        
        // Build step sequence
        steps.forEach(step => {
            const outgoingConnections = this.connections.filter(c => c.from === step.id);
            
            if (step.type === 'decision') {
                outgoingConnections.forEach(conn => {
                    const targetStep = nodeMap[conn.to];
                    if (targetStep) {
                        const branch = conn.branch || 'true';
                        step.branches[branch].push(targetStep.id);
                    }
                });
                delete step.next;
            } else if (step.type === 'parallel-gateway') {
                step.branches = outgoingConnections
                    .map(conn => nodeMap[conn.to]?.id)
                    .filter(id => id != null);
                delete step.next;
            } else {
                const nextConnection = outgoingConnections[0];
                if (nextConnection) {
                    const nextStep = nodeMap[nextConnection.to];
                    if (nextStep) {
                        step.next = nextStep.id;
                    }
                } else {
                    step.isFinal = true;
                }
            }
        });
        
        const result = {
            name: 'Workflow ' + new Date().toISOString(),
            description: 'Visual workflow designer output',
            firstStepId: firstStepId,
            lastStepIds: lastStepIds,
            steps: steps,
            input: this.workflowIOConfig.input,
            output: this.workflowIOConfig.output,
            metadata: {
                createdWith: 'SimpleWorkflowDesigner',
                createdAt: new Date().toISOString(),
                nodePositions: Object.values(this.nodes).reduce((acc, node) => {
                    acc[node.id] = { 
                        x: node.x, 
                        y: node.y,
                        type: node.type
                    };
                    return acc;
                }, {}),
                settings: {}
            }
        };
        
        return result;
    }
    
    /**
     * Load from orchestrator format
     */
    loadFromOrchestratorFormat(data, clearExisting = true) {
        if (!data) {
            return;
        }
        
        if (!data.steps) {
            return;
        }
        
        // Load I/O configuration
        if (data.input) {
            this.workflowIOConfig.input = data.input;
        }
        if (data.output) {
            this.workflowIOConfig.output = data.output;
        }
        
        // Clear existing only if requested (default for initial load)
        if (clearExisting) {
            this.clearWorkflow();
        }
        
        // Get node positions from metadata
        const positions = data.metadata?.nodePositions || {};
        
        // Create nodes from steps
        data.steps.forEach((step, index) => {
            const pos = positions[step.id] || {
                x: 1000,
                y: 300 + (index * 150)
            };
            
            // Map step type back to node type
            let nodeType = 'task';
            if (step.type === 'decision') nodeType = 'condition';
            else if (step.type === 'parallel-gateway') nodeType = 'parallel';
            else if (step.type === 'merge') nodeType = 'merge';
            else if (step.type === 'tool') nodeType = 'tool';
            else if (step.type === 'input-adapter') nodeType = 'InputAdapter';
            else if (step.type === 'output-adapter') nodeType = 'OutputAdapter';
            else if (step.type === 'orchestrator') nodeType = 'orchestrator';
            
            // Create the node
            this.nodes[step.id] = {
                id: step.id,
                type: nodeType,
                name: step.name,
                x: pos.x,
                y: pos.y,
                tool: step.tool || null,
                description: step.description || '',
                condition: step.condition,
                useReAct: step.useReAct,
                timeoutSeconds: step.timeoutSeconds,
                retryCount: step.retryCount,
                configuration: step.configuration || {},
                selectedAdapter: step.adapterId,
                adapterConfiguration: step.adapterConfiguration,
                orchestratorType: step.orchestratorType
            };
        });
        
        
        // Update node ID counter to avoid conflicts with new nodes
        this.updateNodeIdCounter();
        
        // Recreate connections
        data.steps.forEach(step => {
            if (step.type === 'decision' && step.branches) {
                if (step.branches.true && step.branches.true.length > 0) {
                    this.connections.push({
                        from: step.id,
                        to: step.branches.true[0],
                        branch: 'true'
                    });
                }
                if (step.branches.false && step.branches.false.length > 0) {
                    this.connections.push({
                        from: step.id,
                        to: step.branches.false[0],
                        branch: 'false'
                    });
                }
            } else if (step.type === 'parallel-gateway' && step.branches) {
                step.branches.forEach(targetId => {
                    this.connections.push({
                        from: step.id,
                        to: targetId
                    });
                });
            } else if (step.next) {
                this.connections.push({
                    from: step.id,
                    to: step.next
                });
            }
        });
        
        // Trigger re-render callback if available
        if (this.onWorkflowLoaded) {
            this.onWorkflowLoaded();
        }
    }
    
    /**
     * Map node type to step type
     */
    mapNodeTypeToStepType(nodeType) {
        const typeMap = {
            'task': 'process',
            'tool': 'tool',
            'condition': 'decision',
            'parallel': 'parallel-gateway',
            'merge': 'merge',
            'InputAdapter': 'input-adapter',
            'OutputAdapter': 'output-adapter',
            'orchestrator': 'orchestrator'
        };
        return typeMap[nodeType] || nodeType;
    }
}