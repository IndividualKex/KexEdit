using Unity.Mathematics;
using Unity.Properties;
using UnityEngine.UIElements;
using static KexEdit.UI.Constants;

namespace KexEdit.UI.Timeline {
    public class TimelineOutliner : VisualElement {
        private VisualElement _header;
        private Label _headerLabel;
        private FloatField _timeField;
        private CurveButton _curveButton;
        private VisualElement _propertiesContainer;
        private AddPropertyButton _addPropertyButton;

        private TimelineData _data;

        public TimelineOutliner() {
            style.position = Position.Relative;
            style.flexDirection = FlexDirection.Column;
            style.flexGrow = 0;
            style.width = 384f;
            style.alignItems = Align.Stretch;
            style.paddingLeft = 0f;
            style.paddingRight = 0f;
            style.paddingTop = 0f;
            style.paddingBottom = 0f;
            style.marginLeft = 0f;
            style.marginRight = 0f;
            style.marginTop = 0f;
            style.marginBottom = 0f;
            style.backgroundColor = s_DarkBackgroundColor;

            _header = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    height = 20f,
                    paddingLeft = 4f,
                    paddingRight = 4f,
                    paddingTop = 0f,
                    paddingBottom = 0f,
                    marginLeft = 0f,
                    marginRight = 0f,
                    marginTop = 0f,
                    marginBottom = 0f,
                    backgroundColor = s_AltBackgroundColor,
                }
            };
            Add(_header);

            _headerLabel = new Label("") {
                style = {
                    flexGrow = 1f
                }
            };
            _headerLabel.AddToClassList("text-light");
            _header.Add(_headerLabel);

            _timeField = new FloatField {
                formatString = "0.###",
                isDelayed = true,
                style = {
                    width = 44f,
                    paddingLeft = 0f,
                    paddingRight = 0f,
                    paddingTop = 0f,
                    paddingBottom = 0f,
                    marginTop = 2f,
                    marginBottom = 2f,
                }
            };
            _header.Add(_timeField);

            _curveButton = new CurveButton();
            _header.Add(_curveButton);

            var content = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Column,
                    flexGrow = 1f,
                    marginTop = 20f,
                }
            };
            Add(content);

            _propertiesContainer = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Column,
                    flexGrow = 0f,
                }
            };
            content.Add(_propertiesContainer);

            _addPropertyButton = new AddPropertyButton();
            content.Add(_addPropertyButton);

            var timeBinding = new DataBinding {
                dataSourcePath = new PropertyPath(nameof(TimelineData.Time)),
                bindingMode = BindingMode.ToTarget
            };
            timeBinding.sourceToUiConverters.AddConverter((ref float value) => {
                return _data.DurationType switch {
                    DurationType.Distance => Units.DistanceToDisplay(value),
                    _ => value,
                };
            });
            _timeField.SetBinding("value", timeBinding);

            var activeBinding = new DataBinding {
                dataSourcePath = new PropertyPath(nameof(TimelineData.Active)),
                bindingMode = BindingMode.ToTarget
            };

            activeBinding.sourceToUiConverters.AddConverter((ref bool value) => new StyleFloat(value ? 1f : 0.5f));
            activeBinding.sourceToUiConverters.AddConverter((ref bool value) =>
                new StyleEnum<DisplayStyle>(value ? DisplayStyle.Flex : DisplayStyle.None));

            SetBinding("style.opacity", activeBinding);
            _header.SetBinding("style.display", activeBinding);
            _timeField.SetBinding("style.display", activeBinding);
            _curveButton.SetBinding("style.display", activeBinding);

            var displayNameBinding = new DataBinding {
                dataSourcePath = new PropertyPath(nameof(TimelineData.DisplayName)),
                bindingMode = BindingMode.ToTarget
            };
            _headerLabel.SetBinding("text", displayNameBinding);
        }

        public void Initialize(TimelineData data) {
            _data = data;

            _curveButton.Initialize(data);
            _addPropertyButton.Initialize(data);

            foreach (var property in data.OrderedProperties) {
                var propertyData = data.Properties[property];
                var timelineProperty = new TimelineProperty(propertyData);
                _propertiesContainer.Add(timelineProperty);
            }

            RegisterCallback<MouseDownEvent>(OnMouseDown);
            _timeField.RegisterValueChangedCallback<float>(OnTimeFieldChanged);
        }

        private void OnMouseDown(MouseDownEvent evt) {
            this.Send<OutlineMouseDownEvent>();
        }

        private void OnTimeFieldChanged(ChangeEvent<float> evt) {
            float newValue = _data.DurationType switch {
                DurationType.Distance => Units.DisplayToDistance(evt.newValue),
                _ => evt.newValue,
            };
            if (math.abs(newValue - _data.Time) < 1e-3f) return;
            var e = this.GetPooled<TimeChangeEvent>();
            e.Time = newValue;
            e.Snap = false;
            this.Send(e);
        }
    }
}
