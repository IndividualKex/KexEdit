using KexEdit.UI.Timeline;
using Unity.Entities;
using Unity.Mathematics;

namespace KexEdit.UI {
    public struct Optimizer {
        private const float TOLERANCE = 1e-3f;
        private const float LEARNING_RATE = 1e-3f;
        private const float BETA1 = 0.9f;
        private const float BETA2 = 0.999f;
        private const float EPSILON = 1e-8f;
        private const float GRADIENT_DELTA = 1e-4f;
        private const int MAX_ITERS = 10000;

        private enum Phase {
            Baseline,
            Perturbed
        }

        private Entity _entity;
        private KeyframeData _keyframe;
        private OptimizerData _data;

        private float _baselineParam;
        private float _baselineLoss;
        private float _perturbedLoss;

        private Phase _phase;
        private float _m;
        private float _v;
        private int _iters;

        public OptimizerData Data => _data;

        public Optimizer(Entity entity, KeyframeData keyframe, OptimizerData data) {
            _entity = entity;
            _keyframe = keyframe;
            _data = data;
            _baselineParam = keyframe.Value.Value;

            _baselineLoss = 0f;
            _perturbedLoss = 0f;

            _phase = Phase.Baseline;
            _m = 0f;
            _v = 0f;
            _iters = 0;

            _data.Loss = 0f;
            _data.Iteration = 0;
            _data.IsComplete = false;
            _data.IsSuccessful = false;
            _data.IsCanceled = false;
        }

        public void Step(float currentValue) {
            if (!_data.IsStarted || _data.IsComplete) return;

            switch (_phase) {
                case Phase.Baseline:
                    _baselineLoss = ComputeLoss(currentValue);
                    _data.Loss = _baselineLoss;

                    float perturbedParam = _baselineParam + GRADIENT_DELTA;
                    ApplyParam(perturbedParam);

                    _phase = Phase.Perturbed;
                    break;

                case Phase.Perturbed:
                    _perturbedLoss = ComputeLoss(currentValue);
                    _iters++;
                    _data.Iteration = _iters;

                    float gradient = (_perturbedLoss - _baselineLoss) / GRADIENT_DELTA;

                    _m = BETA1 * _m + (1f - BETA1) * gradient;
                    _v = BETA2 * _v + (1f - BETA2) * gradient * gradient;

                    float mHat = _m / (1f - math.pow(BETA1, _iters));
                    float vHat = _v / (1f - math.pow(BETA2, _iters));

                    _baselineParam -= LEARNING_RATE * mHat / (math.sqrt(vHat) + EPSILON);

                    ApplyParam(_baselineParam);

                    _phase = Phase.Baseline;

                    _data.IsComplete = _iters > 0 && (_baselineLoss <= TOLERANCE || _iters >= MAX_ITERS);
                    _data.IsSuccessful = _iters > 0 && _baselineLoss <= TOLERANCE;

                    break;
            }
        }

        private float ComputeLoss(float value) {
            if (_data.ValueType == TargetValueType.Roll ||
                _data.ValueType == TargetValueType.Pitch ||
                _data.ValueType == TargetValueType.Yaw) {
                return math.abs(math.angle(
                    quaternion.Euler(0f, 0f, math.radians(value)),
                    quaternion.Euler(0f, 0f, math.radians(_data.TargetValue))
                ));
            }

            return math.abs(value - _data.TargetValue);
        }

        private void ApplyParam(float param) {
            var adapter = PropertyAdapter.GetAdapter(_data.PropertyType);
            adapter.UpdateKeyframe(_entity, _keyframe.Value.WithValue(param));
        }
    }
}
