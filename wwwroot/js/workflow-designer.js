/**
 * Workflow Designer Component
 * Pokročilý vizuální editor pro návrh workflow
 */

class WorkflowDesigner {
    constructor(containerId, projectId) {
        this.container = document.getElementById(containerId);
        this.projectId = projectId;
        this.stages = [];
        this.connections = [];
        this.selectedStage = null;
        this.isDragging = false;
        this.draggedStage = null;
        
        this.init();
    }

    init() {
        this.setupCanvas();
        this.loadWorkflow();
        this.setupEventListeners();
        this.initDragDrop();
    }

    setupCanvas() {
        // Vytvoření canvas pro kreslení spojení
        this.canvas = document.createElement('canvas');
        this.canvas.id = 'workflow-canvas';
        this.canvas.style.position = 'absolute';
        this.canvas.style.top = '0';
        this.canvas.style.left = '0';
        this.canvas.style.pointerEvents = 'none';
        this.container.style.position = 'relative';
        this.container.insertBefore(this.canvas, this.container.firstChild);
        
        this.ctx = this.canvas.getContext('2d');
        this.resizeCanvas();
    }

    resizeCanvas() {
        this.canvas.width = this.container.offsetWidth;
        this.canvas.height = this.container.offsetHeight;
    }

    async loadWorkflow() {
        try {
            const response = await fetch(`/api/workflow/${this.projectId}/design`);
            const result = await response.json();
            
            if (result.success) {
                this.renderWorkflow(result.data);
            }
        } catch (error) {
            console.error('Error loading workflow:', error);
            toastr.error('Chyba při načítání workflow');
        }
    }

    renderWorkflow(workflowData) {
        this.stages = workflowData.stages || [];
        const stagesList = document.getElementById('stagesList');
        
        if (this.stages.length === 0) {
            stagesList.innerHTML = this.getEmptyStateHtml();
            return;
        }

        stagesList.innerHTML = '';
        this.stages.forEach((stage, index) => {
            const stageElement = this.createStageElement(stage, index);
            stagesList.appendChild(stageElement);
            
            if (index < this.stages.length - 1) {
                const connector = this.createConnectorElement();
                stagesList.appendChild(connector);
            }
        });

        this.updateConnections();
    }

    createStageElement(stage, index) {
        const div = document.createElement('div');
        div.className = 'stage-card';
        div.dataset.stageId = stage.id;
        div.dataset.order = stage.order;
        div.dataset.index = index;
        
        div.innerHTML = `
            <div class="stage-header">
                <div>
                    <h5 class="mb-0">
                        <span class="stage-order">${stage.order}.</span>
                        <span class="stage-name">${stage.name}</span>
                    </h5>
                    ${stage.description ? `<small class="text-muted">${stage.description}</small>` : ''}
                </div>
                <div>
                    <button class="btn btn-sm btn-info" onclick="workflowDesigner.editStage('${stage.id}')">
                        <i class="fas fa-edit"></i>
                    </button>
                    <button class="btn btn-sm btn-warning" onclick="workflowDesigner.duplicateStage('${stage.id}')">
                        <i class="fas fa-copy"></i>
                    </button>
                    <button class="btn btn-sm btn-danger" onclick="workflowDesigner.deleteStage('${stage.id}')">
                        <i class="fas fa-trash"></i>
                    </button>
                    <button class="btn btn-sm btn-secondary drag-handle">
                        <i class="fas fa-grip-vertical"></i>
                    </button>
                </div>
            </div>
            
            <div class="stage-details">
                ${stage.orchestratorType ? `
                    <span class="orchestrator-badge">
                        <i class="fas fa-robot"></i> ${stage.orchestratorType}
                    </span>
                ` : ''}
                ${stage.reactAgentType ? `
                    <span class="react-agent-badge">
                        <i class="fas fa-brain"></i> ${stage.reactAgentType}
                    </span>
                ` : ''}
                <span class="badge badge-secondary">${stage.executionStrategy}</span>
            </div>
            
            ${stage.tools && stage.tools.length > 0 ? `
                <div class="stage-tools">
                    ${stage.tools.map(tool => `
                        <span class="tool-badge" title="${tool.toolName}">
                            <i class="fas fa-wrench"></i> ${tool.toolName}
                        </span>
                    `).join('')}
                </div>
            ` : ''}
        `;
        
        // Přidání event listenerů
        div.addEventListener('click', () => this.selectStage(stage));
        div.addEventListener('dragstart', (e) => this.handleDragStart(e, stage));
        div.addEventListener('dragend', (e) => this.handleDragEnd(e));
        
        return div;
    }

    createConnectorElement() {
        const div = document.createElement('div');
        div.className = 'stage-connector';
        div.innerHTML = '<i class="fas fa-arrow-down fa-2x"></i>';
        return div;
    }

    getEmptyStateHtml() {
        return `
            <div class="empty-state">
                <i class="fas fa-layer-group fa-4x mb-3"></i>
                <h4>Zatím žádné kroky workflow</h4>
                <p>Klikněte na tlačítko "Přidat krok" pro vytvoření prvního kroku workflow</p>
            </div>
        `;
    }

    selectStage(stage) {
        // Odstranění předchozího výběru
        document.querySelectorAll('.stage-card').forEach(card => {
            card.classList.remove('selected');
        });
        
        // Označení vybraného stage
        const stageElement = document.querySelector(`[data-stage-id="${stage.id}"]`);
        if (stageElement) {
            stageElement.classList.add('selected');
        }
        
        this.selectedStage = stage;
        this.showStageDetails(stage);
    }

    showStageDetails(stage) {
        // Zobrazení detailů stage v postranním panelu
        const detailsPanel = document.getElementById('stageDetailsPanel');
        if (!detailsPanel) return;
        
        detailsPanel.innerHTML = `
            <h5>Detail kroku</h5>
            <dl>
                <dt>Název:</dt>
                <dd>${stage.name}</dd>
                
                <dt>Typ:</dt>
                <dd>${stage.type}</dd>
                
                <dt>Orchestrátor:</dt>
                <dd>${stage.orchestratorType || 'Není nastaven'}</dd>
                
                <dt>ReAct Agent:</dt>
                <dd>${stage.reactAgentType || 'Není nastaven'}</dd>
                
                <dt>Strategie:</dt>
                <dd>${stage.executionStrategy}</dd>
                
                <dt>Nástroje:</dt>
                <dd>${stage.tools.length} nástrojů</dd>
            </dl>
            
            <button class="btn btn-primary btn-sm" onclick="workflowDesigner.editStage('${stage.id}')">
                <i class="fas fa-edit"></i> Upravit
            </button>
        `;
    }

    async editStage(stageId) {
        try {
            const response = await fetch(`/WorkflowDesigner/EditStage?stageId=${stageId}`);
            const html = await response.text();
            
            document.getElementById('modalContainer').innerHTML = html;
            $('#editStageModal').modal('show');
        } catch (error) {
            console.error('Error loading edit stage modal:', error);
            toastr.error('Chyba při načítání editoru');
        }
    }

    async duplicateStage(stageId) {
        if (!confirm('Opravdu chcete duplikovat tento krok?')) return;
        
        try {
            const response = await fetch(`/api/workflow/stages/${stageId}/duplicate`, {
                method: 'POST'
            });
            
            if (response.ok) {
                toastr.success('Krok byl zduplikován');
                await this.loadWorkflow();
            } else {
                toastr.error('Chyba při duplikování kroku');
            }
        } catch (error) {
            console.error('Error duplicating stage:', error);
            toastr.error('Chyba při duplikování kroku');
        }
    }

    async deleteStage(stageId) {
        if (!confirm('Opravdu chcete smazat tento krok workflow?')) return;
        
        try {
            const response = await fetch(`/api/workflow/stages/${stageId}`, {
                method: 'DELETE'
            });
            
            if (response.ok) {
                toastr.success('Krok byl smazán');
                await this.loadWorkflow();
            } else {
                toastr.error('Chyba při mazání kroku');
            }
        } catch (error) {
            console.error('Error deleting stage:', error);
            toastr.error('Chyba při mazání kroku');
        }
    }

    async saveWorkflow() {
        const triggerType = document.getElementById('triggerType').value;
        const schedule = document.getElementById('schedule').value;
        
        const workflowDto = {
            projectId: this.projectId,
            triggerType: triggerType,
            schedule: schedule || null,
            stages: this.stages
        };
        
        try {
            const response = await fetch('/api/workflow/design', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify(workflowDto)
            });
            
            const result = await response.json();
            
            if (result.success) {
                toastr.success('Workflow bylo uloženo');
                this.stages = result.data.stages;
            } else {
                toastr.error('Chyba při ukládání workflow');
            }
        } catch (error) {
            console.error('Error saving workflow:', error);
            toastr.error('Chyba při ukládání workflow');
        }
    }

    async validateWorkflow() {
        try {
            const response = await fetch(`/api/workflow/${this.projectId}/validate`);
            const result = await response.json();
            
            if (result.data) {
                toastr.success('Workflow je validní a připravené ke spuštění');
            } else {
                toastr.warning('Workflow obsahuje chyby. Zkontrolujte konfiguraci jednotlivých kroků.');
            }
        } catch (error) {
            console.error('Error validating workflow:', error);
            toastr.error('Chyba při validaci workflow');
        }
    }

    // Drag & Drop functionality
    initDragDrop() {
        const stagesList = document.getElementById('stagesList');
        if (!stagesList) return;
        
        new Sortable(stagesList, {
            handle: '.drag-handle',
            animation: 150,
            ghostClass: 'dragging',
            onEnd: (evt) => {
                this.reorderStages();
            }
        });
    }

    handleDragStart(e, stage) {
        this.isDragging = true;
        this.draggedStage = stage;
        e.target.classList.add('dragging');
    }

    handleDragEnd(e) {
        this.isDragging = false;
        this.draggedStage = null;
        e.target.classList.remove('dragging');
    }

    async reorderStages() {
        const stageElements = document.querySelectorAll('.stage-card');
        const stageIds = Array.from(stageElements).map(el => el.dataset.stageId);
        
        try {
            const response = await fetch(`/api/workflow/${this.projectId}/stages/reorder`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify(stageIds)
            });
            
            if (response.ok) {
                toastr.success('Pořadí kroků bylo aktualizováno');
                this.updateStageNumbers();
            } else {
                toastr.error('Chyba při změně pořadí');
            }
        } catch (error) {
            console.error('Error reordering stages:', error);
            toastr.error('Chyba při změně pořadí');
        }
    }

    updateStageNumbers() {
        document.querySelectorAll('.stage-card').forEach((card, index) => {
            card.querySelector('.stage-order').textContent = (index + 1) + '.';
        });
    }

    // Connection drawing
    updateConnections() {
        // Vyčištění canvas
        this.ctx.clearRect(0, 0, this.canvas.width, this.canvas.height);
        
        // Nastavení stylu čar
        this.ctx.strokeStyle = '#007bff';
        this.ctx.lineWidth = 2;
        this.ctx.setLineDash([5, 5]);
        
        // Kreslení spojení mezi stages
        const stageElements = document.querySelectorAll('.stage-card');
        for (let i = 0; i < stageElements.length - 1; i++) {
            const start = stageElements[i];
            const end = stageElements[i + 1];
            
            const startRect = start.getBoundingClientRect();
            const endRect = end.getBoundingClientRect();
            const containerRect = this.container.getBoundingClientRect();
            
            const startX = startRect.left + startRect.width / 2 - containerRect.left;
            const startY = startRect.bottom - containerRect.top;
            const endX = endRect.left + endRect.width / 2 - containerRect.left;
            const endY = endRect.top - containerRect.top;
            
            this.drawConnection(startX, startY, endX, endY);
        }
    }

    drawConnection(x1, y1, x2, y2) {
        this.ctx.beginPath();
        this.ctx.moveTo(x1, y1);
        
        // Bezierova křivka pro hladké spojení
        const controlY = y1 + (y2 - y1) / 2;
        this.ctx.bezierCurveTo(x1, controlY, x2, controlY, x2, y2);
        
        this.ctx.stroke();
        
        // Šipka na konci
        this.drawArrow(x2, y2, Math.PI / 2);
    }

    drawArrow(x, y, angle) {
        const arrowLength = 10;
        const arrowAngle = Math.PI / 6;
        
        this.ctx.save();
        this.ctx.translate(x, y);
        this.ctx.rotate(angle);
        
        this.ctx.beginPath();
        this.ctx.moveTo(0, 0);
        this.ctx.lineTo(-arrowLength * Math.cos(arrowAngle), -arrowLength * Math.sin(arrowAngle));
        this.ctx.moveTo(0, 0);
        this.ctx.lineTo(arrowLength * Math.cos(arrowAngle), -arrowLength * Math.sin(arrowAngle));
        this.ctx.stroke();
        
        this.ctx.restore();
    }

    setupEventListeners() {
        // Window resize
        window.addEventListener('resize', () => {
            this.resizeCanvas();
            this.updateConnections();
        });
        
        // Trigger type change
        const triggerTypeSelect = document.getElementById('triggerType');
        if (triggerTypeSelect) {
            triggerTypeSelect.addEventListener('change', (e) => {
                const scheduleContainer = document.getElementById('scheduleContainer');
                if (scheduleContainer) {
                    scheduleContainer.style.display = e.target.value === 'Schedule' ? 'block' : 'none';
                }
            });
        }
    }
}

// CSS pro workflow designer
const style = document.createElement('style');
style.textContent = `
    .stage-card.selected {
        border-color: #007bff;
        box-shadow: 0 0 0 3px rgba(0, 123, 255, 0.25);
    }
    
    .stage-card.dragging {
        opacity: 0.5;
        cursor: move;
    }
    
    #workflow-canvas {
        z-index: 0;
    }
    
    .stage-card {
        z-index: 1;
        cursor: pointer;
    }
    
    .stage-connector {
        z-index: 1;
    }
`;
document.head.appendChild(style);

// Globální instance
let workflowDesigner = null;

// Inicializace při načtení stránky
document.addEventListener('DOMContentLoaded', () => {
    const designerContainer = document.getElementById('workflowDesigner');
    if (designerContainer && typeof projectId !== 'undefined') {
        workflowDesigner = new WorkflowDesigner('workflowDesigner', projectId);
    }
});