# Custom Track Meshes

Customize your track's visual style with custom extrusion meshes (i.e. rails) and duplication meshes (i.e. ties).

## Using Existing Presets

1. **View** → **Track Mesh**
2. Select from available configurations
3. Track updates immediately in the 3D view

## Creating Custom Configurations

### Workflow

1. **View** → **Track Mesh** → **Open Folder**
2. Create a `.json` configuration file
3. Add `.obj` mesh files to the folder
4. Select your configuration from the View menu

### Mesh Types

Custom track meshes use two types of 3D objects:

-   **Extrusion Meshes** (i.e. rails) - 2D profiles extruded along the track path
-   **Duplication Meshes** (i.e. ties) - Objects placed at regular intervals

### Requirements

-   Must use triangulated `.obj` files
-   Extrusion meshes need exactly 2 cross-sections
-   Colors defined as RGBA values (0-1 range)

For complete configuration format and technical details, see **`README.md`** in the TrackMesh folder.

---

[← Back to Documentation](../)
