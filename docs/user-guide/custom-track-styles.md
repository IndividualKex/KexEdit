# Custom Track Styles

Customize your track's visual style with custom extrusion meshes (rails) and duplication meshes (ties).

## Using Existing Presets

### Quick Selection

1. **View** → **Track Style**
2. Select from available configurations:
    - **Default** - Basic stylized wooden coaster
    - **Intamin Blitz** - Modern steel coaster with multiple stress-responsive styles
3. Track updates immediately in the 3D view

### Automatic Style Selection

Some presets like **Intamin Blitz** contain multiple styles that automatically activate based on track stress (calculated from curvature and velocity factors). Higher stress sections get reinforced visual styles.

### Manual Override

Override automatic selection for aesthetic control:

1. **Select** track element in node graph
2. **Enable** Track Style property
3. **Choose** style index (0, 1, 2, etc.)
4. Selected style applies to entire element

## Creating Custom Configurations

### Accessing Files

1. **View** → **Track Style** → **Open Folder**
2. Click **README.md** for technical instructions
3. Study existing `.json` files to understand the format

### Basic Workflow

1. Create a `.json` configuration file
2. Add `.obj` mesh files to the folder
3. Select your configuration from the View menu

### Mesh Types

Custom track styles use two types of 3D objects:

-   **Extrusion Meshes** (rails) - 2D profiles extruded along the track path
-   **Duplication Meshes** (ties) - Objects placed at regular intervals

### Requirements

-   Must use triangulated `.obj` files
-   Extrusion meshes need exactly 2 cross-sections
-   Colors defined as RGBA values (0-1 range)

For complete configuration format and technical details, see **README.md** in the TrackStyles folder.

---

**Next**: [Custom Cart Styles](custom-cart-styles.md)

---

[← Back to Documentation](../)
