"""UI layer - Blender operators, panels, and node editor."""

# Hot reload support
if "operators" in locals():
    import importlib
    operators = importlib.reload(operators)
    panels = importlib.reload(panels)
else:
    from . import operators
    from . import panels


def register():
    """Register UI classes with Blender."""
    operators.register()
    panels.register()


def unregister():
    """Unregister UI classes from Blender."""
    panels.unregister()
    operators.unregister()
