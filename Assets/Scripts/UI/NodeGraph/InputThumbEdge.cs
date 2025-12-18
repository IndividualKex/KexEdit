using UnityEngine;
using UnityEngine.UIElements;
using static KexEdit.UI.Constants;

using KexEdit.Legacy;
namespace KexEdit.UI.NodeGraph {
    public class InputThumbEdge : VisualElement {
        private InputThumb _thumb;
        private bool _vertical;

        public InputThumbEdge(InputThumb thumb, bool vertical = false) {
            _thumb = thumb;
            _vertical = vertical;

            style.width = 0f;
            style.height = 0f;
            generateVisualContent += OnGenerateVisualContent;
        }

        private void OnGenerateVisualContent(MeshGenerationContext ctx) {
            Vector2 start = _vertical ? new Vector2(0f, 4f) : Vector2.zero;

            Vector2 end = _thumb.parent.worldBound.center;
            Vector2 offset = _vertical ? new Vector2(0f, -4f) : new Vector2(-4f, 0f);
            end = this.WorldToLocal(end) + offset;

            var painter = ctx.painter2D;
            painter.lineWidth = 2f;
            painter.strokeColor = s_YellowOutline;

            painter.BeginPath();
            painter.MoveTo(start);
            painter.LineTo(end);
            painter.Stroke();
        }
    }
}
