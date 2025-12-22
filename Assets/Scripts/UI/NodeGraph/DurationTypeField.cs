using System.Collections.Generic;
using KexEdit.Legacy;
using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;
using static KexEdit.UI.Constants;

namespace KexEdit.UI.NodeGraph {
    public class DurationTypeField : VisualElement {
        private static readonly Dictionary<DurationType, string> s_StringCache = new() {
            { DurationType.Time, "Time" },
            { DurationType.Distance, "Distance" },
        };

        private VisualElement _field;
        private Label _valueLabel;

        private NodeData _data;
        private DataBinding _valueBinding;

        public DurationTypeField() {
            style.position = Position.Relative;
            style.flexDirection = FlexDirection.Row;
            style.height = 24f;
            style.alignItems = Align.Center;
            style.paddingLeft = 8f;
            style.paddingRight = 0f;
            style.paddingTop = 0f;
            style.paddingBottom = 0f;
            style.marginLeft = 0f;
            style.marginRight = 0f;
            style.marginTop = 0f;
            style.marginBottom = 0f;

            var label = new Label("Type") {
                style = {
                    flexGrow = 1f,
                    paddingLeft = 0f,
                    paddingRight = 0f,
                    paddingTop = 0f,
                    paddingBottom = 0f,
                    marginLeft = 0f,
                    marginRight = 0f,
                    marginTop = 0f,
                    marginBottom = 0f,
                }
            };
            Add(label);

            _field = new VisualElement() {
                style = {
                    position = Position.Relative,
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    justifyContent = Justify.FlexStart,
                    flexGrow = 0f,
                    width = 80f,
                    height = 17f,
                    paddingLeft = 0f,
                    paddingRight = 4f,
                    paddingTop = 0f,
                    paddingBottom = 0f,
                    marginLeft = 3f,
                    marginRight = 3f,
                    marginTop = 4f,
                    marginBottom = 4f,
                    borderLeftWidth = 1f,
                    borderRightWidth = 1f,
                    borderTopWidth = 1f,
                    borderBottomWidth = 1f,
                    borderLeftColor = s_DarkBackgroundColor,
                    borderRightColor = s_DarkBackgroundColor,
                    borderTopColor = s_DarkBackgroundColor,
                    borderBottomColor = s_DarkBackgroundColor,
                    borderTopLeftRadius = 3f,
                    borderTopRightRadius = 3f,
                    borderBottomLeftRadius = 3f,
                    borderBottomRightRadius = 3f,
                    backgroundColor = s_HoverColor,
                }
            };
            Add(_field);

            _valueLabel = new Label() {
                style = {
                    flexGrow = 1f,
                    paddingLeft = 4f,
                    paddingRight = 4f,
                    paddingTop = 0f,
                    paddingBottom = 1f,
                    marginLeft = 0f,
                    marginRight = 0f,
                    marginTop = 0f,
                    marginBottom = 0f,
                    unityTextAlign = TextAnchor.MiddleLeft
                }
            };
            _field.Add(_valueLabel);

            var arrow = new VisualElement() {
                style = {
                    position = Position.Relative,
                    width = 12f,
                    height = 12f,
                    paddingLeft = 0f,
                    paddingRight = 0f,
                    paddingTop = 0f,
                    paddingBottom = 0f,
                    marginLeft = 0f,
                    marginRight = 0f,
                    marginTop = 0f,
                    marginBottom = 0f,
                    backgroundImage = UIService.Instance.DropdownTexture,
                    unityBackgroundImageTintColor = s_TextColor
                }
            };
            _field.Add(arrow);

            _valueBinding = new DataBinding {
                dataSourcePath = new PropertyPath(nameof(NodeData.DurationType)),
                bindingMode = BindingMode.ToTarget
            };
            _valueBinding.sourceToUiConverters.AddConverter((ref DurationType value) => {
                return s_StringCache[value];
            });

            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        public void Bind(NodeData data) {
            _data = data;

            _valueLabel.SetBinding("text", _valueBinding);
        }

        public void Unbind() {
            _data = null;

            ClearBindings();
        }

        private void OnAttachToPanel(AttachToPanelEvent evt) {
            _field.RegisterCallback<MouseOverEvent>(OnMouseOver);
            _field.RegisterCallback<MouseOutEvent>(OnMouseOut);
            _field.RegisterCallback<MouseDownEvent>(OnMouseDown);
        }

        private void OnDetachFromPanel(DetachFromPanelEvent evt) {
            _field.UnregisterCallback<MouseOverEvent>(OnMouseOver);
            _field.UnregisterCallback<MouseOutEvent>(OnMouseOut);
            _field.UnregisterCallback<MouseDownEvent>(OnMouseDown);
        }

        private void OnMouseOver(MouseOverEvent evt) {
            _field.style.backgroundColor = s_ActiveColor;
            SetBorderColor(s_BlueOutlineTransparent);
            evt.StopPropagation();
        }

        private void OnMouseOut(MouseOutEvent evt) {
            _field.style.backgroundColor = s_HoverColor;
            SetBorderColor(s_DarkBackgroundColor);
            evt.StopPropagation();
        }

        private void OnMouseDown(MouseDownEvent evt) {
            Vector2 anchor = new(0f, _field.resolvedStyle.height);
            _field.ShowContextMenu(anchor, menu => {
                foreach (var enumValue in s_StringCache.Keys) {
                    menu.AddItem(s_StringCache[enumValue], () => {
                        OnDurationTypeChanged(enumValue);
                    });
                }
            });
            evt.StopPropagation();
        }

        private void OnDurationTypeChanged(DurationType durationType) {
            if (_data.DurationType == durationType) return;
            Undo.Record();
            var e = this.GetPooled<DurationTypeChangeEvent>();
            e.Node = _data.Entity;
            e.DurationType = durationType;
            this.Send(e);
        }

        private void SetBorderColor(Color color) {
            _field.style.borderLeftColor = color;
            _field.style.borderRightColor = color;
            _field.style.borderTopColor = color;
            _field.style.borderBottomColor = color;
        }
    }
}
