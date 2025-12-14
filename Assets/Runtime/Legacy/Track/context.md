# Track Context

Track construction, node graph, and section building

## Purpose

- Manages track node graph structure
- Handles connections between track sections
- Builds various track section types (force, geometric, curved, etc.)
- Manages ports for node connections

## Key Components

- `Node` - Track graph nodes
- `Connection` - Links between nodes
- `Port` - Connection points (position, velocity, rotation, etc.)
- `Segment` - Track segment data
- Section tags - `BridgeTag`, `ReverseTag`, etc.

## Key Systems

- `GraphSystem` - Manages node graph structure
- `GraphTraversalSystem` - Traverses and processes graph
- `Build*System` - ECS adapters delegating to KexEdit.Nodes.*
- `TrackSegmentInitializationSystem` - Initializes segments

## Entry Points

- `Track.cs` - Core track data structure

## Dependencies

- KexEdit.Core (physics types)
- KexEdit.Nodes.* (section building logic)

## Scope

- In: Graph structure, section building, connections
- Out: Physics simulation, rendering, trains
