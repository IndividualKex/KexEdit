# Physics Context

Simulation, dynamics, and movement systems

## Purpose

- Computes track physics and dynamics
- Manages object movement along tracks
- Handles force calculations and keyframes
- Provides collision detection
- Calculates forces at offset positions from train centroid

## Key Components

- `TrackFollower` - Follows track paths
- `DistanceFollower` - Distance-based movement
- `Steering` - Directional control
- `TrackPoint` - Sampled track positions
- `CurveData` - Track curvature information
- Force keyframes - Lateral, normal, friction, etc.
- Read-only force buffers - `ReadNormalForce`, `ReadLateralForce`, `ReadPitchSpeed`, `ReadYawSpeed`, `ReadRollSpeed`
- `ReadPivot` - Offset position for force calculations
- Collider components - Track collision detection

## Key Systems

- `TrackPointSystem` - Generates track sample points with visualization values
- `ReadOnlyForceComputationSystem` - Computes forces at offset positions with velocity-adjusted angular rates
- `TrackFollowerUpdateSystem` - Updates followers on track
- `DistanceFollowerUpdateSystem` - Distance-based updates
- `TrackSegmentUpdateSystem` - Updates segment physics
- `TrackColliderCreationSystem` - Creates collision geometry
- `TrackColliderCleanupSystem` - Removes old colliders

## Dependencies

- Track (for track structure)
- Core (for base components)

## Scope

- In: Physics simulation, movement, forces, collisions
- Out: Rendering, track construction, UI