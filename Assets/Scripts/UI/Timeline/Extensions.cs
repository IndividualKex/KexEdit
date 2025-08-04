using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;
using static KexEdit.UI.Constants;
using static KexEdit.UI.Timeline.Constants;

namespace KexEdit.UI.Timeline {
    public static class Extensions {
        private static readonly Dictionary<EasingType, string> s_EasingStringCache = new() {
            { EasingType.Sine, "Sine" },
            { EasingType.Quadratic, "Quadratic" },
            { EasingType.Cubic, "Cubic" },
            { EasingType.Quartic, "Quartic" },
            { EasingType.Quintic, "Quintic" },
            { EasingType.Exponential, "Exponential" },
        };

        public static float TimeToPixel(this TimelineData data, float time) {
            float startTime = data.Offset / (data.Zoom * RESOLUTION);
            return (time - startTime) * data.Zoom * RESOLUTION + LEFT_PADDING;
        }

        public static float PixelToTime(this TimelineData data, float x) {
            float adjustedPixelX = x - LEFT_PADDING;
            float startTime = data.Offset / (data.Zoom * RESOLUTION);
            return startTime + adjustedPixelX / (data.Zoom * RESOLUTION);
        }

        public static float ValueToPixel(this ValueBounds bounds, float value, float height) {
            if (bounds.Range <= 0f) return height / 2f;
            float normalizedValue = (value - bounds.Min) / bounds.Range;
            return height - (normalizedValue * (height - 2f * VALUE_PADDING) + VALUE_PADDING);
        }

        public static float PixelToValue(this ValueBounds bounds, float y, float height) {
            if (bounds.Range <= 0f) return bounds.Min;
            float adjustedPixelY = height - y;
            float normalizedValue = (adjustedPixelY - VALUE_PADDING) / (height - 2f * VALUE_PADDING);
            return bounds.Min + normalizedValue * bounds.Range;
        }

        public static ValueBounds ComputeVisualBounds(this ValueBounds bounds, float height) {
            float effectiveHeight = height - 2f * VALUE_PADDING;
            if (effectiveHeight <= 0f) return bounds;
            float padding = bounds.Range * (VALUE_PADDING / effectiveHeight);
            return new(bounds.Min - padding, bounds.Max + padding);
        }

        public static float ClampToVisualBounds(this ValueBounds bounds, float value, float height) {
            var visualBounds = bounds.ComputeVisualBounds(height);
            return visualBounds.Clamp(value);
        }

        public static void ClampOffset(this TimelineData data) {
            float minOffset = -data.ViewWidth;
            float maxOffset = data.Duration * RESOLUTION * data.Zoom + data.ViewWidth;
            data.Offset = math.clamp(data.Offset, minOffset, maxOffset);
        }

        public static float ClampTime(this TimelineData data, float time) {
            float minTime = -data.ViewWidth / (RESOLUTION * data.Zoom);
            float maxTime = data.Duration + data.ViewWidth / (RESOLUTION * data.Zoom);
            return math.clamp(time, minTime, maxTime);
        }

        public static void SortByTime(this NativeList<Keyframe> keyframes) {
            for (int i = 0; i < keyframes.Length - 1; i++) {
                for (int j = 0; j < keyframes.Length - 1 - i; j++) {
                    if (keyframes[j].Time > keyframes[j + 1].Time) {
                        (keyframes[j + 1], keyframes[j]) = (keyframes[j], keyframes[j + 1]);
                    }
                }
            }
        }

        public static Color GetColor(this PropertyType propertyType) {
            return propertyType switch {
                PropertyType.RollSpeed => s_RollSpeedColor,
                PropertyType.NormalForce => s_NormalForceColor,
                PropertyType.LateralForce => s_LateralForceColor,
                PropertyType.PitchSpeed => s_PitchSpeedColor,
                PropertyType.YawSpeed => s_YawSpeedColor,
                _ => s_DefaultColor
            };
        }

        public static string GetDisplayName(this PropertyType propertyType) {
            return propertyType switch {
                PropertyType.RollSpeed => s_RollSpeedName,
                PropertyType.NormalForce => s_NormalForceName,
                PropertyType.LateralForce => s_LateralForceName,
                PropertyType.PitchSpeed => s_PitchSpeedName,
                PropertyType.YawSpeed => s_YawSpeedName,
                PropertyType.FixedVelocity => s_FixedVelocityName,
                PropertyType.Heart => s_HeartName,
                PropertyType.Friction => s_FrictionName,
                PropertyType.Resistance => s_ResistanceName,
                PropertyType.TrackStyle => s_TrackStyleName,
                _ => throw new System.ArgumentOutOfRangeException(nameof(propertyType), propertyType, "Unknown PropertyType")
            };
        }

        public static InterpolationType GetMaxInterpolation(InterpolationType a, InterpolationType b) {
            return (InterpolationType)math.max((int)a, (int)b);
        }

        public static void DrawKeyframes(this Painter2D painter, TimelineData data, ValueBounds bounds, PropertyData propertyData, Rect rect) {
            const float halfSize = KEYFRAME_SIZE / 2f;
            foreach (var keyframe in propertyData.Keyframes) {
                float x = data.TimeToPixel(keyframe.Time);
                float y = bounds.ValueToPixel(keyframe.Value, rect.height);
                if (!rect.Contains(new Vector2(x, y))) continue;

                Color keyframeColor = keyframe.Selected ? s_BlueOutline : s_TextColor;
                painter.fillColor = keyframeColor;
                painter.BeginPath();
                painter.MoveTo(new Vector2(x - halfSize, y - halfSize));
                painter.LineTo(new Vector2(x + halfSize, y - halfSize));
                painter.LineTo(new Vector2(x + halfSize, y + halfSize));
                painter.LineTo(new Vector2(x - halfSize, y + halfSize));
                painter.Fill();

                if (keyframe.IsTimeLocked || keyframe.IsValueLocked) {
                    painter.DrawKeyframeLockBorder(x, y, halfSize, keyframe.IsTimeLocked, keyframe.IsValueLocked, keyframeColor);
                }
            }
        }

        public static void DrawCurves(
            this Painter2D painter,
            TimelineData data,
            ValueBounds bounds,
            PropertyData propertyData,
            Rect rect
        ) {
            Color color = propertyData.Type.GetColor();
            painter.strokeColor = color;
            painter.lineWidth = CURVE_WIDTH;

            float minX = data.TimeToPixel(0f);
            float maxX = data.TimeToPixel(data.Duration);

            if (propertyData.Keyframes.Count == 0) {
                return;
            }

            float startX = data.TimeToPixel(propertyData.Keyframes[0].Time);
            float endX = data.TimeToPixel(propertyData.Keyframes[^1].Time);

            if (startX > maxX) {
                float y = bounds.ValueToPixel(propertyData.Keyframes[0].Value, rect.height);
                Vector2 start = new(minX, y);
                Vector2 end = new(maxX, y);
                painter.DrawLine(rect, start, end, color, true);
                return;
            }

            if (endX < minX) {
                float y = bounds.ValueToPixel(propertyData.Keyframes[^1].Value, rect.height);
                Vector2 start = new(minX, y);
                Vector2 end = new(maxX, y);
                painter.DrawLine(rect, start, end, color, true);
                return;
            }

            if (startX > minX) {
                float y = bounds.ValueToPixel(propertyData.Keyframes[0].Value, rect.height);
                Vector2 start = new(minX, y);
                Vector2 end = new(startX, y);
                painter.DrawLine(rect, start, end, color, true);
            }

            for (int i = 0; i < propertyData.Keyframes.Count - 1; i++) {
                var start = propertyData.Keyframes[i];
                var end = propertyData.Keyframes[i + 1];
                painter.DrawCurveSegment(data, bounds, rect, propertyData.Type, start, end, color);
            }

            if (endX < maxX) {
                float y = bounds.ValueToPixel(propertyData.Keyframes[^1].Value, rect.height);
                Vector2 start = new(endX, y);
                Vector2 end = new(maxX, y);
                painter.DrawLine(rect, start, end, color, true);
            }
        }

        public static void DrawCurvesReadOnly(
            this Painter2D painter,
            TimelineData data,
            ValueBounds bounds,
            PropertyData propertyData,
            Rect rect
        ) {
            if (propertyData.Visible) return;

            Color color = propertyData.Type.GetColor();
            color.a = 0.5f;

            painter.strokeColor = color;
            painter.lineWidth = CURVE_WIDTH;

            float minX = data.TimeToPixel(0f);
            float maxX = data.TimeToPixel(data.Duration);

            const int step = 1;

            painter.BeginPath();

            for (int i = 1; i < propertyData.Values.Length - 1; i += step) {
                int nextIndex = math.min(i + step, propertyData.Values.Length - 1);

                float time = data.Times[i];
                float nextTime = data.Times[nextIndex];

                float startX = data.TimeToPixel(time);
                float endX = data.TimeToPixel(nextTime);

                if (startX < minX || endX > maxX) continue;

                float value = propertyData.Values[i];
                float nextValue = propertyData.Values[nextIndex];

                float startY = bounds.ValueToPixel(value, rect.height);
                float endY = bounds.ValueToPixel(nextValue, rect.height);

                Vector2 currentStart = new(startX, startY);
                Vector2 currentEnd = new(endX, endY);

                painter.MoveTo(currentStart);
                painter.LineTo(currentEnd);
            }

            painter.Stroke();
        }

        public static void DrawBezierHandles(this Painter2D painter, TimelineData data, ValueBounds bounds, PropertyData propertyData, Rect rect) {
            for (int i = 0; i < propertyData.Keyframes.Count; i++) {
                var keyframe = propertyData.Keyframes[i];
                if (!keyframe.Selected) continue;

                float x = data.TimeToPixel(keyframe.Time);
                float y = bounds.ValueToPixel(keyframe.Value, rect.height);
                Vector2 pos = new(x, y);

                if (
                    keyframe.OutInterpolation == InterpolationType.Bezier &&
                    i < propertyData.Keyframes.Count - 1
                ) {
                    var nextKeyframe = propertyData.Keyframes[i + 1];
                    float dt = nextKeyframe.Time - keyframe.Time;
                    painter.DrawBezierHandle(data, bounds, rect, pos,
                        keyframe.Time + (dt * keyframe.OutWeight),
                        keyframe.Value + (keyframe.OutTangent * dt * keyframe.OutWeight));
                }

                if (
                    keyframe.InInterpolation == InterpolationType.Bezier &&
                    i > 0
                ) {
                    var prevKeyframe = propertyData.Keyframes[i - 1];
                    float dt = keyframe.Time - prevKeyframe.Time;
                    painter.DrawBezierHandle(data, bounds, rect, pos,
                        keyframe.Time - (dt * keyframe.InWeight),
                        keyframe.Value - (keyframe.InTangent * dt * keyframe.InWeight));
                }
            }
        }

        public static void DrawBezierHandle(
            this Painter2D painter,
            TimelineData data,
            ValueBounds bounds,
            Rect rect,
            Vector2 pos,
            float time,
            float value
        ) {
            float x = data.TimeToPixel(time);
            float y = bounds.ValueToPixel(value, rect.height);
            Vector2 handlePos = new(x, y);

            painter.lineWidth = HANDLE_LINE_WIDTH;
            painter.DrawLine(rect, pos, handlePos, s_OrangeOutline, false);

            if (rect.Contains(handlePos)) {
                painter.fillColor = s_OrangeOutline;
                painter.BeginPath();
                painter.Arc(handlePos, HANDLE_SIZE / 2f, 0, 360);
                painter.Fill();
            }
        }

        private static void DrawCurveSegment(
            this Painter2D painter,
            TimelineData data,
            ValueBounds bounds,
            Rect rect,
            PropertyType type,
            Keyframe start,
            Keyframe end,
            Color curveColor
        ) {
            var interpolationType = Extensions.GetMaxInterpolation(start.OutInterpolation, end.InInterpolation);

            if (start.OutInterpolation == InterpolationType.Constant ||
                end.InInterpolation == InterpolationType.Constant) {
                interpolationType = InterpolationType.Constant;
            }

            painter.BeginPath();

            switch (interpolationType) {
                case InterpolationType.Constant:
                    painter.DrawConstantCurve(data, bounds, rect, start, end);
                    break;
                case InterpolationType.Linear:
                    painter.DrawLinearCurve(data, bounds, rect, start, end);
                    break;
                case InterpolationType.Bezier:
                    painter.DrawBezierCurve(data, bounds, rect, start, end);
                    break;
            }

            painter.strokeColor = curveColor;
            painter.Stroke();
        }

        private static void DrawConstantCurve(
            this Painter2D painter,
            TimelineData data,
            ValueBounds bounds,
            Rect rect,
            Keyframe start,
            Keyframe end
        ) {
            float startX = data.TimeToPixel(start.Time);
            float startY = bounds.ValueToPixel(start.Value, rect.height);
            float endX = data.TimeToPixel(end.Time);
            float endY = bounds.ValueToPixel(end.Value, rect.height);
            Vector2 startPos = new(startX, startY);
            Vector2 endPos = new(endX, endY);

            if (ClipLine(rect, startPos, new Vector2(endPos.x, startPos.y), out Vector2 clippedStart1, out Vector2 clippedEnd1)) {
                painter.MoveTo(clippedStart1);
                painter.LineTo(clippedEnd1);
            }

            if (ClipLine(rect, new Vector2(endPos.x, startPos.y), endPos, out Vector2 clippedStart2, out Vector2 clippedEnd2)) {
                painter.MoveTo(clippedStart2);
                painter.LineTo(clippedEnd2);
            }
        }

        private static void DrawLinearCurve(
            this Painter2D painter,
            TimelineData data,
            ValueBounds bounds,
            Rect rect,
            Keyframe start,
            Keyframe end
        ) {
            float startX = data.TimeToPixel(start.Time);
            float startY = bounds.ValueToPixel(start.Value, rect.height);
            float endX = data.TimeToPixel(end.Time);
            float endY = bounds.ValueToPixel(end.Value, rect.height);
            Vector2 startPos = new(startX, startY);
            Vector2 endPos = new(endX, endY);

            painter.DrawLine(rect, startPos, endPos, painter.strokeColor, false);
        }

        private static void DrawBezierCurve(
            this Painter2D painter,
            TimelineData data,
            ValueBounds bounds,
            Rect rect,
            Keyframe start,
            Keyframe end
        ) {
            float startX = data.TimeToPixel(start.Time);
            float startY = bounds.ValueToPixel(start.Value, rect.height);
            float endX = data.TimeToPixel(end.Time);
            float endY = bounds.ValueToPixel(end.Value, rect.height);

            float dt = end.Time - start.Time;
            float p0 = start.Value;
            float p1 = p0 + (start.OutTangent * dt * start.OutWeight);
            float p3 = end.Value;
            float p2 = p3 - (end.InTangent * dt * end.InWeight);

            float p0X = startX;
            float p0Y = startY;
            float p1X = data.TimeToPixel(start.Time + (dt * start.OutWeight));
            float p1Y = bounds.ValueToPixel(p1, rect.height);
            float p2X = data.TimeToPixel(end.Time - (dt * end.InWeight));
            float p2Y = bounds.ValueToPixel(p2, rect.height);
            float p3X = endX;
            float p3Y = endY;

            bool hasValidSegment = false;
            Vector2? lastValidPoint = null;

            for (int i = 0; i <= CURVE_SEGMENTS; i++) {
                float t = (float)i / CURVE_SEGMENTS;
                float oneMinusT = 1f - t;
                float timeSquared = t * t;
                float timeCubed = timeSquared * t;
                float oneMinusTSquared = oneMinusT * oneMinusT;
                float oneMinusTCubed = oneMinusTSquared * oneMinusT;

                float x = oneMinusTCubed * p0X
                    + 3f * oneMinusTSquared * t * p1X
                    + 3f * oneMinusT * timeSquared * p2X
                    + timeCubed * p3X;

                float y = oneMinusTCubed * p0Y
                    + 3f * oneMinusTSquared * t * p1Y
                    + 3f * oneMinusT * timeSquared * p2Y
                    + timeCubed * p3Y;

                Vector2 currentPoint = new(x, y);
                bool currentPointVisible = rect.Contains(currentPoint);

                if (currentPointVisible) {
                    if (!hasValidSegment) {
                        painter.MoveTo(currentPoint);
                        hasValidSegment = true;
                    }
                    else {
                        painter.LineTo(currentPoint);
                    }
                    lastValidPoint = currentPoint;
                }
                else if (hasValidSegment && lastValidPoint.HasValue) {
                    if (ClipLine(rect, lastValidPoint.Value, currentPoint, out Vector2 clippedStart, out Vector2 clippedEnd)) {
                        painter.LineTo(clippedEnd);
                    }
                    hasValidSegment = false;
                    lastValidPoint = null;
                }
            }
        }

        private static void DrawLine(this Painter2D painter, Rect rect, Vector2 start, Vector2 end, Color color, bool dotted) {
            if (!ClipLine(rect, start, end, out Vector2 clippedStart, out Vector2 clippedEnd)) {
                return;
            }

            painter.strokeColor = color;
            painter.BeginPath();

            if (!dotted) {
                painter.MoveTo(clippedStart);
                painter.LineTo(clippedEnd);
            }
            else {
                painter.DrawDottedLine(clippedStart, clippedEnd);
            }

            painter.Stroke();
        }

        private static void DrawDottedLine(this Painter2D painter, Vector2 start, Vector2 end) {
            const float dashLength = 4f;
            const float gapLength = 3f;

            Vector2 direction = (end - start).normalized;
            float totalDistance = Vector2.Distance(start, end);
            float currentDistance = 0f;
            bool drawing = true;

            painter.MoveTo(start);

            while (currentDistance < totalDistance) {
                float segmentLength = drawing ? dashLength : gapLength;
                float remainingDistance = totalDistance - currentDistance;
                float actualSegmentLength = math.min(segmentLength, remainingDistance);

                Vector2 nextPoint = start + direction * (currentDistance + actualSegmentLength);

                if (currentDistance + actualSegmentLength >= totalDistance) {
                    nextPoint = end;
                }

                if (drawing) {
                    painter.LineTo(nextPoint);
                }
                else {
                    painter.MoveTo(nextPoint);
                }

                currentDistance += actualSegmentLength;
                drawing = !drawing;

                if (currentDistance >= totalDistance) {
                    break;
                }
            }
        }

        private static bool ClipLine(Rect rect, Vector2 start, Vector2 end, out Vector2 clippedStart, out Vector2 clippedEnd) {
            const int INSIDE = 0, LEFT = 1, RIGHT = 2, BOTTOM = 4, TOP = 8;

            int GetOutCode(Vector2 point) {
                int code = INSIDE;
                if (point.x < 0) code |= LEFT;
                else if (point.x > rect.width) code |= RIGHT;
                if (point.y < 0) code |= BOTTOM;
                else if (point.y > rect.height) code |= TOP;
                return code;
            }

            clippedStart = start;
            clippedEnd = end;
            int code0 = GetOutCode(clippedStart);
            int code1 = GetOutCode(clippedEnd);

            while (true) {
                if ((code0 | code1) == 0) return true;
                if ((code0 & code1) != 0) return false;

                int codeOut = code0 != 0 ? code0 : code1;
                float x, y;

                if ((codeOut & TOP) != 0) {
                    x = clippedStart.x + (clippedEnd.x - clippedStart.x) * (rect.height - clippedStart.y) / (clippedEnd.y - clippedStart.y);
                    y = rect.height;
                }
                else if ((codeOut & BOTTOM) != 0) {
                    x = clippedStart.x + (clippedEnd.x - clippedStart.x) * (-clippedStart.y) / (clippedEnd.y - clippedStart.y);
                    y = 0;
                }
                else if ((codeOut & RIGHT) != 0) {
                    y = clippedStart.y + (clippedEnd.y - clippedStart.y) * (rect.width - clippedStart.x) / (clippedEnd.x - clippedStart.x);
                    x = rect.width;
                }
                else {
                    y = clippedStart.y + (clippedEnd.y - clippedStart.y) * (-clippedStart.x) / (clippedEnd.x - clippedStart.x);
                    x = 0;
                }

                if (codeOut == code0) {
                    clippedStart = new Vector2(x, y);
                    code0 = GetOutCode(clippedStart);
                }
                else {
                    clippedEnd = new Vector2(x, y);
                    code1 = GetOutCode(clippedEnd);
                }
            }
        }

        public static void DrawKeyframeLockBorder(
            this Painter2D painter,
            float x,
            float y,
            float halfSize,
            bool isTimeLocked,
            bool isValueLocked,
            Color borderColor
        ) {
            painter.strokeColor = borderColor;
            painter.lineWidth = 1.5f;
            painter.BeginPath();

            const float padding = 2f;
            float left = x - halfSize - padding;
            float right = x + halfSize + padding;
            float top = y - halfSize - padding;
            float bottom = y + halfSize + padding;

            if (isTimeLocked && isValueLocked) {
                painter.MoveTo(new Vector2(left, top));
                painter.LineTo(new Vector2(right, top));
                painter.LineTo(new Vector2(right, bottom));
                painter.LineTo(new Vector2(left, bottom));
                painter.LineTo(new Vector2(left, top));
            }
            else if (isValueLocked) {
                painter.MoveTo(new Vector2(left, top));
                painter.LineTo(new Vector2(right, top));
                painter.MoveTo(new Vector2(left, bottom));
                painter.LineTo(new Vector2(right, bottom));
            }
            else if (isTimeLocked) {
                painter.MoveTo(new Vector2(left, top));
                painter.LineTo(new Vector2(left, bottom));
                painter.MoveTo(new Vector2(right, top));
                painter.LineTo(new Vector2(right, bottom));
            }

            painter.Stroke();
        }

        public static string GetDisplayName(this EasingType easingType) {
            return s_EasingStringCache[easingType];
        }

        public static IEnumerable<EasingType> EnumerateEasingTypes() {
            return s_EasingStringCache.Keys;
        }
    }
}
