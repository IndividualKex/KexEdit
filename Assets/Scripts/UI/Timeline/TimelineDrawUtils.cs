using System;
using UnityEngine;
using UnityEngine.UIElements;
using static KexEdit.UI.Constants;
using static KexEdit.UI.Timeline.Constants;

namespace KexEdit.UI.Timeline {
    public static class TimelineDrawUtils {
        private const float BASE_VALUE_LABEL_FONT_SIZE = 10f;
        private const float BASE_RULER_FONT_SIZE = 10f;
        private const float VALUE_LABEL_OFFSET = 4f;

        private static readonly string[] s_StringPool = new string[128];
        private static int s_PoolIndex = 0;

        static TimelineDrawUtils() {
            for (int i = 0; i < s_StringPool.Length; i++) {
                s_StringPool[i] = new string('\0', 16);
            }
        }

        public static unsafe string FormatPooledString(float value, ReadOnlySpan<char> format = default) {
            string pooledString = s_StringPool[s_PoolIndex];
            s_PoolIndex = (s_PoolIndex + 1) % s_StringPool.Length;

            Span<char> buffer = stackalloc char[16];

            if (format.IsEmpty) {
                format = value switch {
                    _ when Mathf.Abs(value) < 0.001f => "0",
                    _ when Mathf.Abs(value) >= 1000f => "F0",
                    _ when Mathf.Abs(value) >= 100f => "F1",
                    _ when Mathf.Abs(value) >= 10f => "F2",
                    _ => "0.#"
                };
            }

            if (format.Length == 1 && format[0] == '0') {
                return "0";
            }

            if (!value.TryFormat(buffer, out int charsWritten, format)) {
                return value.ToString();
            }

            fixed (char* pooledPtr = pooledString) {
                for (int i = 0; i < charsWritten; i++) {
                    pooledPtr[i] = buffer[i];
                }
                pooledPtr[charsWritten] = '\0';
            }

            return pooledString;
        }

        public static void DrawPlayhead(Painter2D painter, TimelineData data, Rect rect) {
            float playheadX = data.TimeToPixel(data.Time);

            if (playheadX < 0 || playheadX > rect.width) return;

            painter.strokeColor = s_ActiveTextColor;
            painter.lineWidth = 2f;
            painter.BeginPath();
            painter.MoveTo(new Vector2(playheadX, 0));
            painter.LineTo(new Vector2(playheadX, rect.height));
            painter.Stroke();
        }

        public static void DrawVerticalGrid(Painter2D painter, TimelineData data, Rect rect) {
            float startTime = data.Offset / (data.Zoom * RESOLUTION);
            float timePerTick = TICK_SPACING / RESOLUTION;
            float firstTickIndex = Mathf.Floor(startTime / timePerTick);
            float firstTickPos = (firstTickIndex * timePerTick - startTime) * data.Zoom * RESOLUTION;
            int visibleTicks = Mathf.CeilToInt((rect.width - firstTickPos) / (TICK_SPACING * data.Zoom)) + 1;

            CalculateTickIntervals(data.Zoom, out int majorInterval, out int minorInterval);

            float minorTickSpacing = TICK_SPACING * data.Zoom * minorInterval;
            bool showMinorLabels = minorTickSpacing >= MIN_MAJOR_SPACING;

            float leftPadding = LEFT_PADDING;

            for (int i = 0; i < visibleTicks; i++) {
                float tickIndex = firstTickIndex + i;
                int tickIndexInt = (int)tickIndex;
                float tickTime = tickIndex * timePerTick;
                float x = firstTickPos + i * TICK_SPACING * data.Zoom + leftPadding;

                if (x < leftPadding || x > rect.width) continue;

                bool isMajor = tickIndexInt % majorInterval == 0 || showMinorLabels;
                bool isMinor = tickIndexInt % minorInterval == 0;

                if (!isMajor && !isMinor) continue;

                bool isBeyondDuration = tickTime > data.Duration;
                float opacityMultiplier = isBeyondDuration ? 0.3f : 1f;
                Color gridColor = isMajor ? s_MajorGridColor : s_MinorGridColor;
                gridColor.a *= opacityMultiplier;
                painter.strokeColor = gridColor;

                painter.BeginPath();
                painter.MoveTo(new Vector2(x, 0f));
                painter.LineTo(new Vector2(x, rect.height));
                painter.Stroke();
            }
        }

        public static void DrawRulerTicks(MeshGenerationContext ctx, TimelineData data, Rect rect) {
            var painter = ctx.painter2D;

            float startTime = data.Offset / (data.Zoom * RESOLUTION);
            float timePerTick = TICK_SPACING / RESOLUTION;
            float firstTickIndex = Mathf.Floor(startTime / timePerTick);
            float firstTickPos = (firstTickIndex * timePerTick - startTime) * data.Zoom * RESOLUTION;
            int visibleTicks = Mathf.CeilToInt((rect.width - firstTickPos) / (TICK_SPACING * data.Zoom)) + 1;

            CalculateTickIntervals(data.Zoom, out int majorInterval, out int minorInterval);

            float minorTickSpacing = TICK_SPACING * data.Zoom * minorInterval;
            bool showMinorLabels = minorTickSpacing >= MIN_MAJOR_SPACING;

            for (int i = 0; i < visibleTicks; i++) {
                float tickIndex = firstTickIndex + i;
                int tickIndexInt = (int)tickIndex;
                float tickTime = tickIndex * timePerTick;
                float x = firstTickPos + i * TICK_SPACING * data.Zoom + LEFT_PADDING;

                if (x < 0 || x > rect.width) continue;

                bool isMajor = tickIndexInt % majorInterval == 0 || showMinorLabels;
                bool isMinor = tickIndexInt % minorInterval == 0;

                if (!isMajor && !isMinor) continue;

                float tickHeight = isMajor ? MAJOR_HEIGHT : MINOR_HEIGHT;
                bool isBeyondDuration = tickTime > data.Duration;

                float opacityMultiplier = isBeyondDuration ? MIN_OPACITY : 1f;
                Color tickColor = isMajor ? s_ActiveTextColor : s_ActiveTextColorTransparent;
                tickColor.a *= opacityMultiplier;
                painter.strokeColor = tickColor;

                painter.BeginPath();
                painter.MoveTo(new Vector2(x, rect.height));
                painter.LineTo(new Vector2(x, rect.height - tickHeight));
                painter.Stroke();

                if (isMajor) {
                    float displayValue = data.DurationType == DurationType.Distance
                        ? Units.DistanceToDisplay(tickTime)
                        : tickTime;

                    string timeText = FormatPooledString(displayValue, "0.#");
                    Color textColor = s_ActiveTextColorTransparent;
                    textColor.a *= opacityMultiplier;
                    float scaledFontSize = BASE_RULER_FONT_SIZE * Preferences.UIScale;
                    ctx.DrawText(timeText, new Vector2(x + 3, 4), scaledFontSize, textColor, null);
                }
            }
        }

        public static void DrawValueLegend(MeshGenerationContext ctx, TimelineData data, ValueBounds bounds, Rect rect) {
            if (bounds.Max <= bounds.Min) return;

            var painter = ctx.painter2D;
            float targetLineCount = rect.height / 40f;

            float rawStep = bounds.Range / targetLineCount;
            float step = CalculateNiceStep(rawStep);

            float startValue = Mathf.Ceil(bounds.Min / step) * step;
            float endValue = bounds.Max;

            UnitsType unitsType = UnitsType.None;
            if (data.LatestSelectedProperty.HasValue &&
                data.Properties.ContainsKey(data.LatestSelectedProperty.Value)) {
                unitsType = data.Properties[data.LatestSelectedProperty.Value].Units;
            }

            painter.strokeColor = s_MajorGridColor;
            painter.lineWidth = 1f;

            for (float value = startValue; value <= endValue; value += step) {
                float y = bounds.ValueToPixel(value, rect.height);

                if (y < 0 || y > rect.height) continue;

                painter.BeginPath();
                painter.MoveTo(new Vector2(0, y));
                painter.LineTo(new Vector2(rect.width, y));
                painter.Stroke();

                float displayValue = unitsType.ValueToDisplay(value);
                string labelText = FormatPooledString(displayValue);
                float scaledValueFontSize = BASE_VALUE_LABEL_FONT_SIZE * Preferences.UIScale;
                Vector2 labelPosition = new(VALUE_LABEL_OFFSET, y - scaledValueFontSize / 2f);

                ctx.DrawText(labelText, labelPosition, scaledValueFontSize, s_ActiveTextColorTransparent, null);
            }
        }

        public static void DrawHorizontalPropertyLines(Painter2D painter, Rect rect, int propertyCount) {
            painter.strokeColor = new Color(0.4f, 0.4f, 0.4f, 0.2f);

            for (int i = 1; i <= propertyCount; i++) {
                float y = ROW_HEIGHT * i;
                painter.BeginPath();
                painter.MoveTo(new Vector2(0, y));
                painter.LineTo(new Vector2(rect.width, y));
                painter.Stroke();
            }
        }

        public static void CalculateTickIntervals(float zoom, out int majorInterval, out int minorInterval) {
            majorInterval = MAJOR_INTERVAL;
            minorInterval = 1;

            float majorTickSpacing = TICK_SPACING * zoom * majorInterval;

            while (majorTickSpacing < MIN_MAJOR_SPACING && majorInterval < 1000) {
                majorInterval *= 5;
                minorInterval *= 5;
                majorTickSpacing = TICK_SPACING * zoom * majorInterval;
            }
        }

        private static float CalculateNiceStep(float rawStep) {
            float magnitude = Mathf.Pow(10f, Mathf.Floor(Mathf.Log10(rawStep)));
            float normalizedStep = rawStep / magnitude;

            if (normalizedStep <= 1f) return magnitude;
            if (normalizedStep <= 2f) return 2f * magnitude;
            if (normalizedStep <= 5f) return 5f * magnitude;
            return 10f * magnitude;
        }
    }
}
