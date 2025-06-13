// Universal Workflow Viewer - Uses IDENTICAL code from WorkflowDesigner
class WorkflowViewer {
    constructor(containerId, options = {}) {
        this.containerId = containerId;
        this.readonly = options.readonly || false;
        this.projectId = options.projectId;
        this.nodes = {};
        this.connections = [];
        this.selectedNode = null;
        
        this.init();
    }
    
    init() {
        const container = $(`#${this.containerId}`);
        
        // Clear and setup container with IDENTICAL structure as WorkflowDesigner
        container.empty();
        container.css({
            width: '100%',
            height: '400px',
            position: 'relative',
            overflow: 'auto',
            background: '#f8f9fa',
            border: '1px solid #ddd',
            borderRadius: '4px'
        });
        
        // Create canvas with IDENTICAL styling
        const canvas = $('<div>')
            .attr('id', `${this.containerId}-canvas`)
            .css({
                width: '2000px',
                height: '400px',
                position: 'relative',
                backgroundImage: 'linear-gradient(rgba(0,0,0,.05) 1px, transparent 1px), linear-gradient(90deg, rgba(0,0,0,.05) 1px, transparent 1px)',
                backgroundSize: '20px 20px'
            });
            
        container.append(canvas);
        
        // Create SVG for connections
        this.createConnectionsSVG();
        
        if (!this.readonly) {
            this.initializeEvents();
        }
    }
    
    createConnectionsSVG() {
        const container = $(`#${this.containerId}`);
        const svg = $(/*html*/`
            <svg id="${this.containerId}-svg" style="position: absolute; top: 0; left: 0; width: 100%; height: 100%; pointer-events: none; z-index: 1;">
                <defs>
                    <marker id="arrowhead-${this.containerId}" markerWidth="10" markerHeight="7" refX="9" refY="3.5" orient="auto">
                        <polygon points="0 0, 10 3.5, 0 7" fill="#6c757d"/>
                    </marker>
                </defs>
            </svg>
        `);
        container.append(svg);
    }
    
    loadWorkflow(projectId) {
        this.projectId = projectId;
        
        $.ajax({
            url: '/WorkflowDesigner/LoadWorkflow',
            type: 'GET',
            data: { projectId: projectId },
            success: (response) => {
                console.log('Loaded workflow data in detail view:', response);
                if (response.success && response.workflow) {
                    this.renderWorkflowData(response.workflow);
                } else {
                    this.showEmptyState();
                }
            },
            error: () => {
                this.showEmptyState();
            }
        });
    }
    
    renderWorkflowData(workflowData) {
        this.clearCanvas();
        
        // Check format and load accordingly - IDENTICAL logic as WorkflowDesigner
        if (workflowData.metadata && workflowData.metadata.orchestratorData) {
            this.loadFromOrchestratorFormat(workflowData.metadata.orchestratorData);
        } else if (workflowData.nodes && workflowData.nodes.length > 0) {
            // Load nodes directly from workflow
            workflowData.nodes.forEach(node => {
                const nodeData = {
                    id: node.id,
                    type: node.type.toLowerCase(),
                    name: node.name,
                    x: node.position.x,
                    y: node.position.y,
                    tools: node.tools || [],
                    description: ''
                };
                this.nodes[node.id] = nodeData;
                this.renderNode(nodeData);
            });
            
            // Load edges
            if (workflowData.edges) {
                workflowData.edges.forEach(edge => {
                    this.connections.push({
                        from: edge.source,
                        to: edge.target,
                        branch: edge.branch
                    });
                });
                this.updateConnections();
            }
        } else {
            this.showEmptyState();
        }
    }
    
    loadFromOrchestratorFormat(orchestratorData) {
        // IDENTICAL logic from WorkflowDesigner - use EXACT same positions
        if (orchestratorData.steps) {
            orchestratorData.steps.forEach((step, index) => {
                // Use saved positions from designer, or default vertical layout
                let position = { x: 400, y: 100 + (index * 200) };
                
                if (orchestratorData.metadata && orchestratorData.metadata.nodePositions && orchestratorData.metadata.nodePositions[step.id]) {
                    position = orchestratorData.metadata.nodePositions[step.id];
                }
                
                console.log(`Loading step ${step.id} at position:`, position);
                
                const nodeData = {
                    id: step.id,
                    type: step.type || 'task',
                    name: step.name,
                    x: position.x,
                    y: position.y,
                    tools: step.tools || [],
                    description: step.description || '',
                    useReAct: step.useReAct || false,
                    orchestrator: step.orchestrator,
                    model: step.model,
                    temperature: step.temperature,
                    systemPrompt: step.systemPrompt
                };
                
                this.nodes[step.id] = nodeData;
                this.renderNode(nodeData);
                
                // Add connections
                if (step.next) {
                    this.connections.push({
                        from: step.id,
                        to: step.next
                    });
                }
                
                if (step.branches) {
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
                }
            });
            
            this.updateConnections();
        }
    }
    
    // IDENTICAL renderNode function from WorkflowDesigner
    renderNode(node) {
        const canvas = $(`#${this.containerId}-canvas`);
        
        const nodeEl = $('<div>')
            .addClass('workflow-node')
            .addClass(node.type)
            .attr('id', node.id)
            .css({
                left: node.x + 'px',
                top: node.y + 'px'
            });
            
        console.log(`Rendering node ${node.id} at CSS position: left: ${node.x}px, top: ${node.y}px`);
        
        // Create wrapper for diamond-shaped nodes (condition and parallel)
        let contentWrapper;
        if (node.type === 'condition' || node.type === 'parallel') {
            contentWrapper = $('<div>').addClass('node-content-wrapper');
            nodeEl.append(contentWrapper);
        } else {
            contentWrapper = nodeEl;
        }
        
        // Header
        const header = $('<div>').addClass('node-header');
        header.append($('<span>').html(`<i class="${this.getNodeIcon(node.type)}"></i> ${node.name}`));
        
        contentWrapper.append(header);
        
        // Description
        if (node.description) {
            const descDiv = $('<div>')
                .css({
                    fontSize: '12px',
                    color: '#666',
                    marginTop: '5px',
                    fontStyle: 'italic'
                })
                .text(node.description);
            contentWrapper.append(descDiv);
        }
        
        // Tools
        if (node.tools && node.tools.length > 0) {
            const toolsDiv = $('<div>').addClass('node-tools');
            node.tools.forEach(tool => {
                toolsDiv.append($('<span>').addClass('tool-badge').text(tool.replace(/_/g, ' ')));
            });
            contentWrapper.append(toolsDiv);
        }
        
        // ReAct badge
        if (node.useReAct) {
            const reactBadge = $('<span>')
                .addClass('badge badge-purple')
                .html('<i class="fas fa-brain"></i> ReAct')
                .css({
                    position: 'absolute',
                    top: '5px',
                    right: '25px',
                    fontSize: '11px'
                });
            nodeEl.append(reactBadge);
        }
        
        // Connection dots only if not readonly
        if (!this.readonly) {
            nodeEl.append($('<div>').addClass('connection-dot input'));
            
            if (node.type === 'condition') {
                nodeEl.append($('<div>')
                    .addClass('connection-dot output true')
                    .attr('title', 'TRUE')
                    .attr('data-branch', 'true'));
                nodeEl.append($('<div>')
                    .addClass('connection-dot output false')
                    .attr('title', 'FALSE')
                    .attr('data-branch', 'false'));
            } else {
                nodeEl.append($('<div>').addClass('connection-dot output'));
            }
        }
        
        canvas.append(nodeEl);
    }
    
    getNodeIcon(type) {
        const icons = {
            'task': 'fas fa-tasks',
            'process': 'fas fa-tasks',
            'condition': 'fas fa-question-circle',
            'parallel': 'fas fa-code-branch',
            'ai-tool': 'fas fa-robot'
        };
        return icons[type] || 'fas fa-circle';
    }
    
    updateConnections() {
        const svg = $(`#${this.containerId}-svg`);
        svg.find('path, line').remove(); // Clear existing connections
        
        console.log('Drawing connections:', this.connections);
        
        this.connections.forEach(conn => {
            const fromNode = $(`#${conn.from}`);
            const toNode = $(`#${conn.to}`);
            
            console.log(`Connection ${conn.from} -> ${conn.to}: fromNode found: ${fromNode.length}, toNode found: ${toNode.length}`);
            
            if (fromNode.length && toNode.length) {
                this.drawConnection(fromNode, toNode, conn.branch);
            }
        });
    }
    
    drawConnection(fromNode, toNode, branch) {
        const svg = $(`#${this.containerId}-svg`)[0];
        
        // Calculate positions - IDENTICAL logic
        const fromRect = fromNode[0].getBoundingClientRect();
        const toRect = toNode[0].getBoundingClientRect();
        const containerRect = $(`#${this.containerId}`)[0].getBoundingClientRect();
        
        let x1, y1, x2, y2;
        
        // Calculate connection points based on branch
        if (branch === 'true') {
            x1 = fromRect.right - containerRect.left;
            y1 = fromRect.top - containerRect.top + fromRect.height * 0.2;
        } else if (branch === 'false') {
            x1 = fromRect.right - containerRect.left;
            y1 = fromRect.top - containerRect.top + fromRect.height * 0.8;
        } else {
            x1 = fromRect.right - containerRect.left;
            y1 = fromRect.top - containerRect.top + fromRect.height / 2;
        }
        
        x2 = toRect.left - containerRect.left;
        y2 = toRect.top - containerRect.top + toRect.height / 2;
        
        // Create path with curve
        const midX = (x1 + x2) / 2;
        const path = document.createElementNS('http://www.w3.org/2000/svg', 'path');
        path.setAttribute('d', `M ${x1} ${y1} Q ${midX} ${y1} ${midX} ${(y1 + y2) / 2} Q ${midX} ${y2} ${x2} ${y2}`);
        path.setAttribute('stroke', branch === 'true' ? '#28a745' : branch === 'false' ? '#dc3545' : '#6c757d');
        path.setAttribute('stroke-width', '2');
        path.setAttribute('fill', 'none');
        path.setAttribute('marker-end', `url(#arrowhead-${this.containerId})`);
        
        svg.appendChild(path);
    }
    
    clearCanvas() {
        $(`#${this.containerId}-canvas`).empty();
        this.nodes = {};
        this.connections = [];
    }
    
    showEmptyState() {
        const canvas = $(`#${this.containerId}-canvas`);
        canvas.html(`
            <div style="display: flex; align-items: center; justify-content: center; height: 100%; text-align: center;">
                <div>
                    <i class="fas fa-project-diagram fa-3x text-muted mb-3"></i>
                    <p class="text-muted">Workflow není definováno</p>
                    ${this.projectId ? `<a href="/WorkflowDesigner?projectId=${this.projectId}" class="btn btn-primary">
                        <i class="fas fa-plus"></i> Vytvořit workflow
                    </a>` : ''}
                </div>
            </div>
        `);
    }
    
    initializeEvents() {
        // Add events for interactive mode if needed
    }
}

// Make globally available
window.WorkflowViewer = WorkflowViewer;