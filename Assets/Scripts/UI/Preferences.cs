using Unity.Mathematics;
using UnityEngine;
using static KexEdit.UI.Constants;

namespace KexEdit.UI {
    public static class Preferences {
        private const string PREF_DISTANCE_UNITS = "DistanceUnits";
        private const string PREF_ANGLE_UNITS = "AngleUnits";
        private const string PREF_ANGLE_CHANGE_UNITS = "AngleChangeUnits";
        private const string PREF_SPEED_UNITS = "SpeedUnits";

        private const string PREF_NODE_GRID_SNAPPING = "NodeGridSnapping";
        private const string PREF_RIDE_CAMERA_POSITION_X = "RideCameraPositionX";
        private const string PREF_RIDE_CAMERA_POSITION_Y = "RideCameraPositionY";
        private const string PREF_RIDE_CAMERA_POSITION_Z = "RideCameraPositionZ";
        private const string PREF_RIDE_CAMERA_ROTATION_X = "RideCameraRotationX";
        private const string PREF_RIDE_CAMERA_ROTATION_Y = "RideCameraRotationY";
        private const string PREF_RIDE_CAMERA_ROTATION_Z = "RideCameraRotationZ";
        private const string PREF_UI_SCALE = "UIScale";
        private const string PREF_SHOW_STATS = "ShowStats";
        private const string PREF_SYNC_PLAYBACK = "SyncPlayback";
        private const string PREF_KEYFRAME_EDITOR = "KeyframeEditor";
        private const string PREF_SHOW_GIZMOS = "ShowGizmos";
        private const string PREF_SKY_TYPE = "SkyType";
        private const string PREF_CURRENT_TRAIN_STYLE = "CurrentTrainStyle";
        private const string PREF_CURRENT_TRACK_STYLE = "CurrentTrackStyle";
        private const string PREF_AUTO_STYLE = "AutoStyle";
        private const string PREF_VIS_VELOCITY_MIN = "VisVelocityMin";
        private const string PREF_VIS_VELOCITY_MAX = "VisVelocityMax";
        private const string PREF_VIS_NORMAL_FORCE_MIN = "VisNormalForceMin";
        private const string PREF_VIS_NORMAL_FORCE_MAX = "VisNormalForceMax";
        private const string PREF_VIS_LATERAL_FORCE_MIN = "VisLateralForceMin";
        private const string PREF_VIS_LATERAL_FORCE_MAX = "VisLateralForceMax";
        private const string PREF_VIS_ROLL_SPEED_MIN = "VisRollSpeedMin";
        private const string PREF_VIS_ROLL_SPEED_MAX = "VisRollSpeedMax";
        private const string PREF_VIS_PITCH_SPEED_MIN = "VisPitchSpeedMin";
        private const string PREF_VIS_PITCH_SPEED_MAX = "VisPitchSpeedMax";
        private const string PREF_VIS_YAW_SPEED_MIN = "VisYawSpeedMin";
        private const string PREF_VIS_YAW_SPEED_MAX = "VisYawSpeedMax";
        private const string PREF_VIS_CURVATURE_MIN = "VisCurvatureMin";
        private const string PREF_VIS_CURVATURE_MAX = "VisCurvatureMax";
        private const string PREF_VIS_MODE = "VisMode";

        private const string PREF_SCROLL_INVERT = "ScrollInvert";
        private const string PREF_SCROLL_SENSITIVITY = "ScrollSensitivity";
        private const string PREF_POINTER_SENSITIVITY = "PointerSensitivity";
        private const string PREF_ENABLE_TOPROW_VIEW_HOTKEYS = "EnableTopRowViewHotkeys";

        private static DistanceUnitsType s_DistanceUnits;
        private static AngleUnitsType s_AngleUnits;
        private static AngleChangeUnitsType s_AngleChangeUnits;
        private static SpeedUnitsType s_SpeedUnits;

        private static string s_CurrentTrainStyle;
        private static string s_CurrentTrackStyle;
        private static float s_RideCameraPositionX;
        private static float s_RideCameraPositionY;
        private static float s_RideCameraPositionZ;
        private static float s_RideCameraRotationX;
        private static float s_RideCameraRotationY;
        private static float s_RideCameraRotationZ;
        private static float s_UIScale;
        private static SkyType s_SkyType;
        private static bool s_NodeGridSnapping;
        private static bool s_ShowStats;
        private static bool s_SyncPlayback;
        private static bool s_KeyframeEditor;
        private static bool s_ShowGizmos;
        private static bool s_AutoStyle;
        private static VisualizationMode s_VisualizationMode;

        private static bool s_ScrollInvert;
        private static float s_ScrollSensitivity;
        private static float s_PointerSensitivity;
        private static bool s_EnableTopRowViewHotkeys;

        static Preferences() {
            LoadPreferences();
        }

        public static DistanceUnitsType DistanceUnits {
            get => s_DistanceUnits;
            set {
                s_DistanceUnits = value;
                PlayerPrefs.SetInt(PREF_DISTANCE_UNITS, (int)value);
                PlayerPrefs.Save();
            }
        }

        public static AngleUnitsType AngleUnits {
            get => s_AngleUnits;
            set {
                s_AngleUnits = value;
                PlayerPrefs.SetInt(PREF_ANGLE_UNITS, (int)value);
                PlayerPrefs.Save();
            }
        }

        public static AngleChangeUnitsType AngleChangeUnits {
            get => s_AngleChangeUnits;
            set {
                s_AngleChangeUnits = value;
                PlayerPrefs.SetInt(PREF_ANGLE_CHANGE_UNITS, (int)value);
                PlayerPrefs.Save();
            }
        }

        public static SpeedUnitsType SpeedUnits {
            get => s_SpeedUnits;
            set {
                s_SpeedUnits = value;
                PlayerPrefs.SetInt(PREF_SPEED_UNITS, (int)value);
                PlayerPrefs.Save();
            }
        }

        public static string CurrentTrainStyle {
            get => s_CurrentTrainStyle;
            set {
                s_CurrentTrainStyle = value;
                PlayerPrefs.SetString(PREF_CURRENT_TRAIN_STYLE, value);
                PlayerPrefs.Save();
            }
        }

        public static string CurrentTrackStyle {
            get => s_CurrentTrackStyle;
            set {
                s_CurrentTrackStyle = value;
                PlayerPrefs.SetString(PREF_CURRENT_TRACK_STYLE, value);
                PlayerPrefs.Save();
            }
        }

        public static bool AutoStyle {
            get => s_AutoStyle;
            set {
                s_AutoStyle = value;
                PlayerPrefs.SetInt(PREF_AUTO_STYLE, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public static float UIScale {
            get => s_UIScale;
            set {
                s_UIScale = value;
                PlayerPrefs.SetFloat(PREF_UI_SCALE, value);
                PlayerPrefs.Save();
            }
        }

        public static float RideCameraPositionX {
            get => s_RideCameraPositionX;
            set {
                s_RideCameraPositionX = value;
                PlayerPrefs.SetFloat(PREF_RIDE_CAMERA_POSITION_X, value);
                PlayerPrefs.Save();
            }
        }

        public static float RideCameraPositionY {
            get => s_RideCameraPositionY;
            set {
                s_RideCameraPositionY = value;
                PlayerPrefs.SetFloat(PREF_RIDE_CAMERA_POSITION_Y, value);
                PlayerPrefs.Save();
            }
        }

        public static float RideCameraPositionZ {
            get => s_RideCameraPositionZ;
            set {
                s_RideCameraPositionZ = value;
                PlayerPrefs.SetFloat(PREF_RIDE_CAMERA_POSITION_Z, value);
                PlayerPrefs.Save();
            }
        }

        public static float RideCameraRotationX {
            get => s_RideCameraRotationX;
            set {
                s_RideCameraRotationX = value;
                PlayerPrefs.SetFloat(PREF_RIDE_CAMERA_ROTATION_X, value);
                PlayerPrefs.Save();
            }
        }

        public static float RideCameraRotationY {
            get => s_RideCameraRotationY;
            set {
                s_RideCameraRotationY = value;
                PlayerPrefs.SetFloat(PREF_RIDE_CAMERA_ROTATION_Y, value);
                PlayerPrefs.Save();
            }
        }

        public static float RideCameraRotationZ {
            get => s_RideCameraRotationZ;
            set {
                s_RideCameraRotationZ = value;
                PlayerPrefs.SetFloat(PREF_RIDE_CAMERA_ROTATION_Z, value);
                PlayerPrefs.Save();
            }
        }

        public static SkyType SkyType {
            get => s_SkyType;
            set {
                s_SkyType = value;
                PlayerPrefs.SetInt(PREF_SKY_TYPE, (int)value);
                PlayerPrefs.Save();
            }
        }

        public static bool NodeGridSnapping {
            get => s_NodeGridSnapping;
            set {
                s_NodeGridSnapping = value;
                PlayerPrefs.SetInt(PREF_NODE_GRID_SNAPPING, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public static bool ShowStats {
            get => s_ShowStats;
            set {
                s_ShowStats = value;
                PlayerPrefs.SetInt(PREF_SHOW_STATS, value ? 1 : 1);
                PlayerPrefs.Save();
            }
        }

        public static bool SyncPlayback {
            get => s_SyncPlayback;
            set {
                s_SyncPlayback = value;
                PlayerPrefs.SetInt(PREF_SYNC_PLAYBACK, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public static bool KeyframeEditor {
            get => s_KeyframeEditor;
            set {
                s_KeyframeEditor = value;
                PlayerPrefs.SetInt(PREF_KEYFRAME_EDITOR, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public static bool ShowGizmos {
            get => s_ShowGizmos;
            set {
                s_ShowGizmos = value;
                PlayerPrefs.SetInt(PREF_SHOW_GIZMOS, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public static VisualizationMode VisualizationMode {
            get => s_VisualizationMode;
            set {
                s_VisualizationMode = value;
                PlayerPrefs.SetInt(PREF_VIS_MODE, (int)value);
                PlayerPrefs.Save();
            }
        }

        public static bool InvertScroll {
            get => s_ScrollInvert;
            set {
                s_ScrollInvert = value;
                PlayerPrefs.SetInt(PREF_SCROLL_INVERT, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public static float ScrollSensitivity {
            get => s_ScrollSensitivity;
            set {
                s_ScrollSensitivity = Mathf.Clamp(value, 0.1f, 3f);
                PlayerPrefs.SetFloat(PREF_SCROLL_SENSITIVITY, s_ScrollSensitivity);
                PlayerPrefs.Save();
            }
        }

        public static float PointerSensitivity {
            get => s_PointerSensitivity;
            set {
                s_PointerSensitivity = Mathf.Clamp(value, 0.1f, 3f);
                PlayerPrefs.SetFloat(PREF_POINTER_SENSITIVITY, s_PointerSensitivity);
                PlayerPrefs.Save();
            }
        }

        public static bool EnableTopRowViewHotkeys {
            get => s_EnableTopRowViewHotkeys;
            set {
                s_EnableTopRowViewHotkeys = value;
                PlayerPrefs.SetInt(PREF_ENABLE_TOPROW_VIEW_HOTKEYS, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public static float AdjustScroll(float deltaY) {
            float sign = s_ScrollInvert ? -1f : 1f;
            return deltaY * sign * s_ScrollSensitivity;
        }

        public static float2 AdjustPointerDelta(float2 delta) {
            return delta * s_PointerSensitivity;
        }

        public static Vector2 AdjustPointerDelta(Vector2 delta) {
            return delta * s_PointerSensitivity;
        }
        public static float GetDefaultUIScale() {
            float dpi = Screen.dpi;
            if (dpi <= 96f) return 1f;
            if (dpi <= 144f) return 1.25f;
            if (dpi <= 192f) return 1.5f;
            return 2f;
        }

        private static void LoadPreferences() {
            s_DistanceUnits = (DistanceUnitsType)PlayerPrefs.GetInt(PREF_DISTANCE_UNITS, (int)DistanceUnitsType.Meters);
            s_AngleUnits = (AngleUnitsType)PlayerPrefs.GetInt(PREF_ANGLE_UNITS, (int)AngleUnitsType.Degrees);
            s_AngleChangeUnits = (AngleChangeUnitsType)PlayerPrefs.GetInt(PREF_ANGLE_CHANGE_UNITS, (int)AngleChangeUnitsType.Radians);
            s_SpeedUnits = (SpeedUnitsType)PlayerPrefs.GetInt(PREF_SPEED_UNITS, (int)SpeedUnitsType.MetersPerSecond);

            s_CurrentTrainStyle = PlayerPrefs.GetString(PREF_CURRENT_TRAIN_STYLE, "Modern.json");
            s_CurrentTrackStyle = PlayerPrefs.GetString(PREF_CURRENT_TRACK_STYLE, "Modern.json");

            if (s_CurrentTrainStyle == "Default.json") {
                s_CurrentTrainStyle = "Modern.json";
                PlayerPrefs.SetString(PREF_CURRENT_TRAIN_STYLE, s_CurrentTrainStyle);
            }
            if (s_CurrentTrackStyle == "Default.json") {
                s_CurrentTrackStyle = "Modern.json";
                PlayerPrefs.SetString(PREF_CURRENT_TRACK_STYLE, s_CurrentTrackStyle);
            }
            s_RideCameraPositionX = PlayerPrefs.GetFloat(PREF_RIDE_CAMERA_POSITION_X, DEFAULT_RIDE_CAMERA_POSITION_X);
            s_RideCameraPositionY = PlayerPrefs.GetFloat(PREF_RIDE_CAMERA_POSITION_Y, DEFAULT_RIDE_CAMERA_POSITION_Y);
            s_RideCameraPositionZ = PlayerPrefs.GetFloat(PREF_RIDE_CAMERA_POSITION_Z, DEFAULT_RIDE_CAMERA_POSITION_Z);
            s_RideCameraRotationX = PlayerPrefs.GetFloat(PREF_RIDE_CAMERA_ROTATION_X, DEFAULT_RIDE_CAMERA_ROTATION_X);
            s_RideCameraRotationY = PlayerPrefs.GetFloat(PREF_RIDE_CAMERA_ROTATION_Y, DEFAULT_RIDE_CAMERA_ROTATION_Y);
            s_RideCameraRotationZ = PlayerPrefs.GetFloat(PREF_RIDE_CAMERA_ROTATION_Z, DEFAULT_RIDE_CAMERA_ROTATION_Z);
            s_UIScale = PlayerPrefs.GetFloat(PREF_UI_SCALE, GetDefaultUIScale());
            s_SkyType = (SkyType)PlayerPrefs.GetInt(PREF_SKY_TYPE, (int)SkyType.Solid);
            s_NodeGridSnapping = PlayerPrefs.GetInt(PREF_NODE_GRID_SNAPPING, 0) == 1;
            s_ShowStats = PlayerPrefs.GetInt(PREF_SHOW_STATS, 0) == 1;
            s_SyncPlayback = PlayerPrefs.GetInt(PREF_SYNC_PLAYBACK, 1) == 1;
            s_KeyframeEditor = PlayerPrefs.GetInt(PREF_KEYFRAME_EDITOR, 0) == 1;
            s_ShowGizmos = PlayerPrefs.GetInt(PREF_SHOW_GIZMOS, 0) == 1;
            s_AutoStyle = PlayerPrefs.GetInt(PREF_AUTO_STYLE, 0) == 1;
            s_VisualizationMode = (VisualizationMode)PlayerPrefs.GetInt(PREF_VIS_MODE, (int)VisualizationMode.None);

            bool isMac = Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.OSXPlayer;
            float defaultScrollSensitivity = isMac ? 0.7f : 1f;
            float defaultPointerSensitivity = isMac ? 0.8f : 1f;
            bool defaultInvertScroll = isMac;
            bool defaultEnableTopRow = false;

            s_ScrollInvert = PlayerPrefs.GetInt(PREF_SCROLL_INVERT, defaultInvertScroll ? 1 : 0) == 1;
            s_ScrollSensitivity = PlayerPrefs.GetFloat(PREF_SCROLL_SENSITIVITY, defaultScrollSensitivity);
            s_PointerSensitivity = PlayerPrefs.GetFloat(PREF_POINTER_SENSITIVITY, defaultPointerSensitivity);
            s_EnableTopRowViewHotkeys = PlayerPrefs.GetInt(PREF_ENABLE_TOPROW_VIEW_HOTKEYS, defaultEnableTopRow ? 1 : 0) == 1;
        }

        public static float2 GetVisualizationRange(VisualizationMode mode) {
            return mode switch {
                VisualizationMode.Velocity => new float2(
                    PlayerPrefs.GetFloat(PREF_VIS_VELOCITY_MIN, 0f),
                    PlayerPrefs.GetFloat(PREF_VIS_VELOCITY_MAX, 50f)
                ),
                VisualizationMode.NormalForce => new float2(
                    PlayerPrefs.GetFloat(PREF_VIS_NORMAL_FORCE_MIN, -2f),
                    PlayerPrefs.GetFloat(PREF_VIS_NORMAL_FORCE_MAX, 5f)
                ),
                VisualizationMode.LateralForce => new float2(
                    PlayerPrefs.GetFloat(PREF_VIS_LATERAL_FORCE_MIN, -2f),
                    PlayerPrefs.GetFloat(PREF_VIS_LATERAL_FORCE_MAX, 2f)
                ),
                VisualizationMode.RollSpeed => new float2(
                    PlayerPrefs.GetFloat(PREF_VIS_ROLL_SPEED_MIN, -3f),
                    PlayerPrefs.GetFloat(PREF_VIS_ROLL_SPEED_MAX, 3f)
                ),
                VisualizationMode.PitchSpeed => new float2(
                    PlayerPrefs.GetFloat(PREF_VIS_PITCH_SPEED_MIN, -1f),
                    PlayerPrefs.GetFloat(PREF_VIS_PITCH_SPEED_MAX, 1f)
                ),
                VisualizationMode.YawSpeed => new float2(
                    PlayerPrefs.GetFloat(PREF_VIS_YAW_SPEED_MIN, -1f),
                    PlayerPrefs.GetFloat(PREF_VIS_YAW_SPEED_MAX, 1f)
                ),
                VisualizationMode.Curvature => new float2(
                    PlayerPrefs.GetFloat(PREF_VIS_CURVATURE_MIN, 0f),
                    PlayerPrefs.GetFloat(PREF_VIS_CURVATURE_MAX, 1f)
                ),
                _ => new float2(0f, 1f)
            };
        }

        public static void SetVisualizationRange(VisualizationMode mode, float min, float max) {
            switch (mode) {
                case VisualizationMode.Velocity:
                    PlayerPrefs.SetFloat(PREF_VIS_VELOCITY_MIN, min);
                    PlayerPrefs.SetFloat(PREF_VIS_VELOCITY_MAX, max);
                    break;
                case VisualizationMode.NormalForce:
                    PlayerPrefs.SetFloat(PREF_VIS_NORMAL_FORCE_MIN, min);
                    PlayerPrefs.SetFloat(PREF_VIS_NORMAL_FORCE_MAX, max);
                    break;
                case VisualizationMode.LateralForce:
                    PlayerPrefs.SetFloat(PREF_VIS_LATERAL_FORCE_MIN, min);
                    PlayerPrefs.SetFloat(PREF_VIS_LATERAL_FORCE_MAX, max);
                    break;
                case VisualizationMode.RollSpeed:
                    PlayerPrefs.SetFloat(PREF_VIS_ROLL_SPEED_MIN, min);
                    PlayerPrefs.SetFloat(PREF_VIS_ROLL_SPEED_MAX, max);
                    break;
                case VisualizationMode.PitchSpeed:
                    PlayerPrefs.SetFloat(PREF_VIS_PITCH_SPEED_MIN, min);
                    PlayerPrefs.SetFloat(PREF_VIS_PITCH_SPEED_MAX, max);
                    break;
                case VisualizationMode.YawSpeed:
                    PlayerPrefs.SetFloat(PREF_VIS_YAW_SPEED_MIN, min);
                    PlayerPrefs.SetFloat(PREF_VIS_YAW_SPEED_MAX, max);
                    break;
                case VisualizationMode.Curvature:
                    PlayerPrefs.SetFloat(PREF_VIS_CURVATURE_MIN, min);
                    PlayerPrefs.SetFloat(PREF_VIS_CURVATURE_MAX, max);
                    break;
            }
            PlayerPrefs.Save();
        }

        public static void ResetVisualizationRanges() {
            PlayerPrefs.DeleteKey(PREF_VIS_VELOCITY_MIN);
            PlayerPrefs.DeleteKey(PREF_VIS_VELOCITY_MAX);
            PlayerPrefs.DeleteKey(PREF_VIS_NORMAL_FORCE_MIN);
            PlayerPrefs.DeleteKey(PREF_VIS_NORMAL_FORCE_MAX);
            PlayerPrefs.DeleteKey(PREF_VIS_LATERAL_FORCE_MIN);
            PlayerPrefs.DeleteKey(PREF_VIS_LATERAL_FORCE_MAX);
            PlayerPrefs.DeleteKey(PREF_VIS_ROLL_SPEED_MIN);
            PlayerPrefs.DeleteKey(PREF_VIS_ROLL_SPEED_MAX);
            PlayerPrefs.DeleteKey(PREF_VIS_PITCH_SPEED_MIN);
            PlayerPrefs.DeleteKey(PREF_VIS_PITCH_SPEED_MAX);
            PlayerPrefs.DeleteKey(PREF_VIS_YAW_SPEED_MIN);
            PlayerPrefs.DeleteKey(PREF_VIS_YAW_SPEED_MAX);
            PlayerPrefs.DeleteKey(PREF_VIS_CURVATURE_MIN);
            PlayerPrefs.DeleteKey(PREF_VIS_CURVATURE_MAX);
            PlayerPrefs.Save();
        }
    }
}
