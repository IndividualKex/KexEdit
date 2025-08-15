# UI Systems Context

UI update systems for menu operations and real-time interface updates

## Purpose

- Handle menu bar actions and project operations
- Update UI elements in response to state changes
- Manage keyboard shortcuts and user input
- Display real-time statistics and overlays

## Layout

```
Systems/
├── context.md  # This file, folder context (Tier 2)
├── ProjectOperationsSystem.cs  # Menu bar and file operations
├── StatsOverlaySystem.cs  # Real-time statistics display
├── UIScaleSystem.cs  # UI zoom controls
├── VideoControlSystem.cs  # Fullscreen and display
└── [Various]System.cs  # Other UI update systems
```

## Scope

- In-scope: Menu organization, file operations, keyboard shortcuts, UI updates
- Out-of-scope: Track computation, rendering, physics

## Menu Structure

File → Edit → View → Track → Display → Settings → Help

- **File**: Project operations and export
- **Edit**: Undo/redo and clipboard
- **View**: Camera and zoom controls
- **Track**: Track-specific operations and visualization
- **Display**: Overlays and visual toggles
- **Settings**: User preferences and controls
- **Help**: Documentation and about

## Entrypoints

- `ProjectOperationsSystem.OnStartRunning()` - Initialize menu bar
- `ProjectOperationsSystem.OnUpdate()` - Handle keyboard shortcuts
- Various menu action methods called by menu items

## Dependencies

- MenuBar component for menu rendering
- Preferences for user settings
- ProjectOperations for file management