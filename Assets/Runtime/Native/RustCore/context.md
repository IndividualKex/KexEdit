# KexEdit.Native.RustCore

C# FFI bindings for Rust native library.

## Purpose

- P/Invoke wrappers for `kexedit_core.dll`
- Type marshalling (Unity.Mathematics ↔ Rust FFI types)

## Layout

```
RustCore/
├── RustForceNode.cs   # ForceNode.Build FFI wrapper
├── RustPoint.cs       # Point type marshalling
├── RustKeyframe.cs    # Keyframe type marshalling + evaluation
├── RustFrame.cs       # Frame type marshalling
├── RustMath.cs        # Float3 type marshalling
├── RustQuaternion.cs  # Quaternion ops (diagnostic/validation)
└── context.md
```

## Toggle

Enable via `USE_RUST_BACKEND` scripting define symbol. See `layers/migration-plan.md`.

## Dependencies

- KexEdit.Core (for Core types)
- Unity.Mathematics
- Native: `Assets/Runtime/Plugins/kexedit_core.dll`
