/**
 * Canvas Renderer Module
 * Modul pro vykreslování workflow na canvas
 */
export class CanvasRenderer {
    constructor(canvasId, workflowManager) {
        this.canvasId = canvasId;
        this.workflowManager = workflowManager;
        this.$canvas = $(`#${canvasId}`);
        this.$svg = $('#connections-svg');
        this.isDragging = false;
        this.draggedNode = null;
    }
    
    /**
     * Initialize canvas
     */
    initialize() {
        this.setupCanvasEvents();
        this.render();
    }
    
    /**
     * Setup canvas events
     */
    setupCanvasEvents() {
        // Canvas click - deselect
        this.$canvas.on('click', (e) => {
            if (e.target === this.$canvas[0]) {
                this.workflowManager.deselectAll();
                this.render();
            }
        });
    }
    
    /**
     * Render all nodes and connections
     */
    render() {
        this.clearCanvas();
        this.renderNodes();
        this.renderConnections();
    }
    
    /**
     * Clear canvas
     */
    clearCanvas() {
        this.$canvas.find('.workflow-node').remove();
        this.$svg.find('line:not(#temp-connection)').remove();
    }
    
    /**
     * Render all nodes
     */
    renderNodes() {
        Object.values(this.workflowManager.nodes).forEach(node => {
            this.renderNode(node);
        });
    }
    
    /**
     * Render single node
     */
    renderNode(node) {
        const $node = this.createNodeElement(node);
        this.$canvas.append($node);
        this.setupNodeEvents($node, node);
    }
    
    /**
     * Create node DOM element
     */
    createNodeElement(node) {
        const $node = $('<div>')
            .addClass('workflow-node')
            .addClass(node.type)
            .attr('id', node.id)
            .css({
                left: node.x + 'px',
                top: node.y + 'px'
            });
        
        // Add selected class if selected
        if (this.workflowManager.selectedNode === node.id) {
            $node.addClass('selected');
        }
        
        // Create wrapper for diamond-shaped nodes
        let $contentWrapper;
        if (node.type === 'condition' || node.type === 'parallel') {
            $contentWrapper = $('<div>').addClass('node-content-wrapper');
            $node.append($contentWrapper);
        } else {
            $contentWrapper = $node;
        }
        
        // Header
        const $header = $('<div>').addClass('node-header');
        $header.append($('<span>').html(`<i class="${this.getNodeIcon(node.type)}"></i> ${node.name}`));
        
        // Close button
        $header.append($('<span>').addClass('node-close').html('×'));
        
        $contentWrapper.append($header);
        
        // Description
        if (node.description) {
            const $desc = $('<div>')
                .css({
                    fontSize: '12px',
                    color: '#666',
                    marginTop: '5px',
                    fontStyle: 'italic'
                })
                .text(node.description);
            $contentWrapper.append($desc);
        }
        
        // Tool badge
        if (node.tool) {
            const $toolDiv = $('<div>').addClass('node-tools');
            const $toolBadge = $('<span>').addClass('tool-badge').text(node.tool);
            $toolDiv.append($toolBadge);
            $contentWrapper.append($toolDiv);
        }
        
        // ReAct badge
        if (node.useReAct) {
            const $reactBadge = $('<span>')
                .addClass('badge badge-purple')
                .html('<i class="fas fa-brain"></i> ReAct')
                .css({
                    position: 'absolute',
                    top: '5px',
                    right: '25px',
                    fontSize: '11px'
                });
            $node.append($reactBadge);
        }
        
        // Connection dots
        $node.append($('<div>').addClass('connection-dot input'));
        
        if (node.type === 'condition') {
            $node.append($('<div>')
                .addClass('connection-dot output true')
                .attr('title', 'TRUE')
                .attr('data-branch', 'true'));
            $node.append($('<div>')
                .addClass('connection-dot output false')
                .attr('title', 'FALSE')
                .attr('data-branch', 'false'));
        } else {
            $node.append($('<div>').addClass('connection-dot output'));
        }
        
        return $node;
    }
    
    /**
     * Setup node events
     */
    setupNodeEvents($node, node) {
        let clickTimer = null;
        let clickCount = 0;
        
        // Combined click/double-click handler
        $node.on('click', (e) => {
            e.stopPropagation();
            clickCount++;
            
            if (clickCount === 1) {
                clickTimer = setTimeout(() => {
                    // Single click
                    this.workflowManager.selectNode(node.id);
                    this.render();
                    this.onNodeSelect?.(node);
                    clickCount = 0;
                }, 300);
            } else if (clickCount === 2) {
                // Double click
                clearTimeout(clickTimer);
                clickCount = 0;
                e.preventDefault(); // Prevent text selection
                console.log('=== DOUBLE CLICK DETECTED ===');
                console.log('Node double-clicked:', node);
                console.log('onNodeEdit handler available:', !!this.onNodeEdit);
                if (this.onNodeEdit) {
                    console.log('Calling onNodeEdit...');
                    this.onNodeEdit(node);
                } else {
                    console.error('onNodeEdit handler not set!');
                }
            }
        });
        
        // Close button
        $node.find('.node-close').on('click', (e) => {
            e.stopPropagation();
            this.workflowManager.removeNode(node.id);
            this.render();
        });
        
        // Make draggable
        this.makeDraggable($node, node);
    }
    
    /**
     * Make node draggable
     */
    makeDraggable($element, node) {
        let startX, startY, initialX, initialY;
        
        $element.on('mousedown', (e) => {
            if ($(e.target).hasClass('node-close') || $(e.target).hasClass('connection-dot')) {
                return;
            }
            
            this.isDragging = true;
            this.draggedNode = node;
            startX = e.clientX;
            startY = e.clientY;
            initialX = node.x;
            initialY = node.y;
            
            $(document).on('mousemove.drag', (e) => {
                if (!this.isDragging) return;
                
                const dx = e.clientX - startX;
                const dy = e.clientY - startY;
                
                node.x = initialX + dx;
                node.y = initialY + dy;
                
                $element.css({
                    left: node.x + 'px',
                    top: node.y + 'px'
                });
                
                this.renderConnections();
            });
            
            $(document).on('mouseup.drag', () => {
                this.isDragging = false;
                this.draggedNode = null;
                $(document).off('.drag');
            });
        });
    }
    
    /**
     * Render connections
     */
    renderConnections() {
        this.$svg.find('line:not(#temp-connection)').remove();
        
        this.workflowManager.connections.forEach(conn => {
            const fromNode = this.workflowManager.nodes[conn.from];
            const toNode = this.workflowManager.nodes[conn.to];
            
            if (fromNode && toNode) {
                this.drawConnection(conn, fromNode, toNode);
            }
        });
    }
    
    /**
     * Draw single connection
     */
    drawConnection(conn, fromNode, toNode) {
        const $fromEl = $('#' + conn.from);
        const $toEl = $('#' + conn.to);
        
        // Get connection dots
        let $outputDot;
        if (conn.branch && fromNode.type === 'condition') {
            $outputDot = $fromEl.find(`.connection-dot.output.${conn.branch}`);
        } else {
            $outputDot = $fromEl.find('.connection-dot.output');
        }
        
        const $inputDot = $toEl.find('.connection-dot.input');
        
        // Get positions
        const canvasRect = this.$canvas[0].getBoundingClientRect();
        const outputDotRect = $outputDot[0].getBoundingClientRect();
        const inputDotRect = $inputDot[0].getBoundingClientRect();
        
        const x1 = outputDotRect.left - canvasRect.left + (outputDotRect.width / 2);
        const y1 = outputDotRect.top - canvasRect.top + (outputDotRect.height / 2);
        const x2 = inputDotRect.left - canvasRect.left + (inputDotRect.width / 2);
        const y2 = inputDotRect.top - canvasRect.top + (inputDotRect.height / 2);
        
        // Create line
        const line = document.createElementNS('http://www.w3.org/2000/svg', 'line');
        line.setAttribute('x1', x1);
        line.setAttribute('y1', y1);
        line.setAttribute('x2', x2);
        line.setAttribute('y2', y2);
        line.setAttribute('class', 'connection-line');
        line.setAttribute('marker-end', 'url(#arrowhead)');
        
        // Color code branches
        if (conn.branch === 'true') {
            line.setAttribute('stroke', '#28a745');
            line.setAttribute('marker-end', 'url(#arrowhead-green)');
        } else if (conn.branch === 'false') {
            line.setAttribute('stroke', '#dc3545');
            line.setAttribute('marker-end', 'url(#arrowhead-red)');
        }
        
        this.$svg[0].appendChild(line);
    }
    
    /**
     * Get node icon
     */
    getNodeIcon(type) {
        const icons = {
            'task': 'fas fa-tasks',
            'condition': 'fas fa-question-circle',
            'parallel': 'fas fa-code-branch',
            'tool': 'fas fa-robot',
            'InputAdapter': 'fas fa-download',
            'OutputAdapter': 'fas fa-upload'
        };
        return icons[type] || 'fas fa-circle';
    }
    
    /**
     * Apply zoom
     */
    applyZoom(zoom) {
        this.$canvas.css('transform', `scale(${zoom})`);
        this.$canvas.css('transform-origin', '0 0');
        this.$svg.css('transform', `scale(${zoom})`);
        this.$svg.css('transform-origin', '0 0');
    }
    
    /**
     * Event handlers (to be set from outside)
     */
    onNodeSelect = null;
    onNodeEdit = null;
}