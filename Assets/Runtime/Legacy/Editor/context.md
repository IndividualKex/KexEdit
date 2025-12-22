# Editor Context

Unity Editor tools for track import/export and utilities.

## Purpose

- Editor-only tools that run in Unity Editor
- Track import from external formats
- Track data export for debugging and testing

## Layout

```
Editor/
├── context.md
├── KexEditEditorUtils.cs  # Shared editor utilities
├── TrackImporter.cs       # .kex file asset importer
└── TrackDataExporter.cs   # JSON export (KexEdit > Export Track Data)
```

## Entrypoints

- `TrackDataExporter.Export()` - Menu item, requires Play mode with track loaded
- `TrackImporter` - Asset importer for .kex files

## Dependencies

- KexEdit - Core runtime types
- KexEdit.UI - ProjectOperations for track name
