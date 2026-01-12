"""Blender UI panels for kexedit."""

from __future__ import annotations

import bpy

from ..core.ffi import is_library_available


class KEXEDIT_PT_main(bpy.types.Panel):
    """Main kexedit panel in the 3D View sidebar."""

    bl_idname = "KEXEDIT_PT_main"
    bl_label = "kexedit"
    bl_space_type = 'VIEW_3D'
    bl_region_type = 'UI'
    bl_category = "kexedit"

    def draw(self, context):
        layout = self.layout

        # Library status
        if not is_library_available():
            box = layout.box()
            box.alert = True
            box.label(text="Library not found", icon='ERROR')
            box.label(text="Build kexedit with FFI feature")
            return

        # Simple track generation
        layout.label(text="Generate", icon='CURVE_DATA')
        layout.operator("kexedit.generate_simple_track", text="Simple Track", icon='CURVE_BEZCURVE')

        layout.separator()

        # Test files
        layout.label(text="Load Test Files", icon='FILE_FOLDER')
        col = layout.column(align=True)
        col.operator("kexedit.load_shuttle", icon='LOOP_BACK')
        col.operator("kexedit.load_circuit", icon='LOOP_FORWARDS')
        col.operator("kexedit.load_switch", icon='OUTLINER_OB_EMPTY')
        col.operator("kexedit.load_all_types", icon='PACKAGE')


class KEXEDIT_PT_track_settings(bpy.types.Panel):
    """Track settings panel, only visible for kexedit tracks."""

    bl_idname = "KEXEDIT_PT_track_settings"
    bl_label = "Track Settings"
    bl_space_type = 'VIEW_3D'
    bl_region_type = 'UI'
    bl_category = "kexedit"
    bl_parent_id = "KEXEDIT_PT_main"

    @classmethod
    def poll(cls, context):
        """Only show when a kexedit track is selected."""
        obj = context.active_object
        return obj is not None and obj.get("kex_is_track")

    def draw(self, context):
        layout = self.layout
        layout.use_property_split = True
        layout.use_property_decorate = False


class KEXEDIT_PT_anchor_settings(bpy.types.Panel):
    """Anchor settings subpanel."""

    bl_idname = "KEXEDIT_PT_anchor_settings"
    bl_label = "Anchor"
    bl_space_type = 'VIEW_3D'
    bl_region_type = 'UI'
    bl_category = "kexedit"
    bl_parent_id = "KEXEDIT_PT_track_settings"
    bl_options = {'DEFAULT_CLOSED'}

    @classmethod
    def poll(cls, context):
        obj = context.active_object
        return obj is not None and obj.get("kex_is_track")

    def draw(self, context):
        layout = self.layout
        settings = context.active_object.kex_settings.anchor

        layout.use_property_split = True
        layout.use_property_decorate = False

        col = layout.column()
        col.prop(settings, "position")
        col.prop(settings, "pitch")
        col.prop(settings, "yaw")
        col.prop(settings, "roll")

        layout.separator()

        col = layout.column()
        col.prop(settings, "velocity")
        col.prop(settings, "heart_offset")
        col.prop(settings, "friction")
        col.prop(settings, "resistance")


class KEXEDIT_PT_force_settings(bpy.types.Panel):
    """Force settings subpanel."""

    bl_idname = "KEXEDIT_PT_force_settings"
    bl_label = "Force"
    bl_space_type = 'VIEW_3D'
    bl_region_type = 'UI'
    bl_category = "kexedit"
    bl_parent_id = "KEXEDIT_PT_track_settings"
    bl_options = {'DEFAULT_CLOSED'}

    @classmethod
    def poll(cls, context):
        obj = context.active_object
        return obj is not None and obj.get("kex_is_track")

    def draw(self, context):
        layout = self.layout
        settings = context.active_object.kex_settings.force

        layout.use_property_split = True
        layout.use_property_decorate = True  # Show keyframe decorators

        col = layout.column()
        col.prop(settings, "duration")

        layout.separator()
        layout.label(text="FVD Animation (100 frames = 1 sec)")

        col = layout.column()
        col.prop(settings, "roll_speed")
        col.prop(settings, "normal_force")
        col.prop(settings, "lateral_force")

        # Show hint about keyframe range
        box = layout.box()
        box.scale_y = 0.8
        duration = settings.duration
        max_frame = int(duration * 100)
        box.label(text=f"Keyframes: frame 0-{max_frame}", icon='INFO')

        # Refresh button for after editing F-Curves
        layout.separator()
        layout.operator("kexedit.refresh_track", icon='FILE_REFRESH')


class KEXEDIT_PT_build_settings(bpy.types.Panel):
    """Build settings subpanel."""

    bl_idname = "KEXEDIT_PT_build_settings"
    bl_label = "Build"
    bl_space_type = 'VIEW_3D'
    bl_region_type = 'UI'
    bl_category = "kexedit"
    bl_parent_id = "KEXEDIT_PT_track_settings"
    bl_options = {'DEFAULT_CLOSED'}

    @classmethod
    def poll(cls, context):
        obj = context.active_object
        return obj is not None and obj.get("kex_is_track")

    def draw(self, context):
        layout = self.layout
        settings = context.active_object.kex_settings.build

        layout.use_property_split = True
        layout.use_property_decorate = False

        col = layout.column()
        col.prop(settings, "resolution")


# Registration
classes = [
    KEXEDIT_PT_main,
    KEXEDIT_PT_track_settings,
    KEXEDIT_PT_anchor_settings,
    KEXEDIT_PT_force_settings,
    KEXEDIT_PT_build_settings,
]


def register():
    for cls in classes:
        bpy.utils.register_class(cls)


def unregister():
    for cls in reversed(classes):
        bpy.utils.unregister_class(cls)
