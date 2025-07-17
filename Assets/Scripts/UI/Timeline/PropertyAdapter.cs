using System;
using System.Collections.Generic;
using Unity.Entities;
using static KexEdit.UI.Constants;

namespace KexEdit.UI.Timeline {
    public abstract class PropertyAdapter {
        public abstract PropertyType Type { get; }
        public abstract string DisplayName { get; }

        protected EntityManager EntityManager => World.DefaultGameObjectInjectionWorld.EntityManager;

        public abstract bool HasBuffer(Entity entity);
        public abstract float EvaluateAt(Entity entity, float time, Anchor anchor);

        public abstract void GetKeyframes(Entity entity, List<Keyframe> keyframes);
        public abstract void UpdateKeyframe(Entity entity, Keyframe keyframe);
        public abstract void AddKeyframe(Entity entity, Keyframe keyframe);
        public abstract void RemoveKeyframe(Entity entity, uint id);

        private static readonly PropertyAdapter[] s_Adapters = new PropertyAdapter[] {
            new RollSpeedAdapter(),
            new NormalForceAdapter(),
            new LateralForceAdapter(),
            new PitchSpeedAdapter(),
            new YawSpeedAdapter(),
            new FixedVelocityAdapter(),
            new HeartAdapter(),
            new FrictionAdapter(),
            new ResistanceAdapter(),
            new TrackStyleAdapter(),
        };

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

    public abstract class PropertyAdapter<T> : PropertyAdapter where T : unmanaged, IBufferElementData {
        public override bool HasBuffer(Entity entity) =>
            EntityManager.HasBuffer<T>(entity);

        public override void GetKeyframes(Entity entity, List<Keyframe> keyframes) {
            keyframes.Clear();
            if (!HasBuffer(entity)) return;
            var buffer = EntityManager.GetBuffer<T>(entity);
            for (int i = 0; i < buffer.Length; i++) {
                var element = buffer[i];
                keyframes.Add(GetData(element));
            }
        }

        public override void UpdateKeyframe(Entity entity, Keyframe keyframe) {
            var buffer = EntityManager.GetBuffer<T>(entity);
            for (int i = 0; i < buffer.Length; i++) {
                var element = GetData(buffer[i]);
                if (element.Id == keyframe.Id) {
                    buffer[i] = CreateElement(keyframe);
                    if (element.Time != keyframe.Time) {
                        SortBuffer(entity);
                    }
                    return;
                }
            }
            throw new Exception("Keyframe not found");
        }

        public override void AddKeyframe(Entity entity, Keyframe keyframe) {
            var buffer = EntityManager.GetBuffer<T>(entity);

            if (keyframe.Id == 0) {
                keyframe = keyframe.WithId(Uuid.Create());
            }

            int insertIndex = 0;
            while (insertIndex < buffer.Length && GetData(buffer[insertIndex]).Time < keyframe.Time) {
                insertIndex++;
            }

            buffer.Insert(insertIndex, CreateElement(keyframe));
        }

        public override void RemoveKeyframe(Entity entity, uint id) {
            var buffer = EntityManager.GetBuffer<T>(entity);
            for (int i = buffer.Length - 1; i >= 0; i--) {
                var element = buffer[i];
                if (GetData(element).Id == id) {
                    buffer.RemoveAt(i);
                    return;
                }
            }
            throw new Exception("Keyframe not found");
        }

        private void SortBuffer(Entity entity) {
            var buffer = EntityManager.GetBuffer<T>(entity);
            if (buffer.Length <= 1) return;

            var tempList = new List<T>();
            for (int i = 0; i < buffer.Length; i++) {
                tempList.Add(buffer[i]);
            }

            tempList.Sort((a, b) => GetData(a).Time.CompareTo(GetData(b).Time));

            buffer.Clear();
            foreach (var element in tempList) {
                buffer.Add(element);
            }
        }

        protected abstract Keyframe GetData(T element);
        protected abstract T CreateElement(Keyframe keyframe);
    }

    public class RollSpeedAdapter : PropertyAdapter<RollSpeedKeyframe> {
        public override PropertyType Type => PropertyType.RollSpeed;
        public override string DisplayName => "Roll Speed";

        public override float EvaluateAt(Entity entity, float time, Anchor anchor) =>
            EntityManager.GetBuffer<RollSpeedKeyframe>(entity).Evaluate(time);

        protected override Keyframe GetData(RollSpeedKeyframe element) => element.Value;
        protected override RollSpeedKeyframe CreateElement(Keyframe keyframe) => keyframe;
    }

    public class NormalForceAdapter : PropertyAdapter<NormalForceKeyframe> {
        public override PropertyType Type => PropertyType.NormalForce;
        public override string DisplayName => "Normal Force";

        public override float EvaluateAt(Entity entity, float time, Anchor anchor) =>
            EntityManager.GetBuffer<NormalForceKeyframe>(entity).Evaluate(time);

        protected override Keyframe GetData(NormalForceKeyframe element) => element.Value;
        protected override NormalForceKeyframe CreateElement(Keyframe keyframe) => keyframe;
    }

    public class LateralForceAdapter : PropertyAdapter<LateralForceKeyframe> {
        public override PropertyType Type => PropertyType.LateralForce;
        public override string DisplayName => "Lateral Force";

        public override float EvaluateAt(Entity entity, float time, Anchor anchor) =>
            EntityManager.GetBuffer<LateralForceKeyframe>(entity).Evaluate(time);

        protected override Keyframe GetData(LateralForceKeyframe element) => element.Value;
        protected override LateralForceKeyframe CreateElement(Keyframe keyframe) => keyframe;
    }

    public class PitchSpeedAdapter : PropertyAdapter<PitchSpeedKeyframe> {
        public override PropertyType Type => PropertyType.PitchSpeed;
        public override string DisplayName => "Pitch Speed";

        public override float EvaluateAt(Entity entity, float time, Anchor anchor) =>
            EntityManager.GetBuffer<PitchSpeedKeyframe>(entity).Evaluate(time);

        protected override Keyframe GetData(PitchSpeedKeyframe element) => element.Value;
        protected override PitchSpeedKeyframe CreateElement(Keyframe keyframe) => keyframe;
    }

    public class YawSpeedAdapter : PropertyAdapter<YawSpeedKeyframe> {
        public override PropertyType Type => PropertyType.YawSpeed;
        public override string DisplayName => "Yaw Speed";

        public override float EvaluateAt(Entity entity, float time, Anchor anchor) =>
            EntityManager.GetBuffer<YawSpeedKeyframe>(entity).Evaluate(time);

        protected override Keyframe GetData(YawSpeedKeyframe element) => element.Value;
        protected override YawSpeedKeyframe CreateElement(Keyframe keyframe) => keyframe;
    }

    public class FixedVelocityAdapter : PropertyAdapter<FixedVelocityKeyframe> {
        public override PropertyType Type => PropertyType.FixedVelocity;
        public override string DisplayName => "Fixed Velocity";

        public override float EvaluateAt(Entity entity, float time, Anchor anchor) =>
            EntityManager.GetBuffer<FixedVelocityKeyframe>(entity).Evaluate(time, anchor);

        protected override Keyframe GetData(FixedVelocityKeyframe element) => element.Value;
        protected override FixedVelocityKeyframe CreateElement(Keyframe keyframe) => keyframe;
    }

    public class HeartAdapter : PropertyAdapter<HeartKeyframe> {
        public override PropertyType Type => PropertyType.Heart;
        public override string DisplayName => "Heart";

        public override float EvaluateAt(Entity entity, float time, Anchor anchor) =>
            EntityManager.GetBuffer<HeartKeyframe>(entity).Evaluate(time, anchor);

        protected override Keyframe GetData(HeartKeyframe element) => element.Value;
        protected override HeartKeyframe CreateElement(Keyframe keyframe) => keyframe;
    }

    public class FrictionAdapter : PropertyAdapter<FrictionKeyframe> {
        public override PropertyType Type => PropertyType.Friction;
        public override string DisplayName => "Friction";

        public override float EvaluateAt(Entity entity, float time, Anchor anchor) {
            float physicsValue = EntityManager.GetBuffer<FrictionKeyframe>(entity).Evaluate(time, anchor);
            return physicsValue * FRICTION_PHYSICS_TO_UI_SCALE;
        }

        protected override Keyframe GetData(FrictionKeyframe element) {
            var keyframe = element.Value;
            keyframe.Value *= FRICTION_PHYSICS_TO_UI_SCALE;
            return keyframe;
        }

        protected override FrictionKeyframe CreateElement(Keyframe keyframe) {
            keyframe.Value *= FRICTION_UI_TO_PHYSICS_SCALE;
            return keyframe;
        }
    }

    public class ResistanceAdapter : PropertyAdapter<ResistanceKeyframe> {
        public override PropertyType Type => PropertyType.Resistance;
        public override string DisplayName => "Resistance";

        public override float EvaluateAt(Entity entity, float time, Anchor anchor) {
            float physicsValue = EntityManager.GetBuffer<ResistanceKeyframe>(entity).Evaluate(time, anchor);
            return physicsValue * RESISTANCE_PHYSICS_TO_UI_SCALE;
        }

        protected override Keyframe GetData(ResistanceKeyframe element) {
            var keyframe = element.Value;
            keyframe.Value *= RESISTANCE_PHYSICS_TO_UI_SCALE;
            return keyframe;
        }

        protected override ResistanceKeyframe CreateElement(Keyframe keyframe) {
            keyframe.Value *= RESISTANCE_UI_TO_PHYSICS_SCALE;
            return keyframe;
        }
    }

    public class TrackStyleAdapter : PropertyAdapter<TrackStyleKeyframe> {
        public override PropertyType Type => PropertyType.TrackStyle;
        public override string DisplayName => "Track Style";

        public override float EvaluateAt(Entity entity, float time, Anchor anchor) =>
            EntityManager.GetBuffer<TrackStyleKeyframe>(entity).Evaluate(time);

        protected override Keyframe GetData(TrackStyleKeyframe element) => element.Value;
        protected override TrackStyleKeyframe CreateElement(Keyframe keyframe) => keyframe;
    }
}
