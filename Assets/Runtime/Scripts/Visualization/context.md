# Visualization Context

Rendering, mesh generation, and visual styling

## Purpose

- Generates track and object meshes
- Manages visual styles and materials
- Handles rendering optimization
- Provides debug visualization

## Key Components

- `Render` - Rendering configuration
- `MeshBuffers` - Mesh data storage
- `NodeMesh` - Node visualization
- `TrackStyle` - Track visual style
- `TrackStyleSettings` - Style configuration
- Gizmo components - Debug visualization

## Key Systems

- `MeshUpdateSystem` - Updates mesh geometry
- `MeshCleanupSystem` - Removes unused meshes
- `VisualizationSystem` - Debug visualization
- `TrackStyleBuildSystem` - Builds track visuals
- `TrackStyleRenderSystem` - Renders track styles
- `TrackStyleSettingsLoadingSystem` - Loads style settings
- `StyleHashSystem` - Manages style caching

## Utils

- `ExtrusionMeshConverter` - Converts extrusion data to meshes

## Dependencies

- Track (for track geometry)
- Physics (for track points)
- Core (for utilities)

## Scope

- In: Mesh generation, rendering, visual styles, debug viz
- Out: Physics, track logic, UI