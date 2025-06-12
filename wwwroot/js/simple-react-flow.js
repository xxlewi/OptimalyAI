// Simple React Flow Implementation
const { useState, useCallback, useRef } = React;
const { ReactFlow, useNodesState, useEdgesState, addEdge, MarkerType, ReactFlowProvider } = ReactFlowCore;

// Node ID counter
let nodeIdCounter = 1;

// Simple Node Component
const SimpleNode = ({ data }) => {
    return React.createElement('div', {
        style: {
            background: 'white',
            border: '2px solid #007bff',
            borderRadius: '8px',
            padding: '10px',
            minWidth: '150px'
        }
    }, [
        React.createElement('strong', { key: 'label' }, data.label || 'Uzel'),
        data.tools && data.tools.length > 0 && React.createElement('div', {
            key: 'tools',
            style: { fontSize: '12px', marginTop: '5px' }
        }, data.tools.join(', '))
    ]);
};

// Main App Component
const SimpleWorkflowApp = () => {
    const [nodes, setNodes, onNodesChange] = useNodesState([]);
    const [edges, setEdges, onEdgesChange] = useEdgesState([]);
    const reactFlowWrapper = useRef(null);
    const [reactFlowInstance, setReactFlowInstance] = useState(null);

    // Node types
    const nodeTypes = {
        simple: SimpleNode
    };

    // Connection handler
    const onConnect = useCallback((params) => {
        setEdges((eds) => addEdge({
            ...params,
            type: 'smoothstep',
            animated: true,
            style: { stroke: '#007bff' }
        }, eds));
    }, [setEdges]);

    // Add node handler
    const addNode = useCallback(() => {
        const newNode = {
            id: `node_${nodeIdCounter++}`,
            type: 'simple',
            position: { x: Math.random() * 400, y: Math.random() * 400 },
            data: { label: `Úloha ${nodeIdCounter}` }
        };
        setNodes((nds) => nds.concat(newNode));
    }, [setNodes]);

    // Store instance
    const onInit = useCallback((instance) => {
        setReactFlowInstance(instance);
        window.rfInstance = instance;
    }, []);

    return React.createElement('div', {
        style: { height: '100%', width: '100%' }
    }, [
        React.createElement('div', {
            key: 'toolbar',
            style: {
                position: 'absolute',
                top: '10px',
                left: '10px',
                zIndex: 10
            }
        }, React.createElement('button', {
            onClick: addNode,
            className: 'btn btn-primary btn-sm'
        }, 'Přidat uzel')),
        React.createElement('div', {
            key: 'flow',
            ref: reactFlowWrapper,
            style: { height: '100%', width: '100%' }
        }, React.createElement(ReactFlow, {
            nodes,
            edges,
            onNodesChange,
            onEdgesChange,
            onConnect,
            onInit,
            nodeTypes,
            fitView: true
        }))
    ]);
};

// Initialize the app
function initSimpleReactFlow() {
    const container = document.getElementById('react-flow-container');
    const root = ReactDOM.createRoot(container);
    root.render(
        React.createElement(ReactFlowProvider, null,
            React.createElement(SimpleWorkflowApp)
        )
    );
}

// Export functions
window.initSimpleReactFlow = initSimpleReactFlow;