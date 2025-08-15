using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;
using System;
using System.Collections.Generic;
using static KexEdit.Constants;
using static KexEdit.UI.Constants;

namespace KexEdit.UI {
    public class VideoControls : VisualElement {
        private static readonly string s_TimeString = new('\0', 32);

        private VisualElement _playButton;
        private VisualElement _progressContainer;
        private VisualElement _playhead;
        private Label _playButtonLabel;
        private Label _timeLabel;
        private VisualElement _fullscreenButton;

        private VideoControlData _data;
        private bool _isDragging;

        public event Action TogglePlayPause;
        public event Action<float> SetProgress;
        public event Action ToggleFullscreen;

        public VideoControls(VideoControlData data) {
            _data = data;
            dataSource = _data;

            style.position = Position.Absolute;
            style.bottom = 0;
            style.left = 0;
            style.right = 0;
            style.height = 32f;
            style.backgroundColor = s_DarkBackgroundColor;
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            style.paddingLeft = 8f;
            style.paddingRight = 8f;
            style.paddingTop = 4f;
            style.paddingBottom = 4f;
            style.transitionProperty = new List<StylePropertyName> { "bottom" };
            style.transitionDuration = new List<TimeValue> { new(300, TimeUnit.Millisecond) };
            style.transitionTimingFunction = new List<EasingFunction> { EasingMode.EaseOutCubic };

            _playButton = new VisualElement {
                style = {
                    width = 24f,
                    height = 24f,
                    backgroundColor = s_DarkBackgroundColor,
                    borderTopLeftRadius = 4f,
                    borderTopRightRadius = 4f,
                    borderBottomLeftRadius = 4f,
                    borderBottomRightRadius = 4f,
                    marginRight = 8f,
                    alignItems = Align.Center,
                    justifyContent = Justify.Center,
                }
            };

            _playButtonLabel = new Label("▶") {
                style = {
                    fontSize = 10f,
                    color = new Color(0.8f, 0.8f, 0.8f, 1f),
                    unityTextAlign = TextAnchor.MiddleCenter,
                    marginLeft = 1f
                }
            };

            _playButton.Add(_playButtonLabel);
            _playButton.RegisterCallback<MouseEnterEvent>(_ => _playButton.style.backgroundColor = s_BackgroundColor);
            _playButton.RegisterCallback<MouseLeaveEvent>(_ => _playButton.style.backgroundColor = s_DarkBackgroundColor);
            _playButton.RegisterCallback<MouseDownEvent>(evt => {
                if (evt.button == 0) {
                    TogglePlayPause?.Invoke();
                    evt.StopPropagation();
                }
            });

            Add(_playButton);

            _progressContainer = new VisualElement {
                style = {
                    flexGrow = 1f,
                    height = 24f,
                    marginRight = 8f,
                    alignItems = Align.Center,
                    justifyContent = Justify.Center,
                }
            };

            var ruler = new VisualElement {
                style = {
                    width = Length.Percent(100),
                    height = 2f,
                    backgroundColor = s_BackgroundColor,
                    borderTopLeftRadius = 1f,
                    borderTopRightRadius = 1f,
                    borderBottomLeftRadius = 1f,
                    borderBottomRightRadius = 1f,
                    overflow = Overflow.Visible,
                    position = Position.Relative,
                }
            };

            _playhead = new VisualElement {
                style = {
                    position = Position.Absolute,
                    left = 0,
                    top = -6f,
                    width = 0,
                    height = 0,
                    backgroundColor = Color.clear,
                    borderTopWidth = 12f,
                    borderLeftWidth = 4f,
                    borderRightWidth = 4f,
                    borderBottomWidth = 0,
                    borderTopColor = s_TextColor,
                    borderLeftColor = Color.clear,
                    borderRightColor = Color.clear,
                    borderBottomColor = Color.clear,
                    marginLeft = -4f,
                }
            };

            ruler.Add(_playhead);
            _progressContainer.Add(ruler);
            Add(_progressContainer);

            _timeLabel = new Label("0:00 / 0:00") {
                style = {
                    fontSize = 10f,
                    color = new Color(0.7f, 0.7f, 0.7f, 1f),
                    unityTextAlign = TextAnchor.MiddleCenter,
                    minWidth = 50f,
                }
            };

            Add(_timeLabel);

            _fullscreenButton = new VisualElement {
                style = {
                    width = 14f,
                    height = 14f,
                    backgroundColor = s_DarkBackgroundColor,
                    marginLeft = 16f,
                    backgroundImage = new StyleBackground(UIService.Instance.MaximizeTexture),
                    unityBackgroundImageTintColor = new Color(0.8f, 0.8f, 0.8f, 1f),
                }
            };

            _fullscreenButton.style.transitionProperty = new List<StylePropertyName> { "transform" };
            _fullscreenButton.style.transitionDuration = new List<TimeValue> { new(100, TimeUnit.Millisecond) };
            _fullscreenButton.style.transitionTimingFunction = new List<EasingFunction> { EasingMode.EaseOutCubic };

            _fullscreenButton.RegisterCallback<MouseEnterEvent>(_ => _fullscreenButton.style.scale = new Scale(new Vector3(1.1f, 1.1f, 1f)));
            _fullscreenButton.RegisterCallback<MouseLeaveEvent>(_ => _fullscreenButton.style.scale = new Scale(new Vector3(1f, 1f, 1f)));
            _fullscreenButton.RegisterCallback<MouseDownEvent>(evt => {
                if (evt.button == 0) {
                    ToggleFullscreen?.Invoke();
                    evt.StopPropagation();
                }
            });

            Add(_fullscreenButton);

            _progressContainer.RegisterCallback<MouseDownEvent>(OnMouseDown);
            _progressContainer.RegisterCallback<MouseMoveEvent>(OnMouseMove);
            _progressContainer.RegisterCallback<MouseUpEvent>(OnMouseUp);

            RegisterCallback<MouseDownEvent>(evt => evt.StopPropagation());
            RegisterCallback<ClickEvent>(evt => evt.StopPropagation());

            var displayBinding = new DataBinding {
                dataSourcePath = new PropertyPath(nameof(_data.TotalLength)),
                bindingMode = BindingMode.ToTarget,
            };
            displayBinding.sourceToUiConverters.AddConverter((ref float value) =>
                new StyleEnum<DisplayStyle>(value > 0f ? DisplayStyle.Flex : DisplayStyle.None));

            var playingBinding = new DataBinding {
                dataSourcePath = new PropertyPath(nameof(_data.IsPlaying)),
                bindingMode = BindingMode.ToTarget,
            };
            playingBinding.sourceToUiConverters.AddConverter((ref bool value) => value ? "■" : "▶");
            playingBinding.sourceToUiConverters.AddConverter((ref bool value) =>
                new StyleLength(value ? 0f : 1f));

            SetBinding("style.display", displayBinding);
            _playButtonLabel.SetBinding("text", playingBinding);
            _playButtonLabel.SetBinding("style.marginLeft", playingBinding);

            var progressBinding = new DataBinding {
                dataSourcePath = new PropertyPath(nameof(_data.Progress)),
                bindingMode = BindingMode.ToTarget,
            };
            progressBinding.sourceToUiConverters.AddConverter((ref float value) =>
                new StyleLength(Length.Percent(value * 100f)));
            _playhead.SetBinding("style.left", progressBinding);

            var fullscreenBinding = new DataBinding {
                dataSourcePath = new PropertyPath(nameof(_data.IsFullscreen)),
                bindingMode = BindingMode.ToTarget,
            };
            fullscreenBinding.sourceToUiConverters.AddConverter((ref bool value) =>
                new StyleBackground(value ? UIService.Instance.MinimizeTexture : UIService.Instance.MaximizeTexture));
            _fullscreenButton.SetBinding("style.backgroundImage", fullscreenBinding);

            var controlsVisibilityBinding = new DataBinding {
                dataSourcePath = new PropertyPath(nameof(_data.IsControlsVisible)),
                bindingMode = BindingMode.ToTarget,
            };
            controlsVisibilityBinding.sourceToUiConverters.AddConverter((ref bool value) =>
                new StyleLength(value ? 0f : -32f));
            SetBinding("style.bottom", controlsVisibilityBinding);
        }

        public void Draw() {
            UpdateTimeLabel();
        }

        private void OnMouseDown(MouseDownEvent evt) {
            if (evt.button != 0) return;
            _isDragging = true;
            _progressContainer.CaptureMouse();
            UpdateProgress(evt.localMousePosition.x);
            evt.StopPropagation();
        }

        private void OnMouseMove(MouseMoveEvent evt) {
            if (!_isDragging) return;
            UpdateProgress(evt.localMousePosition.x);
            evt.StopPropagation();
        }

        private void OnMouseUp(MouseUpEvent evt) {
            if (!_isDragging) return;
            _isDragging = false;
            _progressContainer.ReleaseMouse();
            UpdateProgress(evt.localMousePosition.x);
            evt.StopPropagation();
        }

        private void UpdateProgress(float mouseX) {
            float progress = Mathf.Clamp01(mouseX / _progressContainer.resolvedStyle.width);
            SetProgress?.Invoke(progress);
        }

        private void UpdateTimeLabel() {
            float currentTime = _data.Progress * _data.TotalLength;
            _timeLabel.text = FormatTimeRange(currentTime, _data.TotalLength);
            _timeLabel.MarkDirtyRepaint();
        }

        private unsafe string FormatTimeRange(float currentPoints, float totalPoints) {
            float currentSeconds = currentPoints / HZ;
            float totalSeconds = totalPoints / HZ;

            Span<char> buffer = stackalloc char[32];

            int currentMinutes = Mathf.FloorToInt(currentSeconds / 60f);
            int currentSecs = Mathf.FloorToInt(currentSeconds % 60f);
            int totalMinutes = Mathf.FloorToInt(totalSeconds / 60f);
            int totalSecs = Mathf.FloorToInt(totalSeconds % 60f);

            int pos = 0;

            if (!currentMinutes.TryFormat(buffer[pos..], out int charsWritten)) return "0:00 / 0:00";
            pos += charsWritten;

            buffer[pos++] = ':';

            if (!currentSecs.TryFormat(buffer[pos..], out charsWritten, "D2")) return "0:00 / 0:00";
            pos += charsWritten;

            buffer[pos++] = ' ';
            buffer[pos++] = '/';
            buffer[pos++] = ' ';

            if (!totalMinutes.TryFormat(buffer[pos..], out charsWritten)) return "0:00 / 0:00";
            pos += charsWritten;

            buffer[pos++] = ':';

            if (!totalSecs.TryFormat(buffer[pos..], out charsWritten, "D2")) return "0:00 / 0:00";
            pos += charsWritten;

            fixed (char* reusablePtr = s_TimeString) {
                for (int i = 0; i < pos; i++) {
                    reusablePtr[i] = buffer[i];
                }
                reusablePtr[pos] = '\0';
            }

            return s_TimeString;
        }
    }
}
