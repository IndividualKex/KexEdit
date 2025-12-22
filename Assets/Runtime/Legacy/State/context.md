# State Context

Application state and singleton management

## Purpose

- Manages global application state
- Provides singleton entities for shared state
- Handles user preferences
- Manages selection and UI state

## Key Components

- `UIStateSingleton` - UI state management
- `PauseSingleton` - Pause state
- `CameraState` - Camera configuration
- `NodeGraphState` - Node graph view state
- `TimelineState` - Timeline view state
- `Preferences` - User preferences
- `SelectedBlend` - Selection blending
- `SelectedProperties` - Selected object properties
- `PropertyOverrides` - Property override system

## Dependencies

- Core (for base singleton support)

## Scope

- In: Global state, singletons, preferences, selection
- Out: Feature-specific logic, rendering, physics