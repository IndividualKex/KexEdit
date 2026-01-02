# Trains Components Context

Train entity components

## Purpose

- Define train data structures
- Support force calculation offsets
- Provide shared state singletons

## Components

- `Train` - Core train entity data
- `TrainOffset` - Position offset for force calculations
- `SimFollowerSingleton` - Modern train CoM state (wraps KexEdit.Trains.Sim.SimFollower)

## Scope

- In: Train data, position offsets, modern train state
- Out: Systems, rendering