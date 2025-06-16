/**
 * Connection Manager Module
 * Modul pro správu připojení mezi uzly
 */
export class ConnectionManager {
    constructor(canvasId, workflowManager, canvasRenderer) {
        this.canvasId = canvasId;
        this.workflowManager = workflowManager;
        this.canvasRenderer = canvasRenderer;
        this.$canvas = $(`#${canvasId}`);
        this.$svg = $('#connections-svg');
        
        this.isConnecting = false;
        this.connectionStart = null;
        this.magnetRadius = 30;
    }
    
    /**
     * Initialize connection management
     */
    initialize() {
        this.setupConnectionHandlers();
    }
    
    /**
     * Setup connection event handlers
     */
    setupConnectionHandlers() {
        // Start connection from output dot
        this.$canvas.on('mousedown', '.connection-dot.output', (e) => {
            e.stopPropagation();
            e.preventDefault();
            
            const $nodeEl = $(e.target).closest('.workflow-node');
            const nodeId = $nodeEl.attr('id');
            const branch = $(e.target).attr('data-branch');
            
            this.startConnection(nodeId, branch);
            return false;
        });
        
        // End connection on input dot
        this.$canvas.on('mouseup', '.connection-dot.input', (e) => {
            e.stopPropagation();
            e.preventDefault();
            
            const $nodeEl = $(e.target).closest('.workflow-node');
            const nodeId = $nodeEl.attr('id');
            
            if (this.isConnecting && this.connectionStart && this.connectionStart.nodeId !== nodeId) {
                this.endConnection(nodeId);
            }
            return false;
        });
    }
    
    /**
     * Start creating a connection
     */
    startConnection(nodeId, branch) {
        console.log('Starting connection from:', nodeId, 'branch:', branch);
        
        this.isConnecting = true;
        this.connectionStart = {
            nodeId: nodeId,
            branch: branch
        };
        
        // Add visual feedback
        $('#' + nodeId).addClass('connection-source');
        this.$canvas.css('cursor', 'crosshair');
        
        // Create temporary line
        const tempLine = document.createElementNS('http://www.w3.org/2000/svg', 'line');
        tempLine.setAttribute('id', 'temp-connection');
        tempLine.setAttribute('class', 'connection-line-preview');
        this.$svg[0].appendChild(tempLine);
        
        // Get start position
        const startNode = this.workflowManager.nodes[nodeId];
        const $startEl = $('#' + nodeId);
        
        let $outputDot;
        if (branch) {
            $outputDot = $startEl.find(`.connection-dot.output.${branch}`);
        } else {
            $outputDot = $startEl.find('.connection-dot.output');
        }
        
        const canvasRect = this.$canvas[0].getBoundingClientRect();
        const outputDotRect = $outputDot[0].getBoundingClientRect();
        const startX = outputDotRect.left - canvasRect.left + (outputDotRect.width / 2);
        const startY = outputDotRect.top - canvasRect.top + (outputDotRect.height / 2);
        
        // Mouse move handler
        $(document).on('mousemove.connection', (e) => {
            const rect = this.$canvas[0].getBoundingClientRect();
            let x = e.clientX - rect.left + this.$canvas.scrollLeft();
            let y = e.clientY - rect.top + this.$canvas.scrollTop();
            
            // Magnetic effect
            let magnetized = false;
            
            $('.connection-dot.input').each((i, el) => {
                const $input = $(el);
                const inputNodeId = $input.closest('.workflow-node').attr('id');
                
                // Skip same node
                if (inputNodeId === this.connectionStart.nodeId) return;
                
                const inputRect = el.getBoundingClientRect();
                const inputX = inputRect.left - rect.left + this.$canvas.scrollLeft() + (inputRect.width / 2);
                const inputY = inputRect.top - rect.top + this.$canvas.scrollTop() + (inputRect.height / 2);
                
                // Calculate distance
                const distance = Math.sqrt(Math.pow(x - inputX, 2) + Math.pow(y - inputY, 2));
                
                if (distance < this.magnetRadius) {
                    // Snap to input
                    x = inputX;
                    y = inputY;
                    magnetized = true;
                    
                    // Add visual feedback
                    $input.addClass('magnetic-hover');
                    $('#' + inputNodeId).addClass('connection-target-valid');
                    
                    tempLine.setAttribute('data-magnetic-target', inputNodeId);
                    return false; // Break loop
                } else {
                    $input.removeClass('magnetic-hover');
                    $('#' + inputNodeId).removeClass('connection-target-valid');
                }
            });
            
            if (!magnetized) {
                tempLine.removeAttribute('data-magnetic-target');
                $('.connection-dot.input').removeClass('magnetic-hover');
                $('.workflow-node').removeClass('connection-target-valid');
            }
            
            // Update temp line
            tempLine.setAttribute('x1', startX);
            tempLine.setAttribute('y1', startY);
            tempLine.setAttribute('x2', x);
            tempLine.setAttribute('y2', y);
        });
        
        // Global mouseup handler
        $(document).on('mouseup.connection', (e) => {
            // Check if magnetized
            const magneticTarget = tempLine.getAttribute('data-magnetic-target');
            
            if (magneticTarget) {
                this.endConnection(magneticTarget);
            } else if (!($(e.target).hasClass('connection-dot') && $(e.target).hasClass('input'))) {
                // Cancel connection
                this.cancelConnection();
            }
        });
    }
    
    /**
     * End connection
     */
    endConnection(nodeId) {
        console.log('Ending connection at:', nodeId);
        
        if (this.connectionStart && this.connectionStart.nodeId !== nodeId) {
            // Check if connection already exists
            const exists = this.workflowManager.connections.some(c => 
                c.from === this.connectionStart.nodeId && 
                c.to === nodeId &&
                c.branch === this.connectionStart.branch
            );
            
            if (!exists) {
                // Check constraints
                const fromNode = this.workflowManager.nodes[this.connectionStart.nodeId];
                
                // Remove existing input connection to target
                this.workflowManager.connections = this.workflowManager.connections.filter(c => c.to !== nodeId);
                
                // Check output constraints
                const existingFromOutput = this.workflowManager.connections.filter(c => 
                    c.from === this.connectionStart.nodeId && 
                    (!this.connectionStart.branch || c.branch === this.connectionStart.branch)
                );
                
                if (existingFromOutput.length > 0 && fromNode.type !== 'parallel') {
                    toastr.warning('Tento výstup už má spojení. Nejdřív ho odstraňte.');
                    this.cancelConnection();
                    return;
                }
                
                // Add connection
                const added = this.workflowManager.addConnection(
                    this.connectionStart.nodeId,
                    nodeId,
                    this.connectionStart.branch
                );
                
                if (added) {
                    this.canvasRenderer.renderConnections();
                    const branchInfo = this.connectionStart.branch ? ` (${this.connectionStart.branch.toUpperCase()})` : '';
                    toastr.success('Propojeno' + branchInfo + '!');
                    
                    // Trigger callback
                    this.onConnectionAdded?.(this.connectionStart.nodeId, nodeId, this.connectionStart.branch);
                }
            } else {
                toastr.warning('Toto propojení již existuje');
            }
        }
        
        this.cleanup();
    }
    
    /**
     * Cancel connection
     */
    cancelConnection() {
        console.log('Connection cancelled');
        this.cleanup();
    }
    
    /**
     * Cleanup connection state
     */
    cleanup() {
        $('#temp-connection').remove();
        this.isConnecting = false;
        this.connectionStart = null;
        this.$canvas.css('cursor', 'default');
        $(document).off('.connection');
        $('.connection-dot.input').removeClass('magnetic-hover');
        $('.workflow-node').removeClass('connection-source connection-target-valid connection-target-invalid');
    }
    
    /**
     * Event handler for connection added
     */
    onConnectionAdded = null;
}