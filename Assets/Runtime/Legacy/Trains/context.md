# Trains Context

Train vehicles, cars, and wheel assemblies

## Purpose

- Manages train creation and lifecycle
- Controls train car physics and movement
- Handles wheel assemblies and bogies
- Manages train visual styles

## Key Components

- `Train` - Main train entity
- `TrainCar` - Individual train cars
- `WheelAssembly` - Wheel and bogie configuration
- `TrainStyle` - Visual styling data
- `TrainStyleData` - Style configuration

## Key Systems

- `TrainCreationSystem` - Spawns trains
- `TrainUpdateSystem` - Updates train state (reads CorePointBuffer)
- `TrainCarCreationSystem` - Creates train cars
- `TrainCarUpdateSystem` - Updates car physics
- `TrainCarTransformUpdateSystem` - Positions cars on track
- `WheelAssemblyUpdateSystem` - Updates wheel rotation
- `TrainStyleLoadingSystem` - Loads train styles

## Dependencies

- Physics (for track following)
- Visualization (for rendering train meshes)
- Persistence (for loading train styles)

## Scope

- In: Train vehicles, cars, wheels, train styles
- Out: Track construction, physics simulation