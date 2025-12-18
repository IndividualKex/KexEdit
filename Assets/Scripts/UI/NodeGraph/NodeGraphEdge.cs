using UnityEngine;
using UnityEngine.UIElements;
using static KexEdit.UI.Constants;
using static KexEdit.UI.NodeGraph.Constants;

using KexEdit.Legacy;
namespace KexEdit.UI.NodeGraph {
    public class NodeGraphEdge : VisualElement {
        private NodeGraphView _view;
        private EdgeData _data;
        private NodeGraphPort _source;
        private NodeGraphPort _target;
        private Vector2 _dragEnd;
        private Vector2 _start;
        private Vector2 _end;

        public EdgeData Data => _data;

        public NodeGraphEdge(NodeGraphView view, EdgeData data, NodeGraphPort source, NodeGraphPort target = null) {
            _view = view;
            _data = data;
            _source = source;
            _target = target;

            style.position = Position.Absolute;
            style.width = 0f;
            style.height = 0f;

            generateVisualContent += OnGenerateVisualContent;
            RegisterCallback<MouseEnterEvent>(OnMouseEnter);
            RegisterCallback<MouseLeaveEvent>(OnMouseLeave);
            RegisterCallback<MouseDownEvent>(OnMouseDown);
        }

        public void Draw() {
            UpdateBounds();
            MarkDirtyRepaint();
        }

        public void SetDragEnd(Vector2 dragEnd) {
            _dragEnd = dragEnd;
            UpdateBounds();
        }

        private void OnGenerateVisualContent(MeshGenerationContext ctx) {
            float lineWidth = _data.Hovered ? HOVER_WIDTH : NORMAL_WIDTH;
            Color color = _data.Selected ? s_BlueOutline : s_YellowOutline;

            float dist = Mathf.Abs(_end.y - _start.y);
            float maxHeight = dist * 0.5f;
            float height = Mathf.Min(maxHeight, 50f);
            float dy = _source.Data.Port.IsInput ? -height : height;
            Vector2 control1 = _start + new Vector2(0f, dy);
            Vector2 control2 = _end - new Vector2(0f, dy);

            var painter = ctx.painter2D;
            painter.lineWidth = lineWidth;
            painter.strokeColor = color;

            painter.BeginPath();
            painter.MoveTo(_start);
            painter.BezierCurveTo(control1, control2, _end);
            painter.Stroke();
        }

        private void OnMouseEnter(MouseEnterEvent evt) {
            _data.InteractionState |= InteractionState.Hovered;
        }

        private void OnMouseLeave(MouseLeaveEvent evt) {
            _data.InteractionState &= ~InteractionState.Hovered;
        }

        private void UpdateBounds() {
            Vector2 start = _source.Q<Connector>().worldBound.center;
            Vector2 end = _target == null ? _dragEnd : _target.Q<Connector>().worldBound.center;

            start = _view.WorldToLocal(start);
            end = _view.WorldToLocal(end);

            start = (start - _view.Pan) / _view.Zoom;
            end = (end - _view.Pan) / _view.Zoom;

            Vector2 min = Vector2.Min(start, end);
            Vector2 max = Vector2.Max(start, end);

            const float padding = 16f;
            min -= new Vector2(padding, padding);
            max += new Vector2(padding, padding);

            Vector2 size = max - min;
            Vector2 center = (start + end) / 2f;

            style.left = center.x - size.x * 0.5f;
            style.top = center.y - size.y * 0.5f;
            style.width = size.x;
            style.height = size.y;

            _start = start - (center - size * 0.5f);
            _end = end - (center - size * 0.5f);
        }

        private void OnMouseDown(MouseDownEvent evt) {
            if (evt.button == 0 || (evt.button == 1 && !evt.altKey)) {
                var e = this.GetPooled<EdgeClickEvent>();
                e.Edge = _data.Entity;
                e.ShiftKey = evt.shiftKey;
                this.SendEvent(e);
            }
            
            if (evt.button == 1 && !evt.altKey) {
                var e = this.GetPooled<EdgeRightClickEvent>();
                e.MousePosition = evt.localMousePosition;
                this.SendEvent(e);
            }
        }

        public override bool ContainsPoint(Vector2 point) {
            if (_target == null && _dragEnd == Vector2.zero) {
                return false;
            }

            float dist = Mathf.Abs(_end.y - _start.y);
            float maxHeight = dist * 0.5f;
            float height = Mathf.Min(maxHeight, 50f);
            float dy = _source.Data.Port.IsInput ? -height : height;
            Vector2 control1 = _start + new Vector2(0f, dy);
            Vector2 control2 = _end - new Vector2(0f, dy);

            return point.DistanceToQuadraticBezier(_start, control1, control2, _end) <= HIT_DISTANCE;
        }

        public bool IntersectsRect(Rect rect) {
            if (_target == null && _dragEnd == Vector2.zero) {
                return false;
            }

            float dist = Mathf.Abs(_end.y - _start.y);
            float maxHeight = dist * 0.5f;
            float height = Mathf.Min(maxHeight, 50f);
            float dy = _source.Data.Port.IsInput ? -height : height;
            Vector2 control1 = _start + new Vector2(0f, dy);
            Vector2 control2 = _end - new Vector2(0f, dy);

            const int samples = 20;
            for (int i = 0; i <= samples; i++) {
                float t = (float)i / samples;
                Vector2 curvePoint = Extensions.CubicBezier(_start, control1, control2, _end, t);
                Vector2 contentSpacePoint = curvePoint + new Vector2(style.left.value.value, style.top.value.value);
                if (rect.Contains(contentSpacePoint)) {
                    return true;
                }
            }

            return false;
        }
    }
}
