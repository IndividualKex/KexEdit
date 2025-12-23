using System;
using System.Collections.Generic;
using KexEdit.Legacy;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;
using static KexEdit.UI.Constants;

namespace KexEdit.UI {
    public class TrainCarCountDialog : VisualElement {
        private Action _onClose;
        private VisualElement _panel;
        private string _currentTrainStyle;
        private TrainStyleConfig _trainConfig;
        private IntegerField _carCountField;
        private int _defaultCarCount;

        public TrainCarCountDialog(Action onClose) {
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

            LoadCurrentSettings();
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
                }
                evt.StopPropagation();
            });

            schedule.Execute(() => { _panel.style.opacity = 1f; });
        }

        private void LoadCurrentSettings() {
            _currentTrainStyle = Preferences.CurrentTrainStyle;
            _trainConfig = TrainStyleResourceLoader.LoadConfig(_currentTrainStyle);
            _defaultCarCount = _trainConfig?.CarCount ?? 1;
        }

        private void CreateUI() {
            var title = new Label("Train Car Count") {
                style = {
                    fontSize = 14, unityFontStyleAndWeight = FontStyle.Bold,
                    color = s_TextColor, marginBottom = 12f, unityTextAlign = TextAnchor.UpperCenter
                }
            };
            _panel.Add(title);

            CreateCarCountField();
            CreateButtons();
            Add(_panel);
        }

        private void CreateCarCountField() {
            var fieldContainer = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginBottom = 8f
                }
            };

            var countLabel = new Label("Car Count:") {
                style = {
                    fontSize = 12,
                    color = s_TextColor,
                    marginRight = 8f,
                    width = 80f
                }
            };

            int currentValue = TrainCarCountPreferences.GetCarCount(_currentTrainStyle, _defaultCarCount);
            _carCountField = new IntegerField {
                value = currentValue,
                isDelayed = true,
                style = { width = 60f, marginRight = 8f }
            };

            _carCountField.RegisterValueChangedCallback(evt => {
                int clampedValue = Mathf.Clamp(evt.newValue, 1, 32);
                if (evt.newValue != clampedValue) {
                    _carCountField.SetValueWithoutNotify(clampedValue);
                }
                ApplyCarCountChange(clampedValue);
            });

            fieldContainer.Add(countLabel);
            fieldContainer.Add(_carCountField);
            _panel.Add(fieldContainer);
        }

        private void CreateButtons() {
            var buttonContainer = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    justifyContent = Justify.FlexEnd,
                    marginTop = 12f
                }
            };

            var resetButton = new Label("Reset to Default") {
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

            resetButton.RegisterCallback<MouseDownEvent>(_ => ResetToDefault());
            closeButton.RegisterCallback<MouseDownEvent>(_ => Close());

            buttonContainer.Add(resetButton);
            buttonContainer.Add(closeButton);
            _panel.Add(buttonContainer);
        }

        private void ResetToDefault() {
            _carCountField.SetValueWithoutNotify(_defaultCarCount);
            TrainCarCountPreferences.ResetCarCount(_currentTrainStyle);
            TriggerTrainStyleReload();
        }

        private void ApplyCarCountChange(int newValue) {
            if (newValue == _defaultCarCount) {
                TrainCarCountPreferences.ResetCarCount(_currentTrainStyle);
            }
            else {

                TrainCarCountPreferences.SetCarCount(_currentTrainStyle, newValue);
            }
            TriggerTrainStyleReload();
        }

        private void TriggerTrainStyleReload() {
            var world = World.DefaultGameObjectInjectionWorld;
            var singletonEntity = world.EntityManager.CreateEntityQuery(typeof(EditorTrainStyleSingleton)).GetSingletonEntity();
            var singleton = world.EntityManager.GetComponentData<EditorTrainStyleSingleton>(singletonEntity);
            singleton.Dirty = true;
            world.EntityManager.SetComponentData(singletonEntity, singleton);
        }

        private void Close() {
            _carCountField.Blur();

            schedule.Execute(() => {
                RemoveFromHierarchy();
                _onClose?.Invoke();
            });
        }
    }
}
