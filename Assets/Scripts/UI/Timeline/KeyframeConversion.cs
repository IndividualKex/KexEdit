using KexEdit.Legacy;
using CoreInterpolationType = KexEdit.Sim.InterpolationType;
using LegacyInterpolationType = KexEdit.Legacy.InterpolationType;
using CoreKeyframe = KexEdit.Sim.Keyframe;

namespace KexEdit.UI.Timeline {
    public static class KeyframeConversion {
        public static CoreKeyframe ToCore(Legacy.Keyframe legacy) {
            return new CoreKeyframe(
                time: legacy.Time,
                value: legacy.Value,
                inInterpolation: ToCore(legacy.InInterpolation),
                outInterpolation: ToCore(legacy.OutInterpolation),
                inTangent: legacy.InTangent,
                outTangent: legacy.OutTangent,
                inWeight: legacy.InWeight,
                outWeight: legacy.OutWeight
            );
        }

        public static Legacy.Keyframe ToLegacy(
            CoreKeyframe core,
            uint id,
            HandleType handleType,
            KeyframeFlags flags,
            bool selected = false
        ) {
            return new Legacy.Keyframe {
                Id = id,
                Time = core.Time,
                Value = core.Value,
                InInterpolation = ToLegacy(core.InInterpolation),
                OutInterpolation = ToLegacy(core.OutInterpolation),
                HandleType = handleType,
                Flags = flags,
                InTangent = core.InTangent,
                OutTangent = core.OutTangent,
                InWeight = core.InWeight,
                OutWeight = core.OutWeight,
                Selected = selected
            };
        }

        private static CoreInterpolationType ToCore(LegacyInterpolationType legacy) => legacy switch {
            LegacyInterpolationType.Constant => CoreInterpolationType.Constant,
            LegacyInterpolationType.Linear => CoreInterpolationType.Linear,
            LegacyInterpolationType.Bezier => CoreInterpolationType.Bezier,
            _ => CoreInterpolationType.Bezier
        };

        private static LegacyInterpolationType ToLegacy(CoreInterpolationType core) => core switch {
            CoreInterpolationType.Constant => LegacyInterpolationType.Constant,
            CoreInterpolationType.Linear => LegacyInterpolationType.Linear,
            CoreInterpolationType.Bezier => LegacyInterpolationType.Bezier,
            _ => LegacyInterpolationType.Bezier
        };
    }
}
