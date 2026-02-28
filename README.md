# KexEdit

Roller coaster editor based on Force Vector Design (FVD).

## Structure

- **packages/core/** — Rust physics simulation library. FVD track generation, node graph evaluation, binary serialization
- **plugins/blender/** — Blender 4.2+ addon. Visual track editing with live preview, F-Curve animation, .kex file support
- **app/** — Future web-based editor (Shallot engine)

## Building

Build the core library:

```bash
cd packages/core && cargo build --release --features ffi
```

Copy to blender addon:

```bash
plugins/blender/scripts/build_lib.sh
```

## Links

- [Discord](https://discord.gg/eEY75Nqk3C)
- [Documentation](https://individualkex.github.io/KexEdit/)
- [itch.io](https://individualkex.itch.io/kexedit)

## License

MIT
