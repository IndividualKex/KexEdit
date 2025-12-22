# Track Context

Track construction and node graph management

## Purpose

- Manages track node graph structure
- Handles connections between track sections
- Manages ports for node connections

## Key Components

- `Node` - Track graph nodes (UI state: position, selection; uint Id for Coaster lookup; Next/Previous for traversal)
- `Connection` - Links between nodes (graph edges)
- `Port` - Connection topology (Type, Id, IsInput) - graph structure only, values in Coaster
- `Segment` - Track segment data for rendering
- `CorePointBuffer` - Computed track points from CoasterEvaluator
- `CoasterData` - Holds KexEdit.Coaster.Coaster aggregate on coaster entity (source of truth)
- `Dirty` - Marks entities modified by UI for sync to Coaster
- Section tags - `BridgeTag`, `ReverseTag`, etc.

## Key Systems

- `CoasterSyncSystem` - Evaluates Coaster aggregate → writes CorePointBuffer
- `GraphTraversalSystem` - Populates Node.Next/Previous and Coaster.RootNode
- `TrackSegmentInitializationSystem` - Creates rendering segments from CorePointBuffer
- `ConnectionCleanupSystem` - Removes orphaned connections
- `NodeCleanupSystem` - Removes orphaned nodes

## Entry Points

- `Track.cs` - Core track data structure

## Dependencies

- KexEdit.Core (physics types)
- KexEdit.Coaster (Coaster aggregate, CoasterEvaluator)
- KexGraph (graph data structure)

## Scope

- In: Graph structure, connections, Coaster evaluation
- Out: Physics simulation, rendering, trains
