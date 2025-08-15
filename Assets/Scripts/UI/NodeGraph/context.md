# NodeGraph Context

Node-based visual editor for track construction and connections

## Purpose

- Implements a node graph interface for visual track editing
- Manages node creation, connections, and property editing
- Provides visual feedback and manipulation tools

## Layout

```
NodeGraph/
├── context.md  # This file, folder context (Tier 2)
├── NodeGraphView.cs  # Main graph canvas
├── NodeView.cs  # Individual node representation
├── ConnectionView.cs  # Node connections
├── NodePort.cs  # Connection ports
├── NodeGraphController.cs  # Graph logic
├── NodePropertyPanel.cs  # Property inspector
├── NodeContextMenu.cs  # Right-click options
└── NodeGraphUtils.cs  # Helper utilities
```

## Scope

- In-scope: Node graph visualization, connections, property editing, user interactions
- Out-of-scope: Track computation, physics calculations, file I/O

## Entrypoints

- `NodeGraphView.cs` - Main graph canvas initialization
- `NodeGraphController.cs` - Handles graph logic and updates
- Called from UIManager during UI initialization

## Dependencies

- Unity UI Toolkit - Graph rendering
- Parent UI layer - Integration with main editor
- Runtime Components - Node data structures