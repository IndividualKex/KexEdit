"""Blender UI panels for kexengine."""

from __future__ import annotations

import bpy

from ..core.ffi import is_library_available


class KEXENGINE_PT_main(bpy.types.Panel):
    """Main kexengine panel in the 3D View sidebar."""

    bl_idname = "KEXENGINE_PT_main"
    bl_label = "kexengine"
    bl_space_type = 'VIEW_3D'
    bl_region_type = 'UI'
    bl_category = "kexengine"

    def draw(self, context):
        layout = self.layout

        # Library status
        if not is_library_available():
            box = layout.box()
            box.alert = True
            box.label(text="Library not found", icon='ERROR')
            box.label(text="Build kexengine with FFI feature")
            return

        # Test track generation
        layout.label(text="Test Generators", icon='CURVE_DATA')
        col = layout.column(align=True)
        col.operator("kexengine.generate_simple_track", text="Simple Track", icon='CURVE_BEZCURVE')
        col.operator("kexengine.generate_test_track", text="Animated Track", icon='PLAY')
        col.operator("kexengine.generate_helix", icon='FORCE_VORTEX')


class KEXENGINE_PT_helix_options(bpy.types.Panel):
    """Options panel for helix generation."""

    bl_idname = "KEXENGINE_PT_helix_options"
    bl_label = "Helix Options"
    bl_space_type = 'VIEW_3D'
    bl_region_type = 'UI'
    bl_category = "kexengine"
    bl_parent_id = "KEXENGINE_PT_main"
    bl_options = {'DEFAULT_CLOSED'}

    @classmethod
    def poll(cls, context):
        return is_library_available()

    def draw(self, context):
        layout = self.layout

        # These will show operator defaults
        # Full property support comes in Phase 5
        layout.label(text="Configure in operator popup")
        layout.label(text="(Press F6 after running)")


# Registration
classes = [
    KEXENGINE_PT_main,
    KEXENGINE_PT_helix_options,
]


def register():
    for cls in classes:
        bpy.utils.register_class(cls)


def unregister():
    for cls in reversed(classes):
        bpy.utils.unregister_class(cls)
