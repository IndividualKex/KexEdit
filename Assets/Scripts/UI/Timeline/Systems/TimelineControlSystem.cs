using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System;
using System.Collections;
using Unity.Burst;
using Unity.Jobs;
using static KexEdit.Constants;
using static KexEdit.UI.Timeline.Constants;

namespace KexEdit.UI.Timeline {
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class TimelineControlSystem : SystemBase, IEditableHandler {
        private List<KeyframeData> _clipboard = new();
        private TimelineData _data;
        private Timeline _timeline;

        private BufferLookup<Point> _pointLookup;

        private EntityQuery _playheadQuery;
        private EntityQuery _nodeQuery;

        protected override void OnCreate() {
            _data = new TimelineData {
                EnableKeyframeEditor = Preferences.KeyframeEditor
            };

            foreach (PropertyType propertyType in System.Enum.GetValues(typeof(PropertyType))) {
                _data.OrderedProperties.Add(propertyType);
                _data.Properties.Add(propertyType, new PropertyData {
                    Type = propertyType,
                    ViewMode = _data.ViewMode
                });
            }

            _pointLookup = SystemAPI.GetBufferLookup<Point>(true);

            _playheadQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<Cart, LocalTransform, PlayheadGizmoTag>()
                .Build(EntityManager);

            _nodeQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAspect<NodeAspect>()
                .Build(EntityManager);

            RequireForUpdate(_playheadQuery);
            RequireForUpdate<UIState>();
        }

        protected override void OnStartRunning() {
            var root = UIService.Instance.UIDocument.rootVisualElement;
            _timeline = root.Q<Timeline>();

            var uiState = SystemAPI.GetSingleton<UIState>();
            _data.Offset = uiState.TimelineOffset;
            _data.Zoom = uiState.TimelineZoom;

            _timeline.Initialize(_data);

            _timeline.RegisterCallback<CurveButtonClickEvent>(OnCurveButtonClick);
            _timeline.RegisterCallback<TimeChangeEvent>(OnTimeChange);
            _timeline.RegisterCallback<DurationChangeEvent>(OnDurationChange);
            _timeline.RegisterCallback<AddPropertyClickEvent>(OnAddPropertyClick);
            _timeline.RegisterCallback<OutlineMouseDownEvent>(_ => DeselectAll());
            _timeline.RegisterCallback<PropertyClickEvent>(OnPropertyClick);
            _timeline.RegisterCallback<PropertyRightClickEvent>(OnPropertyRightClick);
            _timeline.RegisterCallback<RemovePropertyClickEvent>(OnRemovePropertyClick);
            _timeline.RegisterCallback<KeyframeClickEvent>(OnKeyframeClick);
            _timeline.RegisterCallback<KeyframeDoubleClickEvent>(OnKeyframeDoubleClick);
            _timeline.RegisterCallback<ViewClickEvent>(OnViewClick);
            _timeline.RegisterCallback<ViewRightClickEvent>(OnViewRightClick);
            _timeline.RegisterCallback<SetKeyframeEvent>(OnSetKeyframe);
            _timeline.RegisterCallback<SetKeyframeAtTimeEvent>(OnSetKeyframeAtTime);
            _timeline.RegisterCallback<SetKeyframeValueEvent>(OnSetKeyframeValue);
            _timeline.RegisterCallback<JumpToKeyframeEvent>(OnJumpToKeyframe);
            _timeline.RegisterCallback<KeyframeButtonClickEvent>(OnKeyframeButtonClick);
            _timeline.RegisterCallback<DragKeyframesEvent>(OnDragKeyframes);
            _timeline.RegisterCallback<DragBezierHandleEvent>(OnDragBezierHandle);
            _timeline.RegisterCallback<SelectKeyframesEvent>(OnSelectKeyframes);
            _timeline.RegisterCallback<AddKeyframeEvent>(OnAddKeyframe);
            _timeline.RegisterCallback<TimelineOffsetChangeEvent>(OnTimelineOffsetChange);
            _timeline.RegisterCallback<TimelineZoomChangeEvent>(OnTimelineZoomChange);

            EditOperationsSystem.RegisterHandler(this);
        }

        protected override void OnDestroy() {
            EditOperationsSystem.UnregisterHandler(this);
            _data.Dispose();
        }

        protected override void OnUpdate() {
            SyncUIState();
            UpdateActive();
            SyncWithPlayback();
            UpdatePlayhead();
            UpdateTimelineData();
            _timeline.Draw();
        }

        private void SyncUIState() {
            var uiState = SystemAPI.GetSingleton<UIState>();
            _data.Offset = uiState.TimelineOffset;
            _data.Zoom = uiState.TimelineZoom;
        }

        private void UpdateActive() {
            _data.Active = false;
            _data.Entity = Entity.Null;
            using var entities = _nodeQuery.ToEntityArray(Allocator.Temp);
            foreach (var entity in entities) {
                var node = SystemAPI.GetAspect<NodeAspect>(entity);
                if (!node.Selected) continue;
                if (_data.Entity != Entity.Null) {
                    _data.Entity = Entity.Null;
                    return;
                }
                if (!SystemAPI.HasComponent<TrackHash>(entity)) continue;
                _data.Entity = entity;
            }
            _data.Active = _data.Entity != Entity.Null;
        }

        private void SyncWithPlayback() {
            if (!Preferences.SyncPlayback || !_data.Active) return;

            foreach (var cart in SystemAPI.Query<Cart>()) {
                if (cart.Active && !cart.Kinematic && cart.Section == _data.Entity) {
                    float timelineTime = CartPositionToTime(cart.Position);
                    if (math.abs(_data.Time - timelineTime) > 0.01f) {
                        _data.Time = math.clamp(timelineTime, 0f, _data.Duration);
                    }
                    return;
                }
            }
        }

        private void UpdatePlayhead() {
            ref Cart playhead = ref SystemAPI.GetComponentRW<Cart>(_playheadQuery.GetSingletonEntity()).ValueRW;
            playhead.Section = _data.Entity;

            bool isSynced = false;
            if (Preferences.SyncPlayback && _data.Active) {
                foreach (var cart in SystemAPI.Query<Cart>()) {
                    if (cart.Active && !cart.Kinematic && cart.Section == _data.Entity) {
                        isSynced = true;
                        break;
                    }
                }
            }
            playhead.Active = _data.Active && !isSynced;

            if (!playhead.Active) return;

            var pointBuffer = SystemAPI.GetBuffer<Point>(_data.Entity);
            if (pointBuffer.Length < 2) return;

            if (SystemAPI.HasComponent<Duration>(_data.Entity)) {
                var duration = SystemAPI.GetComponent<Duration>(_data.Entity);
                if (duration.Type == DurationType.Time) {
                    playhead.Position = _data.Time * HZ;
                    return;
                }
                else {
                    float targetDistance = SystemAPI.GetComponent<Anchor>(_data.Entity).Value.TotalLength + _data.Time;
                    for (int i = 0; i < pointBuffer.Length - 1; i++) {
                        float currentDistance = pointBuffer[i].Value.TotalLength;
                        float nextDistance = pointBuffer[i + 1].Value.TotalLength;
                        if (targetDistance >= currentDistance && targetDistance <= nextDistance) {
                            float t = (nextDistance - currentDistance) > 0 ?
                                (targetDistance - currentDistance) / (nextDistance - currentDistance) : 0f;
                            playhead.Position = i + t;
                            return;
                        }
                    }
                    playhead.Position = pointBuffer.Length - 1;
                    return;
                }
            }

            float index = math.round(_data.Time * HZ);
            playhead.Position = math.clamp(index, 0, pointBuffer.Length - 1);
        }

        private void UpdateTimelineData() {
            UpdateProperties();

            if (!_data.Active) {
                _data.HasEditingKeyframe = false;
                return;
            }

            _data.DisplayName = GetDisplayName();
            _data.Duration = GetDuration();
            _data.DurationType = GetDurationType();
            _data.Time = math.clamp(_data.Time, 0f, _data.Duration);
            _data.HasEditableDuration = HasEditableDuration();
            UpdateKeyframes();
            UpdateValues();
            UpdateValueBounds();
        }

        private void UpdateProperties() {
            bool isAlt = false;
            _data.DrawAnyReadOnly = false;
            foreach (var property in _data.OrderedProperties) {
                var propertyData = _data.Properties[property];
                propertyData.Visible = false;

                var adapter = PropertyAdapter.GetAdapter(property);
                adapter.UpdateKeyframes(_data.Entity, propertyData.Keyframes);

                if (!_data.Active) continue;

                propertyData.Visible = IsPropertyVisible(property);
                propertyData.HasActiveKeyframe = FindKeyframe(property, _data.Time, out _);
                propertyData.Selected = IsPropertySelected(property);
                propertyData.DrawReadOnly &= !propertyData.Visible && !propertyData.Selected;
                propertyData.Value = EvaluateAt(property, _data.Time);
                propertyData.Units = property.GetUnits(_data.DurationType);
                _data.DrawAnyReadOnly |= propertyData.DrawReadOnly;

                if (propertyData.Visible) {
                    propertyData.IsAlt = isAlt;
                    isAlt = !isAlt;
                }
            }

            _data.AddPropertyButtonVisible = ShouldShowAddButton();
        }

        private void UpdateKeyframes() {
            _data.SelectedKeyframeCount = 0;
            foreach (var (type, propertyData) in _data.Properties) {
                var adapter = PropertyAdapter.GetAdapter(type);
                propertyData.SelectedKeyframeCount = 0;
                adapter.UpdateKeyframes(_data.Entity, propertyData.Keyframes);
                if (!propertyData.Visible) continue;
                foreach (var keyframe in propertyData.Keyframes) {
                    if (keyframe.Selected) {
                        _data.SelectedKeyframeCount++;
                        propertyData.SelectedKeyframeCount++;
                    }
                }
            }
            bool hasSelectedKeyframe = _data.SelectedKeyframeCount == 1;
            KeyframeData? editingKeyframeData = hasSelectedKeyframe ? GetSelectedKeyframe() : null;
            _data.HasEditingKeyframe = _data.EnableKeyframeEditor && editingKeyframeData.HasValue;
            if (_data.HasEditingKeyframe) {
                KeyframeData editingKeyframe = editingKeyframeData.Value;
                _data.EditingKeyframeType = editingKeyframe.Type;
                _data.EditingKeyframeInInterpolation = editingKeyframe.Value.InInterpolation;
                _data.EditingKeyframeOutInterpolation = editingKeyframe.Value.OutInterpolation;
                _data.EditingKeyframeHandleType = editingKeyframe.Value.HandleType;
                _data.EditingKeyframeId = editingKeyframe.Value.Id;
                _data.EditingKeyframeValue = editingKeyframe.Value.Value;
                _data.EditingKeyframeTime = editingKeyframe.Value.Time;
                _data.EditingKeyframeInWeight = editingKeyframe.Value.InWeight;
                _data.EditingKeyframeInTangent = editingKeyframe.Value.InTangent;
                _data.EditingKeyframeOutWeight = editingKeyframe.Value.OutWeight;
                _data.EditingKeyframeOutTangent = editingKeyframe.Value.OutTangent;
            }
        }

        private void UpdateValues() {
            if (!_data.DrawAnyReadOnly) return;

            _pointLookup.Update(this);
            var pointBuffer = _pointLookup[_data.Entity];

            new UpdateTimesJob {
                Points = pointBuffer,
                Times = _data.Times,
                DurationType = _data.DurationType
            }.Run();

            NativeArray<JobHandle> jobs = new(_data.Properties.Count, Allocator.TempJob);
            int i = 0;
            foreach (var (type, propertyData) in _data.Properties) {
                jobs[i++] = new UpdateValuesJob {
                    Points = pointBuffer,
                    Values = propertyData.Values,
                    Type = type
                }.Schedule();
            }

            JobHandle.CombineDependencies(jobs).Complete();
            jobs.Dispose();
        }

        [BurstCompile]
        private struct UpdateTimesJob : IJob {
            [ReadOnly]
            public DynamicBuffer<Point> Points;
            public NativeList<float> Times;
            public DurationType DurationType;

            public void Execute() {
                if (Points.Length == 0) return;
                Times.Clear();
                Times.ResizeUninitialized(Points.Length);
                float startLength = Points[0].Value.TotalLength;
                for (int i = 0; i < Points.Length; i++) {
                    var point = Points[i].Value;
                    if (DurationType == DurationType.Time) {
                        Times[i] = i / HZ;
                    }
                    else {
                        Times[i] = point.TotalLength - startLength;
                    }
                }
            }
        }

        [BurstCompile]
        private struct UpdateValuesJob : IJob {
            [ReadOnly]
            public DynamicBuffer<Point> Points;
            public NativeList<float> Values;
            public PropertyType Type;

            public void Execute() {
                if (Points.Length == 0) return;
                Values.Clear();
                Values.ResizeUninitialized(Points.Length);

                for (int i = 0; i < Points.Length; i++) {
                    var point = Points[i].Value;
                    switch (Type) {
                        case PropertyType.NormalForce:
                            Values[i] = point.NormalForce;
                            break;
                        case PropertyType.LateralForce:
                            Values[i] = point.LateralForce;
                            break;
                        case PropertyType.RollSpeed:
                            Values[i] = point.RollSpeed;
                            break;
                        case PropertyType.PitchSpeed:
                            Values[i] = point.PitchFromLast;
                            break;
                        case PropertyType.YawSpeed:
                            Values[i] = point.YawFromLast;
                            break;
                        default:
                            break;
                    }
                }
            }
        }

        private void UpdateValueBounds() {
            if (!_data.Active) {
                _data.ValueBounds = ValueBounds.Default;
                return;
            }

            float tempMin = float.MaxValue;
            float tempMax = float.MinValue;
            bool hasAnyKeyframes = false;

            foreach (var (type, propertyData) in _data.Properties) {
                if (propertyData.Visible && !propertyData.Hidden && !propertyData.DrawReadOnly) {
                    foreach (var keyframe in propertyData.Keyframes) {
                        hasAnyKeyframes = true;
                        tempMin = math.min(tempMin, keyframe.Value);
                        tempMax = math.max(tempMax, keyframe.Value);

                        if (keyframe.OutInterpolation == InterpolationType.Bezier) {
                            float nextKeyframeDt = 0f;
                            foreach (var nextKeyframe in propertyData.Keyframes) {
                                if (nextKeyframe.Time > keyframe.Time &&
                                    (nextKeyframeDt == 0f || nextKeyframe.Time < keyframe.Time + nextKeyframeDt)) {
                                    nextKeyframeDt = nextKeyframe.Time - keyframe.Time;
                                }
                            }

                            if (nextKeyframeDt > 0f) {
                                float handleValue = keyframe.Value + keyframe.OutTangent * nextKeyframeDt * keyframe.OutWeight;
                                tempMin = math.min(tempMin, handleValue);
                                tempMax = math.max(tempMax, handleValue);
                            }
                        }

                        if (keyframe.InInterpolation == InterpolationType.Bezier) {
                            float prevKeyframeDt = 0f;
                            foreach (var prevKeyframe in propertyData.Keyframes) {
                                if (prevKeyframe.Time < keyframe.Time &&
                                    (prevKeyframeDt == 0f || prevKeyframe.Time > keyframe.Time - prevKeyframeDt)) {
                                    prevKeyframeDt = keyframe.Time - prevKeyframe.Time;
                                }
                            }

                            if (prevKeyframeDt > 0f) {
                                float handleValue = keyframe.Value - keyframe.InTangent * prevKeyframeDt * keyframe.InWeight;
                                tempMin = math.min(tempMin, handleValue);
                                tempMax = math.max(tempMax, handleValue);
                            }
                        }
                    }
                }
                else if (propertyData.DrawReadOnly) {
                    for (int i = 1; i < propertyData.Values.Length; i++) {
                        hasAnyKeyframes = true;
                        tempMin = math.min(tempMin, propertyData.Values[i]);
                        tempMax = math.max(tempMax, propertyData.Values[i]);
                    }
                }
            }

            if (!hasAnyKeyframes) {
                _data.ValueBounds = ValueBounds.Default;
                return;
            }

            float range = tempMax - tempMin;
            float padding = range * 0.2f;
            float paddedMinValue = tempMin - padding;
            float paddedMaxValue = tempMax + padding;

            const float MIN_VALUE_RANGE = 0.5f;
            float paddedRange = paddedMaxValue - paddedMinValue;

            if (paddedRange < MIN_VALUE_RANGE) {
                float center = (paddedMinValue + paddedMaxValue) * 0.5f;
                float halfMinRange = MIN_VALUE_RANGE * 0.5f;
                _data.ValueBounds = new ValueBounds(center - halfMinRange, center + halfMinRange);
                return;
            }

            _data.ValueBounds = new ValueBounds(paddedMinValue, paddedMaxValue);
        }

        private string GetDisplayName() {
            NodeType type = SystemAPI.GetComponent<Node>(_data.Entity).Type;
            return type.GetDisplayName();
        }

        private float GetDuration() {
            if (SystemAPI.HasComponent<Duration>(_data.Entity)) {
                return SystemAPI.GetComponent<Duration>(_data.Entity).Value;
            }

            if (SystemAPI.HasBuffer<Point>(_data.Entity)) {
                var pointBuffer = SystemAPI.GetBuffer<Point>(_data.Entity);
                return pointBuffer.Length > 0 ? pointBuffer.Length / HZ : 0f;
            }

            return 0f;
        }

        private bool HasEditableDuration() {
            return SystemAPI.HasComponent<Duration>(_data.Entity);
        }

        private DurationType GetDurationType() {
            if (_data.Active && SystemAPI.HasComponent<Duration>(_data.Entity)) {
                return SystemAPI.GetComponent<Duration>(_data.Entity).Type;
            }
            return DurationType.Time;
        }

        private bool IsPropertyVisible(PropertyType type) {
            var adapter = PropertyAdapter.GetAdapter(type);
            if (!adapter.HasBuffer(_data.Entity)) return false;

            var overrides = SystemAPI.HasComponent<PropertyOverrides>(_data.Entity)
                ? SystemAPI.GetComponent<PropertyOverrides>(_data.Entity)
                : PropertyOverrides.Default;

            return type switch {
                PropertyType.FixedVelocity => overrides.FixedVelocity,
                PropertyType.Heart => overrides.Heart,
                PropertyType.Friction => overrides.Friction,
                PropertyType.Resistance => overrides.Resistance,
                _ => true
            };
        }

        private bool IsPropertySelected(PropertyType type) {
            if (!SystemAPI.HasComponent<SelectedProperties>(_data.Entity)) return false;
            var selectedProperties = SystemAPI.GetComponent<SelectedProperties>(_data.Entity);
            return selectedProperties.IsSelected(type);
        }

        private bool ShouldShowAddButton() {
            if (!_data.Active) return false;

            var overrides = SystemAPI.HasComponent<PropertyOverrides>(_data.Entity)
                ? SystemAPI.GetComponent<PropertyOverrides>(_data.Entity)
                : PropertyOverrides.Default;

            bool canAddFixedVelocity = !overrides.FixedVelocity;
            bool canAddHeart = !overrides.Heart;
            bool canAddFriction = !overrides.Friction;
            bool canAddResistance = !overrides.Resistance;

            return canAddFixedVelocity || canAddHeart || canAddFriction || canAddResistance;
        }

        private void MarkTrackDirty() {
            ref var dirty = ref SystemAPI.GetComponentRW<Dirty>(_data.Entity).ValueRW;
            dirty = true;
        }

        private float EvaluateAt(PropertyType type, float time) {
            var adapter = PropertyAdapter.GetAdapter(type);
            if (!adapter.HasBuffer(_data.Entity)) return 0f;
            var anchor = SystemAPI.GetComponent<Anchor>(_data.Entity);
            return adapter.EvaluateAt(_data.Entity, time, anchor);
        }

        private Keyframe FindKeyframeById(PropertyAdapter adapter, uint keyframeId) {
            foreach (var keyframe in _data.Properties[adapter.Type].Keyframes) {
                if (keyframe.Id == keyframeId) {
                    return keyframe;
                }
            }
            throw new Exception($"Keyframe with id {keyframeId} not found");
        }

        private void SetPropertyOverride(PropertyType type, bool value) {
            ref var overrides = ref SystemAPI.GetComponentRW<PropertyOverrides>(_data.Entity).ValueRW;
            switch (type) {
                case PropertyType.FixedVelocity:
                    overrides.FixedVelocity = value;
                    break;
                case PropertyType.Heart:
                    overrides.Heart = value;
                    break;
                case PropertyType.Friction:
                    overrides.Friction = value;
                    break;
                case PropertyType.Resistance:
                    overrides.Resistance = value;
                    break;
                default:
                    throw new System.NotImplementedException($"Property override not implemented for {type}");
            }

            ref var dirty = ref SystemAPI.GetComponentRW<Dirty>(_data.Entity).ValueRW;
            dirty = true;
        }

        private void SelectProperty(PropertyType type) {
            ref var selectedProperties = ref SystemAPI.GetComponentRW<SelectedProperties>(_data.Entity).ValueRW;
            if (selectedProperties.IsSelected(type)) return;
            selectedProperties.Select(type);
            _data.LatestSelectedProperty = type;
            UpdateSelectionState();
        }

        private void DeselectProperty(PropertyType type) {
            ref var selectedProperties = ref SystemAPI.GetComponentRW<SelectedProperties>(_data.Entity).ValueRW;
            if (!selectedProperties.IsSelected(type)) return;
            selectedProperties.Deselect(type);
            UpdateSelectionState();
        }

        private void SelectAllProperties() {
            ref var selectedProperties = ref SystemAPI.GetComponentRW<SelectedProperties>(_data.Entity).ValueRW;
            foreach (var (type, propertyData) in _data.Properties) {
                if (!propertyData.Visible) continue;
                selectedProperties.Select(type);
            }
            UpdateSelectionState();
        }

        private void DeselectAllProperties() {
            ref var selectedProperties = ref SystemAPI.GetComponentRW<SelectedProperties>(_data.Entity).ValueRW;
            if (selectedProperties.IsEmpty) return;
            selectedProperties.Clear();
            UpdateSelectionState();
        }

        private void SelectKeyframe(KeyframeData keyframe) {
            if (keyframe.Value.Selected) return;
            var adapter = PropertyAdapter.GetAdapter(keyframe.Type);
            adapter.UpdateKeyframe(_data.Entity, keyframe.Value.WithSelected(true));
            _data.LatestSelectedProperty = keyframe.Type;
            UpdateKeyframes();
            UpdateSelectionState();
        }

        private void DeselectKeyframe(KeyframeData keyframe) {
            if (!keyframe.Value.Selected) return;
            var adapter = PropertyAdapter.GetAdapter(keyframe.Type);
            adapter.UpdateKeyframe(_data.Entity, keyframe.Value.WithSelected(false));
            UpdateKeyframes();
            UpdateSelectionState();
        }

        private void SelectAllKeyframesForProperty(PropertyType type) {
            var adapter = PropertyAdapter.GetAdapter(type);
            foreach (var keyframe in _data.Properties[type].Keyframes) {
                adapter.UpdateKeyframe(_data.Entity, keyframe.WithSelected(true));
            }
            UpdateKeyframes();
            UpdateSelectionState();
        }

        private void DeselectAllKeyframesForProperty(PropertyType type) {
            var adapter = PropertyAdapter.GetAdapter(type);
            foreach (var keyframe in _data.Properties[type].Keyframes) {
                adapter.UpdateKeyframe(_data.Entity, keyframe.WithSelected(false));
            }
            UpdateKeyframes();
            UpdateSelectionState();
        }

        private void SelectAllKeyframes() {
            foreach (var (type, propertyData) in _data.Properties) {
                if (!propertyData.Visible) continue;
                var adapter = PropertyAdapter.GetAdapter(type);
                foreach (var keyframe in propertyData.Keyframes) {
                    adapter.UpdateKeyframe(_data.Entity, keyframe.WithSelected(true));
                }
            }
            UpdateKeyframes();
            UpdateSelectionState();
        }

        private void DeselectAllKeyframes() {
            foreach (var (type, propertyData) in _data.Properties) {
                if (!propertyData.Visible) continue;
                var adapter = PropertyAdapter.GetAdapter(type);
                foreach (var keyframe in propertyData.Keyframes) {
                    adapter.UpdateKeyframe(_data.Entity, keyframe.WithSelected(false));
                }
            }
            UpdateKeyframes();
            UpdateSelectionState();
        }

        private void UpdateSelectionState() {
            ref var selectedProperties = ref SystemAPI.GetComponentRW<SelectedProperties>(_data.Entity).ValueRW;
            foreach (var (type, propertyData) in _data.Properties) {
                if (!propertyData.Visible) continue;
                if (propertyData.SelectedKeyframeCount > 0) {
                    selectedProperties.Select(type);
                }
                else {
                    selectedProperties.Deselect(type);
                }
            }
        }

        private bool FindKeyframe(PropertyType type, float time, out Keyframe result) {
            result = default;
            var adapter = PropertyAdapter.GetAdapter(type);
            foreach (var keyframe in _data.Properties[type].Keyframes) {
                if (math.abs(keyframe.Time - time) < 1e-2f) {
                    result = keyframe;
                    return true;
                }
            }
            return false;
        }

        private void SetKeyframeInterpolation(InterpolationType interpolationType, bool inHandle, bool bothHandles) {
            bool anyChanged = false;
            foreach (var (type, propertyData) in _data.Properties) {
                if (!propertyData.Visible) continue;
                var adapter = PropertyAdapter.GetAdapter(type);
                foreach (var keyframe in propertyData.Keyframes) {
                    if (!keyframe.Selected) continue;

                    var updatedKeyframe = keyframe;
                    bool changed = false;

                    if (bothHandles) {
                        if (updatedKeyframe.InInterpolation != interpolationType) {
                            updatedKeyframe.InInterpolation = interpolationType;
                            changed = true;
                        }
                        if (updatedKeyframe.OutInterpolation != interpolationType) {
                            updatedKeyframe.OutInterpolation = interpolationType;
                            changed = true;
                        }
                        if (changed && updatedKeyframe.HasAlignedHandles()) {
                            updatedKeyframe.InTangent = updatedKeyframe.OutTangent;
                        }
                    }
                    else {
                        if (inHandle && updatedKeyframe.InInterpolation != interpolationType) {
                            updatedKeyframe.InInterpolation = interpolationType;
                            changed = true;
                            if (interpolationType != InterpolationType.Bezier &&
                                updatedKeyframe.OutInterpolation == InterpolationType.Bezier) {
                                updatedKeyframe.OutInterpolation = InterpolationType.Bezier;
                            }
                        }
                        else if (!inHandle && updatedKeyframe.OutInterpolation != interpolationType) {
                            updatedKeyframe.OutInterpolation = interpolationType;
                            changed = true;
                            if (interpolationType != InterpolationType.Bezier &&
                                updatedKeyframe.InInterpolation == InterpolationType.Bezier) {
                                updatedKeyframe.InInterpolation = InterpolationType.Bezier;
                            }
                        }
                    }

                    if (changed) {
                        adapter.UpdateKeyframe(_data.Entity, updatedKeyframe);
                        anyChanged = true;
                    }
                }
            }

            if (anyChanged) {
                UpdateDefaultInterpolationType(interpolationType, inHandle, bothHandles);
                MarkTrackDirty();
            }
        }

        private void UpdateDefaultInterpolationType(InterpolationType interpolationType, bool inHandle, bool bothHandles) {
            if (bothHandles) {
                Keyframe.SetDefaultInterpolation(interpolationType, interpolationType);
            }
            else {
                var (currentInType, currentOutType) = GetSelectedKeyframesInterpolation();
                InterpolationType newInType = inHandle ? interpolationType : (currentInType ?? InterpolationType.Bezier);
                InterpolationType newOutType = !inHandle ? interpolationType : (currentOutType ?? InterpolationType.Bezier);
                Keyframe.SetDefaultInterpolation(newInType, newOutType);
            }
        }

        private void OnCurveButtonClick(CurveButtonClickEvent evt) {
            if (!evt.IsRightClick) {
                ToggleCurveView();
                return;
            }

            (evt.target as VisualElement).ShowContextMenu(evt.MousePosition, menu => {
                foreach (var (type, propertyData) in _data.Properties) {
                    if (propertyData.Visible || !propertyData.IsReadable) continue;

                    menu.AddItem(type.GetDisplayName(), () => {
                        propertyData.DrawReadOnly = !propertyData.DrawReadOnly;
                        EnableCurveView();
                    }, isChecked: propertyData.DrawReadOnly);
                }
            });
        }

        private void ToggleCurveView() {
            _data.ViewMode = _data.ViewMode == TimelineViewMode.DopeSheet ?
                TimelineViewMode.Curve :
                TimelineViewMode.DopeSheet;

            foreach (var (type, propertyData) in _data.Properties) {
                propertyData.ViewMode = _data.ViewMode;
            }
        }

        private void EnableCurveView() {
            if (_data.ViewMode == TimelineViewMode.Curve) return;
            ToggleCurveView();
        }

        private void OnTimeChange(TimeChangeEvent evt) {
            SetTime(evt.Time, evt.Snap);

            if (Preferences.SyncPlayback && _data.Active) {
                foreach (var (cart, entity) in SystemAPI.Query<RefRW<Cart>>().WithEntityAccess()) {
                    if (cart.ValueRO.Active && !cart.ValueRO.Kinematic) {
                        cart.ValueRW.Section = _data.Entity;
                        cart.ValueRW.Position = TimeToCartPosition(_data.Time);
                        break;
                    }
                }
            }
        }

        private float TimeToCartPosition(float time) {
            if (GetDurationType() == DurationType.Time) {
                return time * HZ;
            }

            return DistanceToCartPosition(SystemAPI.GetComponent<Anchor>(_data.Entity).Value.TotalLength + time);
        }

        private float CartPositionToTime(float cartPosition) {
            if (GetDurationType() == DurationType.Time) {
                return cartPosition / HZ;
            }

            var pointBuffer = SystemAPI.GetBuffer<Point>(_data.Entity);
            if (pointBuffer.Length < 2) return 0f;

            int index = math.clamp((int)math.floor(cartPosition), 0, pointBuffer.Length - 2);
            float t = cartPosition - index;

            float distance = math.lerp(pointBuffer[index].Value.TotalLength, pointBuffer[index + 1].Value.TotalLength, t);
            return distance - SystemAPI.GetComponent<Anchor>(_data.Entity).Value.TotalLength;
        }

        private float DistanceToCartPosition(float targetDistance) {
            var pointBuffer = SystemAPI.GetBuffer<Point>(_data.Entity);
            if (pointBuffer.Length < 2) return 0f;

            for (int i = 0; i < pointBuffer.Length - 1; i++) {
                float currentDistance = pointBuffer[i].Value.TotalLength;
                float nextDistance = pointBuffer[i + 1].Value.TotalLength;
                if (targetDistance >= currentDistance && targetDistance <= nextDistance) {
                    float t = (nextDistance - currentDistance) > 0 ?
                        (targetDistance - currentDistance) / (nextDistance - currentDistance) : 0f;
                    return i + t;
                }
            }
            return pointBuffer.Length - 1;
        }

        private void OnDurationChange(DurationChangeEvent evt) {
            if (!SystemAPI.HasComponent<Duration>(_data.Entity)) {
                throw new Exception("No entity or duration component found");
            }

            float duration = evt.Duration;

            if (evt.Snap) {
                duration = math.round(duration / SNAPPING) * SNAPPING;
            }

            duration = math.max(0.1f, duration);

            var inputPorts = SystemAPI.GetBuffer<InputPortReference>(_data.Entity);

            foreach (var portRef in inputPorts) {
                if (SystemAPI.HasComponent<Port>(portRef.Value) &&
                    SystemAPI.GetComponent<Port>(portRef.Value).Type == PortType.Duration &&
                    SystemAPI.HasComponent<DurationPort>(portRef.Value)) {

                    ref var durationPort = ref SystemAPI.GetComponentRW<DurationPort>(portRef.Value).ValueRW;
                    durationPort.Value = duration;

                    ref var dirty = ref SystemAPI.GetComponentRW<Dirty>(portRef.Value).ValueRW;
                    dirty = true;

                    MarkTrackDirty();
                    return;
                }
            }
        }

        private void OnPropertyClick(PropertyClickEvent evt) {
            var propertyData = _data.Properties[evt.Type];
            if (evt.ShiftKey) {
                if (propertyData.Selected) {
                    DeselectAllKeyframesForProperty(evt.Type);
                    DeselectProperty(evt.Type);
                }
                else {
                    SelectProperty(evt.Type);
                    SelectAllKeyframesForProperty(evt.Type);
                }
            }
            else if (!propertyData.Selected) {
                DeselectAll();
                SelectProperty(evt.Type);
                SelectAllKeyframesForProperty(evt.Type);
            }
            else {
                _data.LatestSelectedProperty = evt.Type;
            }
        }

        private void OnPropertyRightClick(PropertyRightClickEvent evt) {
            (evt.target as VisualElement).ShowContextMenu(evt.MousePosition, menu => {
                menu.AddItem("Remove", () => {
                    Undo.Record();
                    RemoveProperty(evt.Type);
                });
            });
        }

        private void OnRemovePropertyClick(RemovePropertyClickEvent evt) {
            Undo.Record();
            RemoveProperty(evt.Type);
        }

        private void OnKeyframeClick(KeyframeClickEvent evt) {
            if (evt.ShiftKey) {
                if (evt.Keyframe.Value.Selected) {
                    DeselectKeyframe(evt.Keyframe);
                }
                else {
                    SelectKeyframe(evt.Keyframe);
                }
            }
            else {
                if (!evt.Keyframe.Value.Selected) {
                    DeselectAllKeyframes();
                }
                SelectKeyframe(evt.Keyframe);
            }
        }

        private void OnKeyframeDoubleClick(KeyframeDoubleClickEvent evt) {
            _data.EnableKeyframeEditor = !_data.EnableKeyframeEditor;
            Preferences.KeyframeEditor = _data.EnableKeyframeEditor;
        }

        private void ShowKeyframeValueEditor(KeyframeData keyframe, UnityEngine.Vector2 position) {
            var unitsType = keyframe.Type.GetUnits(_data.DurationType);
            _timeline.View.ShowFloatFieldEditor(
                position,
                keyframe.Value.Value,
                newValue => {
                    Undo.Record();
                    var adapter = PropertyAdapter.GetAdapter(keyframe.Type);
                    adapter.UpdateKeyframe(_data.Entity, keyframe.Value.WithValue(newValue));
                    MarkTrackDirty();
                },
                unitsType
            );
        }

        private void ShowKeyframeTimeEditor(KeyframeData keyframe, UnityEngine.Vector2 position) {
            var unitsType = _data.DurationType == DurationType.Time ? UnitsType.Time : UnitsType.Distance;
            _timeline.View.ShowFloatFieldEditor(
                position,
                keyframe.Value.Time,
                newValue => {
                    Undo.Record();
                    var adapter = PropertyAdapter.GetAdapter(keyframe.Type);
                    newValue = _data.ClampTime(newValue);
                    var updatedKeyframe = new Keyframe {
                        Id = keyframe.Value.Id,
                        Time = newValue,
                        Value = keyframe.Value.Value,
                        InInterpolation = keyframe.Value.InInterpolation,
                        OutInterpolation = keyframe.Value.OutInterpolation,
                        HandleType = keyframe.Value.HandleType,
                        Flags = keyframe.Value.Flags,
                        InTangent = keyframe.Value.InTangent,
                        OutTangent = keyframe.Value.OutTangent,
                        InWeight = keyframe.Value.InWeight,
                        OutWeight = keyframe.Value.OutWeight,
                        Selected = keyframe.Value.Selected
                    };
                    adapter.UpdateKeyframe(_data.Entity, updatedKeyframe);
                    MarkTrackDirty();
                },
                unitsType
            );
        }

        private void ShowKeyframeInWeightEditor(KeyframeData keyframe, UnityEngine.Vector2 position) {
            _timeline.View.ShowFloatFieldEditor(
                position,
                keyframe.Value.InWeight,
                newValue => {
                    Undo.Record();
                    var adapter = PropertyAdapter.GetAdapter(keyframe.Type);
                    newValue = math.clamp(newValue, 0.01f, 2f);
                    adapter.UpdateKeyframe(_data.Entity, keyframe.Value.WithInEasing(keyframe.Value.InTangent, newValue));
                    MarkTrackDirty();
                },
                UnitsType.None
            );
        }

        private void ShowKeyframeOutWeightEditor(KeyframeData keyframe, UnityEngine.Vector2 position) {
            _timeline.View.ShowFloatFieldEditor(
                position,
                keyframe.Value.OutWeight,
                newValue => {
                    Undo.Record();
                    var adapter = PropertyAdapter.GetAdapter(keyframe.Type);
                    newValue = math.clamp(newValue, 0.01f, 2f);
                    adapter.UpdateKeyframe(_data.Entity, keyframe.Value.WithOutEasing(keyframe.Value.OutTangent, newValue));
                    MarkTrackDirty();
                },
                UnitsType.None
            );
        }

        private void ShowKeyframeInTangentEditor(KeyframeData keyframe, UnityEngine.Vector2 position) {
            _timeline.View.ShowFloatFieldEditor(
                position,
                keyframe.Value.InTangent,
                newValue => {
                    Undo.Record();
                    var adapter = PropertyAdapter.GetAdapter(keyframe.Type);
                    adapter.UpdateKeyframe(_data.Entity, keyframe.Value.WithInEasing(newValue, keyframe.Value.InWeight));
                    MarkTrackDirty();
                },
                UnitsType.None
            );
        }

        private void ShowKeyframeOutTangentEditor(KeyframeData keyframe, UnityEngine.Vector2 position) {
            _timeline.View.ShowFloatFieldEditor(
                position,
                keyframe.Value.OutTangent,
                newValue => {
                    Undo.Record();
                    var adapter = PropertyAdapter.GetAdapter(keyframe.Type);
                    adapter.UpdateKeyframe(_data.Entity, keyframe.Value.WithOutEasing(newValue, keyframe.Value.OutWeight));
                    MarkTrackDirty();
                },
                UnitsType.None
            );
        }

        private void OnViewClick(ViewClickEvent evt) {
            if (!evt.ShiftKey) {
                DeselectAllKeyframes();
            }
        }

        private void OnViewRightClick(ViewRightClickEvent evt) {
            bool hasSelection = _data.SelectedKeyframeCount > 0;
            bool hasMultiSelection = _data.SelectedKeyframeCount > 1;
            bool canPaste = EditOperationsSystem.CanPaste;

            if (!hasSelection && !canPaste) return;

            VisualElement target = evt.target as VisualElement;
            target.ShowContextMenu(evt.MousePosition, menu => {
                bool canCut = EditOperationsSystem.CanCut;
                bool canCopy = EditOperationsSystem.CanCopy;

                if (hasSelection && !hasMultiSelection) {
                    menu.AddItem("Edit", () => {
                        _data.EnableKeyframeEditor = !_data.EnableKeyframeEditor;
                        Preferences.KeyframeEditor = _data.EnableKeyframeEditor;
                    }, isChecked: _data.EnableKeyframeEditor);
                    menu.AddSubmenu("Optimize", submenu => {
                        submenu.AddItem("Roll", () => {
                            Undo.Record();
                            Optimize(TargetValueType.Roll);
                        });
                        submenu.AddItem("Pitch", () => {
                            Undo.Record();
                            Optimize(TargetValueType.Pitch);
                        });
                        submenu.AddItem("Yaw", () => {
                            Undo.Record();
                            Optimize(TargetValueType.Yaw);
                        });
                        submenu.AddSeparator();
                        submenu.AddItem("Normal Force", () => {
                            Undo.Record();
                            Optimize(TargetValueType.NormalForce);
                        });
                        submenu.AddItem("Lateral Force", () => {
                            Undo.Record();
                            Optimize(TargetValueType.LateralForce);
                        });
                        submenu.AddSeparator();
                        submenu.AddSubmenu("Position", positionSubmenu => {
                            positionSubmenu.AddItem("X", () => {
                                Undo.Record();
                                Optimize(TargetValueType.X);
                            });
                            positionSubmenu.AddItem("Y", () => {
                                Undo.Record();
                                Optimize(TargetValueType.Y);
                            });
                            positionSubmenu.AddItem("Z", () => {
                                Undo.Record();
                                Optimize(TargetValueType.Z);
                            });
                        });
                    });
                    menu.AddSubmenu("Reset", submenu => {
                        submenu.AddItem("To Default", () => {
                            Undo.Record();
                            ResetKeyframe();
                        });
                        submenu.AddItem("To Previous", () => {
                            Undo.Record();
                            ResetKeyframeToPrevious();
                        });
                    });
                    menu.AddSeparator();
                }

                menu.AddItem("Set Value", () => {
                    var keyframe = GetSelectedKeyframe();
                    ShowKeyframeValueEditor(keyframe, evt.MousePosition);
                }, "V");
                menu.AddSeparator();
                menu.AddPlatformItem(canCut ? "Cut" : "Cannot Cut", EditOperationsSystem.HandleCut, "Ctrl+X", enabled: canCut && hasSelection);
                menu.AddPlatformItem(canCopy ? "Copy" : "Cannot Copy", EditOperationsSystem.HandleCopy, "Ctrl+C", enabled: canCopy && hasSelection);
                menu.AddPlatformItem(canPaste ? "Paste" : "Cannot Paste", EditOperationsSystem.HandlePaste, "Ctrl+V", enabled: canPaste);
                menu.AddItem(hasSelection ? "Delete" : "Cannot Delete", () => {
                    if (hasSelection) {
                        Undo.Record();
                        RemoveSelectedKeyframes();
                    }
                }, "Del", enabled: hasSelection);

                if (hasSelection) {
                    AddInterpolationMenu(menu);
                    AddLockMenu(menu);
                }
            });
        }

        private void AddInterpolationMenu(ContextMenu menu) {
            var (currentInType, currentOutType) = GetSelectedKeyframesInterpolation();
            bool isBezier = currentInType == InterpolationType.Bezier || currentOutType == InterpolationType.Bezier;

            menu.AddSeparator();

            menu.AddSubmenu("Interpolation Mode", interpolationSubmenu => {
                interpolationSubmenu.AddItem("Constant", () => {
                    Undo.Record();
                    SetKeyframeInterpolation(InterpolationType.Constant, false, true);
                }, isChecked: currentInType == InterpolationType.Constant && currentOutType == InterpolationType.Constant);

                interpolationSubmenu.AddItem("Linear", () => {
                    Undo.Record();
                    SetKeyframeInterpolation(InterpolationType.Linear, false, true);
                }, isChecked: currentInType == InterpolationType.Linear && currentOutType == InterpolationType.Linear);

                interpolationSubmenu.AddItem("Bezier", () => {
                    Undo.Record();
                    SetKeyframeInterpolation(InterpolationType.Bezier, false, true);
                }, isChecked: isBezier);
            });

            var currentHandleType = GetSelectedKeyframesHandleType();
            bool isAligned = currentHandleType == HandleType.Aligned;

            menu.AddSubmenu("Handle Type", handleSubmenu => {
                handleSubmenu.AddItem("Free", () => {
                    Undo.Record();
                    if (!isBezier) {
                        SetKeyframeInterpolation(InterpolationType.Bezier, false, true);
                    }
                    SetKeyframeHandleType(HandleType.Free);
                }, isChecked: isBezier && !isAligned);

                handleSubmenu.AddItem("Aligned", () => {
                    Undo.Record();
                    if (!isBezier) {
                        SetKeyframeInterpolation(InterpolationType.Bezier, false, true);
                    }
                    SetKeyframeHandleType(HandleType.Aligned);
                }, isChecked: isBezier && isAligned);
            });

            var currentEasing = GetCurrentEasingType();

            menu.AddSubmenu("Easing Mode", easingSubmenu => {
                easingSubmenu.AddItem("Sine", () => {
                    Undo.Record();
                    if (!isBezier) {
                        SetKeyframeInterpolation(InterpolationType.Bezier, false, true);
                    }
                    ApplyEasingToSelectedKeyframes(EasingType.Sine);
                }, isChecked: isBezier && currentEasing == EasingType.Sine);
                easingSubmenu.AddItem("Quadratic", () => {
                    Undo.Record();
                    if (!isBezier) {
                        SetKeyframeInterpolation(InterpolationType.Bezier, false, true);
                    }
                    ApplyEasingToSelectedKeyframes(EasingType.Quadratic);
                }, isChecked: isBezier && currentEasing == EasingType.Quadratic);
                easingSubmenu.AddItem("Cubic", () => {
                    Undo.Record();
                    if (!isBezier) {
                        SetKeyframeInterpolation(InterpolationType.Bezier, false, true);
                    }
                    ApplyEasingToSelectedKeyframes(EasingType.Cubic);
                }, isChecked: isBezier && currentEasing == EasingType.Cubic);
                easingSubmenu.AddItem("Quartic", () => {
                    Undo.Record();
                    if (!isBezier) {
                        SetKeyframeInterpolation(InterpolationType.Bezier, false, true);
                    }
                    ApplyEasingToSelectedKeyframes(EasingType.Quartic);
                }, isChecked: isBezier && currentEasing == EasingType.Quartic);
                easingSubmenu.AddItem("Quintic", () => {
                    Undo.Record();
                    if (!isBezier) {
                        SetKeyframeInterpolation(InterpolationType.Bezier, false, true);
                    }
                    ApplyEasingToSelectedKeyframes(EasingType.Quintic);
                }, isChecked: isBezier && currentEasing == EasingType.Quintic);
                easingSubmenu.AddItem("Exponential", () => {
                    Undo.Record();
                    if (!isBezier) {
                        SetKeyframeInterpolation(InterpolationType.Bezier, false, true);
                    }
                    ApplyEasingToSelectedKeyframes(EasingType.Exponential);
                }, isChecked: isBezier && currentEasing == EasingType.Exponential);
            });
        }

        private void AddLockMenu(ContextMenu menu) {
            var (timeLocked, valueLocked) = GetSelectedKeyframesLockState();
            bool bothLocked = timeLocked && valueLocked;

            menu.AddSeparator();
            menu.AddSubmenu("Lock", lockSubmenu => {
                lockSubmenu.AddItem("Time", () => {
                    Undo.Record();
                    SetKeyframesTimeLock(!timeLocked);
                }, isChecked: timeLocked);

                lockSubmenu.AddItem("Value", () => {
                    Undo.Record();
                    SetKeyframesValueLock(!valueLocked);
                }, isChecked: valueLocked);

                lockSubmenu.AddSeparator();

                lockSubmenu.AddItem("Both", () => {
                    Undo.Record();
                    SetKeyframesBothLock(!bothLocked);
                }, isChecked: bothLocked);
            });
        }

        private (bool timeLocked, bool valueLocked) GetSelectedKeyframesLockState() {
            bool anyTimeLocked = false;
            bool anyValueLocked = false;

            foreach (var (type, propertyData) in _data.Properties) {
                if (!propertyData.Visible) continue;
                foreach (var keyframe in propertyData.Keyframes) {
                    if (!keyframe.Selected) continue;
                    if (keyframe.IsTimeLocked) anyTimeLocked = true;
                    if (keyframe.IsValueLocked) anyValueLocked = true;
                }
            }

            return (anyTimeLocked, anyValueLocked);
        }

        private void SetKeyframesTimeLock(bool locked) {
            foreach (var (type, propertyData) in _data.Properties) {
                if (!propertyData.Visible) continue;
                var adapter = PropertyAdapter.GetAdapter(type);
                foreach (var keyframe in propertyData.Keyframes) {
                    if (!keyframe.Selected) continue;
                    var updatedKeyframe = keyframe.WithTimeLock(locked);
                    adapter.UpdateKeyframe(_data.Entity, updatedKeyframe);
                }
            }
            UpdateKeyframes();
            MarkTrackDirty();
        }

        private void SetKeyframesValueLock(bool locked) {
            foreach (var (type, propertyData) in _data.Properties) {
                if (!propertyData.Visible) continue;
                var adapter = PropertyAdapter.GetAdapter(type);
                foreach (var keyframe in propertyData.Keyframes) {
                    if (!keyframe.Selected) continue;
                    var updatedKeyframe = keyframe.WithValueLock(locked);
                    adapter.UpdateKeyframe(_data.Entity, updatedKeyframe);
                }
            }
            UpdateKeyframes();
            MarkTrackDirty();
        }

        private void SetKeyframesBothLock(bool locked) {
            foreach (var (type, propertyData) in _data.Properties) {
                if (!propertyData.Visible) continue;
                var adapter = PropertyAdapter.GetAdapter(type);
                foreach (var keyframe in propertyData.Keyframes) {
                    if (!keyframe.Selected) continue;
                    var updatedKeyframe = keyframe.WithTimeLock(locked).WithValueLock(locked);
                    adapter.UpdateKeyframe(_data.Entity, updatedKeyframe);
                }
            }
            UpdateKeyframes();
            MarkTrackDirty();
        }

        private void AddInterpolationOption(ContextMenu menu, string label, InterpolationType interpolationType, bool isSelected, bool setBoth, bool? isInHandle = null) {
            menu.AddItem(label, () => {
                Undo.Record();
                SetKeyframeInterpolation(interpolationType, isInHandle ?? false, setBoth);
            }, isChecked: isSelected);
        }

        private (InterpolationType? inType, InterpolationType? outType) GetSelectedKeyframesInterpolation() {
            InterpolationType? commonInType = null;
            InterpolationType? commonOutType = null;
            bool firstKeyframe = true;
            bool hasSelectedKeyframes = false;

            foreach (var (type, propertyData) in _data.Properties) {
                if (!propertyData.Visible) continue;
                var adapter = PropertyAdapter.GetAdapter(type);
                foreach (var keyframe in propertyData.Keyframes) {
                    if (!keyframe.Selected) continue;

                    hasSelectedKeyframes = true;

                    if (firstKeyframe) {
                        commonInType = keyframe.InInterpolation;
                        commonOutType = keyframe.OutInterpolation;
                        firstKeyframe = false;
                    }
                    else {
                        if (commonInType.HasValue && commonInType.Value != keyframe.InInterpolation) {
                            commonInType = null;
                        }
                        if (commonOutType.HasValue && commonOutType.Value != keyframe.OutInterpolation) {
                            commonOutType = null;
                        }
                    }
                }
            }

            return hasSelectedKeyframes ? (commonInType, commonOutType) : (null, null);
        }

        private void OnSetKeyframe(SetKeyframeEvent evt) {
            var adapter = PropertyAdapter.GetAdapter(evt.Type);
            if (FindKeyframe(evt.Type, _data.Time, out var keyframe)) {
                adapter.UpdateKeyframe(_data.Entity, keyframe.WithValue(evt.Value));
            }
            else {
                DeselectAllKeyframes();
                var newKeyframe = Keyframe.Create(_data.Time, evt.Value).WithSelected(true);
                adapter.AddKeyframe(_data.Entity, newKeyframe);
                UpdateKeyframes();
                UpdateSelectionState();
            }

            MarkTrackDirty();
        }

        private void OnSetKeyframeAtTime(SetKeyframeAtTimeEvent evt) {
            var adapter = PropertyAdapter.GetAdapter(evt.Type);
            var keyframe = FindKeyframeById(adapter, evt.KeyframeId);
            if (keyframe.Id != 0) {
                var updatedKeyframe = new Keyframe {
                    Id = keyframe.Id,
                    Time = evt.Time,
                    Value = evt.Value,
                    InInterpolation = evt.InInterpolation,
                    OutInterpolation = evt.OutInterpolation,
                    HandleType = keyframe.HandleType,
                    Flags = keyframe.Flags,
                    InTangent = evt.InTangent,
                    OutTangent = evt.OutTangent,
                    InWeight = evt.InWeight,
                    OutWeight = evt.OutWeight,
                    Selected = keyframe.Selected
                };
                adapter.UpdateKeyframe(_data.Entity, updatedKeyframe);
            }
            else {
                UnityEngine.Debug.LogError($"No keyframe found for type {evt.Type} with ID {evt.KeyframeId}");
            }
            MarkTrackDirty();
        }

        private void OnSetKeyframeValue(SetKeyframeValueEvent evt) {
            ShowKeyframeValueEditor(evt.Keyframe, evt.MousePosition);
        }

        private void ResetKeyframe() {
            var keyframe = GetSelectedKeyframe();
            float defaultValue = keyframe.Type.Default(_data.Time);
            var adapter = PropertyAdapter.GetAdapter(keyframe.Type);
            adapter.UpdateKeyframe(_data.Entity, keyframe.Value.WithValue(defaultValue));
            MarkTrackDirty();
        }

        private void ResetKeyframeToPrevious() {
            PointData anchor = SystemAPI.GetComponent<Anchor>(_data.Entity);
            var keyframe = GetSelectedKeyframe();
            var adapter = PropertyAdapter.GetAdapter(keyframe.Type);
            float previousValue = keyframe.Type.Previous(_data.Time, anchor, _data.DurationType);
            adapter.UpdateKeyframe(_data.Entity, keyframe.Value.WithValue(previousValue));
            MarkTrackDirty();
        }

        private void Optimize(TargetValueType targetValueType) {
            UIService.Instance.StartCoroutine(OptimizeCoroutine(targetValueType));
        }

        private IEnumerator OptimizeCoroutine(TargetValueType targetValueType) {
            var keyframe = GetSelectedKeyframe();

            var root = UIService.Instance.UIDocument.rootVisualElement;
            var optimizerData = new OptimizerData {
                Time = _data.Time,
                ValueType = targetValueType,
                PropertyType = keyframe.Type,
                DurationType = _data.DurationType,
                Units = targetValueType.GetUnits()
            };
            var dialog = root.ShowOptimizerDialog(optimizerData);
            var optimizer = new Optimizer(_data.Entity, keyframe, optimizerData);

            while (!optimizerData.IsComplete && !optimizerData.IsCanceled) {
                if (!EntityManager.Exists(_data.Entity) || !_data.Active) {
                    optimizerData.IsCanceled = true;
                    break;
                }

                try {
                    var timelinePoint = GetPointAtTime(_data.Time);
                    float value = targetValueType switch {
                        TargetValueType.Roll => timelinePoint.Roll,
                        TargetValueType.Pitch => timelinePoint.GetPitch(),
                        TargetValueType.Yaw => timelinePoint.GetYaw(),
                        TargetValueType.X => timelinePoint.Position.x,
                        TargetValueType.Y => timelinePoint.Position.y,
                        TargetValueType.Z => timelinePoint.Position.z,
                        TargetValueType.NormalForce => timelinePoint.NormalForce,
                        TargetValueType.LateralForce => timelinePoint.LateralForce,
                        _ => throw new NotImplementedException()
                    };
                    optimizer.Step(value);
                }
                catch (System.Exception) {
                    optimizerData.IsCanceled = true;
                    break;
                }

                SystemAPI.SetComponent<Dirty>(_data.Entity, true);
                while (SystemAPI.GetComponent<Dirty>(_data.Entity).Value) {
                    yield return null;
                }
            }
        }

        private PointData GetPlayheadPoint() {
            Entity playheadEntity = _playheadQuery.GetSingletonEntity();
            Cart playhead = SystemAPI.GetComponent<Cart>(playheadEntity);
            float playheadPosition = playhead.Position;
            int index = (int)math.floor(playheadPosition);
            int nextIndex = index + 1;

            var points = SystemAPI.GetBuffer<Point>(_data.Entity);
            if (index < 0 || nextIndex >= points.Length) {
                UnityEngine.Debug.LogError($"Playhead position {playheadPosition} is out of bounds for entity {_data.Entity}");
                return PointData.Create();
            }

            PointData p0 = points[index].Value;
            PointData p1 = points[nextIndex].Value;

            float t = playheadPosition - math.floor(playheadPosition);
            return PointData.Lerp(p0, p1, t);
        }

        private PointData GetPointAtTime(float time) {
            var points = SystemAPI.GetBuffer<Point>(_data.Entity);
            if (points.Length == 0) {
                return PointData.Create();
            }

            float position;
            if (SystemAPI.HasComponent<Duration>(_data.Entity)) {
                var duration = SystemAPI.GetComponent<Duration>(_data.Entity);
                if (duration.Type == DurationType.Time) {
                    position = time * HZ;
                }
                else {
                    float targetDistance = SystemAPI.GetComponent<Anchor>(_data.Entity).Value.TotalLength + time;
                    position = DistanceToCartPosition(targetDistance);
                }
            }
            else {
                position = time * HZ;
            }

            position = math.clamp(position, 0, points.Length - 1);
            int index = (int)math.floor(position);
            int nextIndex = math.min(index + 1, points.Length - 1);

            if (index == nextIndex) {
                return points[index].Value;
            }

            PointData p0 = points[index].Value;
            PointData p1 = points[nextIndex].Value;

            float t = position - math.floor(position);
            return PointData.Lerp(p0, p1, t);
        }

        private KeyframeData GetSelectedKeyframe() {
            UnityEngine.Debug.Assert(_data.SelectedKeyframeCount == 1);

            foreach (var (type, propertyData) in _data.Properties) {
                if (!propertyData.Visible) continue;
                foreach (var keyframe in propertyData.Keyframes) {
                    if (!keyframe.Selected) continue;
                    return new KeyframeData(type, keyframe);
                }
            }

            throw new Exception("No selected keyframe found");
        }

        private void OnJumpToKeyframe(JumpToKeyframeEvent evt) {
            float? targetTime = null;

            foreach (var keyframe in _data.Properties[evt.Type].Keyframes) {
                if (evt.Direction == NavigationDirection.Previous) {
                    if (keyframe.Time < _data.Time - 1e-2f &&
                        (!targetTime.HasValue || keyframe.Time > targetTime.Value)) {
                        targetTime = keyframe.Time;
                    }
                }
                else {
                    if (keyframe.Time > _data.Time + 1e-2f &&
                        (!targetTime.HasValue || keyframe.Time < targetTime.Value)) {
                        targetTime = keyframe.Time;
                    }
                }
            }

            if (targetTime.HasValue) {
                SetTime(targetTime.Value, false);

                if (Preferences.SyncPlayback && _data.Active) {
                    foreach (var (cart, entity) in SystemAPI.Query<RefRW<Cart>>().WithEntityAccess()) {
                        if (cart.ValueRO.Active && !cart.ValueRO.Kinematic) {
                            cart.ValueRW.Section = _data.Entity;
                            cart.ValueRW.Position = TimeToCartPosition(_data.Time);
                            break;
                        }
                    }
                }
            }
        }

        private void OnKeyframeButtonClick(KeyframeButtonClickEvent evt) {
            if (FindKeyframe(evt.Type, _data.Time, out var keyframe)) {
                RemoveKeyframe(new KeyframeData(evt.Type, keyframe));
            }
            else {
                OnSetKeyframe(new SetKeyframeEvent {
                    Type = evt.Type,
                    Value = _data.Properties[evt.Type].Value,
                });
            }
        }

        private void OnDragKeyframes(DragKeyframesEvent evt) {
            foreach (var (type, propertyData) in _data.Properties) {
                if (!propertyData.Visible) continue;
                var adapter = PropertyAdapter.GetAdapter(type);
                foreach (var keyframe in propertyData.Keyframes) {
                    if (!keyframe.Selected) continue;
                    if (!evt.StartTimes.TryGetValue(keyframe.Id, out float startTime)) continue;

                    float time = startTime + evt.TimeDelta;
                    float value = keyframe.Value;

                    if (!keyframe.IsTimeLocked) {
                        time = _data.ClampTime(time);

                        if (_data.ViewMode == TimelineViewMode.DopeSheet && !evt.ShiftKey) {
                            time = math.round(time / SNAPPING) * SNAPPING;
                        }
                    }
                    else {
                        time = keyframe.Time;
                    }

                    if (_data.ViewMode == TimelineViewMode.Curve) {
                        if (!evt.StartValues.TryGetValue(keyframe.Id, out float startValue)) continue;

                        if (!keyframe.IsValueLocked) {
                            value = startValue + evt.ValueDelta;
                            value = evt.Bounds.ClampToVisualBounds(value, evt.ContentHeight);
                        }
                    }

                    var updatedKeyframe = keyframe;
                    updatedKeyframe.Time = time;
                    updatedKeyframe.Value = value;

                    adapter.UpdateKeyframe(_data.Entity, updatedKeyframe);
                }
            }

            MarkTrackDirty();
        }

        private void OnDragBezierHandle(DragBezierHandleEvent evt) {
            var adapter = PropertyAdapter.GetAdapter(evt.Keyframe.Type);

            float mouseTime = evt.StartTime + evt.TimeDelta;
            float mouseValue = evt.StartValue + evt.ValueDelta;

            if (evt.ContentHeight > 0) {
                mouseValue = evt.Bounds.ClampToVisualBounds(mouseValue, evt.ContentHeight);
            }

            float dt = 0f;
            foreach (var nextKeyframe in _data.Properties[evt.Keyframe.Type].Keyframes) {
                bool isValid = evt.IsOutHandle
                    ? nextKeyframe.Time > evt.Keyframe.Value.Time && (dt == 0f || nextKeyframe.Time < evt.Keyframe.Value.Time + dt)
                    : nextKeyframe.Time < evt.Keyframe.Value.Time && (dt == 0f || nextKeyframe.Time > evt.Keyframe.Value.Time - dt);

                if (isValid) {
                    dt = math.abs(nextKeyframe.Time - evt.Keyframe.Value.Time);
                }
            }

            if (dt > 0f) {
                float timeDiff = evt.IsOutHandle ? mouseTime - evt.Keyframe.Value.Time : evt.Keyframe.Value.Time - mouseTime;
                float weight = math.clamp(timeDiff / dt, 0.01f, 2f);
                float tangent = weight > 0.001f ?
                    (evt.IsOutHandle ? mouseValue - evt.Keyframe.Value.Value :
                    evt.Keyframe.Value.Value - mouseValue) / (dt * weight) : 0f;

                if (evt.IsOutHandle) {
                    evt.Keyframe.Value.OutWeight = weight;
                    evt.Keyframe.Value.OutTangent = tangent;
                    if (evt.Keyframe.Value.HasAlignedHandles()) {
                        evt.Keyframe.Value.InTangent = tangent;
                    }
                }
                else {
                    evt.Keyframe.Value.InWeight = weight;
                    evt.Keyframe.Value.InTangent = tangent;
                    if (evt.Keyframe.Value.HasAlignedHandles()) {
                        evt.Keyframe.Value.OutTangent = tangent;
                    }
                }
            }

            adapter.UpdateKeyframe(_data.Entity, evt.Keyframe.Value);
            MarkTrackDirty();
        }

        private void OnSelectKeyframes(SelectKeyframesEvent evt) {
            foreach (var keyframe in evt.Keyframes) {
                var adapter = PropertyAdapter.GetAdapter(keyframe.Type);
                adapter.UpdateKeyframe(_data.Entity, keyframe.Value.WithSelected(true));
            }
            UpdateKeyframes();
            UpdateSelectionState();
        }

        private void OnAddKeyframe(AddKeyframeEvent evt) {
            if (!_data.Active) return;

            PropertyType? targetProperty = null;

            if (_data.LatestSelectedProperty.HasValue &&
                _data.Properties.ContainsKey(_data.LatestSelectedProperty.Value) &&
                _data.Properties[_data.LatestSelectedProperty.Value].Visible) {
                targetProperty = _data.LatestSelectedProperty.Value;
            }
            else {
                foreach (var (type, propertyData) in _data.Properties) {
                    if (propertyData.Visible && propertyData.Selected) {
                        targetProperty = type;
                        break;
                    }
                }
            }

            if (!targetProperty.HasValue) return;

            var currentValue = _data.Properties[targetProperty.Value].Value;
            var setKeyframeEvent = new SetKeyframeEvent {
                Type = targetProperty.Value,
                Value = currentValue
            };

            Undo.Record();
            OnSetKeyframe(setKeyframeEvent);
        }

        private void AddProperty(PropertyType type) {
            Undo.Record();
            SetPropertyOverride(type, true);
            MarkTrackDirty();
        }

        private void RemoveProperty(PropertyType type) {
            Undo.Record();
            var adapter = PropertyAdapter.GetAdapter(type);
            foreach (var keyframe in _data.Properties[type].Keyframes) {
                adapter.RemoveKeyframe(_data.Entity, keyframe.Id);
            }
            SetPropertyOverride(type, false);
            UpdateKeyframes();
            UpdateSelectionState();
            MarkTrackDirty();
        }

        private void RemoveKeyframe(KeyframeData keyframe) {
            var adapter = PropertyAdapter.GetAdapter(keyframe.Type);
            adapter.RemoveKeyframe(_data.Entity, keyframe.Value.Id);
            UpdateKeyframes();
            UpdateSelectionState();
            MarkTrackDirty();
        }

        private void OnAddPropertyClick(AddPropertyClickEvent evt) {
            (evt.target as VisualElement).ShowContextMenu(evt.MousePosition, menu => {
                bool canAddFixedVelocity = !IsPropertyVisible(PropertyType.FixedVelocity);
                bool canAddHeart = !IsPropertyVisible(PropertyType.Heart);
                bool canAddFriction = !IsPropertyVisible(PropertyType.Friction);
                bool canAddResistance = !IsPropertyVisible(PropertyType.Resistance);

                if (canAddFixedVelocity) {
                    menu.AddItem("Fixed Velocity", () => AddProperty(PropertyType.FixedVelocity));
                }
                if (canAddHeart) {
                    menu.AddItem("Heart", () => AddProperty(PropertyType.Heart));
                }
                if (canAddFriction) {
                    menu.AddItem("Friction", () => AddProperty(PropertyType.Friction));
                }
                if (canAddResistance) {
                    menu.AddItem("Resistance", () => AddProperty(PropertyType.Resistance));
                }
            });
        }

        private void SetTime(float time, bool snap = false) {
            time = _data.ClampTime(time);

            if (snap) {
                time = math.round(time / SNAPPING) * SNAPPING;
            }

            _data.Time = time;
        }

        private void CopyKeyframes() {
            _clipboard.Clear();

            float time = float.MaxValue;
            bool foundAny = false;

            foreach (var (type, propertyData) in _data.Properties) {
                if (!propertyData.Visible) continue;
                foreach (var keyframe in propertyData.Keyframes) {
                    if (keyframe.Selected) {
                        time = math.min(time, keyframe.Time);
                        foundAny = true;
                    }
                }
            }

            if (!foundAny) return;

            foreach (var (type, propertyData) in _data.Properties) {
                if (!propertyData.Visible) continue;
                foreach (var keyframe in propertyData.Keyframes) {
                    if (keyframe.Selected) {
                        float offset = keyframe.Time - time;
                        _clipboard.Add(new KeyframeData(type, keyframe, offset));
                    }
                }
            }
        }

        private void PasteKeyframes(List<KeyframeData> clipboard, float pasteTime) {
            DeselectAllKeyframes();

            foreach (var copiedKeyframe in clipboard) {
                float time = pasteTime + copiedKeyframe.Offset;
                time = _data.ClampTime(time);

                var newKeyframe = copiedKeyframe.Value;
                newKeyframe.Time = time;

                var adapter = PropertyAdapter.GetAdapter(copiedKeyframe.Type);
                bool keyframeExists = FindKeyframe(copiedKeyframe.Type, time, out var keyframe);

                if (keyframeExists) {
                    newKeyframe.Id = keyframe.Id;
                    adapter.UpdateKeyframe(_data.Entity, newKeyframe);
                }
                else {
                    newKeyframe.Id = Uuid.Create();
                    adapter.AddKeyframe(_data.Entity, newKeyframe);
                }
            }

            clipboard.Clear();

            UpdateKeyframes();
            UpdateSelectionState();
            MarkTrackDirty();
        }

        private void RemoveSelectedKeyframes() {
            bool anyRemoved = false;
            foreach (var (type, propertyData) in _data.Properties) {
                if (!propertyData.Visible || propertyData.SelectedKeyframeCount == 0) continue;
                var adapter = PropertyAdapter.GetAdapter(type);
                foreach (var keyframe in _data.Properties[type].Keyframes) {
                    if (!keyframe.Selected) continue;
                    adapter.RemoveKeyframe(_data.Entity, keyframe.Id);
                }
                anyRemoved = true;
            }

            if (anyRemoved) {
                UpdateKeyframes();
                UpdateSelectionState();
                MarkTrackDirty();
            }
        }

        public bool CanCopy() => _data.Active && _data.SelectedKeyframeCount > 0;
        public bool CanPaste() => _data.Active && _clipboard.Count > 0;
        public bool CanDelete() => _data.Active && _data.SelectedKeyframeCount > 0;
        public bool CanCut() => CanCopy();
        public bool CanSelectAll() => _data.Active;
        public bool CanDeselectAll() => _data.Active && _data.SelectedKeyframeCount > 0;
        public bool CanFocus() => false;

        public void Copy() {
            CopyKeyframes();
        }

        public void Paste(float2? worldPosition = null) {
            if (CanPaste()) {
                PasteKeyframes(_clipboard, _data.Time);
            }
        }

        public void Delete() {
            if (CanDelete()) {
                RemoveSelectedKeyframes();
            }
        }

        public void Cut() {
            if (CanCut()) {
                Copy();
                Delete();
            }
        }

        public void SelectAll() {
            SelectAllProperties();
            SelectAllKeyframes();
        }

        public void DeselectAll() {
            DeselectAllKeyframes();
            DeselectAllProperties();
        }

        public void Focus() { }

        public bool IsInBounds(Vector2 mousePosition) {
            return _timeline.worldBound.Contains(mousePosition);
        }

        private void ApplyEasingToSelectedKeyframes(EasingType easing) {
            Undo.Record();
            easing.GetEasingHandles(out float tangent, out float weight, out _, out _);

            foreach (var (type, propertyData) in _data.Properties) {
                if (!propertyData.Visible) continue;
                var adapter = PropertyAdapter.GetAdapter(type);

                int selectedCount = 0;
                int firstSelected = -1;
                for (int i = 0; i < propertyData.Keyframes.Count; i++) {
                    if (propertyData.Keyframes[i].Selected) {
                        if (firstSelected == -1) firstSelected = i;
                        selectedCount++;
                    }
                }

                if (selectedCount == 0) continue;

                if (selectedCount == 1) {
                    var keyframe = propertyData.Keyframes[firstSelected];
                    adapter.UpdateKeyframe(_data.Entity, keyframe.WithEasing(tangent, weight));
                }
                else {
                    int prev = -1;
                    for (int i = 0; i < propertyData.Keyframes.Count; i++) {
                        if (!propertyData.Keyframes[i].Selected) continue;
                        if (prev != -1 && i == prev + 1) {
                            adapter.UpdateKeyframe(_data.Entity, propertyData.Keyframes[prev].WithOutEasing(tangent, weight));
                            adapter.UpdateKeyframe(_data.Entity, propertyData.Keyframes[i].WithInEasing(tangent, weight));
                        }
                        prev = i;
                    }
                }
            }

            MarkTrackDirty();
        }

        private EasingType? GetCurrentEasingType() {
            EasingType? result = null;
            bool hasSelection = false;
            bool isSingleSelection = _data.SelectedKeyframeCount == 1;

            foreach (var (type, propertyData) in _data.Properties) {
                if (!propertyData.Visible) continue;

                for (int i = 0; i < propertyData.Keyframes.Count; i++) {
                    var keyframe = propertyData.Keyframes[i];
                    if (!keyframe.Selected) continue;

                    hasSelection = true;

                    if (isSingleSelection) {
                        if (keyframe.InInterpolation != InterpolationType.Bezier ||
                            keyframe.OutInterpolation != InterpolationType.Bezier) {
                            return null;
                        }
                        return UI.Extensions.GetEasingFromWeights(keyframe.InWeight, keyframe.OutWeight);
                    }

                    if (i > 0 && propertyData.Keyframes[i - 1].Selected &&
                        keyframe.InInterpolation == InterpolationType.Bezier) {
                        var easing = UI.Extensions.GetEasingFromWeight(keyframe.InWeight, true);
                        if (result == null) result = easing;
                        else if (result != easing) return null;
                    }

                    if (i < propertyData.Keyframes.Count - 1 &&
                        propertyData.Keyframes[i + 1].Selected &&
                        keyframe.OutInterpolation == InterpolationType.Bezier) {
                        var easing = UI.Extensions.GetEasingFromWeight(keyframe.OutWeight, false);
                        if (result == null) result = easing;
                        else if (result != easing) return null;
                    }
                }
            }

            return hasSelection ? result : null;
        }

        private HandleType? GetSelectedKeyframesHandleType() {
            HandleType? commonHandleType = null;
            bool firstKeyframe = true;

            foreach (var (type, propertyData) in _data.Properties) {
                if (!propertyData.Visible) continue;

                foreach (var keyframe in propertyData.Keyframes) {
                    if (!keyframe.Selected) continue;

                    if (firstKeyframe) {
                        commonHandleType = keyframe.HandleType;
                        firstKeyframe = false;
                    }
                    else if (commonHandleType != keyframe.HandleType) {
                        return null;
                    }
                }
            }

            return commonHandleType;
        }

        private void SetKeyframeHandleType(HandleType handleType) {
            foreach (var (type, propertyData) in _data.Properties) {
                if (!propertyData.Visible) continue;
                var adapter = PropertyAdapter.GetAdapter(type);

                foreach (var keyframe in propertyData.Keyframes) {
                    if (!keyframe.Selected) continue;

                    var updatedKeyframe = new Keyframe {
                        Id = keyframe.Id,
                        Time = keyframe.Time,
                        Value = keyframe.Value,
                        InInterpolation = keyframe.InInterpolation,
                        OutInterpolation = keyframe.OutInterpolation,
                        HandleType = handleType,
                        Flags = keyframe.Flags,
                        InTangent = keyframe.InTangent,
                        OutTangent = keyframe.OutTangent,
                        InWeight = keyframe.InWeight,
                        OutWeight = keyframe.OutWeight,
                        Selected = keyframe.Selected
                    };

                    adapter.UpdateKeyframe(_data.Entity, updatedKeyframe);
                }
            }

            MarkTrackDirty();
        }

        private void OnTimelineOffsetChange(TimelineOffsetChangeEvent evt) {
            ref var uiState = ref SystemAPI.GetSingletonRW<UIState>().ValueRW;
            uiState.TimelineOffset = evt.Offset;
        }

        private void OnTimelineZoomChange(TimelineZoomChangeEvent evt) {
            ref var uiState = ref SystemAPI.GetSingletonRW<UIState>().ValueRW;
            uiState.TimelineZoom = evt.Zoom;
        }
    }
}
