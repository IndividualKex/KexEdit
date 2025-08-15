# Menu Reference

Main application menu for project management, editing, and preferences.

## File Menu

### Project Operations

| Menu Item         | Shortcut | Description                       |
| ----------------- | -------- | --------------------------------- |
| **New**           | `Ctrl+N` | Create new project                |
| **Open...**       | `Ctrl+O` | Load existing project             |
| **Open Recent →** | -        | Recent files and recovery options |
| **Save**          | `Ctrl+S` | Save to current file              |
| **Save As...**    | -        | Save to new file location         |

**Open Recent** submenu includes:
- **Recover Last Session** - Restore from auto-recovery file
- Recent project files list

### Export

| Menu Item           | Description                                             |
| ------------------- | ------------------------------------------------------- |
| **NoLimits 2...**   | Export track as `.nl2elem` file with node spacing dialog |
| **Track Mesh**      | Export track mesh geometry                              |

## Edit Menu

| Menu Item        | Shortcut             | Description             |
| ---------------- | -------------------- | ----------------------- |
| **Undo**         | `Ctrl+Z`             | Undo last action        |
| **Redo**         | `Ctrl+Y` / `Ctrl+Shift+Z` | Redo undone action |
| **Cut**          | `Ctrl+X`             | Cut selected items      |
| **Copy**         | `Ctrl+C`             | Copy selected items     |
| **Paste**        | `Ctrl+V`             | Paste clipboard content |
| **Delete**       | `Del`                | Delete selected items   |
| **Select All**   | `Ctrl+A`             | Select all items        |
| **Deselect All** | `Alt+A`              | Clear selection         |

## View Menu

### Display

| Menu Item      | Shortcut | Description       |
| -------------- | -------- | ----------------- |
| **Fullscreen** | `F11`    | Toggle fullscreen |

### Camera

| Menu Item                | Shortcut       | Description                    |
| ------------------------ | -------------- | ------------------------------ |
| **Front View**           | `Numpad 1`     | View from front                |
| **Back View**            | `Ctrl+Numpad 1`| View from back                 |
| **Right View**           | `Numpad 3`     | View from right side           |
| **Left View**            | `Ctrl+Numpad 3`| View from left side            |
| **Top View**             | `Numpad 7`     | View from top                  |
| **Bottom View**          | `Ctrl+Numpad 7`| View from bottom               |
| **Toggle Orthographic**  | `Numpad 5`     | Toggle perspective/orthographic |
| **Ride Camera**          | `R`            | Toggle ride camera view        |

### UI Scaling

| Menu Item      | Shortcut | Description       |
| -------------- | -------- | ----------------- |
| **Zoom In**    | `Ctrl++` | Increase UI scale |
| **Zoom Out**   | `Ctrl+-` | Decrease UI scale |
| **Reset Zoom** | `Ctrl+0` | Reset to 100%     |

## Track Menu

### Track Operations

| Menu Item         | Shortcut | Description                       |
| ----------------- | -------- | --------------------------------- |
| **Add Keyframe**  | `I`      | Insert keyframe at current time   |
| **Sync Playback** | `T`      | Sync timeline to simulation time  |
| **Pivot...**      | -        | Edit pivot point for force calculations |

### Visualization Mode

Toggle color-coded track overlays for analysis:

| Menu Item         | Shortcut | Description                      |
| ----------------- | -------- | -------------------------------- |
| **None**          | -        | No visualization overlay          |
| **Velocity**      | `Ctrl+1` | Show velocity color overlay      |
| **Curvature**     | `Ctrl+2` | Show curvature color overlay     |
| **Normal Force**  | `Ctrl+3` | Show normal force color overlay  |
| **Lateral Force** | `Ctrl+4` | Show lateral force color overlay |
| **Roll Speed**    | `Ctrl+5` | Show roll speed color overlay    |
| **Pitch Speed**   | `Ctrl+6` | Show pitch speed color overlay   |
| **Yaw Speed**     | `Ctrl+7` | Show yaw speed color overlay     |
| **Edit Ranges...** | -       | Customize visualization color ranges |

### Track Style

Customize visual appearance of track rails and ties:

- Select from available track style presets
- **Edit Colors...** - Customize track element colors
- **Auto Style** - Enable automatic style selection based on track stress
- **Open Styles Folder** - Access track style configuration files

### Train Style

Customize ride vehicle appearance:

- Select from available train style presets
- **Edit Count...** - Set number of cars in the train
- **Open Trains Folder** - Access train style configuration files

## Display Menu

### Visibility Options

| Menu Item      | Shortcut | Description                       |
| -------------- | -------- | --------------------------------- |
| **Gizmos**     | `F1`     | Show/hide track manipulation gizmos |
| **Grid**       | `F2`     | Show/hide ground alignment grid  |
| **Stats**      | `F3`     | Show/hide simulation statistics overlay |
| **Node Grid**  | `F4`     | Enable/disable node grid snapping |

### Background

| Menu Item  | Description                     |
| ---------- | ------------------------------- |
| **Solid**  | Solid color background          |
| **Sky**    | Procedural sky with atmosphere  |

## Settings Menu

### Units

**Quick Presets:**
- **Metric** - Meters, m/s or km/h
- **Imperial** - Feet, mph

**Individual Settings:**
- **Distance** → Meters (m) | Feet (ft)
- **Velocity** → m/s | km/h | mph
- **Angle** → Degrees | Radians
- **Angle Change** → Degrees | Radians (per time/distance)

### Controls

| Menu Item          | Description                              |
| ------------------ | ---------------------------------------- |
| **Invert Scroll**  | Reverse mouse scroll direction           |
| **Sensitivity...** | Adjust camera and control sensitivity    |
| **Emulate Numpad** | Use number row keys (1-7) for view shortcuts |

### Other Settings

| Menu Item          | Description                              |
| ------------------ | ---------------------------------------- |
| **Ride Camera...** | Configure ride camera settings          |
| **Reset to Default** | Restore all settings to defaults      |

## Help Menu

| Menu Item    | Shortcut | Description                     |
| ------------ | -------- | ------------------------------- |
| **About...** | -        | Application version and credits |
| **Controls...** | `Ctrl+H` | Show keyboard shortcuts dialog

## KEX Format

KexEdit's native project file format for complete data preservation.

### File Format Features

-   **Binary**: Lightweight, optimized format
-   **Versioned**: Backward compatibility support
-   **Complete**: Preserves all project data including timeline animations

---

[← Back to Documentation](../)
