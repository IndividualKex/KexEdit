using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace KexEdit {
    [Serializable]
    public struct Keyframe {
        private static InterpolationType _defaultInInterpolation = InterpolationType.Bezier;
        private static InterpolationType _defaultOutInterpolation = InterpolationType.Bezier;
        private static HandleType _defaultHandleType = HandleType.Aligned;
        private static bool _defaultsLoaded = false;

        private const string PREF_DEFAULT_IN_INTERPOLATION = "Keyframe_DefaultInInterpolation";
        private const string PREF_DEFAULT_OUT_INTERPOLATION = "Keyframe_DefaultOutInterpolation";

        public uint Id;
        public float Time;
        public float Value;
        public InterpolationType InInterpolation;
        public InterpolationType OutInterpolation;
        public HandleType HandleType;
        public KeyframeFlags Flags;

        public float InTangent;
        public float OutTangent;
        public float InWeight;
        public float OutWeight;
        [MarshalAs(UnmanagedType.U1)]
        public bool Selected;

        public bool IsTimeLocked => (Flags & KeyframeFlags.LockTime) != 0;
        public bool IsValueLocked => (Flags & KeyframeFlags.LockValue) != 0;

        static Keyframe() {
            LoadDefaults();
        }

        private static void LoadDefaults() {
            if (_defaultsLoaded) return;
            
            int inValue = PlayerPrefs.GetInt(PREF_DEFAULT_IN_INTERPOLATION, (int)InterpolationType.Bezier);
            int outValue = PlayerPrefs.GetInt(PREF_DEFAULT_OUT_INTERPOLATION, (int)InterpolationType.Bezier);
            
            _defaultInInterpolation = Enum.IsDefined(typeof(InterpolationType), inValue) 
                ? (InterpolationType)inValue 
                : InterpolationType.Bezier;
            _defaultOutInterpolation = Enum.IsDefined(typeof(InterpolationType), outValue) 
                ? (InterpolationType)outValue 
                : InterpolationType.Bezier;
                
            _defaultsLoaded = true;
        }

        public static void SetDefaultInterpolation(InterpolationType inType, InterpolationType outType) {
            _defaultInInterpolation = inType;
            _defaultOutInterpolation = outType;
            PlayerPrefs.SetInt(PREF_DEFAULT_IN_INTERPOLATION, (int)inType);
            PlayerPrefs.SetInt(PREF_DEFAULT_OUT_INTERPOLATION, (int)outType);
            PlayerPrefs.Save();
        }

        public static Keyframe Default => new() {
            Id = Uuid.Create(),
            Time = 0f,
            Value = 0f,
            InInterpolation = _defaultInInterpolation,
            OutInterpolation = _defaultOutInterpolation,
            HandleType = _defaultHandleType,
            Flags = KeyframeFlags.None,
            InTangent = 0f,
            OutTangent = 0f,
            InWeight = 0.36f,
            OutWeight = 0.36f,
            Selected = false,
        };

        public static Keyframe Create(float time, float value) => new() {
            Id = Uuid.Create(),
            Time = time,
            Value = value,
            InInterpolation = _defaultInInterpolation,
            OutInterpolation = _defaultOutInterpolation,
            HandleType = _defaultHandleType,
            Flags = KeyframeFlags.None,
            InTangent = 0f,
            OutTangent = 0f,
            InWeight = 0.36f,
            OutWeight = 0.36f,
            Selected = false,
        };

        public Keyframe WithValue(float value) => new() {
            Id = Id,
            Time = Time,
            Value = value,
            InInterpolation = InInterpolation,
            OutInterpolation = OutInterpolation,
            HandleType = HandleType,
            Flags = Flags,
            InTangent = InTangent,
            OutTangent = OutTangent,
            InWeight = InWeight,
            OutWeight = OutWeight,
            Selected = Selected,
        };

        public Keyframe WithId(uint id) => new() {
            Id = id,
            Time = Time,
            Value = Value,
            InInterpolation = InInterpolation,
            OutInterpolation = OutInterpolation,
            HandleType = HandleType,
            Flags = Flags,
            InTangent = InTangent,
            OutTangent = OutTangent,
            InWeight = InWeight,
            OutWeight = OutWeight,
            Selected = Selected,
        };

        public Keyframe WithSelected(bool selected) => new() {
            Id = Id,
            Time = Time,
            Value = Value,
            InInterpolation = InInterpolation,
            OutInterpolation = OutInterpolation,
            HandleType = HandleType,
            Flags = Flags,
            InTangent = InTangent,
            OutTangent = OutTangent,
            InWeight = InWeight,
            OutWeight = OutWeight,
            Selected = selected,
        };

        public Keyframe WithEasing(float tangent, float weight) => new() {
            Id = Id,
            Time = Time,
            Value = Value,
            InInterpolation = InterpolationType.Bezier,
            OutInterpolation = InterpolationType.Bezier,
            HandleType = HandleType.Aligned,
            Flags = Flags,
            InTangent = tangent,
            OutTangent = tangent,
            InWeight = weight,
            OutWeight = weight,
            Selected = Selected,
        };

        public Keyframe WithOutEasing(float tangent, float weight) => new() {
            Id = Id,
            Time = Time,
            Value = Value,
            InInterpolation = InInterpolation,
            OutInterpolation = InterpolationType.Bezier,
            HandleType = HandleType,
            Flags = Flags,
            InTangent = InTangent,
            OutTangent = tangent,
            InWeight = InWeight,
            OutWeight = weight,
            Selected = Selected,
        };

        public Keyframe WithInEasing(float tangent, float weight) => new() {
            Id = Id,
            Time = Time,
            Value = Value,
            InInterpolation = InterpolationType.Bezier,
            OutInterpolation = OutInterpolation,
            HandleType = HandleType,
            Flags = Flags,
            InTangent = tangent,
            OutTangent = OutTangent,
            InWeight = weight,
            OutWeight = OutWeight,
            Selected = Selected,
        };

        public bool HasAlignedHandles() {
            return InInterpolation == InterpolationType.Bezier &&
                   OutInterpolation == InterpolationType.Bezier &&
                   HandleType == HandleType.Aligned;
        }

        public Keyframe WithFlags(KeyframeFlags flags) => new() {
            Id = Id,
            Time = Time,
            Value = Value,
            InInterpolation = InInterpolation,
            OutInterpolation = OutInterpolation,
            HandleType = HandleType,
            Flags = flags,
            InTangent = InTangent,
            OutTangent = OutTangent,
            InWeight = InWeight,
            OutWeight = OutWeight,
            Selected = Selected,
        };

        public Keyframe WithTimeLock(bool locked) {
            var newFlags = locked ? (Flags | KeyframeFlags.LockTime) : (Flags & ~KeyframeFlags.LockTime);
            return WithFlags(newFlags);
        }

        public Keyframe WithValueLock(bool locked) {
            var newFlags = locked ? (Flags | KeyframeFlags.LockValue) : (Flags & ~KeyframeFlags.LockValue);
            return WithFlags(newFlags);
        }
    }
}
