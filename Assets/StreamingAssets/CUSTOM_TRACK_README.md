# Custom Track Meshes

Instructions for adding custom track meshes.

## Configuration

Edit `Config.json` in the `TrackMesh` folder to configure which meshes to use:

### Spacing

The spacing is the distance between each track point in meters.

```json
"Spacing": 0.4
```

### Duplication Meshes

These are meshes that will be instanced along the track (e.g., railroad ties, bolts, supports):

```json
{
    "MeshPath": "DefaultTie.obj",
    "Color": [1, 1, 1, 1],
    "Step": 2,
    "TexturePath": ""
}
```

-   `MeshPath`: Relative path of the `.obj` mesh asset
-   `Color`: Optional color override
-   `Step`: How often to place the mesh (1 = every track point, 2 = every other point, etc.)
-   `TexturePath`: Optional texture override

### Extrusion Meshes

These are 2D cross-section meshes that will be extruded along the track path (e.g., rail profiles):

```json
{
    "MeshPath": "DefaultRail.obj",
    "Color": [1, 1, 1, 1],
    "TexturePath": "DefaultRailTexture.png"
}
```

-   `MeshPath`: Relative path of the `.obj` mesh asset
-   `Color`: Optional color override
-   `TexturePath`: Optional texture override

_Note: The mesh should consist of single cross-section extruded along the Z axis._

### Gizmos

Experimental debug visualization settings:

```json
"DuplicationGizmos": [
  {
    "StartHeart": -0.5,
    "EndHeart": 0.5,
    "Color": [1, 1, 1, 1]
  }
],
"ExtrusionGizmos": [
  {
    "Heart": 0.0,
    "Color": [1, 1, 1, 1]
  }
]
```
