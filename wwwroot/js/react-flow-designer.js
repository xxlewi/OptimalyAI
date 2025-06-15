// React Flow Workflow Designer
// Global variables
let reactFlowInstance = null;
let nodeIdCounter = 1;
let currentEditingNode = null;
let globalModel = null;

// Initialize React Flow
const { useState, useCallback, useRef, useEffect } = React;
const { ReactFlow, useNodesState, useEdgesState, addEdge, MarkerType } = ReactFlowCore;

// Import additional components if available
let Controls = null;
let Background = null; 
let MiniMap = null;

// Try to load additional components
if (typeof ReactFlowControls !== 'undefined') {
    Controls = ReactFlowControls.Controls;
}
if (typeof ReactFlowBackground !== 'undefined') {
    Background = ReactFlowBackground.Background;
}
if (typeof ReactFlowMinimap !== 'undefined') {
    MiniMap = ReactFlowMinimap.MiniMap;
}

// Custom Node Component
const CustomNode = ({ data, selected }) => {
    const getNodeIcon = (type) => {
        switch(type) {
            case 'task': return 'fas fa-cog';
            case 'condition': return 'fas fa-code-branch';
            case 'parallel': return 'fas fa-sitemap';
            case 'InputAdapter': return 'fas fa-download';
            case 'OutputAdapter': return 'fas fa-upload';
            default: return 'fas fa-circle';
        }
    };
    
    const getNodeTypeLabel = (type) => {
        switch(type) {
            case 'task': return 'Úloha';
            case 'condition': return 'Podmínka';
            case 'parallel': return 'Paralelní';
            case 'InputAdapter': return 'Input Adaptér';
            case 'OutputAdapter': return 'Output Adaptér';
            default: return 'Uzel';
        }
    };
    
    return React.createElement('div', {
        className: `react-flow__node-custom ${selected ? 'selected' : ''}`,
        onDoubleClick: (e) => {
            e.stopPropagation();
            editNode(data.id);
        }
    }, [
        React.createElement('div', {
            key: 'header',
            className: `node-header ${data.type || 'task'}`
        }, [
            React.createElement('span', { key: 'title' }, [
                React.createElement('i', { 
                    key: 'icon',
                    className: getNodeIcon(data.type)
                }),
                ` ${data.label || data.name || 'Nový uzel'}`
            ]),
            React.createElement('div', {
                key: 'actions',
                className: 'node-actions'
            }, [
                React.createElement('button', {
                    key: 'edit',
                    className: 'node-action-btn',
                    onClick: (e) => {
                        e.stopPropagation();
                        editNode(data.id);
                    },
                    title: 'Upravit'
                }, React.createElement('i', { className: 'fas fa-edit' })),
                React.createElement('button', {
                    key: 'delete',
                    className: 'node-action-btn',
                    onClick: (e) => {
                        e.stopPropagation();
                        deleteNode(data.id);
                    },
                    title: 'Smazat'
                }, React.createElement('i', { className: 'fas fa-trash' }))
            ])
        ]),
        React.createElement('div', {
            key: 'body',
            className: 'node-body'
        }, [
            data.description && React.createElement('div', {
                key: 'description',
                className: 'node-description'
            }, data.description),
            data.tools && data.tools.length > 0 && React.createElement('div', {
                key: 'tools',
                className: 'node-tools'
            }, data.tools.map((tool, index) => 
                React.createElement('span', {
                    key: index,
                    className: 'tool-badge'
                }, tool.replace(/_/g, ' '))
            ))
        ])
    ]);
};

// Main Workflow Designer Component
const WorkflowDesigner = () => {
    const [nodes, setNodes, onNodesChange] = useNodesState([]);
    const [edges, setEdges, onEdgesChange] = useEdgesState([]);
    const reactFlowWrapper = useRef(null);
    const [reactFlowInstance, setReactFlowInstance] = useState(null);
    
    // Node types
    const nodeTypes = {
        custom: CustomNode
    };
    
    // Connection handler
    const onConnect = useCallback((params) => {
        const edge = {
            ...params,
            type: 'smoothstep',
            animated: true,
            markerEnd: {
                type: MarkerType.ArrowClosed,
                color: '#007bff'
            },
            style: { stroke: '#007bff', strokeWidth: 2 }
        };
        setEdges((eds) => addEdge(edge, eds));
    }, [setEdges]);
    
    // Drop handler
    const onDrop = useCallback((event) => {
        event.preventDefault();
        
        const reactFlowBounds = reactFlowWrapper.current.getBoundingClientRect();
        const nodeType = event.dataTransfer.getData('application/reactflow-nodetype');
        const tool = event.dataTransfer.getData('application/reactflow-tool');
        
        if (!nodeType && !tool) return;
        
        const position = reactFlowInstance.project({
            x: event.clientX - reactFlowBounds.left,
            y: event.clientY - reactFlowBounds.top,
        });
        
        let newNode;
        if (tool) {
            // Create task node with tool
            newNode = {
                id: `node_${nodeIdCounter++}`,
                type: 'custom',
                position,
                data: {
                    id: `node_${nodeIdCounter}`,
                    label: tool.replace(/_/g, ' '),
                    name: tool.replace(/_/g, ' '),
                    type: 'task',
                    description: 'AI nástroj',
                    tools: [tool],
                    useReAct: false
                }
            };
        } else {
            // Create basic node
            const typeLabels = {
                task: 'Úloha',
                condition: 'Podmínka',
                parallel: 'Paralelní zpracování',
                InputAdapter: 'Input Adaptér',
                OutputAdapter: 'Output Adaptér'
            };
            
            newNode = {
                id: `node_${nodeIdCounter++}`,
                type: 'custom',
                position,
                data: {
                    id: `node_${nodeIdCounter}`,
                    label: `${typeLabels[nodeType]} ${nodeIdCounter}`,
                    name: `${typeLabels[nodeType]} ${nodeIdCounter}`,
                    type: nodeType,
                    description: '',
                    tools: [],
                    useReAct: false
                }
            };
        }
        
        setNodes((nds) => nds.concat(newNode));
        toastr.success('Uzel přidán');
    }, [reactFlowInstance, setNodes]);
    
    const onDragOver = useCallback((event) => {
        event.preventDefault();
        event.dataTransfer.dropEffect = 'move';
    }, []);
    
    // Store React Flow instance
    const onInit = useCallback((instance) => {
        setReactFlowInstance(instance);
        window.reactFlowInstance = instance; // Global access
        window.setNodes = setNodes; // Global access
        window.setEdges = setEdges; // Global access
        window.flowNodes = nodes; // Global access
        window.flowEdges = edges; // Global access
    }, [setNodes, setEdges]);
    
    // Update global references when state changes
    useEffect(() => {
        window.flowNodes = nodes;
        window.flowEdges = edges;
    }, [nodes, edges]);
    
    // Load initial data
    useEffect(() => {
        if (globalModel && globalModel.nodes && globalModel.nodes.length > 0) {
            // Convert model nodes to React Flow format
            const flowNodes = globalModel.nodes.map((node, index) => ({
                id: node.id || `node_${index + 1}`,
                type: 'custom',
                position: { x: (index * 250) + 100, y: 100 },
                data: {
                    id: node.id || `node_${index + 1}`,
                    label: node.name || 'Uzel',
                    name: node.name || 'Uzel',
                    type: getNodeTypeString(node.type),
                    description: node.description || '',
                    tools: node.tools || [],
                    useReAct: node.useReAct || false
                }
            }));
            
            const flowEdges = (globalModel.edges || []).map((edge, index) => ({
                id: edge.id || `edge_${index + 1}`,
                source: edge.sourceId,
                target: edge.targetId,
                type: 'smoothstep',
                animated: true,
                markerEnd: {
                    type: MarkerType.ArrowClosed,
                    color: '#007bff'
                },
                style: { stroke: '#007bff', strokeWidth: 2 }
            }));
            
            setNodes(flowNodes);
            setEdges(flowEdges);
            nodeIdCounter = Math.max(nodeIdCounter, flowNodes.length + 1);
        }
    }, []);
    
    return React.createElement('div', {
        className: 'react-flow-wrapper',
        ref: reactFlowWrapper
    }, 
        React.createElement(ReactFlow, {
            nodes,
            edges,
            onNodesChange,
            onEdgesChange,
            onConnect,
            onInit,
            onDrop,
            onDragOver,
            nodeTypes,
            fitView: true,
            attributionPosition: 'bottom-left'
        })
    );
};

// Helper function to convert enum to string
function getNodeTypeString(typeEnum) {
    switch(typeEnum) {
        case 2: return 'task';
        case 3: return 'condition';
        case 4: return 'parallel';
        case 5: return 'InputAdapter';
        case 6: return 'OutputAdapter';
        default: return 'task';
    }
}

// Initialize the designer
function initializeReactFlowDesigner(model) {
    globalModel = model;
    
    const container = document.getElementById('react-flow-container');
    const root = ReactDOM.createRoot(container);
    root.render(React.createElement(WorkflowDesigner));
    
    // Setup drag and drop
    setupDragAndDrop();
}

// Drag and drop setup
function setupDragAndDrop() {
    // Handle both tool-item and palette-item classes
    document.querySelectorAll('.tool-item[draggable="true"], .palette-item[draggable="true"]').forEach(item => {
        item.addEventListener('dragstart', (event) => {
            const nodeType = event.target.closest('[data-node-type]')?.getAttribute('data-node-type');
            const tool = event.target.closest('[data-tool]')?.getAttribute('data-tool');
            
            if (nodeType) {
                event.dataTransfer.setData('application/reactflow-nodetype', nodeType);
            } else if (tool) {
                event.dataTransfer.setData('application/reactflow-tool', tool);
            }
            
            event.dataTransfer.effectAllowed = 'move';
            event.target.style.opacity = '0.5';
        });
        
        item.addEventListener('dragend', (event) => {
            event.target.style.opacity = '1';
        });
    });
}

// Node editing functions
function editNode(nodeId) {
    const nodes = window.flowNodes;
    const node = nodes.find(n => n.id === nodeId);
    if (!node) return;
    
    currentEditingNode = nodeId;
    const data = node.data;
    
    document.getElementById('nodeName').value = data.name || '';
    document.getElementById('nodeDescription').value = data.description || '';
    document.getElementById('nodeType').value = data.type || 'task';
    document.getElementById('nodeCondition').value = data.condition || '';
    document.getElementById('nodeUseReAct').checked = data.useReAct || false;
    
    // Set tools
    const toolsSelect = document.getElementById('nodeTools');
    Array.from(toolsSelect.options).forEach(option => {
        option.selected = (data.tools || []).includes(option.value);
    });
    
    // Show/hide sections based on type
    document.getElementById('conditionSection').style.display = 
        data.type === 'condition' ? 'block' : 'none';
    document.getElementById('toolsSection').style.display = 
        data.type === 'task' ? 'block' : 'none';
    
    // Show adapter configuration section for adapter nodes
    const adapterSection = document.getElementById('adapterSection');
    if (adapterSection) {
        adapterSection.style.display = 
            (data.type === 'InputAdapter' || data.type === 'OutputAdapter') ? 'block' : 'none';
    }
    
    $('#nodeEditModal').modal('show');
}

function updateNode() {
    if (!currentEditingNode) return;
    
    const name = document.getElementById('nodeName').value;
    const description = document.getElementById('nodeDescription').value;
    const type = document.getElementById('nodeType').value;
    const condition = document.getElementById('nodeCondition').value;
    const useReAct = document.getElementById('nodeUseReAct').checked;
    
    const toolsSelect = document.getElementById('nodeTools');
    const tools = Array.from(toolsSelect.selectedOptions).map(option => option.value);
    
    window.setNodes(nodes => 
        nodes.map(node => 
            node.id === currentEditingNode 
                ? {
                    ...node,
                    data: {
                        ...node.data,
                        name,
                        label: name,
                        description,
                        type,
                        condition,
                        useReAct,
                        tools
                    }
                }
                : node
        )
    );
    
    $('#nodeEditModal').modal('hide');
    toastr.success('Uzel aktualizován');
}

function deleteCurrentNode() {
    if (!currentEditingNode) return;
    
    if (confirm('Opravdu smazat tento uzel?')) {
        deleteNode(currentEditingNode);
        $('#nodeEditModal').modal('hide');
    }
}

function deleteNode(nodeId) {
    window.setNodes(nodes => nodes.filter(node => node.id !== nodeId));
    window.setEdges(edges => edges.filter(edge => 
        edge.source !== nodeId && edge.target !== nodeId
    ));
    toastr.success('Uzel smazán');
}

// Workflow operations
function saveWorkflow() {
    const nodes = window.flowNodes || [];
    const edges = window.flowEdges || [];
    
    const workflow = {
        ProjectId: window.currentProjectId,
        ProjectName: window.currentProjectName,
        Nodes: nodes.map(node => ({
            Id: node.id,
            Name: node.data.name,
            Description: node.data.description || '',
            Type: getNodeTypeEnum(node.data.type),
            Position: { X: node.position.x, Y: node.position.y },
            Tools: node.data.tools || [],
            UseReAct: node.data.useReAct || false,
            Orchestrator: '',
            InputPorts: [],
            OutputPorts: [],
            ConditionExpression: node.data.condition || '',
            LoopCondition: '',
            MaxIterations: 10,
            Configuration: {},
            Status: 0
        })),
        Edges: edges.map(edge => ({
            Id: edge.id,
            SourceId: edge.source,
            TargetId: edge.target,
            SourcePortId: '',
            TargetPortId: '',
            Condition: '',
            Label: '',
            DataMapping: ''
        })),
        Metadata: {
            Description: 'Workflow vytvořený pomocí React Flow Designeru',
            Version: '1.0',
            Variables: {},
            Trigger: { Type: 'Manual', CronExpression: '', EventName: '', Config: {} },
            Settings: { MaxExecutionTime: 3600, MaxRetries: 3, EnableDebugLogging: true, ErrorHandling: 'StopOnError' }
        },
        LastModified: new Date().toISOString()
    };
    
    $.ajax({
        url: '/WorkflowDesigner/SaveWorkflow',
        type: 'POST',
        contentType: 'application/json',
        data: JSON.stringify(workflow),
        success: function(response) {
            if (response.success) {
                toastr.success('Workflow uloženo');
            } else {
                toastr.error('Chyba při ukládání: ' + (response.message || 'Neznámá chyba'));
            }
        },
        error: function(xhr, status, error) {
            toastr.error('Chyba při ukládání workflow: ' + error);
        }
    });
}

function getNodeTypeEnum(type) {
    switch(type) {
        case 'task': return 2;
        case 'condition': return 3;
        case 'parallel': return 4;
        case 'InputAdapter': return 5;
        case 'OutputAdapter': return 6;
        default: return 2;
    }
}

function testWorkflow() {
    const nodes = window.flowNodes || [];
    if (nodes.length === 0) {
        toastr.warning('Přidejte alespoň jeden uzel');
        return;
    }
    
    toastr.info('Spouštím test workflow...');
    
    // Simulate execution
    let i = 0;
    const interval = setInterval(() => {
        if (i < nodes.length) {
            toastr.success(`Zpracovávám: ${nodes[i].data.name}`);
            i++;
        } else {
            clearInterval(interval);
            toastr.success('Test dokončen!');
        }
    }, 1000);
}

function clearWorkflow() {
    if (confirm('Opravdu vymazat celý workflow?')) {
        window.setNodes([]);
        window.setEdges([]);
        nodeIdCounter = 1;
        toastr.info('Workflow vyčištěno');
    }
}

function exportWorkflow() {
    const nodes = window.flowNodes || [];
    const edges = window.flowEdges || [];
    
    const workflow = {
        ProjectId: window.currentProjectId,
        ProjectName: window.currentProjectName,
        Nodes: nodes,
        Edges: edges,
        ExportDate: new Date().toISOString()
    };
    
    const json = JSON.stringify(workflow, null, 2);
    const blob = new Blob([json], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    
    const a = document.createElement('a');
    a.href = url;
    a.download = `react-flow-workflow-${Date.now()}.json`;
    a.click();
    
    URL.revokeObjectURL(url);
    toastr.success('Workflow exportováno');
}

function validateWorkflow() {
    const nodes = window.flowNodes || [];
    const edges = window.flowEdges || [];
    const errors = [];
    
    if (nodes.length === 0) {
        errors.push('Workflow neobsahuje žádné uzly');
    }
    
    // Check for disconnected nodes
    nodes.forEach(node => {
        const hasIncoming = edges.some(edge => edge.target === node.id);
        const hasOutgoing = edges.some(edge => edge.source === node.id);
        
        if (!hasIncoming && !hasOutgoing && nodes.length > 1) {
            errors.push(`Uzel "${node.data.name}" není připojen`);
        }
    });
    
    // Check task nodes have tools or description
    nodes.filter(n => n.data.type === 'task').forEach(node => {
        if ((!node.data.tools || node.data.tools.length === 0) && !node.data.description) {
            errors.push(`Úloha "${node.data.name}" nemá přiřazené nástroje ani popis`);
        }
    });
    
    if (errors.length === 0) {
        toastr.success('Workflow je validní!');
    } else {
        toastr.error('Workflow obsahuje chyby:<br>' + errors.join('<br>'));
    }
}