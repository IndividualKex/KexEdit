using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;
using static KexEdit.Legacy.Constants;
using static KexEdit.UI.Constants;

using KexEdit.Legacy;
namespace KexEdit.UI {
    [UpdateInGroup(typeof(UISimulationSystemGroup))]
    public partial class StatsOverlaySystem : SystemBase {
        private VisualElement _statsOverlay;
        private Dictionary<string, Label> _labels;
        private Dictionary<string, Label> _valueLabels;
        private Dictionary<string, string> _cachedStrings;

        private bool _isVisible;
        private PointData _lastPoint;
        private PointData _interpolatedPoint;
        private float3 _lastCameraPosition;
        private UnityEngine.Camera _camera;
        private static bool s_stringPoolInitialized;

        private TrainStyleConfig _cachedTrainConfig;
        private string _cachedTrainStyle;
        private int _cachedCarCount;
        private float[] _cachedCarOffsets;
        private const int MaxCars = 20;
        private const string CenterString = "Center";
        private float _lastPivotOffset = float.NaN;

        protected override void OnStartRunning() {
            if (!s_stringPoolInitialized) {
                StatsStringPool.Initialize();
                s_stringPoolInitialized = true;
            }

            var root = UIService.Instance.UIDocument.rootVisualElement;
            CreateStatsOverlay(root);
            _camera = UnityEngine.Camera.main;

            _cachedStrings = new Dictionary<string, string>();
            _cachedCarOffsets = new float[MaxCars];
        }

        private void CreateStatsOverlay(VisualElement root) {
            _labels = new Dictionary<string, Label>();
            _valueLabels = new Dictionary<string, Label>();

            _statsOverlay = new VisualElement {
                name = "stats-overlay",
                style = {
                    position = Position.Absolute,
                    top = 10f,
                    right = 10f,
                    width = 260f,
                    backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.85f),
                    borderTopLeftRadius = 8f,
                    borderTopRightRadius = 8f,
                    borderBottomLeftRadius = 8f,
                    borderBottomRightRadius = 8f,
                    paddingTop = 12f,
                    paddingBottom = 12f,
                    paddingLeft = 14f,
                    paddingRight = 14f,
                    display = DisplayStyle.None,
                }
            };

            var container = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Column,
                }
            };

            var transformSection = CreateSection("Transform");
            CreateTwoColumnRow(transformSection,
                ("Position X", "pos_x", s_StatsRowColor1),
                (Units.GetDistanceUnitsSuffix(), null, null));
            CreateTwoColumnRow(transformSection,
                ("Position Y", "pos_y", s_StatsRowColor2),
                (Units.GetDistanceUnitsSuffix(), null, null));
            CreateTwoColumnRow(transformSection,
                ("Position Z", "pos_z", s_StatsRowColor1),
                (Units.GetDistanceUnitsSuffix(), null, null));

            CreateTwoColumnRow(transformSection,
                ("Roll", "roll", s_StatsRowColor2),
                (Units.GetAngleUnitsSuffix(), null, null));
            CreateTwoColumnRow(transformSection,
                ("Pitch", "pitch", s_StatsRowColor1),
                (Units.GetAngleUnitsSuffix(), null, null));
            CreateTwoColumnRow(transformSection,
                ("Yaw", "yaw", s_StatsRowColor2),
                (Units.GetAngleUnitsSuffix(), null, null));

            CreateTwoColumnRow(transformSection,
                ("Velocity", "velocity", s_StatsRowColor1),
                (Units.GetSpeedUnitsSuffix(), null, null));
            container.Add(transformSection);

            var forcesSection = CreateSection("Properties");
            CreateTwoColumnRow(forcesSection,
                ("Pivot", "pivot", s_StatsRowColor2),
                ("", null, null));
            CreateTwoColumnRow(forcesSection,
                ("Roll Speed", "roll_speed", s_StatsRollSpeedColor),
                (Units.GetAnglePerTimeSuffix(), null, null));

            CreateTwoColumnRow(forcesSection,
                ("Pitch Speed", "pitch_speed", s_StatsPitchSpeedColor),
                (Units.GetAnglePerTimeSuffix(), null, null));

            CreateTwoColumnRow(forcesSection,
                ("Yaw Speed", "yaw_speed", s_StatsYawSpeedColor),
                (Units.GetAnglePerTimeSuffix(), null, null));

            CreateTwoColumnRow(forcesSection,
                ("Normal Force", "normal_force", s_StatsNormalForceColor),
                ("(G)", null, null));

            CreateTwoColumnRow(forcesSection,
                ("Lateral Force", "lateral_force", s_StatsLateralForceColor),
                ("(G)", null, null));

            container.Add(forcesSection);

            var cameraSection = CreateSection("Camera");
            CreateTwoColumnRow(cameraSection,
                ("Position X", "cam_x", s_StatsRowColor1),
                (Units.GetDistanceUnitsSuffix(), null, null));
            CreateTwoColumnRow(cameraSection,
                ("Position Y", "cam_y", s_StatsRowColor2),
                (Units.GetDistanceUnitsSuffix(), null, null));
            CreateTwoColumnRow(cameraSection,
                ("Position Z", "cam_z", s_StatsRowColor1),
                (Units.GetDistanceUnitsSuffix(), null, null));
            container.Add(cameraSection);

            _statsOverlay.Add(container);

            var gameView = root.Q<GameView>();
            gameView?.Add(_statsOverlay);
        }

        private VisualElement CreateSection(string title) {
            var section = new VisualElement {
                name = title,
                style = {
                    marginBottom = 10f,
                }
            };

            var header = new Label(title) {
                style = {
                    fontSize = 12,
                    color = s_MutedTextColor,
                    marginBottom = 6f,
                    unityFontStyleAndWeight = FontStyle.Bold,
                }
            };
            section.Add(header);

            var content = new VisualElement {
                name = title + "_content",
                style = {
                    backgroundColor = new Color(0.05f, 0.05f, 0.05f, 0.4f),
                    borderTopLeftRadius = 4f,
                    borderTopRightRadius = 4f,
                    borderBottomLeftRadius = 4f,
                    borderBottomRightRadius = 4f,
                    paddingTop = 6f,
                    paddingBottom = 6f,
                    paddingLeft = 8f,
                    paddingRight = 8f,
                }
            };
            section.Add(content);

            return section;
        }

        private void CreateThreeColumnRow(VisualElement parent,
            (string label, string key, Color? color) col1,
            (string label, string key, Color? color) col2,
            (string label, string key, Color? color) col3) {

            var row = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    justifyContent = Justify.SpaceBetween,
                    marginBottom = 3f,
                }
            };

            var content = parent.Q<VisualElement>(parent.name + "_content");

            CreateCompactLabelValue(row, col1.label, col1.key, col1.color ?? s_TextColor, 75f);
            CreateCompactLabelValue(row, col2.label, col2.key, col2.color ?? s_TextColor, 75f);
            CreateCompactLabelValue(row, col3.label, col3.key, col3.color ?? s_TextColor, 75f);

            content.Add(row);
        }

        private void CreateSingleRow(VisualElement parent, string label, string key, Color color) {
            var row = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    marginBottom = 3f,
                }
            };

            var content = parent.Q<VisualElement>(parent.name + "_content");

            var labelEl = new Label(label) {
                style = {
                    fontSize = 12,
                    color = s_MutedTextColor,
                    marginRight = 8f,
                }
            };
            row.Add(labelEl);

            var value = new Label("--") {
                style = {
                    fontSize = 12,
                    color = color,
                    unityFont = Font.CreateDynamicFontFromOSFont("Consolas", 12),
                    unityTextAlign = TextAnchor.MiddleRight,
                    minWidth = 60f,
                }
            };
            _valueLabels[key] = value;
            row.Add(value);

            content.Add(row);
        }

        private void CreateTwoColumnRow(VisualElement parent,
            (string label, string key, Color? color) col1,
            (string suffix, string key, Color? color) col2) {

            var row = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    marginBottom = 3f,
                }
            };

            var content = parent.Q<VisualElement>(parent.name + "_content");

            var labelEl = new Label(col1.label) {
                style = {
                    fontSize = 12,
                    color = col1.color ?? s_MutedTextColor,
                    width = 85f,
                }
            };
            row.Add(labelEl);

            if (col1.key != null) {
                var value = new Label("--") {
                    style = {
                        fontSize = 12,
                        color = col1.color ?? s_TextColor,
                        unityFont = Font.CreateDynamicFontFromOSFont("Consolas", 12),
                        width = 75f,
                        unityTextAlign = TextAnchor.MiddleRight,
                    }
                };
                _valueLabels[col1.key] = value;
                row.Add(value);
            }

            if (!string.IsNullOrEmpty(col2.suffix)) {
                var suffix = new Label(col2.suffix) {
                    style = {
                        fontSize = 12,
                        color = s_MutedTextColor,
                        marginLeft = 4f,
                    }
                };
                row.Add(suffix);
            }

            content.Add(row);
        }

        private void CreateCompactLabelValue(VisualElement parent, string label, string key, Color color, float width) {
            var container = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    width = width,
                }
            };

            var labelEl = new Label(label + ":") {
                style = {
                    fontSize = 12,
                    color = s_MutedTextColor,
                    marginRight = 4f,
                }
            };
            container.Add(labelEl);

            var value = new Label("--") {
                style = {
                    fontSize = 12,
                    color = color,
                    unityFont = Font.CreateDynamicFontFromOSFont("Consolas", 12),
                    unityTextAlign = TextAnchor.MiddleRight,
                    flexGrow = 1,
                }
            };
            _valueLabels[key] = value;
            container.Add(value);

            parent.Add(container);
        }

        protected override void OnUpdate() {
            bool shouldShow = Preferences.ShowStats;

            if (shouldShow != _isVisible) {
                _isVisible = shouldShow;
                _statsOverlay.style.display = _isVisible ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (!_isVisible) return;

            UpdatePivotLabel();

            var (trainEntity, follower) = GetActiveTrain();
            bool hasValidTrain = trainEntity != Entity.Null &&
                               follower.Section != Entity.Null &&
                               SystemAPI.Exists(follower.Section) &&
                               SystemAPI.HasBuffer<Point>(follower.Section);

            if (!hasValidTrain) {
                ShowNoStatsMessage();
                return;
            }

            var pointBuffer = SystemAPI.GetBuffer<Point>(follower.Section);
            if (pointBuffer.Length == 0) {
                ShowNoStatsMessage();
                return;
            }

            bool hasReadOnlyForces = SystemAPI.HasBuffer<ReadNormalForce>(follower.Section) &&
                                   SystemAPI.HasBuffer<ReadLateralForce>(follower.Section);

            if (hasReadOnlyForces) {
                GetInterpolatedPointWithReadOnlyForces(ref _interpolatedPoint, pointBuffer, follower.Section, follower.Index);
            } else {
                GetInterpolatedPoint(ref _interpolatedPoint, pointBuffer, follower.Index);
            }

            bool pointChanged = !PointsEqual(_interpolatedPoint, _lastPoint);
            if (pointChanged) {
                _lastPoint = _interpolatedPoint;
                UpdateLabels(_interpolatedPoint);
            }

            UpdateCameraLabels();
        }

        private void UpdateLabels(PointData point) {
            UpdateLabelIfChanged("pos_x", StatsFormatter.FormatPositionX(point.Position.x));
            UpdateLabelIfChanged("pos_y", StatsFormatter.FormatPositionY(point.Position.y));
            UpdateLabelIfChanged("pos_z", StatsFormatter.FormatPositionZ(point.Position.z));

            UpdateLabelIfChanged("roll", StatsFormatter.FormatRoll(point.Roll));
            UpdateLabelIfChanged("pitch", StatsFormatter.FormatPitch(point.GetPitch()));
            UpdateLabelIfChanged("yaw", StatsFormatter.FormatYaw(point.GetYaw()));

            UpdateLabelIfChanged("velocity", StatsFormatter.FormatVelocity(point.Velocity));

            float pitchSpeed = math.radians(point.PitchFromLast * HZ);
            float yawSpeed = math.radians(point.YawFromLast * HZ);

            UpdateLabelIfChanged("roll_speed", StatsFormatter.FormatRollSpeed(point.RollSpeed));
            UpdateLabelIfChanged("pitch_speed", StatsFormatter.FormatPitchSpeed(pitchSpeed));
            UpdateLabelIfChanged("yaw_speed", StatsFormatter.FormatYawSpeed(yawSpeed));
            UpdateLabelIfChanged("normal_force", StatsFormatter.FormatNormalForce(point.NormalForce));
            UpdateLabelIfChanged("lateral_force", StatsFormatter.FormatLateralForce(point.LateralForce));
        }

        private void UpdateLabelIfChanged(string key, string newValue) {
            if (!_cachedStrings.TryGetValue(key, out var cachedValue) || cachedValue != newValue) {
                _cachedStrings[key] = newValue;
                _valueLabels[key].text = newValue;
            }
        }

        private void UpdateCameraLabels() {
            if (_camera == null) {
                _camera = UnityEngine.Camera.main;
                if (_camera == null) return;
            }

            var cameraPosition = _camera.transform.position;

            if (math.distance(cameraPosition, _lastCameraPosition) > 0.01f) {
                _lastCameraPosition = cameraPosition;
                UpdateLabelIfChanged("cam_x", StatsFormatter.FormatCameraX(cameraPosition.x));
                UpdateLabelIfChanged("cam_y", StatsFormatter.FormatCameraY(cameraPosition.y));
                UpdateLabelIfChanged("cam_z", StatsFormatter.FormatCameraZ(cameraPosition.z));
            }
        }

        private void UpdatePivotLabel() {
            if (!SystemAPI.TryGetSingleton<ReadPivot>(out var readPivot)) return;

            float offset = readPivot.Offset;

            string currentStyle = Preferences.CurrentTrainStyle;
            bool styleChanged = _cachedTrainStyle != currentStyle;
            bool offsetChanged = float.IsNaN(_lastPivotOffset) || math.abs(offset - _lastPivotOffset) > 0.001f;

            if (!styleChanged && !offsetChanged) return;

            _lastPivotOffset = offset;
            UpdateTrainConfigCache();

            string pivotText = GetPivotText(offset);
            UpdateLabelIfChanged("pivot", pivotText);
        }

        private void UpdateTrainConfigCache() {
            string currentStyle = Preferences.CurrentTrainStyle;

            if (_cachedTrainStyle != currentStyle || _cachedTrainConfig == null) {
                _cachedTrainStyle = currentStyle;
                _cachedTrainConfig = TrainStyleResourceLoader.LoadConfig(currentStyle);

                if (_cachedTrainConfig != null) {
                    int carCount = TrainCarCountPreferences.GetCarCount(currentStyle, _cachedTrainConfig.CarCount);
                    _cachedTrainConfig.CarCount = carCount;
                    _cachedCarCount = carCount;

                    for (int i = 0; i < math.min(carCount, MaxCars); i++) {
                        _cachedCarOffsets[i] = TrainCarPositionCalculator.GetCarOffsetFromIndex(i, carCount, _cachedTrainConfig.CarSpacing);
                    }
                }
            } else {
                int currentCarCount = TrainCarCountPreferences.GetCarCount(currentStyle, _cachedTrainConfig.CarCount);
                if (currentCarCount != _cachedCarCount) {
                    _cachedCarCount = currentCarCount;
                    _cachedTrainConfig.CarCount = currentCarCount;

                    for (int i = 0; i < math.min(currentCarCount, MaxCars); i++) {
                        _cachedCarOffsets[i] = TrainCarPositionCalculator.GetCarOffsetFromIndex(i, currentCarCount, _cachedTrainConfig.CarSpacing);
                    }
                }
            }
        }

        private string GetPivotText(float offset) {
            if (_cachedTrainConfig == null) return CenterString;

            int carCount = _cachedCarCount;

            if (carCount == 1 && math.abs(offset) < 0.001f) {
                return StatsStringPool.GetCarString(1);
            }

            if (carCount > 1 && math.abs(offset) < 0.001f) {
                return CenterString;
            }

            for (int i = 0; i < math.min(carCount, MaxCars); i++) {
                if (math.abs(offset - _cachedCarOffsets[i]) < 0.001f) {
                    return StatsStringPool.GetCarString(i + 1);
                }
            }

            return FormatOffset(offset);
        }

        private string FormatOffset(float offset) {
            float displayOffset = Units.DistanceToDisplay(offset);
            return StatsStringPool.GetDecimalTwo(displayOffset);
        }

        private void ShowNoStatsMessage() {
            string nullValue = StatsStringPool.GetNull();
            foreach (var kvp in _valueLabels) {
                UpdateLabelIfChanged(kvp.Key, nullValue);
            }
            _lastPivotOffset = float.NaN;
        }

        private (Entity trainEntity, TrackFollower follower) GetActiveTrain() {
            foreach (var (train, trackFollower, entity) in SystemAPI.Query<Train, TrackFollower>().WithEntityAccess()) {
                if (train.Enabled && !train.Kinematic) {
                    return (entity, trackFollower);
                }
            }
            return (Entity.Null, default);
        }

        private void GetInterpolatedPoint(ref PointData result, DynamicBuffer<Point> points, float position) {
            position = math.clamp(position, 0f, points.Length - 1f);
            int frontIndex = (int)math.floor(position);
            float t = position - frontIndex;

            if (frontIndex >= points.Length - 1) {
                result = points[^1].Value;
                return;
            }

            if (t < 0.001f) {
                result = points[frontIndex].Value;
                return;
            }

            PointData frontPoint = points[frontIndex].Value;
            PointData backPoint = points[frontIndex + 1].Value;

            result.Position = math.lerp(frontPoint.Position, backPoint.Position, t);
            result.Direction = math.normalize(math.lerp(frontPoint.Direction, backPoint.Direction, t));
            result.Lateral = math.normalize(math.lerp(frontPoint.Lateral, backPoint.Lateral, t));
            result.Normal = math.normalize(math.lerp(frontPoint.Normal, backPoint.Normal, t));
            result.Roll = math.lerp(frontPoint.Roll, backPoint.Roll, t);
            result.Velocity = math.lerp(frontPoint.Velocity, backPoint.Velocity, t);
            result.NormalForce = math.lerp(frontPoint.NormalForce, backPoint.NormalForce, t);
            result.LateralForce = math.lerp(frontPoint.LateralForce, backPoint.LateralForce, t);
            result.RollSpeed = math.lerp(frontPoint.RollSpeed, backPoint.RollSpeed, t);
            result.PitchFromLast = math.lerp(frontPoint.PitchFromLast, backPoint.PitchFromLast, t);
            result.YawFromLast = math.lerp(frontPoint.YawFromLast, backPoint.YawFromLast, t);
            result.Energy = math.lerp(frontPoint.Energy, backPoint.Energy, t);
            result.Heart = math.lerp(frontPoint.Heart, backPoint.Heart, t);
            result.Friction = math.lerp(frontPoint.Friction, backPoint.Friction, t);
            result.Resistance = math.lerp(frontPoint.Resistance, backPoint.Resistance, t);
            result.Facing = frontPoint.Facing;
        }

        private void GetInterpolatedPointWithReadOnlyForces(ref PointData result, DynamicBuffer<Point> points, Entity section, float position) {
            position = math.clamp(position, 0f, points.Length - 1f);
            int frontIndex = (int)math.floor(position);
            float t = position - frontIndex;

            if (frontIndex >= points.Length - 1) {
                result = points[^1].Value;
                if (SystemAPI.HasBuffer<ReadNormalForce>(section)) {
                    var normalForces = SystemAPI.GetBuffer<ReadNormalForce>(section);
                    if (normalForces.Length > 0 && frontIndex < normalForces.Length) {
                        result.NormalForce = normalForces[math.min(frontIndex, normalForces.Length - 1)].Value;
                    }
                }
                if (SystemAPI.HasBuffer<ReadLateralForce>(section)) {
                    var lateralForces = SystemAPI.GetBuffer<ReadLateralForce>(section);
                    if (lateralForces.Length > 0 && frontIndex < lateralForces.Length) {
                        result.LateralForce = lateralForces[math.min(frontIndex, lateralForces.Length - 1)].Value;
                    }
                }
                return;
            }

            if (t < 0.001f) {
                result = points[frontIndex].Value;
                if (SystemAPI.HasBuffer<ReadNormalForce>(section)) {
                    var normalForces = SystemAPI.GetBuffer<ReadNormalForce>(section);
                    if (normalForces.Length > frontIndex) {
                        result.NormalForce = normalForces[frontIndex].Value;
                    }
                }
                if (SystemAPI.HasBuffer<ReadLateralForce>(section)) {
                    var lateralForces = SystemAPI.GetBuffer<ReadLateralForce>(section);
                    if (lateralForces.Length > frontIndex) {
                        result.LateralForce = lateralForces[frontIndex].Value;
                    }
                }
                return;
            }

            PointData frontPoint = points[frontIndex].Value;
            PointData backPoint = points[frontIndex + 1].Value;

            if (SystemAPI.HasBuffer<ReadNormalForce>(section)) {
                var normalForces = SystemAPI.GetBuffer<ReadNormalForce>(section);
                if (normalForces.Length > frontIndex + 1) {
                    frontPoint.NormalForce = normalForces[frontIndex].Value;
                    backPoint.NormalForce = normalForces[frontIndex + 1].Value;
                }
            }
            if (SystemAPI.HasBuffer<ReadLateralForce>(section)) {
                var lateralForces = SystemAPI.GetBuffer<ReadLateralForce>(section);
                if (lateralForces.Length > frontIndex + 1) {
                    frontPoint.LateralForce = lateralForces[frontIndex].Value;
                    backPoint.LateralForce = lateralForces[frontIndex + 1].Value;
                }
            }

            result.Position = math.lerp(frontPoint.Position, backPoint.Position, t);
            result.Direction = math.normalize(math.lerp(frontPoint.Direction, backPoint.Direction, t));
            result.Lateral = math.normalize(math.lerp(frontPoint.Lateral, backPoint.Lateral, t));
            result.Normal = math.normalize(math.lerp(frontPoint.Normal, backPoint.Normal, t));
            result.Roll = math.lerp(frontPoint.Roll, backPoint.Roll, t);
            result.Velocity = math.lerp(frontPoint.Velocity, backPoint.Velocity, t);
            result.NormalForce = math.lerp(frontPoint.NormalForce, backPoint.NormalForce, t);
            result.LateralForce = math.lerp(frontPoint.LateralForce, backPoint.LateralForce, t);
            result.RollSpeed = math.lerp(frontPoint.RollSpeed, backPoint.RollSpeed, t);
            result.PitchFromLast = math.lerp(frontPoint.PitchFromLast, backPoint.PitchFromLast, t);
            result.YawFromLast = math.lerp(frontPoint.YawFromLast, backPoint.YawFromLast, t);
            result.Energy = math.lerp(frontPoint.Energy, backPoint.Energy, t);
            result.Heart = math.lerp(frontPoint.Heart, backPoint.Heart, t);
            result.Friction = math.lerp(frontPoint.Friction, backPoint.Friction, t);
            result.Resistance = math.lerp(frontPoint.Resistance, backPoint.Resistance, t);
            result.Facing = frontPoint.Facing;
        }

        private bool PointsEqual(PointData a, PointData b) {
            return math.abs(a.Position.x - b.Position.x) < 0.01f &&
                   math.abs(a.Position.y - b.Position.y) < 0.01f &&
                   math.abs(a.Position.z - b.Position.z) < 0.01f &&
                   math.abs(a.Roll - b.Roll) < 0.1f &&
                   math.abs(a.Velocity - b.Velocity) < 0.01f &&
                   math.abs(a.NormalForce - b.NormalForce) < 0.001f &&
                   math.abs(a.LateralForce - b.LateralForce) < 0.001f &&
                   math.abs(a.RollSpeed - b.RollSpeed) < 0.01f &&
                   math.abs(a.PitchFromLast - b.PitchFromLast) < 0.01f &&
                   math.abs(a.YawFromLast - b.YawFromLast) < 0.01f;
        }
    }
}
