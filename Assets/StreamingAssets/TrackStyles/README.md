# Track Style Configuration

Create custom track visual styles by adding configuration files and 3D assets.

## Quick Start

1. Create a `.json` config file in this folder
2. Add your `.obj` mesh files and textures
3. Select your config from **View → Track Style**

## Configuration Format

```json
{
    "Name": "MyTrack",
    "BaseColor": { "r": 0.8, "g": 0.8, "b": 0.8, "a": 1.0 },
    "Styles": [
        {
            "Spacing": 0.4,
            "Threshold": 0.0,
            "DuplicationMeshes": [
                {
                    "MeshPath": "MyTie.obj",
                    "Step": 2,
                    "Offset": 0,
                    "Color": { "r": 0.6, "g": 0.4, "b": 0.3, "a": 1.0 },
                    "TexturePath": "TieTexture.png"
                }
            ],
            "ExtrusionMeshes": [
                {
                    "MeshPath": "MyRail.obj",
                    "Color": { "r": 0.9, "g": 0.9, "b": 0.9, "a": 1.0 },
                    "TexturePath": "RailTexture.png"
                }
            ],
            "StartCapMeshes": [
                {
                    "MeshPath": "MyRail_StartCap.obj"
                }
            ],
            "EndCapMeshes": [
                {
                    "MeshPath": "MyRail_EndCap.obj"
                }
            ]
        }
    ]
}
```

## Properties

**Name**: Display name in the UI

**BaseColor**: Default RGBA color (0-1 range) inherited by meshes with `Color.a = 0`

**Styles**: Array of style definitions. Multiple styles can be defined with different thresholds.

**Spacing**: Distance between extrusions in meters (e.g. 0.4)

**Threshold**: Minimum stress level to activate this style (0.0 = always active)

**DuplicationMeshes**: Objects placed along the track (ties)

-   `MeshPath`: Path to .obj file
-   `Color`: RGBA values (0-1 range)
-   `Step`: Placement frequency (1 = every extrusion, 2 = every other)
-   `Offset`: Phase shift in placement pattern
-   `TexturePath`: Optional texture file

**ExtrusionMeshes**: 2D profiles extruded along track path (rails)

-   `MeshPath`: Path to 2D profile .obj file
-   `Color`: RGBA values (0-1 range)
-   `TexturePath`: Optional texture file

**StartCapMeshes**: Transition pieces at segment beginnings

**EndCapMeshes**: Transition pieces at segment endings

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

### Cap Meshes

-   Start caps are placed at segment beginnings
-   End caps are placed at segment endings
-   Should align with corresponding extrusion mesh geometry

## Texture Files

-   Supported: `.png`, `.jpg`
-   Applied to mesh materials with specified color tint
