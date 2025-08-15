# Train Style Configuration

Create custom train visual styles by adding configuration files and 3D assets.

## Quick Start

1. Create a `.json` config file in this folder
2. Add your `.obj` or `.gltf/.glb` mesh files
3. Select your config from **View → Train Style**

## Configuration Format

### Simple Configuration
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

### Advanced Configuration with Car Overrides
```json
{
    "Name": "RollerCoaster",
    "CarCount": 5,
    "CarSpacing": 2.5,
    "DefaultCar": {
        "MeshPath": "DefaultCar.glb",
        "WheelAssemblies": [
            {
                "MeshPath": "DefaultWheels.glb",
                "Offset": 0.0
            }
        ]
    },
    "CarOverrides": [
        {
            "Index": 0,
            "MeshPath": "FrontCar.glb"
        },
        {
            "Index": -1,
            "MeshPath": "BackCar.glb"
        }
    ]
}
```

## Properties

**Name**: Display name in the UI

**CarCount**: Number of cars in the train (when using DefaultCar template)

**CarSpacing**: Distance between car centers

**DefaultCar**: Template configuration applied to all cars

**CarOverrides**: Array of overrides for specific car positions

**MeshPath**: Path to mesh file

**WheelAssemblies**: Array of wheel assembly configurations

**Index**: Car position (0-based from front, negative from back: -1 = last car, -2 = second to last)

**Offset**: Position offset for wheel assemblies

**Styles**: Array of train meshes (legacy format)

## Supported Formats

**OBJ files**: Loaded with default PBR material

**GLTF/GLB files**: Loaded with embedded materials and textures
