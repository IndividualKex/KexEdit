# Contributing

## Setup

### Prerequisites

-   **Unity 6000.1.0f1** (exact version required)
-   Git
-   A GitHub account

### Getting Started

1. **Fork the repository**

    ```bash
    # Fork via GitHub web interface, then clone your fork
    git clone https://github.com/YOUR_USERNAME/KexEdit.git
    cd KexEdit
    ```

2. **Open in Unity**

    - Launch Unity Hub
    - Click "Open" or "Add project from disk"
    - Select the `KexEdit` folder (the one containing `Assets/`, `Packages/`, etc.)
    - Unity will automatically detect and open the project

3. **Verify setup**
    - Open the `Main` scene in `Assets/Scenes/`
    - Press Play to ensure the project runs without errors
    - Check that all packages resolve correctly (may take a few minutes on first open)

### Development Workflow

1. **Create a feature branch**

    ```bash
    git checkout -b feature/your-feature-name
    ```

2. **Make your changes**

    - Follow the existing code style and conventions
    - Test your changes thoroughly
    - Ensure the project builds without errors

3. **Commit your changes**

    ```bash
    git add .
    git commit -m "Add feature: brief description"
    ```

4. **Push and create a Pull Request**
    ```bash
    git push origin feature/your-feature-name
    ```
    Then create a PR via GitHub web interface.

## How to Contribute

### Reporting Issues

-   Use the [GitHub Issues](../../issues) page
-   Include your Unity version, OS, and reproduction steps
-   Attach screenshots or videos if helpful
-   Check existing issues first to avoid duplicates

## Project Structure

```
Assets/
├── Scripts/           # Core C# scripts
│   ├── Components/    # ECS components
│   ├── Systems/       # ECS systems
│   ├── UI/           # UI-related code
│   └── Authoring/    # MonoBehaviour authoring components
├── Scenes/           # Unity scenes
├── Settings/         # Project settings and configurations
└── [other folders]   # Meshes, materials, etc.
```

## Getting Help

-   **Discord**: [Join our community](https://discord.gg/eEY75Nqk3C) for general discussion
-   **Issues**: Use GitHub Issues for bugs and feature requests

---

Thanks for contributing 🎢
