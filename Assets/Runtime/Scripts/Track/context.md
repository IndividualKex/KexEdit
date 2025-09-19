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

## Authoring

- `NodeAspect` - Node configuration and behavior
- `ConnectionAspect` - Connection management
- Section aspects - Build logic for each track type

## Key Systems

- `GraphSystem` - Manages node graph structure
- `GraphTraversalSystem` - Traverses and processes graph
- `Build*System` - Constructs specific section types
- `TrackSegmentInitializationSystem` - Initializes segments

## Entry Points

- `Track.cs` - Core track data structure

## Dependencies

- Core (utilities, base components)
- Physics (for track point generation)

## Scope

- In: Graph structure, section building, connections
- Out: Physics simulation, rendering, trains