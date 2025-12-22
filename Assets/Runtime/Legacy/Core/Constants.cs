using UnityEngine;
using static KexEdit.Sim.Sim;

namespace KexEdit.Legacy {
    public static class Constants {
        public const float TORSION_STRESS_FACTOR = 0.1f;
        public const float MIN_CURVATURE = 0.001f;
        public const float MIN_SEGMENT_LENGTH = 15f;
        public const float MAX_SEGMENT_LENGTH = 30f;
        public const float FRICTION_UI_TO_PHYSICS_SCALE = 1e-2f;
        public const float FRICTION_PHYSICS_TO_UI_SCALE = 1e2f;
        public const float RESISTANCE_UI_TO_PHYSICS_SCALE = 1e-6f;
        public const float RESISTANCE_PHYSICS_TO_UI_SCALE = 1e6f;
        public const int STRESS_ROLLING_WINDOW = 50;

        public static readonly Color SELECTED_COLOR = Color.white;
        public static readonly Color VISUALIZATION_MIN_COLOR = new Color(0f, 0f, 1f, 1f);
        public static readonly Color VISUALIZATION_MAX_COLOR = new Color(1f, 0f, 0f, 1f);
        public static readonly Color VISUALIZATION_ZERO_COLOR = new Color(0.7f, 0.7f, 0.7f, 1f);
        public static readonly Color HIGHLIGHT_COLOR = new Color(1f, 0.9f, 0.3f, 1f);
    }
}
