using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;
using static KexEdit.UI.Constants;

namespace KexEdit.UI.Timeline {
    public class KeyframeButton : VisualElement {
        private PropertyData _data;
        private bool _hovered;

        public KeyframeButton(PropertyData data) {
            _data = data;

            style.width = 16f;
            style.height = 16f;
            style.marginLeft = 4f;
            style.marginRight = 4f;
            style.marginTop = 2f;
            style.marginBottom = 2f;

            focusable = true;

            generateVisualContent += OnGenerateVisualContent;

            RegisterCallback<MouseDownEvent>(OnMouseDown);
            RegisterCallback<MouseEnterEvent>(OnMouseEnter);
            RegisterCallback<MouseLeaveEvent>(OnMouseLeave);

            var activeBinding = new DataBinding {
                dataSourcePath = new PropertyPath(nameof(PropertyData.HasActiveKeyframe)),
                bindingMode = BindingMode.ToTarget
            };
            activeBinding.sourceToUiConverters.AddConverter((ref bool value) => {
                MarkDirtyRepaint();
                return "";
            });
            SetBinding("tooltip", activeBinding);
        }

        private void OnGenerateVisualContent(MeshGenerationContext ctx) {
            var painter = ctx.painter2D;
            Rect rect = contentRect;

            painter.fillColor = s_AltDarkBackgroundColor;
            painter.BeginPath();
            painter.MoveTo(new Vector2(0, 0));
            painter.LineTo(new Vector2(rect.width, 0));
            painter.LineTo(new Vector2(rect.width, rect.height));
            painter.LineTo(new Vector2(0, rect.height));
            painter.Fill();

            if (_hovered) {
                painter.strokeColor = s_ActiveHoverColor;
                painter.lineWidth = 1f;
                painter.BeginPath();
                painter.MoveTo(new Vector2(0.5f, 0.5f));
                painter.LineTo(new Vector2(rect.width - 0.5f, 0.5f));
                painter.LineTo(new Vector2(rect.width - 0.5f, rect.height - 0.5f));
                painter.LineTo(new Vector2(0.5f, rect.height - 0.5f));
                painter.LineTo(new Vector2(0.5f, 0.5f));
                painter.Stroke();
            }

            painter.fillColor = _data.HasActiveKeyframe ? s_BlueOutline : s_ActiveColor;

            float centerX = rect.width / 2f;
            float centerY = rect.height / 2f;
            float size = Mathf.Min(rect.width, rect.height) * 0.5f;

            painter.BeginPath();
            painter.MoveTo(new Vector2(centerX, centerY - size / 2f));
            painter.LineTo(new Vector2(centerX + size / 2f, centerY));
            painter.LineTo(new Vector2(centerX, centerY + size / 2f));
            painter.LineTo(new Vector2(centerX - size / 2f, centerY));
            painter.Fill();
        }

        private void OnMouseDown(MouseDownEvent evt) {
            if (evt.button == 0) {
                Undo.Record();
                var e = this.GetPooled<KeyframeButtonClickEvent>();
                e.Type = _data.Type;
                this.Send(e);
                evt.StopPropagation();
            }
        }

        private void OnMouseEnter(MouseEnterEvent evt) {
            _hovered = true;
            MarkDirtyRepaint();
        }

        private void OnMouseLeave(MouseLeaveEvent evt) {
            _hovered = false;
            MarkDirtyRepaint();
        }
    }
}
