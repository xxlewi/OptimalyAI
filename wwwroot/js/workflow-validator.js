/**
 * Workflow Validator Component
 * Real-time validace workflow konfigurace
 */

class WorkflowValidator {
    constructor() {
        this.validationRules = {
            stage: {
                minCount: 1,
                requiredFields: ['name', 'orchestratorType'],
                maxNameLength: 200,
                validOrchestrators: ['ConversationOrchestrator', 'ToolChainOrchestrator', 'CustomOrchestrator'],
                validReActAgents: ['ConversationReActAgent', 'AnalysisReActAgent', 'ProcessingReActAgent'],
                validExecutionStrategies: ['Sequential', 'Parallel', 'Conditional']
            },
            workflow: {
                validTriggerTypes: ['Manual', 'Schedule', 'Event'],
                requiredForSchedule: ['schedule']
            }
        };
        
        this.errors = [];
        this.warnings = [];
    }

    validateWorkflow(workflowData) {
        this.errors = [];
        this.warnings = [];
        
        // Validace základních vlastností workflow
        this.validateWorkflowProperties(workflowData);
        
        // Validace stages
        this.validateStages(workflowData.stages || []);
        
        // Validace connections a flow
        this.validateWorkflowFlow(workflowData.stages || []);
        
        return {
            isValid: this.errors.length === 0,
            errors: this.errors,
            warnings: this.warnings
        };
    }

    validateWorkflowProperties(workflow) {
        // Validace trigger typu
        if (!workflow.triggerType) {
            this.addError('Workflow musí mít nastaven typ triggeru');
        } else if (!this.validationRules.workflow.validTriggerTypes.includes(workflow.triggerType)) {
            this.addError(`Neplatný typ triggeru: ${workflow.triggerType}`);
        }
        
        // Validace schedule pro plánované workflow
        if (workflow.triggerType === 'Schedule') {
            if (!workflow.schedule) {
                this.addError('Plánované workflow musí mít nastaven cron výraz');
            } else if (!this.isValidCronExpression(workflow.schedule)) {
                this.addError('Neplatný cron výraz');
            }
        }
    }

    validateStages(stages) {
        // Minimální počet stages
        if (stages.length < this.validationRules.stage.minCount) {
            this.addError('Workflow musí obsahovat alespoň jeden krok');
            return;
        }
        
        // Validace jednotlivých stages
        const stageNames = new Set();
        stages.forEach((stage, index) => {
            this.validateStage(stage, index + 1);
            
            // Kontrola duplicitních názvů
            if (stageNames.has(stage.name?.toLowerCase())) {
                this.addError(`Duplicitní název kroku: ${stage.name}`);
            }
            stageNames.add(stage.name?.toLowerCase());
        });
        
        // Validace pořadí
        const orders = stages.map(s => s.order).sort((a, b) => a - b);
        for (let i = 0; i < orders.length; i++) {
            if (orders[i] !== i + 1) {
                this.addError('Pořadí kroků musí být souvislé (1, 2, 3...)');
                break;
            }
        }
    }

    validateStage(stage, order) {
        const prefix = `Krok ${order}`;
        
        // Povinná pole
        this.validationRules.stage.requiredFields.forEach(field => {
            if (!stage[field]) {
                this.addError(`${prefix}: Chybí povinné pole '${field}'`);
            }
        });
        
        // Validace názvu
        if (stage.name) {
            if (stage.name.length > this.validationRules.stage.maxNameLength) {
                this.addError(`${prefix}: Název je příliš dlouhý (max ${this.validationRules.stage.maxNameLength} znaků)`);
            }
        }
        
        // Validace orchestrátoru
        if (stage.orchestratorType && !this.validationRules.stage.validOrchestrators.includes(stage.orchestratorType)) {
            this.addError(`${prefix}: Neplatný orchestrátor '${stage.orchestratorType}'`);
        }
        
        // Validace ReAct agenta
        if (stage.reactAgentType && !this.validationRules.stage.validReActAgents.includes(stage.reactAgentType)) {
            this.addError(`${prefix}: Neplatný ReAct agent '${stage.reactAgentType}'`);
        }
        
        // Validace execution strategy
        if (stage.executionStrategy && !this.validationRules.stage.validExecutionStrategies.includes(stage.executionStrategy)) {
            this.addError(`${prefix}: Neplatná strategie vykonávání '${stage.executionStrategy}'`);
        }
        
        // Stage musí mít buď nástroje nebo ReAct agenta
        if (!stage.reactAgentType && (!stage.tools || stage.tools.length === 0)) {
            this.addError(`${prefix}: Krok musí mít alespoň jeden nástroj nebo ReAct agenta`);
        }
        
        // Validace konfigurace
        if (stage.orchestratorConfiguration) {
            if (!this.isValidJson(stage.orchestratorConfiguration)) {
                this.addError(`${prefix}: Neplatná JSON konfigurace orchestrátoru`);
            }
        }
        
        if (stage.reactAgentConfiguration) {
            if (!this.isValidJson(stage.reactAgentConfiguration)) {
                this.addError(`${prefix}: Neplatná JSON konfigurace ReAct agenta`);
            }
        }
        
        // Validace nástrojů
        if (stage.tools && stage.tools.length > 0) {
            this.validateStageTools(stage.tools, prefix);
        }
        
        // Varování
        if (!stage.description) {
            this.addWarning(`${prefix}: Doporučujeme přidat popis kroku`);
        }
        
        if (stage.timeoutSeconds && stage.timeoutSeconds > 300) {
            this.addWarning(`${prefix}: Timeout je nastaven na více než 5 minut`);
        }
    }

    validateStageTools(tools, stagePrefix) {
        const toolIds = new Set();
        
        tools.forEach((tool, index) => {
            const toolPrefix = `${stagePrefix}, nástroj ${index + 1}`;
            
            // Povinná pole
            if (!tool.toolId) {
                this.addError(`${toolPrefix}: Chybí ID nástroje`);
            }
            
            if (!tool.toolName) {
                this.addError(`${toolPrefix}: Chybí název nástroje`);
            }
            
            // Duplicity
            if (toolIds.has(tool.toolId)) {
                this.addWarning(`${toolPrefix}: Duplicitní nástroj '${tool.toolId}'`);
            }
            toolIds.add(tool.toolId);
            
            // Validace konfigurace
            if (tool.configuration && !this.isValidJson(tool.configuration)) {
                this.addError(`${toolPrefix}: Neplatná JSON konfigurace`);
            }
            
            if (tool.inputMapping && !this.isValidJson(tool.inputMapping)) {
                this.addError(`${toolPrefix}: Neplatné JSON mapování vstupu`);
            }
            
            if (tool.outputMapping && !this.isValidJson(tool.outputMapping)) {
                this.addError(`${toolPrefix}: Neplatné JSON mapování výstupu`);
            }
        });
    }

    validateWorkflowFlow(stages) {
        if (stages.length === 0) return;
        
        // Kontrola návaznosti
        const hasConditionalFlow = stages.some(s => s.executionStrategy === 'Conditional');
        if (hasConditionalFlow) {
            // Kontrola podmínek pro větvení
            stages.forEach(stage => {
                if (stage.executionStrategy === 'Conditional' && !stage.continueCondition) {
                    this.addWarning(`Krok '${stage.name}' má podmíněné vykonávání, ale chybí podmínka pokračování`);
                }
            });
        }
        
        // Kontrola paralelního zpracování
        const hasParallelFlow = stages.some(s => s.executionStrategy === 'Parallel');
        if (hasParallelFlow) {
            this.addWarning('Workflow obsahuje paralelní zpracování - ujistěte se, že nástroje mohou běžet současně');
        }
    }

    isValidCronExpression(cron) {
        if (!cron) return false;
        const parts = cron.trim().split(' ');
        return parts.length === 5 || parts.length === 6;
    }

    isValidJson(jsonString) {
        if (!jsonString) return true;
        try {
            JSON.parse(jsonString);
            return true;
        } catch {
            return false;
        }
    }

    addError(message) {
        this.errors.push({
            type: 'error',
            message: message,
            timestamp: new Date()
        });
    }

    addWarning(message) {
        this.warnings.push({
            type: 'warning',
            message: message,
            timestamp: new Date()
        });
    }

    // UI metody pro zobrazení výsledků validace
    showValidationResults(containerId) {
        const container = document.getElementById(containerId);
        if (!container) return;
        
        container.innerHTML = '';
        
        if (this.errors.length === 0 && this.warnings.length === 0) {
            container.innerHTML = `
                <div class="alert alert-success">
                    <i class="fas fa-check-circle"></i> Workflow je validní
                </div>
            `;
            return;
        }
        
        if (this.errors.length > 0) {
            const errorDiv = document.createElement('div');
            errorDiv.className = 'alert alert-danger';
            errorDiv.innerHTML = `
                <h6><i class="fas fa-exclamation-circle"></i> Chyby (${this.errors.length})</h6>
                <ul class="mb-0">
                    ${this.errors.map(e => `<li>${e.message}</li>`).join('')}
                </ul>
            `;
            container.appendChild(errorDiv);
        }
        
        if (this.warnings.length > 0) {
            const warningDiv = document.createElement('div');
            warningDiv.className = 'alert alert-warning';
            warningDiv.innerHTML = `
                <h6><i class="fas fa-exclamation-triangle"></i> Varování (${this.warnings.length})</h6>
                <ul class="mb-0">
                    ${this.warnings.map(w => `<li>${w.message}</li>`).join('')}
                </ul>
            `;
            container.appendChild(warningDiv);
        }
    }

    // Real-time validace při editaci
    attachToForm(formId) {
        const form = document.getElementById(formId);
        if (!form) return;
        
        // Validace při změně hodnot
        form.addEventListener('input', (e) => {
            this.validateField(e.target);
        });
        
        form.addEventListener('change', (e) => {
            this.validateField(e.target);
        });
    }

    validateField(field) {
        // Odstranění předchozích chybových hlášek
        const errorElement = field.parentElement.querySelector('.invalid-feedback');
        if (errorElement) {
            errorElement.remove();
        }
        field.classList.remove('is-invalid');
        
        // Validace podle typu pole
        let isValid = true;
        let errorMessage = '';
        
        switch (field.name || field.id) {
            case 'stageName':
                if (!field.value) {
                    isValid = false;
                    errorMessage = 'Název je povinný';
                } else if (field.value.length > this.validationRules.stage.maxNameLength) {
                    isValid = false;
                    errorMessage = `Název je příliš dlouhý (max ${this.validationRules.stage.maxNameLength} znaků)`;
                }
                break;
                
            case 'orchestratorType':
                if (!field.value) {
                    isValid = false;
                    errorMessage = 'Orchestrátor je povinný';
                }
                break;
                
            case 'orchestratorConfig':
            case 'reactAgentConfig':
            case 'metadata':
                if (field.value && !this.isValidJson(field.value)) {
                    isValid = false;
                    errorMessage = 'Neplatný JSON formát';
                }
                break;
                
            case 'schedule':
                if (field.value && !this.isValidCronExpression(field.value)) {
                    isValid = false;
                    errorMessage = 'Neplatný cron výraz (např. "0 0 * * *")';
                }
                break;
        }
        
        // Zobrazení chyby
        if (!isValid) {
            field.classList.add('is-invalid');
            const feedback = document.createElement('div');
            feedback.className = 'invalid-feedback';
            feedback.textContent = errorMessage;
            field.parentElement.appendChild(feedback);
        }
        
        return isValid;
    }
}

// Export pro použití v jiných modulech
window.WorkflowValidator = WorkflowValidator;