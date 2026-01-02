# Trains Context

Legacy ECS train systems. Migration target: `Runtime/Trains/`

## Purpose

- Legacy train entities and systems (auto-advancing TrackFollower)
- SimFollowerSingleton: ECS wrapper for modern train state
- SimFollowerInitializationSystem: creates singleton at startup

## Layout

```
Trains/
├── Components/
│   ├── Train.cs, TrainCar.cs, TrainOffset.cs  # Legacy train components
│   └── SimFollowerSingleton.cs                # Modern train state wrapper
├── Systems/
│   ├── TrainUpdateSystem.cs                   # Legacy auto-advance
│   └── SimFollowerInitializationSystem.cs     # Creates SimFollowerSingleton
```

## Dependencies

- KexEdit.Trains.Sim (SimFollower, SimFollowerLogic)
- Track Backend (Sim, Graph, Spline)