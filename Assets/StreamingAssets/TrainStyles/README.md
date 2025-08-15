# Train Style Configuration

Create custom train visual styles by adding configuration files and 3D assets.

## Quick Start

1. Create a `.json` config file in this folder
2. Add your `.obj` or `.gltf/.glb` mesh files
3. Select your config from **View → Train Style**

## Configuration Format

```json
{
    "Name": "MyTrain",
    "Styles": [
        {
            "MeshPath": "StylizedCart.glb"
        }
    ]
}
```

## Properties

**Name**: Display name in the UI

**Styles**: Array of train meshes (typically one)

**MeshPath**: Path to mesh file

## Supported Formats

**OBJ files**: Loaded with default PBR material

**GLTF/GLB files**: Loaded with embedded materials and textures
