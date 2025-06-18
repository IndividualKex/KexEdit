using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace KexEdit {
    [Serializable]
    public struct Keyframe {
        private static InterpolationType _defaultInInterpolation = InterpolationType.ContinuousBezier;
        private static InterpolationType _defaultOutInterpolation = InterpolationType.ContinuousBezier;
        private static bool _defaultsLoaded = false;

        private const string PREF_DEFAULT_IN_INTERPOLATION = "Keyframe_DefaultInInterpolation";
        private const string PREF_DEFAULT_OUT_INTERPOLATION = "Keyframe_DefaultOutInterpolation";

        public uint Id;
        public float Time;
        public float Value;
        public InterpolationType InInterpolation;
        public InterpolationType OutInterpolation;

        public float InTangent;
        public float OutTangent;
        public float InWeight;
        public float OutWeight;
        [MarshalAs(UnmanagedType.U1)]
        public bool Selected;

        static Keyframe() {
            LoadDefaults();
        }

        private static void LoadDefaults() {
            if (_defaultsLoaded) return;
            _defaultInInterpolation = (InterpolationType)PlayerPrefs.GetInt(PREF_DEFAULT_IN_INTERPOLATION, (int)InterpolationType.ContinuousBezier);
            _defaultOutInterpolation = (InterpolationType)PlayerPrefs.GetInt(PREF_DEFAULT_OUT_INTERPOLATION, (int)InterpolationType.ContinuousBezier);
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
            InTangent = 0f,
            OutTangent = 0f,
            InWeight = 1 / 3f,
            OutWeight = 1 / 3f,
            Selected = false,
        };

        public static Keyframe Create(float time, float value) => new() {
            Id = Uuid.Create(),
            Time = time,
            Value = value,
            InInterpolation = _defaultInInterpolation,
            OutInterpolation = _defaultOutInterpolation,
            InTangent = 0f,
            OutTangent = 0f,
            InWeight = 1f / 3f,
            OutWeight = 1f / 3f,
            Selected = false,
        };

        public Keyframe WithValue(float value) => new() {
            Id = Id,
            Time = Time,
            Value = value,
            InInterpolation = InInterpolation,
            OutInterpolation = OutInterpolation,
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
            InTangent = InTangent,
            OutTangent = OutTangent,
            InWeight = InWeight,
            OutWeight = OutWeight,
            Selected = selected,
        };
    }
}
