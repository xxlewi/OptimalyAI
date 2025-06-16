/**
 * Auto Layout Module
 * Modul pro automatické rozložení uzlů
 */
export class AutoLayout {
    constructor(workflowManager, canvasRenderer) {
        this.workflowManager = workflowManager;
        this.canvasRenderer = canvasRenderer;
        
        // Layout parameters
        this.verticalSpacing = 150;
        this.horizontalSpacing = 250;
        this.canvasWidth = $('#workflow-canvas').width();
        this.centerX = this.canvasWidth / 2;
    }
    
    /**
     * Perform auto layout
     */
    performLayout() {
        // Check if there are any nodes
        if (Object.keys(this.workflowManager.nodes).length === 0) {
            toastr.warning('Nejsou žádné uzly k rozložení');
            return;
        }
        
        // Find entry point
        const nodesWithIncoming = new Set(this.workflowManager.connections.map(c => c.to));
        let startNodeId = Object.keys(this.workflowManager.nodes).find(id => !nodesWithIncoming.has(id));
        
        // If no clear entry point, use first node
        if (!startNodeId) {
            startNodeId = Object.keys(this.workflowManager.nodes)[0];
        }
        
        // Layout
        const visited = new Set();
        let maxY = 100;
        
        // Layout starting from entry point
        this.layoutNode(startNodeId, this.centerX, 100, visited);
        
        // Layout any unvisited nodes (disconnected components)
        Object.keys(this.workflowManager.nodes).forEach(nodeId => {
            if (!visited.has(nodeId)) {
                maxY = Math.max(...Array.from(visited).map(id => this.workflowManager.nodes[id].y)) + this.verticalSpacing;
                this.layoutNode(nodeId, this.centerX, maxY, visited);
            }
        });
        
        // Re-render
        this.canvasRenderer.render();
        toastr.success('Uzly byly automaticky rozmístěny');
    }
    
    /**
     * Layout node recursively
     */
    layoutNode(nodeId, x, y, visited) {
        if (visited.has(nodeId)) return y;
        visited.add(nodeId);
        
        const node = this.workflowManager.nodes[nodeId];
        if (!node) return y;
        
        // Center node based on type
        let adjustedX = x;
        if (node.type === 'condition' || node.type === 'parallel') {
            adjustedX = x - 70; // Half of diamond width
        } else {
            adjustedX = x - 90; // Half of normal node width
        }
        
        // Position node
        node.x = adjustedX;
        node.y = y;
        
        // Get connections
        const connections = this.getNodeConnections(nodeId);
        let nextY = y + this.verticalSpacing;
        
        if (node.type === 'condition' && Object.keys(connections.branches).length > 0) {
            // Layout branches side by side
            const leftX = x - this.horizontalSpacing / 2;
            const rightX = x + this.horizontalSpacing / 2;
            const branchY = nextY + 50;
            let branchMaxY = branchY;
            
            if (connections.branches.true) {
                branchMaxY = Math.max(branchMaxY, this.layoutNode(connections.branches.true, leftX, branchY, visited));
            }
            if (connections.branches.false) {
                branchMaxY = Math.max(branchMaxY, this.layoutNode(connections.branches.false, rightX, branchY, visited));
            }
            nextY = branchMaxY;
        } else if (node.type === 'parallel' && Object.keys(connections.branches).length > 0) {
            // Layout parallel branches
            const branchCount = Object.keys(connections.branches).length;
            const adjustedSpacing = branchCount > 4 ? this.horizontalSpacing * 0.7 : this.horizontalSpacing;
            const totalWidth = (branchCount - 1) * adjustedSpacing;
            let branchX = x - totalWidth / 2;
            const branchY = nextY + 50;
            let branchMaxY = branchY;
            
            Object.values(connections.branches).forEach(targetId => {
                branchMaxY = Math.max(branchMaxY, this.layoutNode(targetId, branchX, branchY, visited));
                branchX += adjustedSpacing;
            });
            nextY = branchMaxY;
        } else if (connections.next) {
            // Regular next node
            nextY = this.layoutNode(connections.next, x, nextY, visited);
        }
        
        return nextY;
    }
    
    /**
     * Get node connections
     */
    getNodeConnections(nodeId) {
        const node = this.workflowManager.nodes[nodeId];
        const outgoing = this.workflowManager.connections.filter(c => c.from === nodeId);
        const result = {
            next: null,
            branches: {}
        };
        
        if (node.type === 'condition') {
            outgoing.forEach(conn => {
                const branch = conn.branch || 'true';
                result.branches[branch] = conn.to;
            });
        } else if (node.type === 'parallel') {
            result.branches = outgoing.reduce((acc, conn, idx) => {
                acc[`branch${idx}`] = conn.to;
                return acc;
            }, {});
        } else if (outgoing.length > 0) {
            result.next = outgoing[0].to;
        }
        
        return result;
    }
}