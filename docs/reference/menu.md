# Menu Reference

Main application menu for project management, editing, and preferences.

## File Menu

### Project Operations

| Menu Item      | Shortcut | Description               |
| -------------- | -------- | ------------------------- |
| **New**        | `Ctrl+N` | Create new project        |
| **Open**       | `Ctrl+O` | Load existing project     |
| **Save**       | `Ctrl+S` | Save to current file      |
| **Save As...** | -        | Save to new file location |

### Export

**NoLimits 2** - Export track as `.nl2elem` file with configurable node spacing (default: 2m).

### Units

**Quick Presets**

-   **Metric** - Meters, current speed unit (m/s or km/h)
-   **Imperial** - Feet, miles per hour

**Individual Settings**

-   **Distance**: Meters (m) | Feet (ft)
-   **Velocity**: m/s | km/h | mph
-   **Angle**: Degrees (deg) | Radians (rad)
-   **Angle Change**: Degrees | Radians (per time/distance)

## Edit Menu

| Menu Item         | Shortcut | Description               |
| ----------------- | -------- | ------------------------- |
| **Undo**          | `Ctrl+Z` | Undo last action          |
| **Redo**          | `Ctrl+Y` | Redo undone action        |
| **Cut**           | `Ctrl+X` | Cut selected items        |
| **Copy**          | `Ctrl+C` | Copy selected items       |
| **Paste**         | `Ctrl+V` | Paste clipboard content   |
| **Delete**        | `Del`    | Delete selected items     |
| **Select All**    | `Ctrl+A` | Select all items          |
| **Deselect All**  | `Alt+A`  | Clear selection           |
| **Sync Playback** | `T`      | Sync timeline to playback |

## View Menu

| Menu Item      | Shortcut | Description                       |
| -------------- | -------- | --------------------------------- |
| **Zoom In**    | `Ctrl++` | Increase UI scale                 |
| **Zoom Out**   | `Ctrl+-` | Decrease UI scale                 |
| **Reset Zoom** | -        | Restore default scale             |
| **Grid**       | `F2`     | Show/hide alignment grid          |
| **Stats**      | `F3`     | Show/hide simulation statistics   |
| **Node Grid**  | `F4`     | Enable/disable node grid snapping |

### Visualization

| Menu Item         | Shortcut | Description                      |
| ----------------- | -------- | -------------------------------- |
| **Velocity**      | `Ctrl+1` | Show velocity color overlay      |
| **Curvature**     | `Ctrl+2` | Show curvature color overlay     |
| **Normal Force**  | `Ctrl+3` | Show normal force color overlay  |
| **Lateral Force** | `Ctrl+4` | Show lateral force color overlay |
| **Roll Speed**    | `Ctrl+5` | Show roll speed color overlay    |
| **Pitch Speed**   | `Ctrl+6` | Show pitch speed color overlay   |
| **Yaw Speed**     | `Ctrl+7` | Show yaw speed color overlay     |

## Help Menu

| Menu Item    | Shortcut | Description                     |
| ------------ | -------- | ------------------------------- |
| **About**    | -        | Application version and credits |
| **Controls** | `Ctrl+H` | Show keyboard shortcuts dialog  |

## KEX Format

KexEdit's native project file format for complete data preservation.

### File Format Features

-   **Binary**: Lightweight, optimized format
-   **Versioned**: Backward compatibility support
-   **Complete**: Preserves all project data including timeline animations

---

[← Back to Documentation](../)
