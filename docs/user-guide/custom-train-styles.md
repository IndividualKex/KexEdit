# Custom Train Styles

Customize your ride vehicles with custom 3D train models in various formats, including support for multi-car trains.

## Using Existing Presets

### Quick Selection

1. **Track** → **Train Style**
2. Select from available configurations
3. Train updates immediately in the 3D view and ride camera

### Adjusting Train Length

1. **Track** → **Train Style** → **Edit Count...**
2. Enter the number of cars (1-20)
3. Train automatically updates with the specified number of cars

## Creating Custom Configurations

### Accessing Files

1. **Track** → **Train Style** → **Open Trains Folder**
2. Click **README.md** for technical instructions
3. Study existing `.json` files to understand the format

### Basic Workflow

1. Create a `.json` configuration file
2. Add your `.obj` or `.gltf/.glb` mesh files to the folder
3. Select your configuration from the View menu

### File Requirements

-   **JSON configuration** defines the train name, mesh paths, and car arrangements
-   **3D mesh files** in `.obj`, `.gltf`, or `.glb` format
-   GLTF/GLB files preserve materials and textures
-   OBJ files get default materials applied
-   Support for front, middle, and back car variations

### Configuration Examples

#### Simple Single Car
```json
{
    "Name": "MyCart",
    "Styles": [
        {
            "MeshPath": "MyCustomCart.glb"
        }
    ]
}
```

#### Multi-Car Train
```json
{
    "Name": "Modern Train",
    "Styles": [
        {
            "FrontCarMeshPath": "FrontCar.glb",
            "MidCarMeshPath": "MidCar.glb",
            "BackCarMeshPath": "BackCar.glb",
            "WheelAssemblyMeshPath": "WheelAssembly.glb"
        }
    ]
}
```

For complete configuration format and technical details, see **README.md** in the TrainStyles folder.

---

[Back to Documentation](../)
