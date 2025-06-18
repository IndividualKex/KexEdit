using UnityEngine;
using UnityEngine.UIElements;
using static KexEdit.UI.Constants;

namespace KexEdit.UI.NodeGraph {
    public class InputThumbEdge : VisualElement {
        private InputThumb _thumb;

        public InputThumbEdge(InputThumb thumb) {
            _thumb = thumb;

            style.width = 0f;
            style.height = 0f;
            generateVisualContent += OnGenerateVisualContent;
        }

        private void OnGenerateVisualContent(MeshGenerationContext ctx) {
            Vector2 start = Vector2.zero;

            Vector2 end = _thumb.parent.worldBound.center;
            end = this.WorldToLocal(end) + new Vector2(-4f, 0f);

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
