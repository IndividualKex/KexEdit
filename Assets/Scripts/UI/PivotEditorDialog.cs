using System;
using System.Collections.Generic;
using KexEdit.Legacy;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;
using static KexEdit.UI.Constants;

namespace KexEdit.UI {
    public class PivotEditorDialog : VisualElement {
        private Action _onClose;
        private VisualElement _panel;
        private FloatField _offsetField;
        private VisualElement _carButtonsContainer;
        private float _currentOffset;
        private TrainStyleConfig _trainStyleConfig;

        public PivotEditorDialog(Action onClose) {
            _onClose = onClose;

            focusable = true;

            style.position = Position.Absolute;
            style.left = 0;
            style.right = 0;
            style.top = 0;
            style.bottom = 0;
            style.backgroundColor = new Color(0, 0, 0, 0.5f);
            style.alignItems = Align.Center;
            style.justifyContent = Justify.Center;

            _panel = new VisualElement {
                style = {
                    backgroundColor = s_BackgroundColor,
                    borderTopLeftRadius = 3f, borderTopRightRadius = 3f,
                    borderBottomLeftRadius = 3f, borderBottomRightRadius = 3f,
                    borderTopWidth = 1f, borderRightWidth = 1f, borderBottomWidth = 1f, borderLeftWidth = 1f,
                    borderTopColor = s_BorderColor, borderRightColor = s_BorderColor,
                    borderBottomColor = s_BorderColor, borderLeftColor = s_BorderColor,
                    paddingTop = 16f, paddingRight = 16f, paddingBottom = 16f, paddingLeft = 16f,
                    minWidth = 350f
                }
            };

            LoadCurrentPivot();
            LoadTrainStyleData();
            CreateUI();

            _panel.style.opacity = 0f;
            _panel.style.transitionProperty = new List<StylePropertyName> { "opacity" };
            _panel.style.transitionDuration = new List<TimeValue> { new(100, TimeUnit.Millisecond) };
            _panel.style.transitionTimingFunction = new List<EasingFunction> { EasingMode.EaseOutCubic };

            RegisterCallback<MouseDownEvent>(evt => {
                if (evt.target == this) {
                    Close();
                    evt.StopPropagation();
                }
            });

            RegisterCallback<KeyDownEvent>(evt => {
                if (evt.keyCode == KeyCode.Escape) {
                    Close();
                    evt.StopPropagation();
                }
            });

            schedule.Execute(() => { _panel.style.opacity = 1f; });
        }

        private void LoadCurrentPivot() {
            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            var stateEntity = entityManager.CreateEntityQuery(typeof(ReadPivot)).GetSingletonEntity();
            var readPivot = entityManager.GetComponentData<ReadPivot>(stateEntity);
            _currentOffset = readPivot.Offset;
        }

        private void LoadTrainStyleData() {
            var currentTrainStyle = Preferences.CurrentTrainStyle;
            _trainStyleConfig = TrainStyleResourceLoader.LoadConfig(currentTrainStyle);

            if (_trainStyleConfig != null) {
                int carCount = TrainCarCountPreferences.GetCarCount(currentTrainStyle, _trainStyleConfig.CarCount);
                _trainStyleConfig.CarCount = carCount;
            }
            else {
                _trainStyleConfig = new TrainStyleConfig {
                    CarCount = 1,
                    CarSpacing = 3.0f
                };
            }
        }

        private void CreateUI() {
            var title = new Label("Pivot") {
                style = {
                    fontSize = 14, unityFontStyleAndWeight = FontStyle.Bold,
                    color = s_TextColor, marginBottom = 12f, unityTextAlign = TextAnchor.UpperCenter
                }
            };
            _panel.Add(title);

            CreateOffsetField();
            CreateCarButtons();
            CreateBottomButtons();

            Add(_panel);
        }

        private void CreateOffsetField() {
            var fieldContainer = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginBottom = 12f
                }
            };

            var offsetLabel = new Label("Offset:") {
                style = {
                    fontSize = 12,
                    color = s_TextColor,
                    marginRight = 8f,
                    width = 60f
                }
            };

            _offsetField = new FloatField {
                value = _currentOffset,
                style = { flexGrow = 1f }
            };

            _offsetField.RegisterValueChangedCallback(evt => {
                ApplyPivotChange(evt.newValue);
            });

            var unitsLabel = new Label(Units.GetDistanceUnitsSuffix()) {
                style = {
                    fontSize = 12,
                    color = new Color(0.7f, 0.7f, 0.7f),
                    marginLeft = 8f
                }
            };

            fieldContainer.Add(offsetLabel);
            fieldContainer.Add(_offsetField);
            fieldContainer.Add(unitsLabel);
            _panel.Add(fieldContainer);
        }

        private void CreateCarButtons() {
            if (_trainStyleConfig.CarCount <= 1) {
                return;
            }

            _carButtonsContainer = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    flexWrap = Wrap.Wrap,
                    justifyContent = Justify.Center,
                    marginBottom = 12f
                }
            };

            var carOffsets = TrainCarPositionCalculator.GetCarOffsetsFromConfig(_trainStyleConfig);

            for (int i = 0; i < _trainStyleConfig.CarCount; i++) {
                int carIndex = i;
                float carOffset = i < carOffsets.Count ? carOffsets[i] : 0f;

                var carButton = new Label($"Car {i + 1}") {
                    style = {
                        marginTop = 4f, marginRight = 4f, marginBottom = 4f, marginLeft = 4f,
                        paddingTop = 6f, paddingRight = 10f, paddingBottom = 6f, paddingLeft = 10f,
                        backgroundColor = s_BackgroundColor, color = s_TextColor,
                        unityTextAlign = TextAnchor.MiddleCenter,
                        fontSize = 11
                    }
                };

                carButton.RegisterCallback<MouseEnterEvent>(_ => carButton.style.backgroundColor = s_HoverColor);
                carButton.RegisterCallback<MouseLeaveEvent>(_ => carButton.style.backgroundColor = s_BackgroundColor);
                carButton.RegisterCallback<MouseDownEvent>(_ => {
                    _offsetField.SetValueWithoutNotify(carOffset);
                    ApplyPivotChange(carOffset);
                });

                _carButtonsContainer.Add(carButton);
            }

            _panel.Add(_carButtonsContainer);
        }

        private void CreateBottomButtons() {
            var buttonContainer = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    justifyContent = Justify.FlexEnd,
                    marginTop = 12f
                }
            };

            var resetButton = new Label("Reset to Center") {
                style = {
                    marginRight = 8f,
                    paddingTop = 8f, paddingRight = 12f, paddingBottom = 8f, paddingLeft = 12f,
                    backgroundColor = s_BackgroundColor, color = s_TextColor,
                    unityTextAlign = TextAnchor.MiddleCenter
                }
            };

            var closeButton = new Label("Close") {
                style = {
                    paddingTop = 8f, paddingRight = 12f, paddingBottom = 8f, paddingLeft = 12f,
                    backgroundColor = s_BackgroundColor, color = s_TextColor,
                    unityTextAlign = TextAnchor.MiddleCenter
                }
            };

            resetButton.RegisterCallback<MouseEnterEvent>(_ => resetButton.style.backgroundColor = s_HoverColor);
            resetButton.RegisterCallback<MouseLeaveEvent>(_ => resetButton.style.backgroundColor = s_BackgroundColor);
            closeButton.RegisterCallback<MouseEnterEvent>(_ => closeButton.style.backgroundColor = s_HoverColor);
            closeButton.RegisterCallback<MouseLeaveEvent>(_ => closeButton.style.backgroundColor = s_BackgroundColor);

            resetButton.RegisterCallback<MouseDownEvent>(_ => {
                _offsetField.SetValueWithoutNotify(0f);
                ApplyPivotChange(0f);
            });
            closeButton.RegisterCallback<MouseDownEvent>(_ => Close());

            buttonContainer.Add(resetButton);
            buttonContainer.Add(closeButton);
            _panel.Add(buttonContainer);
        }

        private void ApplyPivotChange(float newOffset) {
            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            var stateEntity = entityManager.CreateEntityQuery(typeof(ReadPivot)).GetSingletonEntity();

            var readPivot = new ReadPivot { Offset = newOffset };
            entityManager.SetComponentData(stateEntity, readPivot);

            _currentOffset = newOffset;
        }

        private void Close() {
            _offsetField.Blur();

            schedule.Execute(() => {
                RemoveFromHierarchy();
                _onClose?.Invoke();
            });
        }
    }
}
