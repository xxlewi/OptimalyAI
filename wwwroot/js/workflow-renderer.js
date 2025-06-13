// Workflow Renderer - Shared component for displaying workflow diagrams
class WorkflowRenderer {
    constructor(containerId, readonly = true) {
        this.containerId = containerId;
        this.readonly = readonly;
        this.nodes = {};
        this.connections = [];
        this.svgNamespace = "http://www.w3.org/2000/svg";
    }

    renderWorkflow(workflowData) {
        console.log('Rendering workflow:', workflowData);
        const container = $(`#${this.containerId}`);
        container.empty();

        // Create SVG for connections
        const svg = document.createElementNS(this.svgNamespace, "svg");
        svg.setAttribute("width", "100%");
        svg.setAttribute("height", "100%");
        svg.setAttribute("style", "position: absolute; top: 0; left: 0; pointer-events: none; z-index: 1;");
        
        // Create defs for arrow marker
        const defs = document.createElementNS(this.svgNamespace, "defs");
        const marker = document.createElementNS(this.svgNamespace, "marker");
        marker.setAttribute("id", "arrowhead");
        marker.setAttribute("markerWidth", "10");
        marker.setAttribute("markerHeight", "7");
        marker.setAttribute("refX", "9");
        marker.setAttribute("refY", "3.5");
        marker.setAttribute("orient", "auto");
        
        const polygon = document.createElementNS(this.svgNamespace, "polygon");
        polygon.setAttribute("points", "0 0, 10 3.5, 0 7");
        polygon.setAttribute("fill", "#6c757d");
        
        marker.appendChild(polygon);
        defs.appendChild(marker);
        svg.appendChild(defs);
        
        container.append(svg);

        // Process workflow data - check for different formats
        let nodes = [];
        let edges = [];

        if (workflowData.nodes && workflowData.edges) {
            // Direct format
            nodes = workflowData.nodes;
            edges = workflowData.edges;
        } else if (workflowData.metadata && workflowData.metadata.orchestratorData) {
            // OrchestratorData format
            const orchestratorData = workflowData.metadata.orchestratorData;
            nodes = orchestratorData.steps.map(step => ({
                id: step.id,
                name: step.name,
                type: step.type,
                position: orchestratorData.metadata?.nodePositions?.[step.id] || { x: 100, y: 100 },
                tools: step.tools || []
            }));

            // Create edges from step connections
            orchestratorData.steps.forEach(step => {
                if (step.next) {
                    edges.push({
                        source: step.id,
                        target: step.next
                    });
                }
                if (step.branches) {
                    if (step.branches.true && step.branches.true.length > 0) {
                        edges.push({
                            source: step.id,
                            target: step.branches.true[0],
                            branch: 'true'
                        });
                    }
                    if (step.branches.false && step.branches.false.length > 0) {
                        edges.push({
                            source: step.id,
                            target: step.branches.false[0],
                            branch: 'false'
                        });
                    }
                }
            });
        }

        // Render nodes
        if (nodes.length > 0) {
            nodes.forEach(node => {
                this.createNodeElement(node, container);
            });

            // Draw connections after a delay to ensure nodes are positioned
            setTimeout(() => {
                this.drawConnections(edges, svg);
            }, 100);
        } else {
            // Show empty state
            container.html(`
                <div class="text-center py-5">
                    <i class="fas fa-project-diagram fa-3x text-muted mb-3"></i>
                    <p>Workflow není definováno</p>
                </div>
            `);
        }
    }

    createNodeElement(node, container) {
        const nodeClass = node.type === 'Condition' ? 'workflow-node condition' : 'workflow-node';
        
        let nodeContent;
        if (node.type === 'Condition') {
            nodeContent = `<div class="node-content">${node.name}</div>`;
        } else {
            nodeContent = `
                <div class="node-header">${node.name}</div>
                <div class="node-type">${node.type}</div>
                ${node.tools && node.tools.length > 0 ? `<div class="text-muted" style="font-size: 10px;">${node.tools.join(', ')}</div>` : ''}
            `;
        }

        const nodeEl = $(`
            <div class="${nodeClass}" id="${node.id}" style="left: ${node.position.x}px; top: ${node.position.y}px;">
                ${nodeContent}
                ${this.readonly ? '' : this.getConnectionDots(node.type)}
            </div>
        `);

        container.append(nodeEl);
        this.nodes[node.id] = node;
    }

    getConnectionDots(nodeType) {
        if (nodeType === 'Condition') {
            return `
                <div class="connection-dot input"></div>
                <div class="connection-dot output true" data-branch="true"></div>
                <div class="connection-dot output false" data-branch="false"></div>
            `;
        } else {
            return `
                <div class="connection-dot input"></div>
                <div class="connection-dot output"></div>
            `;
        }
    }

    drawConnections(edges, svg) {
        edges.forEach(edge => {
            const fromNode = $(`#${edge.source}`);
            const toNode = $(`#${edge.target}`);
            
            if (fromNode.length && toNode.length) {
                // Calculate connection points
                const fromLeft = parseInt(fromNode.css('left')) || 0;
                const fromTop = parseInt(fromNode.css('top')) || 0;
                const toLeft = parseInt(toNode.css('left')) || 0;
                const toTop = parseInt(toNode.css('top')) || 0;
                
                let x1, y1;
                if (edge.branch === 'true') {
                    x1 = fromLeft + fromNode.outerWidth();
                    y1 = fromTop + fromNode.outerHeight() * 0.2;
                } else if (edge.branch === 'false') {
                    x1 = fromLeft + fromNode.outerWidth();
                    y1 = fromTop + fromNode.outerHeight() * 0.8;
                } else {
                    x1 = fromLeft + fromNode.outerWidth();
                    y1 = fromTop + fromNode.outerHeight() / 2;
                }
                
                const x2 = toLeft;
                const y2 = toTop + toNode.outerHeight() / 2;
                
                // Create curved path for better visual appeal
                const midX = (x1 + x2) / 2;
                const path = document.createElementNS(this.svgNamespace, "path");
                path.setAttribute("d", `M ${x1} ${y1} Q ${midX} ${y1} ${midX} ${(y1 + y2) / 2} Q ${midX} ${y2} ${x2} ${y2}`);
                path.setAttribute("stroke", "#6c757d");
                path.setAttribute("stroke-width", "2");
                path.setAttribute("fill", "none");
                path.setAttribute("marker-end", "url(#arrowhead)");
                
                // Add branch label for condition nodes
                if (edge.branch) {
                    path.setAttribute("stroke", edge.branch === 'true' ? "#28a745" : "#dc3545");
                }
                
                svg.appendChild(path);
            }
        });
    }
}

// Make it globally available
window.WorkflowRenderer = WorkflowRenderer;