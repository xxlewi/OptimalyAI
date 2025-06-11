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
            
            if (result.success && result.data) {
                this.renderWorkflow(result.data);
                // Hide templates section if workflow has stages
                if (result.data.stages && result.data.stages.length > 0) {
                    $('#templatesSection').slideUp();
                }
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
        
        // Handle tools that might be strings or objects
        const toolsList = stage.tools || [];
        const toolsHtml = toolsList.map(tool => {
            const toolName = typeof tool === 'string' ? tool : tool.toolName;
            return `
                <span class="tool-badge" title="${toolName}">
                    <i class="fas fa-wrench"></i> ${toolName}
                </span>
            `;
        }).join('');
        
        div.innerHTML = `
            <div class="stage-header">
                <div class="stage-content">
                    <span class="stage-number">${stage.order}</span>
                    <div class="stage-info">
                        <h5 class="mb-1">${stage.name}</h5>
                        ${stage.description ? `<p class="text-muted mb-2" style="font-size: 0.9rem;">${stage.description}</p>` : ''}
                        <div class="stage-components">
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
                            <span class="badge badge-secondary">${stage.executionStrategy || 'Sequential'}</span>
                        </div>
                        ${toolsList.length > 0 ? `
                            <div class="stage-tools mt-2">
                                ${toolsHtml}
                            </div>
                        ` : ''}
                    </div>
                </div>
                <div class="stage-actions">
                    <button class="btn btn-sm btn-info" onclick="editStage('${stage.id}')" title="Upravit">
                        <i class="fas fa-edit"></i>
                    </button>
                    <button class="btn btn-sm btn-warning" onclick="workflowDesigner.duplicateStage('${stage.id}')" title="Duplikovat">
                        <i class="fas fa-copy"></i>
                    </button>
                    <button class="btn btn-sm btn-danger" onclick="workflowDesigner.deleteStage('${stage.id}')" title="Smazat">
                        <i class="fas fa-trash"></i>
                    </button>
                    <button class="btn btn-sm btn-secondary drag-handle" title="Přesunout">
                        <i class="fas fa-grip-vertical"></i>
                    </button>
                </div>
            </div>
        `;
        
        // Přidání event listenerů
        div.addEventListener('click', (e) => {
            // Zabránit kliknutí na tlačítka
            if (!e.target.closest('.stage-actions')) {
                this.selectStage(stage);
            }
        });
        div.addEventListener('dragstart', (e) => this.handleDragStart(e, stage));
        div.addEventListener('dragend', (e) => this.handleDragEnd(e));
        
        return div;
    }

    createConnectorElement() {
        const div = document.createElement('div');
        div.className = 'stage-connector';
        div.innerHTML = '<i class="fas fa-chevron-down fa-2x"></i>';
        return div;
    }

    getEmptyStateHtml() {
        return `
            <div class="empty-state">
                <i class="fas fa-layer-group fa-5x mb-4"></i>
                <h3>Začněte vytvářet workflow</h3>
                <p class="lead mb-4">Vyberte šablonu výše nebo přidejte první krok</p>
                <button class="btn btn-success btn-lg" onclick="createStage()">
                    <i class="fas fa-plus-circle"></i> Přidat první krok
                </button>
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
        // This is now handled by global editStage function
        window.editStage(stageId);
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
        
        // Get current workflow data
        const stages = await this.getWorkflowStages();
        
        const workflowDto = {
            projectId: this.projectId,
            triggerType: triggerType,
            schedule: schedule || null,
            stages: stages
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
                if (result.data && result.data.stages) {
                    this.stages = result.data.stages;
                }
            } else {
                toastr.error(result.message || 'Chyba při ukládání workflow');
            }
        } catch (error) {
            console.error('Error saving workflow:', error);
            toastr.error('Chyba při ukládání workflow');
        }
    }
    
    async getWorkflowStages() {
        // Get stages from current UI state
        const stageElements = document.querySelectorAll('.stage-card');
        const stages = [];
        
        for (let i = 0; i < stageElements.length; i++) {
            const stageId = stageElements[i].dataset.stageId;
            const stage = this.stages.find(s => s.id === stageId);
            if (stage) {
                stage.order = i + 1;
                stages.push(stage);
            }
        }
        
        return stages;
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
        
        // Initialize Sortable for drag & drop reordering
        new Sortable(stagesList, {
            handle: '.drag-handle',
            animation: 150,
            ghostClass: 'dragging',
            filter: '.stage-connector',
            onEnd: (evt) => {
                this.reorderStages();
            }
        });
        
        // Initialize drag & drop for components from sidebar
        this.initComponentDragDrop();
    }
    
    initComponentDragDrop() {
        // Make component items draggable
        document.querySelectorAll('.component-item[draggable="true"]').forEach(item => {
            item.addEventListener('dragstart', (e) => {
                e.dataTransfer.effectAllowed = 'copy';
                e.dataTransfer.setData('component-type', item.dataset.type);
                e.dataTransfer.setData('component-value', item.dataset.value);
                item.style.opacity = '0.5';
            });
            
            item.addEventListener('dragend', (e) => {
                item.style.opacity = '';
            });
        });
        
        // Make stages droppable for components
        const stagesList = document.getElementById('stagesList');
        if (stagesList) {
            stagesList.addEventListener('dragover', (e) => {
                if (e.dataTransfer.types.includes('component-type')) {
                    e.preventDefault();
                    e.dataTransfer.dropEffect = 'copy';
                }
            });
            
            stagesList.addEventListener('drop', (e) => {
                if (e.dataTransfer.types.includes('component-type')) {
                    e.preventDefault();
                    const type = e.dataTransfer.getData('component-type');
                    const value = e.dataTransfer.getData('component-value');
                    
                    // Find which stage was dropped on
                    const stageCard = e.target.closest('.stage-card');
                    if (stageCard) {
                        const stageId = stageCard.dataset.stageId;
                        this.addComponentToStage(stageId, type, value);
                    }
                }
            });
        }
    }
    
    async addComponentToStage(stageId, componentType, componentValue) {
        // Handle adding component to stage
        if (componentType === 'tool') {
            // Add tool to stage
            try {
                const dto = {
                    toolName: componentValue,
                    configuration: {},
                    order: 0
                };
                
                const response = await fetch(`/api/workflow/stages/${stageId}/tools`, {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json'
                    },
                    body: JSON.stringify(dto)
                });
                
                const result = await response.json();
                
                if (result.success) {
                    toastr.success(`Nástroj ${componentValue} byl přidán`);
                    await this.loadWorkflow();
                } else {
                    toastr.error(result.message || 'Chyba při přidávání nástroje');
                }
            } catch (error) {
                console.error('Error adding tool:', error);
                toastr.error('Chyba při přidávání nástroje');
            }
        } else {
            toastr.info(`Pro změnu ${componentType} upravte krok`);
            this.editStage(stageId);
        }
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