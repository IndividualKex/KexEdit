# UI Context

User interface layer implementing node graph editor, timeline, and controls

## Purpose

- Provides the main editor interface using Unity UI Toolkit
- Implements node-based track editing and timeline controls
- Manages user interactions, dialogs, and visualization

## Layout

```
UI/
├── context.md  # This file, folder context (Tier 2)
├── NodeGraph/  # Node-based editor
│   ├── context.md  # Node graph context
│   └── [Various].cs  # Node graph implementation
├── Timeline/  # Timeline editor
│   ├── context.md  # Timeline context
│   └── [Various].cs  # Timeline implementation
├── Components/  # Reusable UI components
├── Systems/  # UI update systems
│   └── StatsOverlaySystem.cs  # Real-time stats display
├── UIManager.cs  # Main UI coordinator
├── MenuBar.cs  # Application menu
├── VideoControls.cs  # Playback controls
├── ContextMenu.cs  # Right-click menus
├── Preferences.cs  # User preferences
├── ProjectOperations.cs  # File operations
├── StatsFormatter.cs  # Stats formatting with string pools
├── StatsStringPool.cs  # Pre-generated string cache
├── Units.cs  # Unit conversion
├── Extensions.cs  # UI extensions
├── TrainCarPositionCalculator.cs  # Train car position calculations
└── [Various]Dialog.cs  # Additional dialogs
```

## Scope

- In-scope: All user interface, dialogs, visualization, user input handling
- Out-of-scope: Track computation, physics, ECS systems

## Entrypoints

- `UIManager.cs` - Main UI initialization on scene load
- `InitializeUIEvent.cs` - UI startup event
- Various dialogs opened through menu/controls

## Dependencies

- Unity UI Toolkit - Modern UI framework
- TextMeshPro - Text rendering
- StandaloneFileBrowser - File dialogs
- Runtime package - Track data access