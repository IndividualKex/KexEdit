# Track Context

Track construction, node graph management, and rendering

## Purpose

- Manages track node graph structure
- Handles connections between track sections
- Evaluates Document → Track (single source of truth)
- GPU-accelerated track mesh rendering
- Style configuration and breakpoint detection

## Key Components

- `Node` - Track graph nodes (UI state: position, selection; uint Id for Coaster lookup; Next/Previous for traversal)
- `Connection` - Links between nodes (graph edges)
- `Port` - Connection topology (Type, Id, IsInput) - graph structure only, values in Coaster
- `Segment` - Track segment data for rendering
- `CorePointBuffer` - Computed track points per entity (legacy, for train/physics systems)
- `CoasterData` - Holds Document aggregate on coaster entity (source of truth)
- `TrackSingleton` - Built Track struct (single source of truth for all track data)
- `UIStateData` - UI metadata: node positions, keyframe IDs
- `Dirty` - Marks entities modified by UI for sync
- Section tags - `BridgeTag`, `ReverseTag`, etc.
- `TrackStyle` - Style entity with spacing, threshold parameters
- `TrackStyleSettings` - Style settings (default style, auto style mode)
- `TrackStyleHash` - Hash for style change invalidation
- `Render` - Boolean flag for section visibility
- `RenderStyleSingleton` - Render colors (Primary, Secondary, Tertiary)
- `PieceStyleSingleton` - Piece meshes and native arrays for GPU pipeline
- `StyleConfigSingleton` - Style scalars (DefaultStyleIndex, StyleCount, Version)

## Key Systems

- `CoasterSyncSystem` - Evaluates Document → TrackSingleton, syncs CorePointBuffer per entity
- `GraphTraversalSystem` - Populates Node.Next/Previous and Coaster.RootNode
- `TrackSegmentInitializationSystem` - Creates rendering segments with style breakpoints
- `TrackRenderSystem` - GPU mesh deformation via TrackMeshPipeline
- `SegmentStyleHashSystem` - Computes style hashes for invalidation
- `StyleLoadingSystem` - Loads style settings and creates style entities
- `StyleCleanupSystem` - Removes orphaned style entities
- `ConnectionCleanupSystem` - Removes orphaned connections
- `NodeCleanupSystem` - Removes orphaned nodes

## Dependencies

- KexEdit.Sim, KexEdit.Spline, KexEdit.Document, KexEdit.Track
- KexEdit.Rendering (TrackMeshPipeline, StyleBreakpointDetector)
- KexEdit.Spline.Rendering (SegmentBuilder, StylePieceConfig)

## Scope

- In: Graph structure, connections, Document evaluation, track rendering, styles
- Out: Physics simulation, trains
