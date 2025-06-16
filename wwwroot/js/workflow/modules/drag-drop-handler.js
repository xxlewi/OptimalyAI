/**
 * Drag & Drop Handler Module
 * Modul pro správu drag & drop operací
 */
export class DragDropHandler {
    constructor(canvasId, workflowManager, canvasRenderer) {
        this.canvasId = canvasId;
        this.workflowManager = workflowManager;
        this.canvasRenderer = canvasRenderer;
        this.$canvas = $(`#${canvasId}`);
        this.$dropIndicator = $('#dropZoneIndicator');
    }
    
    /**
     * Initialize drag & drop
     */
    initialize() {
        this.setupToolboxDragHandlers();
        this.setupCanvasDropHandlers();
    }
    
    /**
     * Setup toolbox drag handlers
     */
    setupToolboxDragHandlers() {
        $('.tool-item').on('dragstart', (e) => {
            e.originalEvent.dataTransfer.setData('type', $(e.target).data('type'));
            e.originalEvent.dataTransfer.setData('tool', $(e.target).data('tool') || '');
            
            $(e.target).addClass('dragging');
            e.originalEvent.dataTransfer.effectAllowed = 'copy';
        }).on('dragend', (e) => {
            $(e.target).removeClass('dragging');
        });
    }
    
    /**
     * Setup canvas drop handlers
     */
    setupCanvasDropHandlers() {
        this.$canvas
            .on('dragenter', (e) => {
                e.preventDefault();
                $(e.target).addClass('drag-over');
            })
            .on('dragover', (e) => {
                e.preventDefault();
                e.originalEvent.dataTransfer.dropEffect = 'copy';
                
                // Show drop zone indicator
                const rect = this.$canvas[0].getBoundingClientRect();
                const x = e.clientX - rect.left - 75;
                const y = e.clientY - rect.top - 40;
                
                this.$dropIndicator.css({
                    left: x + 'px',
                    top: y + 'px',
                    display: 'block'
                });
            })
            .on('dragleave', (e) => {
                if (e.target === this.$canvas[0]) {
                    $(e.target).removeClass('drag-over');
                    this.$dropIndicator.hide();
                }
            })
            .on('drop', (e) => {
                e.preventDefault();
                
                // Remove visual feedback
                $(e.target).removeClass('drag-over');
                this.$dropIndicator.hide();
                
                const type = e.originalEvent.dataTransfer.getData('type');
                const tool = e.originalEvent.dataTransfer.getData('tool');
                const rect = this.$canvas[0].getBoundingClientRect();
                let x = e.clientX - rect.left;
                let y = e.clientY - rect.top;
                
                // Adjust position to center the node at cursor
                if (type === 'condition' || type === 'parallel') {
                    x -= 70; // Half of diamond width
                    y -= 70; // Half of diamond height
                } else {
                    x -= 90; // Half of normal node width
                    y -= 30; // Approximate half height
                }
                
                // Add node
                const node = this.workflowManager.addNode(type, x, y, tool);
                this.canvasRenderer.render();
                
                // Show success notification
                toastr.success('Nástroj byl přidán do workflow', '', {
                    timeOut: 1000,
                    positionClass: 'toast-bottom-right'
                });
                
                // Trigger callback if set
                this.onNodeAdded?.(node);
            });
    }
    
    /**
     * Event handler for node added
     */
    onNodeAdded = null;
}