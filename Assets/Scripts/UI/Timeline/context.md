# Timeline Context

Timeline and curve editor for animating track properties over time

## Purpose

- Provides dope sheet and curve editing for track animations
- Manages keyframes, interpolation, and property curves
- Displays read-only computed properties as dashed curves
- Enables time-based control of track parameters

## Layout

```
Timeline/
├── context.md  # This file
├── Components/
│   ├── PropertyData.cs  # Editable property keyframes
│   ├── ReadOnlyPropertyData.cs  # Computed property display
│   └── TimelineData.cs  # Timeline state
├── Systems/
│   └── TimelineControlSystem.cs  # Timeline logic
├── CurveView.cs  # Curve rendering
├── TimelineOutliner.cs  # Property list
├── ReadOnlyButton.cs  # Read-only toggle
├── Extensions.cs  # Drawing utilities
└── PropertyAdapter.cs  # Editable property adapters
```

## Scope

- In-scope: Timeline UI, keyframe editing, curve manipulation, time scrubbing
- Out-of-scope: Track physics, node graph, animation evaluation

## Entrypoints

- `TimelineView.cs` - Timeline initialization
- `TimelineController.cs` - Timeline logic
- Called from UIManager during UI setup

## Dependencies

- Unity UI Toolkit - Rendering
- Parent UI layer - Editor integration
- Runtime Systems - Animation data