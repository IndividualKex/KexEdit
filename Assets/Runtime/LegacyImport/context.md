# LegacyImport Context

Legacy .kex file import adapter.

## Purpose

- Converts legacy SerializedGraph format → new Coaster format
- Enables loading existing .kex files in new architecture
- Maps legacy node/port/edge structure to KexGraph
- Converts legacy PointData anchors to new Point type
- Migrates keyframe data to KeyframeStore

## Layout

```
LegacyImport/
├── context.md
├── KexEdit.LegacyImport.asmdef
└── LegacyImporter.cs
```

## Dependencies

- KexGraph, KexEdit.Core, KexEdit.Nodes, KexEdit.Coaster
- KexEdit.Legacy (for SerializedGraph format)
