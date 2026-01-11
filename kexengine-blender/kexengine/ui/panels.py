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

        # Simple track generation
        layout.label(text="Generate", icon='CURVE_DATA')
        layout.operator("kexengine.generate_simple_track", text="Simple Track", icon='CURVE_BEZCURVE')

        layout.separator()

        # Test files
        layout.label(text="Load Test Files", icon='FILE_FOLDER')
        col = layout.column(align=True)
        col.operator("kexengine.load_shuttle", icon='LOOP_BACK')
        col.operator("kexengine.load_circuit", icon='LOOP_FORWARDS')
        col.operator("kexengine.load_switch", icon='OUTLINER_OB_EMPTY')
        col.operator("kexengine.load_all_types", icon='PACKAGE')


# Registration
classes = [
    KEXENGINE_PT_main,
]


def register():
    for cls in classes:
        bpy.utils.register_class(cls)


def unregister():
    for cls in reversed(classes):
        bpy.utils.unregister_class(cls)
