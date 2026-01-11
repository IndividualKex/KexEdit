"""Blender operators for kexengine."""

from __future__ import annotations

from pathlib import Path

import bpy

from ..core.ffi import KexEngine, KexError, build_from_kexd, is_library_available
from ..integration.curve import create_track_bezier, create_track_from_sections


def _get_test_data_dir() -> Path:
    """Get the kexengine test-data directory."""
    # Try development location first (KexEdit repo)
    dev_path = Path(r"C:\Users\dylan\Documents\Games\KexEdit\kexengine\test-data")
    if dev_path.exists():
        return dev_path

    # Fall back to relative path (for bundled distribution)
    addon_dir = Path(__file__).parent.parent.parent
    return addon_dir.parent / "kexengine" / "test-data"


class KEXENGINE_OT_generate_simple_track(bpy.types.Operator):
    """Generate a simple straight track with no animation."""

    bl_idname = "kexengine.generate_simple_track"
    bl_label = "Generate Simple Track"
    bl_description = "Generate a simple straight track section"
    bl_options = {'REGISTER', 'UNDO'}

    @classmethod
    def poll(cls, context):
        return is_library_available()

    def execute(self, context):
        try:
            engine = KexEngine()

            # Simple: anchor + force with no keyframes
            anchor_id = engine.add_anchor(
                position=(0.0, 3.0, 0.0),
                pitch=0.0,
                yaw=0.0,
                roll=0.0,
                velocity=10.0,
            )

            force_id = engine.add_force(anchor_id, duration=5.0)

            result = engine.build(resolution=0.5)

            # Use section-aware curve creation for consistency
            curve_obj = create_track_from_sections(
                result.spline_points,
                result.sections,
                name="KexSimpleTrack",
            )

            bpy.ops.object.select_all(action='DESELECT')
            curve_obj.select_set(True)
            context.view_layer.objects.active = curve_obj

            self.report(
                {'INFO'},
                f"Generated {len(result.spline_points)} points, {result.points[0].velocity:.1f} m/s"
            )
            return {'FINISHED'}

        except KexError as e:
            self.report({'ERROR'}, f"kexengine error: {e}")
            return {'CANCELLED'}


class KEXENGINE_OT_load_test_file(bpy.types.Operator):
    """Load and build a test kexd file."""

    bl_idname = "kexengine.load_test_file"
    bl_label = "Load Test File"
    bl_description = "Load a test kexd file and generate track"
    bl_options = {'REGISTER', 'UNDO'}

    file_name: bpy.props.EnumProperty(
        name="File",
        description="Test file to load",
        items=[
            ('shuttle_kexd', "Shuttle", "Shuttle coaster with rollback"),
            ('circuit_kexd', "Circuit", "Complete circuit track"),
            ('switch_kexd', "Switch", "Track with switch/branch"),
            ('all_types_kexd', "All Types", "Track demonstrating all node types"),
        ],
        default='shuttle_kexd',
    )

    @classmethod
    def poll(cls, context):
        return is_library_available()

    def invoke(self, context, event):
        return context.window_manager.invoke_props_dialog(self)

    def execute(self, context):
        test_dir = _get_test_data_dir()
        file_path = test_dir / f"{self.file_name}.kex"

        if not file_path.exists():
            self.report({'ERROR'}, f"Test file not found: {file_path}")
            return {'CANCELLED'}

        try:
            data = file_path.read_bytes()
            result = build_from_kexd(data, resolution=0.5)

            # Create curve with name based on file
            name = self.file_name.replace('_kexd', '').title()
            # Use section-aware curve creation to avoid false connections
            curve_obj = create_track_from_sections(
                result.spline_points,
                result.sections,
                name=f"Kex{name}Track",
            )

            bpy.ops.object.select_all(action='DESELECT')
            curve_obj.select_set(True)
            context.view_layer.objects.active = curve_obj

            self.report(
                {'INFO'},
                f"Loaded {name}: {len(result.spline_points)} points, {len(result.sections)} sections"
            )
            return {'FINISHED'}

        except KexError as e:
            self.report({'ERROR'}, f"kexengine error: {e}")
            return {'CANCELLED'}
        except Exception as e:
            self.report({'ERROR'}, f"Unexpected error: {e}")
            return {'CANCELLED'}


# Individual operators for each test file (for direct button access)
class KEXENGINE_OT_load_shuttle(bpy.types.Operator):
    """Load the shuttle test track."""

    bl_idname = "kexengine.load_shuttle"
    bl_label = "Load Shuttle"
    bl_description = "Load shuttle coaster with rollback"
    bl_options = {'REGISTER', 'UNDO'}

    @classmethod
    def poll(cls, context):
        return is_library_available()

    def execute(self, context):
        return _load_test_file(self, context, "shuttle_kexd", "Shuttle")


class KEXENGINE_OT_load_circuit(bpy.types.Operator):
    """Load the circuit test track."""

    bl_idname = "kexengine.load_circuit"
    bl_label = "Load Circuit"
    bl_description = "Load complete circuit track"
    bl_options = {'REGISTER', 'UNDO'}

    @classmethod
    def poll(cls, context):
        return is_library_available()

    def execute(self, context):
        return _load_test_file(self, context, "circuit_kexd", "Circuit")


class KEXENGINE_OT_load_switch(bpy.types.Operator):
    """Load the switch test track."""

    bl_idname = "kexengine.load_switch"
    bl_label = "Load Switch"
    bl_description = "Load track with switch/branch"
    bl_options = {'REGISTER', 'UNDO'}

    @classmethod
    def poll(cls, context):
        return is_library_available()

    def execute(self, context):
        return _load_test_file(self, context, "switch_kexd", "Switch")


class KEXENGINE_OT_load_all_types(bpy.types.Operator):
    """Load the all-types test track."""

    bl_idname = "kexengine.load_all_types"
    bl_label = "Load All Types"
    bl_description = "Load track demonstrating all node types"
    bl_options = {'REGISTER', 'UNDO'}

    @classmethod
    def poll(cls, context):
        return is_library_available()

    def execute(self, context):
        return _load_test_file(self, context, "all_types_kexd", "AllTypes")


def _load_test_file(op, context, file_name: str, display_name: str):
    """Helper to load a test file."""
    test_dir = _get_test_data_dir()
    file_path = test_dir / f"{file_name}.kex"

    if not file_path.exists():
        op.report({'ERROR'}, f"Test file not found: {file_path}")
        return {'CANCELLED'}

    try:
        data = file_path.read_bytes()
        result = build_from_kexd(data, resolution=0.5)

        # Use section-aware curve creation to avoid false connections
        curve_obj = create_track_from_sections(
            result.spline_points,
            result.sections,
            name=f"Kex{display_name}Track",
        )

        bpy.ops.object.select_all(action='DESELECT')
        curve_obj.select_set(True)
        context.view_layer.objects.active = curve_obj

        op.report(
            {'INFO'},
            f"Loaded {display_name}: {len(result.spline_points)} points, {len(result.sections)} sections"
        )
        return {'FINISHED'}

    except KexError as e:
        op.report({'ERROR'}, f"kexengine error: {e}")
        return {'CANCELLED'}
    except Exception as e:
        op.report({'ERROR'}, f"Unexpected error: {e}")
        return {'CANCELLED'}


# Registration
classes = [
    KEXENGINE_OT_generate_simple_track,
    KEXENGINE_OT_load_test_file,
    KEXENGINE_OT_load_shuttle,
    KEXENGINE_OT_load_circuit,
    KEXENGINE_OT_load_switch,
    KEXENGINE_OT_load_all_types,
]


def register():
    for cls in classes:
        bpy.utils.register_class(cls)


def unregister():
    for cls in reversed(classes):
        bpy.utils.unregister_class(cls)
