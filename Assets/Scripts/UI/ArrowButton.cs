using UnityEngine;
using UnityEngine.UIElements;
using System;
using static KexEdit.UI.Constants;

using KexEdit.Legacy;
namespace KexEdit.UI {
    public class ArrowButton : VisualElement {
        private bool _hovered;
        private bool _pointingRight;

        public event Action OnClicked;

        public ArrowButton(bool pointingRight) {
            _pointingRight = pointingRight;

            style.width = 12f;
            style.height = 16f;
            style.marginTop = 2f;
            style.marginBottom = 2f;
            style.marginLeft = 2f;
            style.marginRight = 2f;

            generateVisualContent += OnGenerateVisualContent;

            RegisterCallback<MouseDownEvent>(OnMouseDown);
            RegisterCallback<MouseEnterEvent>(OnMouseEnter);
            RegisterCallback<MouseLeaveEvent>(OnMouseLeave);
        }

        private void OnGenerateVisualContent(MeshGenerationContext ctx) {
            var painter = ctx.painter2D;
            Rect rect = contentRect;

            painter.fillColor = _hovered ? s_AltBackgroundColor : s_AltDarkBackgroundColor;
            painter.BeginPath();
            painter.MoveTo(new Vector2(0, 0));
            painter.LineTo(new Vector2(rect.width, 0));
            painter.LineTo(new Vector2(rect.width, rect.height));
            painter.LineTo(new Vector2(0, rect.height));
            painter.Fill();

            painter.fillColor = s_MutedTextColor;

            float centerX = rect.width / 2f;
            float centerY = rect.height / 2f;
            float arrowSize = 3f;

            painter.BeginPath();
            if (_pointingRight) {
                painter.MoveTo(new Vector2(centerX - arrowSize, centerY - arrowSize));
                painter.LineTo(new Vector2(centerX + arrowSize, centerY));
                painter.LineTo(new Vector2(centerX - arrowSize, centerY + arrowSize));
            }
            else {
                painter.MoveTo(new Vector2(centerX + arrowSize, centerY - arrowSize));
                painter.LineTo(new Vector2(centerX - arrowSize, centerY));
                painter.LineTo(new Vector2(centerX + arrowSize, centerY + arrowSize));
            }
            painter.Fill();
        }

        private void OnMouseDown(MouseDownEvent evt) {
            if (evt.button == 0) {
                OnClicked?.Invoke();
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
