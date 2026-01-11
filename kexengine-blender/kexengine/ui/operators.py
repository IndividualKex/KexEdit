"""Blender operators for kexengine."""

from __future__ import annotations

import bpy

from ..core.ffi import KexEngine, KexError, is_library_available
from ..core.types import Keyframe, InterpolationType
from ..integration.curve import create_track_curve, create_track_bezier


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

            curve_obj = create_track_bezier(
                result.spline_points,
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


class KEXENGINE_OT_generate_test_track(bpy.types.Operator):
    """Generate a test roller coaster track."""

    bl_idname = "kexengine.generate_test_track"
    bl_label = "Generate Test Track"
    bl_description = "Generate a test track using kexengine"
    bl_options = {'REGISTER', 'UNDO'}

    use_bezier: bpy.props.BoolProperty(
        name="Use Bezier",
        description="Create a Bezier curve instead of poly curve",
        default=True,
    )

    @classmethod
    def poll(cls, context):
        return is_library_available()

    def execute(self, context):
        try:
            result = self._generate_track()

            if self.use_bezier:
                curve_obj = create_track_bezier(
                    result.spline_points,
                    name="KexTestTrack",
                )
            else:
                curve_obj = create_track_curve(
                    result.spline_points,
                    name="KexTestTrack",
                )

            # Select the new object
            bpy.ops.object.select_all(action='DESELECT')
            curve_obj.select_set(True)
            context.view_layer.objects.active = curve_obj

            self.report(
                {'INFO'},
                f"Generated track with {len(result.spline_points)} spline points"
            )
            return {'FINISHED'}

        except KexError as e:
            self.report({'ERROR'}, f"kexengine error: {e}")
            return {'CANCELLED'}
        except Exception as e:
            self.report({'ERROR'}, f"Unexpected error: {e}")
            return {'CANCELLED'}

    def _generate_track(self):
        """Build a test track with an anchor and force section."""
        engine = KexEngine()

        # Create anchor at origin, elevated, pointing forward
        anchor_id = engine.add_anchor(
            position=(0.0, 3.0, 0.0),
            pitch=0.0,
            yaw=0.0,
            roll=0.0,
            velocity=15.0,
        )

        # Create force node with some animated roll
        force_id = engine.add_force(
            anchor_id,
            duration=8.0,
        )

        # Add roll animation keyframes
        engine.set_keyframes(
            force_id,
            property_id=0,  # RollSpeed
            keyframes=[
                Keyframe.simple(0.0, 0.0),
                Keyframe.simple(2.0, 45.0),  # Roll right
                Keyframe.simple(4.0, 0.0),
                Keyframe.simple(6.0, -45.0),  # Roll left
                Keyframe.simple(8.0, 0.0),
            ],
        )

        # Add some vertical force variation
        engine.set_keyframes(
            force_id,
            property_id=1,  # NormalForce
            keyframes=[
                Keyframe.simple(0.0, 1.0),
                Keyframe.simple(2.0, 0.5),  # Lighter
                Keyframe.simple(4.0, 2.0),  # Heavier
                Keyframe.simple(6.0, 0.0),  # Weightless
                Keyframe.simple(8.0, 1.0),
            ],
        )

        return engine.build(resolution=0.5)


class KEXENGINE_OT_generate_helix(bpy.types.Operator):
    """Generate a helix test track."""

    bl_idname = "kexengine.generate_helix"
    bl_label = "Generate Helix"
    bl_description = "Generate a helix track section"
    bl_options = {'REGISTER', 'UNDO'}

    turns: bpy.props.FloatProperty(
        name="Turns",
        description="Number of helix turns",
        default=2.0,
        min=0.5,
        max=10.0,
    )

    duration: bpy.props.FloatProperty(
        name="Duration",
        description="Time duration in seconds",
        default=6.0,
        min=1.0,
        max=30.0,
    )

    @classmethod
    def poll(cls, context):
        return is_library_available()

    def execute(self, context):
        try:
            result = self._generate_helix()

            curve_obj = create_track_bezier(
                result.spline_points,
                name="KexHelix",
            )

            bpy.ops.object.select_all(action='DESELECT')
            curve_obj.select_set(True)
            context.view_layer.objects.active = curve_obj

            self.report(
                {'INFO'},
                f"Generated helix with {len(result.spline_points)} points"
            )
            return {'FINISHED'}

        except KexError as e:
            self.report({'ERROR'}, f"kexengine error: {e}")
            return {'CANCELLED'}

    def _generate_helix(self):
        """Build a helix using constant lateral force."""
        engine = KexEngine()

        anchor_id = engine.add_anchor(
            position=(0.0, 10.0, 0.0),
            pitch=-10.0,  # Slight downward pitch
            yaw=0.0,
            roll=0.0,
            velocity=20.0,
        )

        force_id = engine.add_force(
            anchor_id,
            duration=self.duration,
        )

        # Constant lateral force creates helix
        yaw_rate = (360.0 * self.turns) / self.duration
        engine.set_keyframes(
            force_id,
            property_id=4,  # YawSpeed
            keyframes=[
                Keyframe.linear(0.0, yaw_rate),
                Keyframe.linear(self.duration, yaw_rate),
            ],
        )

        # Maintain 1G normal force
        engine.set_keyframes(
            force_id,
            property_id=1,  # NormalForce
            keyframes=[
                Keyframe.linear(0.0, 1.0),
                Keyframe.linear(self.duration, 1.0),
            ],
        )

        return engine.build(resolution=0.5)


# Registration
classes = [
    KEXENGINE_OT_generate_simple_track,
    KEXENGINE_OT_generate_test_track,
    KEXENGINE_OT_generate_helix,
]


def register():
    for cls in classes:
        bpy.utils.register_class(cls)


def unregister():
    for cls in reversed(classes):
        bpy.utils.unregister_class(cls)
