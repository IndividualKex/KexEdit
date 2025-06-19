Export systems for saving projects and sharing with other platforms.

## KEX Format

Native KexEdit binary format for saving and loading projects.

### File Format

-   **Binary**: Lightweight, optimized format
-   **Versioned**: Backward compatibility support
-   **Complete**: Preserves all project data

### Save Operations

| Method      | Shortcut | Description               |
| ----------- | -------- | ------------------------- |
| **Save**    | `Ctrl+S` | Save to current file      |
| **Save As** | -        | Save to new file location |
| **New**     | `Ctrl+N` | Create new project        |
| **Open**    | `Ctrl+O` | Load existing project     |

## NoLimits 2 Export

Export track geometry for NoLimits 2 simulator.

### Export

**File → Export → NoLimits 2** - Set node spacing (default: 2m) and save as `.nl2elem` file.

### Limitations

-   ✅ **Track Shape**: Position and orientation data
-   ✅ **Banking**: Roll information preserved
-   ❌ **Property Overrides**: Velocity, friction, etc. overrides not exported
-   ❌ **Complete Circuits**: Bridge connections not supported
-   ❌ **Timeline Data**: Animation curves not included

---

[← Back to Documentation](../)
