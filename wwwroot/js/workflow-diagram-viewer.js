// Workflow Diagram Viewer - Read-only component for displaying workflows
(function(window) {
    'use strict';

    class WorkflowDiagramViewer {
        constructor(containerId, projectId) {
            this.containerId = containerId;
            this.projectId = projectId;
            this.container = document.getElementById(containerId);
            this.nodes = {};
            this.connections = [];
            
            if (!this.container) {
                console.error('Container not found:', containerId);
                return;
            }
            
            this.init();
        }
        
        init() {
            // Create the diagram structure
            this.container.innerHTML = `
                <div class="workflow-diagram-readonly" style="position: relative; width: 100%; height: 100%; overflow: auto; background: #f8f9fa;">
                    <div class="workflow-canvas-readonly" style="position: relative; width: 2000px; height: 1500px; background-image: linear-gradient(rgba(0,0,0,.05) 1px, transparent 1px), linear-gradient(90deg, rgba(0,0,0,.05) 1px, transparent 1px); background-size: 20px 20px;">
                        <svg class="workflow-svg-readonly" style="position: absolute; top: 0; left: 0; width: 100%; height: 100%; pointer-events: none; z-index: 1;">
                            <defs>
                                <marker id="arrowhead-viewer" markerWidth="8" markerHeight="6" refX="7" refY="3" orient="auto">
                                    <polygon points="0 0, 8 3, 0 6" fill="#007bff" />
                                </marker>
                            </defs>
                        </svg>
                    </div>
                </div>
            `;
            
            this.canvas = this.container.querySelector('.workflow-canvas-readonly');
            this.svg = this.container.querySelector('.workflow-svg-readonly');
            
            // Load workflow data
            this.loadWorkflow();
        }
        
        loadWorkflow() {
            fetch(`/api/workflow-designer/${this.projectId}`)
                .then(response => response.json())
                .then(data => {
                    console.log('Workflow data loaded:', data);
                    if (data.success && data.data) {
                        // The API returns data in 'data' property
                        this.renderFromOrchestratorData(data.data);
                    }
                })
                .catch(error => {
                    console.error('Failed to load workflow:', error);
                });
        }
        
        renderWorkflow(workflow) {
            console.log('Rendering workflow:', workflow);
            
            // Clear existing
            this.canvas.querySelectorAll('.workflow-node').forEach(node => node.remove());
            this.svg.querySelectorAll('line').forEach(line => line.remove());
            
            // Check if we have orchestrator data
            if (workflow.metadata && workflow.metadata.orchestratorData) {
                console.log('Found orchestrator data, rendering from it');
                this.renderFromOrchestratorData(workflow.metadata.orchestratorData);
            } else if (workflow.nodes && workflow.nodes.length > 0) {
                // Render nodes
                workflow.nodes.forEach(node => {
                    this.renderNode({
                        id: node.id,
                        type: node.type.toLowerCase(),
                        name: node.name,
                        x: node.position.x,
                        y: node.position.y,
                        tools: node.tools || []
                    });
                });
                
                // Render connections
                if (workflow.edges) {
                    workflow.edges.forEach(edge => {
                        this.connections.push({
                            from: edge.source,
                            to: edge.target
                        });
                    });
                    this.updateConnections();
                }
            }
        }
        
        renderFromOrchestratorData(data) {
            console.log('Rendering from orchestrator data:', data);
            
            if (!data || !data.steps) {
                console.log('No data or steps to render');
                return;
            }
            
            console.log('Number of steps to render:', data.steps.length);
            const positions = data.metadata?.nodePositions || {};
            
            // Create nodes from steps
            data.steps.forEach((step, index) => {
                console.log('Processing step:', step);
                const pos = positions[step.id] || {
                    x: 200 + (index % 3) * 250,
                    y: 200 + Math.floor(index / 3) * 150
                };
                
                // Map step type to node type
                let nodeType = 'task';
                if (step.type === 'decision') nodeType = 'condition';
                else if (step.type === 'parallel-gateway') nodeType = 'parallel';
                else if (step.type === 'tool') nodeType = 'ai-tool';
                else if (step.type === 'process') nodeType = 'task';
                else if (step.type === 'input-adapter') nodeType = 'task';
                else if (step.type === 'output-adapter') nodeType = 'task';
                
                this.renderNode({
                    id: step.id,
                    type: nodeType,
                    name: step.name,
                    x: pos.x,
                    y: pos.y,
                    tools: step.tools || [],
                    description: step.description || '',
                    useReAct: step.useReAct
                });
            });
            
            // Create connections
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
            
            this.updateConnections();
        }
        
        renderNode(node) {
            console.log('Rendering node:', node);
            
            const nodeEl = document.createElement('div');
            nodeEl.className = 'workflow-node ' + node.type;
            nodeEl.id = node.id;
            nodeEl.style.cssText = `
                position: absolute;
                left: ${node.x}px;
                top: ${node.y}px;
                background: #ffffff;
                border: 2px solid #007bff;
                border-radius: 8px;
                padding: 15px;
                min-width: 180px;
                cursor: default;
                user-select: none;
                box-shadow: 0 2px 8px rgba(0,0,0,0.1);
                z-index: 5;
                color: #333;
            `;
            
            // Special styling for condition and parallel nodes
            if (node.type === 'condition' || node.type === 'parallel') {
                nodeEl.style.width = '140px';
                nodeEl.style.height = '140px';
                nodeEl.style.border = 'none';
                nodeEl.style.background = 'transparent';
                nodeEl.style.padding = '0';
                nodeEl.style.minWidth = 'unset';
                nodeEl.style.boxShadow = 'none';
                
                // Add diamond shape
                const diamond = document.createElement('div');
                diamond.style.cssText = `
                    position: absolute;
                    top: 0;
                    left: 0;
                    width: 100%;
                    height: 100%;
                    transform: rotate(45deg);
                    border: 3px solid ${node.type === 'condition' ? '#ffc107' : '#6f42c1'};
                    background: ${node.type === 'condition' ? '#fff8e1' : '#f3e5f5'};
                    border-radius: 15px;
                    z-index: 1;
                    box-shadow: 0 2px 8px rgba(0,0,0,0.1);
                `;
                nodeEl.appendChild(diamond);
                
                if (node.type === 'parallel') {
                    const plus = document.createElement('div');
                    plus.style.cssText = `
                        position: absolute;
                        top: 50%;
                        left: 50%;
                        transform: translate(-50%, -50%);
                        font-size: 48px;
                        font-weight: bold;
                        color: #6f42c1;
                        z-index: 2;
                    `;
                    plus.textContent = '+';
                    nodeEl.appendChild(plus);
                }
            }
            
            // Create content wrapper
            const contentWrapper = document.createElement('div');
            if (node.type === 'condition' || node.type === 'parallel') {
                contentWrapper.style.cssText = `
                    position: relative;
                    width: 100%;
                    height: 100%;
                    display: flex;
                    flex-direction: column;
                    align-items: center;
                    justify-content: center;
                    padding: 20px;
                    z-index: 3;
                `;
            }
            
            // Header
            const header = document.createElement('div');
            header.style.cssText = 'font-weight: bold; margin-bottom: 8px;';
            header.innerHTML = `<i class="${this.getNodeIcon(node.type)}"></i> ${node.name}`;
            contentWrapper.appendChild(header);
            
            // Description
            if (node.description) {
                const desc = document.createElement('div');
                desc.style.cssText = 'font-size: 12px; color: #666; margin-top: 5px; font-style: italic;';
                desc.textContent = node.description;
                contentWrapper.appendChild(desc);
            }
            
            // Tools
            if (node.tools && node.tools.length > 0) {
                const toolsDiv = document.createElement('div');
                toolsDiv.style.cssText = 'margin-top: 8px; display: flex; flex-wrap: wrap; gap: 4px;';
                node.tools.forEach(tool => {
                    const badge = document.createElement('span');
                    badge.style.cssText = 'background: #e9ecef; color: #495057; padding: 2px 6px; border-radius: 3px; font-size: 11px;';
                    badge.textContent = tool.replace(/_/g, ' ');
                    toolsDiv.appendChild(badge);
                });
                contentWrapper.appendChild(toolsDiv);
            }
            
            // ReAct badge
            if (node.useReAct) {
                const reactBadge = document.createElement('span');
                reactBadge.style.cssText = `
                    position: absolute;
                    top: 5px;
                    right: 5px;
                    background: #6f42c1;
                    color: white;
                    padding: 2px 6px;
                    border-radius: 3px;
                    font-size: 11px;
                `;
                reactBadge.innerHTML = '<i class="fas fa-brain"></i> ReAct';
                nodeEl.appendChild(reactBadge);
            }
            
            // Add connection dots
            if (node.type !== 'condition' && node.type !== 'parallel') {
                // Input dot (top)
                const inputDot = document.createElement('div');
                inputDot.style.cssText = `
                    position: absolute;
                    width: 12px;
                    height: 12px;
                    background: #007bff;
                    border: 2px solid white;
                    border-radius: 50%;
                    left: 50%;
                    top: -8px;
                    transform: translateX(-50%);
                    box-shadow: 0 1px 3px rgba(0,0,0,0.3);
                `;
                nodeEl.appendChild(inputDot);
                
                // Output dot (bottom)
                const outputDot = document.createElement('div');
                outputDot.style.cssText = `
                    position: absolute;
                    width: 12px;
                    height: 12px;
                    background: #007bff;
                    border: 2px solid white;
                    border-radius: 50%;
                    left: 50%;
                    bottom: -8px;
                    transform: translateX(-50%);
                    box-shadow: 0 1px 3px rgba(0,0,0,0.3);
                `;
                nodeEl.appendChild(outputDot);
            }
            
            nodeEl.appendChild(contentWrapper);
            this.canvas.appendChild(nodeEl);
            
            // Store node reference
            this.nodes[node.id] = nodeEl;
        }
        
        getNodeIcon(type) {
            const icons = {
                'task': 'fas fa-tasks',
                'condition': 'fas fa-question-circle',
                'parallel': 'fas fa-code-branch',
                'ai-tool': 'fas fa-robot'
            };
            return icons[type] || 'fas fa-circle';
        }
        
        updateConnections() {
            // Clear existing lines
            this.svg.querySelectorAll('line').forEach(line => line.remove());
            
            this.connections.forEach(conn => {
                const fromNode = this.nodes[conn.from];
                const toNode = this.nodes[conn.to];
                
                if (fromNode && toNode) {
                    this.drawConnection(fromNode, toNode, conn.branch);
                }
            });
        }
        
        drawConnection(fromNode, toNode, branch) {
            const fromRect = fromNode.getBoundingClientRect();
            const toRect = toNode.getBoundingClientRect();
            const containerRect = this.canvas.getBoundingClientRect();
            
            let x1, y1, x2, y2;
            
            // Always connect from bottom of fromNode to top of toNode
            x1 = fromRect.left - containerRect.left + fromRect.width / 2;
            y1 = fromRect.bottom - containerRect.top;
            
            x2 = toRect.left - containerRect.left + toRect.width / 2;
            y2 = toRect.top - containerRect.top;
            
            // Create line instead of path for consistency with designer
            const line = document.createElementNS('http://www.w3.org/2000/svg', 'line');
            line.setAttribute('x1', x1);
            line.setAttribute('y1', y1);
            line.setAttribute('x2', x2);
            line.setAttribute('y2', y2);
            line.setAttribute('stroke', branch === 'true' ? '#28a745' : branch === 'false' ? '#dc3545' : '#007bff');
            line.setAttribute('stroke-width', '2');
            line.setAttribute('marker-end', 'url(#arrowhead-viewer)');
            
            this.svg.appendChild(line);
        }
    }
    
    // Export to global scope
    window.WorkflowDiagramViewer = WorkflowDiagramViewer;
})(window);