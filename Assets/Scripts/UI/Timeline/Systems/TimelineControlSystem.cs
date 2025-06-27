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
            _data = new TimelineData();
            foreach (PropertyType propertyType in System.Enum.GetValues(typeof(PropertyType))) {
                _data.OrderedProperties.Add(propertyType);
                _data.Properties.Add(propertyType, new PropertyData {
                    Type = propertyType
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
        }

        protected override void OnStartRunning() {
            var root = UIService.Instance.UIDocument.rootVisualElement;
            _timeline = root.Q<Timeline>();
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
            _timeline.RegisterCallback<JumpToKeyframeEvent>(OnJumpToKeyframe);
            _timeline.RegisterCallback<KeyframeButtonClickEvent>(OnKeyframeButtonClick);
            _timeline.RegisterCallback<DragKeyframesEvent>(OnDragKeyframes);
            _timeline.RegisterCallback<DragBezierHandleEvent>(OnDragBezierHandle);
            _timeline.RegisterCallback<SelectKeyframesEvent>(OnSelectKeyframes);
            _timeline.RegisterCallback<AddKeyframeEvent>(OnAddKeyframe);

            EditOperationsSystem.RegisterHandler(this);
        }

        protected override void OnDestroy() {
            EditOperationsSystem.UnregisterHandler(this);
            _data.Dispose();
        }

        protected override void OnUpdate() {
            UpdateActive();
            SyncWithPlayback();
            UpdatePlayhead();
            UpdateTimelineData();
            _timeline.Draw();
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
                    float timelineTime = cart.Position / HZ;
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

            if (!_data.Active) return;

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
                if (propertyData.Selected && !propertyData.DrawReadOnly) {
                    foreach (var keyframe in propertyData.Keyframes) {
                        hasAnyKeyframes = true;
                        tempMin = math.min(tempMin, keyframe.Value);
                        tempMax = math.max(tempMax, keyframe.Value);

                        if (keyframe.OutInterpolation == InterpolationType.Bezier ||
                            keyframe.OutInterpolation == InterpolationType.ContinuousBezier) {
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

                        if (keyframe.InInterpolation == InterpolationType.Bezier ||
                            keyframe.InInterpolation == InterpolationType.ContinuousBezier) {
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
                else if (_data.ViewMode == TimelineViewMode.DopeSheet) {
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
                        if (interpolationType == InterpolationType.ContinuousBezier && changed) {
                            updatedKeyframe.InTangent = updatedKeyframe.OutTangent;
                        }
                    }
                    else {
                        if (inHandle && updatedKeyframe.InInterpolation != interpolationType) {
                            updatedKeyframe.InInterpolation = interpolationType;
                            changed = true;
                            if (interpolationType != InterpolationType.ContinuousBezier &&
                                updatedKeyframe.OutInterpolation == InterpolationType.ContinuousBezier) {
                                updatedKeyframe.OutInterpolation = InterpolationType.Bezier;
                            }
                        }
                        else if (!inHandle && updatedKeyframe.OutInterpolation != interpolationType) {
                            updatedKeyframe.OutInterpolation = interpolationType;
                            changed = true;
                            if (interpolationType != InterpolationType.ContinuousBezier &&
                                updatedKeyframe.InInterpolation == InterpolationType.ContinuousBezier) {
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
                InterpolationType newInType = inHandle ? interpolationType : (currentInType ?? InterpolationType.ContinuousBezier);
                InterpolationType newOutType = !inHandle ? interpolationType : (currentOutType ?? InterpolationType.ContinuousBezier);
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
            TimelineViewMode fromMode = _data.ViewMode;
            TimelineViewMode toMode = fromMode == TimelineViewMode.DopeSheet ?
                TimelineViewMode.Curve :
                TimelineViewMode.DopeSheet;

            _data.ViewMode = toMode;

            if (fromMode == TimelineViewMode.Curve && toMode == TimelineViewMode.DopeSheet) {
                foreach (var (type, propertyData) in _data.Properties) {
                    if (propertyData.Selected) {
                        SelectAllKeyframesForProperty(type);
                    }
                }
            }
            else if (fromMode == TimelineViewMode.DopeSheet && toMode == TimelineViewMode.Curve) {
                DeselectAllKeyframes();
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
                        cart.ValueRW.Position = _data.Time * HZ;
                        break;
                    }
                }
            }
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
                    DeselectProperty(evt.Type);
                    if (_data.ViewMode == TimelineViewMode.DopeSheet) {
                        DeselectAllKeyframesForProperty(evt.Type);
                    }
                }
                else {
                    SelectProperty(evt.Type);
                    if (_data.ViewMode == TimelineViewMode.DopeSheet) {
                        SelectAllKeyframesForProperty(evt.Type);
                    }
                }
            }
            else if (!propertyData.Selected) {
                DeselectAll();
                SelectProperty(evt.Type);
                if (_data.ViewMode == TimelineViewMode.DopeSheet) {
                    SelectAllKeyframesForProperty(evt.Type);
                }
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
            ShowKeyframeValueEditor(evt.Keyframe, evt.MousePosition);
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
                        var selectedKeyframe = GetSelectedKeyframe();
                        ShowKeyframeValueEditor(selectedKeyframe, evt.MousePosition);
                    });
                    menu.AddSeparator();
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
                }
            });
        }

        private void AddInterpolationMenu(ContextMenu menu) {
            var (currentInType, currentOutType) = GetSelectedKeyframesInterpolation();

            menu.AddSeparator();

            menu.AddItem("Constant", () => {
                Undo.Record();
                SetKeyframeInterpolation(InterpolationType.Constant, false, true);
            }, isChecked: currentInType == InterpolationType.Constant && currentOutType == InterpolationType.Constant);

            menu.AddItem("Linear", () => {
                Undo.Record();
                SetKeyframeInterpolation(InterpolationType.Linear, false, true);
            }, isChecked: currentInType == InterpolationType.Linear && currentOutType == InterpolationType.Linear);

            menu.AddItem("Bezier", () => {
                Undo.Record();
                SetKeyframeInterpolation(InterpolationType.Bezier, false, true);
            }, isChecked: currentInType == InterpolationType.Bezier && currentOutType == InterpolationType.Bezier);

            menu.AddItem("Continuous Bezier", () => {
                Undo.Record();
                SetKeyframeInterpolation(InterpolationType.ContinuousBezier, false, true);
            }, isChecked: currentInType == InterpolationType.ContinuousBezier && currentOutType == InterpolationType.ContinuousBezier);

            menu.AddSeparator();

            menu.AddSubmenu("In Tangent", inSubmenu => {
                AddInterpolationOption(inSubmenu, "Constant", InterpolationType.Constant,
                    currentInType == InterpolationType.Constant, false, true);
                AddInterpolationOption(inSubmenu, "Linear", InterpolationType.Linear,
                    currentInType == InterpolationType.Linear, false, true);
                AddInterpolationOption(inSubmenu, "Bezier", InterpolationType.Bezier,
                    currentInType == InterpolationType.Bezier, false, true);
            });

            menu.AddSubmenu("Out Tangent", outSubmenu => {
                AddInterpolationOption(outSubmenu, "Constant", InterpolationType.Constant,
                    currentOutType == InterpolationType.Constant, false, false);
                AddInterpolationOption(outSubmenu, "Linear", InterpolationType.Linear,
                    currentOutType == InterpolationType.Linear, false, false);
                AddInterpolationOption(outSubmenu, "Bezier", InterpolationType.Bezier,
                    currentOutType == InterpolationType.Bezier, false, false);
            });
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
                    var playheadPoint = GetPlayheadPoint();
                    float value = targetValueType switch {
                        TargetValueType.Roll => playheadPoint.Roll,
                        TargetValueType.Pitch => playheadPoint.GetPitch(),
                        TargetValueType.Yaw => playheadPoint.GetYaw(),
                        TargetValueType.X => playheadPoint.Position.x,
                        TargetValueType.Y => playheadPoint.Position.y,
                        TargetValueType.Z => playheadPoint.Position.z,
                        TargetValueType.NormalForce => playheadPoint.NormalForce,
                        TargetValueType.LateralForce => playheadPoint.LateralForce,
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

                    time = _data.ClampTime(time);

                    if (_data.ViewMode == TimelineViewMode.DopeSheet && !evt.ShiftKey) {
                        time = math.round(time / SNAPPING) * SNAPPING;
                    }

                    if (_data.ViewMode == TimelineViewMode.Curve) {
                        if (!evt.StartValues.TryGetValue(keyframe.Id, out float startValue)) continue;
                        value = startValue + evt.ValueDelta;
                        value = evt.Bounds.ClampToVisualBounds(value, evt.ContentHeight);
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
                    if (evt.Keyframe.Value.OutInterpolation == InterpolationType.ContinuousBezier) {
                        evt.Keyframe.Value.InTangent = tangent;
                    }
                }
                else {
                    evt.Keyframe.Value.InWeight = weight;
                    evt.Keyframe.Value.InTangent = tangent;
                    if (evt.Keyframe.Value.InInterpolation == InterpolationType.ContinuousBezier) {
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
    }
}
