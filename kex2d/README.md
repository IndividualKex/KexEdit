# kex2d

2D prototype for a parallel iterative coaster solver. AVBD-style augmented-Lagrangian
trajectory optimization with free / locked / weighted parameters. The 2D harness
proves the solver math before committing to full 6-DOF.

Currently: barebones shell. Shallot's update loop, canvas2D draw-group system,
Svelte canvas mount, F3 stats overlay. No solver yet. Rendering migrates to
WebGPU later.

## Dev

```bash
bun install
bun link @dylanebert/shallot   # if developing against the local shallot copy at ../../shallot
bun dev                         # vite on :3000
bun check                       # format + tsc + svelte-check + biome
```

## Layout

- `src/main.ts` — boots Shallot (`ProfilePlugin` + `RenderPlugin`, no defaults), mounts Svelte, F3 toggles `data-shallot-debug`
- `src/render.ts` — `Canvas2D` singleton + `attachCanvas2D`, `RenderPlugin` with the draw-group `GridSystem`
- `src/App.svelte` — owns the canvas DOM, calls `attachCanvas2D` on mount

Solver lands as ECS components + systems on top of this shell.
