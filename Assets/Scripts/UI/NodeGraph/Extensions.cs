using KexEdit.Legacy;
using UnityEngine;

namespace KexEdit.UI.NodeGraph {
    public static class Extensions {
        public static PortBounds GetPortBounds(this PortType portType) {
            float min = portType switch {
                PortType.Roll or PortType.Yaw => -180f,
                PortType.Pitch => -90f,
                PortType.Duration or PortType.Radius or PortType.Arc or
                PortType.LeadIn or PortType.LeadOut or PortType.Start => 0f,
                PortType.Velocity or PortType.InWeight or PortType.OutWeight => 0.01f,
                PortType.Axis => -360f,
                PortType.End => -1f,
                _ => float.MinValue
            };
            float max = portType switch {
                PortType.Roll or PortType.Yaw => 180f,
                PortType.Pitch => 90f,
                PortType.InWeight or PortType.OutWeight => 1f,
                _ => float.MaxValue
            };
            return new(min, max);
        }

        public static float DistanceToQuadraticBezier(this Vector2 point, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3) {
            const int samples = 50;
            float minDistance = float.MaxValue;

            for (int i = 0; i <= samples; i++) {
                float t = (float)i / samples;
                Vector2 curvePoint = CubicBezier(p0, p1, p2, p3, t);
                float distance = Vector2.Distance(point, curvePoint);
                minDistance = Mathf.Min(minDistance, distance);
            }

            return minDistance;
        }

        public static Vector2 CubicBezier(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t) {
            float u = 1f - t;
            float tt = t * t;
            float uu = u * u;
            float uuu = uu * u;
            float ttt = tt * t;

            Vector2 p = uuu * p0;
            p += 3f * uu * t * p1;
            p += 3f * u * tt * p2;
            p += ttt * p3;

            return p;
        }
    }
}
