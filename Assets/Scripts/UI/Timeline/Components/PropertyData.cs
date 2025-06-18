using System;
using System.Collections.Generic;
using Unity.Collections;

namespace KexEdit.UI.Timeline {
    public class PropertyData : IDisposable {
        public List<Keyframe> Keyframes = new();
        public NativeList<float> Values = new(Allocator.Persistent);
        public PropertyType Type;
        public float Value = 0f;
        public UnitsType Units = UnitsType.None;
        public int SelectedKeyframeCount = 0;
        public bool Visible = false;
        public bool Selected = false;
        public bool IsAlt = false;
        public bool HasActiveKeyframe = false;
        public bool DrawReadOnly = false;

        public bool IsReadable => Type switch {
            PropertyType.NormalForce => true,
            PropertyType.LateralForce => true,
            PropertyType.RollSpeed => true,
            PropertyType.PitchSpeed => true,
            PropertyType.YawSpeed => true,
            _ => false
        };

        public bool IsRemovable => Type switch {
            PropertyType.FixedVelocity => true,
            PropertyType.Heart => true,
            PropertyType.Friction => true,
            PropertyType.Resistance => true,
            _ => false
        };

        public void Dispose() {
            Values.Dispose();
        }
    }
}
