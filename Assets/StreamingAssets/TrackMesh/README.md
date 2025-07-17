# Track Mesh Configuration Guide

Create custom track visual styles by adding configuration files and 3D assets.

## Quick Start

1. Create a `.json` config file in this folder
2. Add your `.obj` mesh files and textures
3. Select your config from **View → Track Mesh**

## Configuration Format

```json
{
    "Name": "MyTrack",
    "Spacing": 0.4,
    "DuplicationMeshes": [
        {
            "MeshPath": "MyTie.obj",
            "Color": [0.6, 0.4, 0.3, 1],
            "Step": 2
        }
    ],
    "ExtrusionMeshes": [
        {
            "MeshPath": "MyRail.obj",
            "Color": [0.8, 0.8, 0.8, 1],
            "TexturePath": "RailTexture.png"
        }
    ]
}
```

## Properties

**Name**: The name of the track mesh config. This is used to identify the config in the UI.

**Spacing**: Distance between extrusions in meters (e.g. 0.4)

**DuplicationMeshes**: Objects placed along the track (ties)

-   `MeshPath`: Path to .obj file
-   `Color`: RGBA values (0-1 range)
-   `Step`: Placement frequency (1 = every extrusion, 2 = every other)
-   `TexturePath`: Optional texture file

**ExtrusionMeshes**: 2D profiles extruded along track path (rails)

-   `MeshPath`: Path to 2D profile .obj file
-   `Color`: RGBA values (0-1 range)
-   `TexturePath`: Optional texture file

## Mesh Requirements

### File Format

-   **Must be .obj files** (Wavefront OBJ format)
-   Only vertices, UVs, faces, and normals are imported
-   Materials are ignored - only `Color` property is used
-   **Only triangle faces supported**

### Blender Export Settings

When exporting from Blender as .obj:

-   Check **"Triangulated Mesh"** in export options
-   This ensures compatibility with the track system

### Duplication Meshes

-   Standard 3D objects (ties, bolts, supports, etc.)
-   Any orientation works

### Extrusion Meshes

-   **Must have exactly 2 identical cross-sections** along the Z-axis (Y-axis in Blender)
-   Cross-sections must be perfectly aligned (same X,Y positions at different Z values)
-   Examples: rail profiles, tube cross-sections extruded along Z
-   The cross-section shape will be extruded along the track path

## Texture Files

-   Supported: `.png`, `.jpg`
-   Applied to mesh materials with specified color tint
