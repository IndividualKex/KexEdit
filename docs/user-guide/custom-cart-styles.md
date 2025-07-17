# Custom Cart Styles

Customize your ride vehicles with custom 3D cart models in various formats.

## Using Existing Presets

### Quick Selection

1. **View** → **Cart Style**
2. Select from available configurations
3. Cart updates immediately in the 3D view and ride camera

## Creating Custom Configurations

### Accessing Files

1. **View** → **Cart Style** → **Open Folder**
2. Click **README.md** for technical instructions
3. Study existing `.json` files to understand the format

### Basic Workflow

1. Create a `.json` configuration file
2. Add your `.obj` or `.gltf/.glb` mesh files to the folder
3. Select your configuration from the View menu

### File Requirements

-   **JSON configuration** defines the cart name and mesh path
-   **3D mesh files** in `.obj`, `.gltf`, or `.glb` format
-   GLTF/GLB files preserve materials and textures
-   OBJ files get default materials applied

### Simple Configuration Example

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

For complete configuration format and technical details, see **README.md** in the CartStyles folder.

---

[Back to Documentation](../)
