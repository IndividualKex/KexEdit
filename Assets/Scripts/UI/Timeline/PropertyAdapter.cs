using System;
using System.Collections.Generic;
using KexEdit.Legacy;
using static KexEdit.Legacy.Constants;

namespace KexEdit.UI.Timeline {
    public abstract class PropertyAdapter {
        public abstract PropertyType Type { get; }
        public abstract string DisplayName { get; }
        public virtual float ValueScale => 1f;

        public bool HasKeyframes(CoasterKeyframeManager manager, uint nodeId) =>
            manager != null && manager.HasKeyframes(nodeId, Type);

        public void GetKeyframes(CoasterKeyframeManager manager, uint nodeId, List<Keyframe> keyframes) {
            keyframes.Clear();
            if (manager == null) return;
            manager.GetKeyframes(nodeId, Type, keyframes);
            if (ValueScale != 1f) {
                for (int i = 0; i < keyframes.Count; i++) {
                    var kf = keyframes[i];
                    kf.Value *= ValueScale;
                    keyframes[i] = kf;
                }
            }
        }

        public void UpdateKeyframe(CoasterKeyframeManager manager, uint nodeId, Keyframe keyframe) {
            if (ValueScale != 1f) {
                keyframe.Value /= ValueScale;
            }
            manager.UpdateKeyframe(nodeId, Type, keyframe);
        }

        public void AddKeyframe(CoasterKeyframeManager manager, uint nodeId, Keyframe keyframe) {
            if (ValueScale != 1f) {
                keyframe.Value /= ValueScale;
            }
            manager.AddKeyframe(nodeId, Type, keyframe);
        }

        public void RemoveKeyframe(CoasterKeyframeManager manager, uint nodeId, uint id) =>
            manager.RemoveKeyframe(nodeId, Type, id);

        public float EvaluateAt(CoasterKeyframeManager manager, uint nodeId, float time) {
            if (manager == null) return 0f;
            float value = manager.EvaluateAt(nodeId, Type, time);
            return value * ValueScale;
        }

        private static readonly RollSpeedAdapter s_RollSpeedAdapter = new();
        private static readonly NormalForceAdapter s_NormalForceAdapter = new();
        private static readonly LateralForceAdapter s_LateralForceAdapter = new();
        private static readonly PitchSpeedAdapter s_PitchSpeedAdapter = new();
        private static readonly YawSpeedAdapter s_YawSpeedAdapter = new();
        private static readonly FixedVelocityAdapter s_FixedVelocityAdapter = new();
        private static readonly HeartAdapter s_HeartAdapter = new();
        private static readonly FrictionAdapter s_FrictionAdapter = new();
        private static readonly ResistanceAdapter s_ResistanceAdapter = new();
        private static readonly TrackStyleAdapter s_TrackStyleAdapter = new();

        public static PropertyAdapter GetAdapter(PropertyType type) => type switch {
            PropertyType.RollSpeed => s_RollSpeedAdapter,
            PropertyType.NormalForce => s_NormalForceAdapter,
            PropertyType.LateralForce => s_LateralForceAdapter,
            PropertyType.PitchSpeed => s_PitchSpeedAdapter,
            PropertyType.YawSpeed => s_YawSpeedAdapter,
            PropertyType.FixedVelocity => s_FixedVelocityAdapter,
            PropertyType.Heart => s_HeartAdapter,
            PropertyType.Friction => s_FrictionAdapter,
            PropertyType.Resistance => s_ResistanceAdapter,
            PropertyType.TrackStyle => s_TrackStyleAdapter,
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
    }

    public class RollSpeedAdapter : PropertyAdapter {
        public override PropertyType Type => PropertyType.RollSpeed;
        public override string DisplayName => "Roll Speed";
    }

    public class NormalForceAdapter : PropertyAdapter {
        public override PropertyType Type => PropertyType.NormalForce;
        public override string DisplayName => "Normal Force";
    }

    public class LateralForceAdapter : PropertyAdapter {
        public override PropertyType Type => PropertyType.LateralForce;
        public override string DisplayName => "Lateral Force";
    }

    public class PitchSpeedAdapter : PropertyAdapter {
        public override PropertyType Type => PropertyType.PitchSpeed;
        public override string DisplayName => "Pitch Speed";
    }

    public class YawSpeedAdapter : PropertyAdapter {
        public override PropertyType Type => PropertyType.YawSpeed;
        public override string DisplayName => "Yaw Speed";
    }

    public class FixedVelocityAdapter : PropertyAdapter {
        public override PropertyType Type => PropertyType.FixedVelocity;
        public override string DisplayName => "Fixed Velocity";
    }

    public class HeartAdapter : PropertyAdapter {
        public override PropertyType Type => PropertyType.Heart;
        public override string DisplayName => "Heart";
    }

    public class FrictionAdapter : PropertyAdapter {
        public override PropertyType Type => PropertyType.Friction;
        public override string DisplayName => "Friction";
        public override float ValueScale => FRICTION_PHYSICS_TO_UI_SCALE;
    }

    public class ResistanceAdapter : PropertyAdapter {
        public override PropertyType Type => PropertyType.Resistance;
        public override string DisplayName => "Resistance";
        public override float ValueScale => RESISTANCE_PHYSICS_TO_UI_SCALE;
    }

    public class TrackStyleAdapter : PropertyAdapter {
        public override PropertyType Type => PropertyType.TrackStyle;
        public override string DisplayName => "Track Style";
    }
}
