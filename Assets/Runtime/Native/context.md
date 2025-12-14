# KexEdit.Native

Native library bindings for performance-critical code.

## Purpose

FFI interop layer between C# and native libraries (Rust, C++). Not adapters - these are alternative implementations of Core domain logic.

## Layout

```
Native/
├── context.md
└── RustCore/  # Rust implementation of Core domain
    ├── RustMath.cs
    ├── RustFrame.cs
    ├── KexEdit.Native.RustCore.asmdef
    └── context.md
```

## Dependencies

- Native DLLs in `Assets/Runtime/Plugins/`
- Unity.Mathematics (for type conversions)
