using KexEdit.Native.RustCore;
using NUnit.Framework;
using Unity.Mathematics;

namespace Tests {
    [TestFixture]
    [Category("Validation")]
    public class QuaternionEquivalenceTests {
        private const float TOLERANCE = 1e-6f;

        [Test]
        public void QuaternionMul_MatchesUnity() {
            float3 axis1 = math.normalize(new float3(1, 0, 0));
            float angle1 = 0.1f;
            quaternion unityQ1 = quaternion.AxisAngle(axis1, angle1);
            RustQuaternion rustQ1 = new RustQuaternion {
                x = unityQ1.value.x,
                y = unityQ1.value.y,
                z = unityQ1.value.z,
                w = unityQ1.value.w
            };

            float3 axis2 = math.normalize(new float3(0, 1, 0));
            float angle2 = 0.05f;
            quaternion unityQ2 = quaternion.AxisAngle(axis2, angle2);
            RustQuaternion rustQ2 = new RustQuaternion {
                x = unityQ2.value.x,
                y = unityQ2.value.y,
                z = unityQ2.value.z,
                w = unityQ2.value.w
            };

            quaternion unityResult = math.mul(unityQ1, unityQ2);
            RustQuaternion rustResult = RustQuaternion.Mul(rustQ1, rustQ2);

            Assert.AreEqual(unityResult.value.x, rustResult.x, TOLERANCE, $"x: unity={unityResult.value.x}, rust={rustResult.x}");
            Assert.AreEqual(unityResult.value.y, rustResult.y, TOLERANCE, $"y: unity={unityResult.value.y}, rust={rustResult.y}");
            Assert.AreEqual(unityResult.value.z, rustResult.z, TOLERANCE, $"z: unity={unityResult.value.z}, rust={rustResult.z}");
            Assert.AreEqual(unityResult.value.w, rustResult.w, TOLERANCE, $"w: unity={unityResult.value.w}, rust={rustResult.w}");
        }

        [Test]
        public void QuaternionMulVec_MatchesUnity() {
            float3 axis = math.normalize(new float3(0, 1, 0));
            float angle = math.PI / 4f;
            quaternion unityQ = quaternion.AxisAngle(axis, angle);
            RustQuaternion rustQ = new RustQuaternion {
                x = unityQ.value.x,
                y = unityQ.value.y,
                z = unityQ.value.z,
                w = unityQ.value.w
            };

            float3 vec = new float3(1, 0, 0);

            float3 unityResult = math.mul(unityQ, vec);
            float3 rustResult = RustQuaternion.MulVec(rustQ, vec);

            Assert.AreEqual(unityResult.x, rustResult.x, TOLERANCE, $"x: unity={unityResult.x}, rust={rustResult.x}");
            Assert.AreEqual(unityResult.y, rustResult.y, TOLERANCE, $"y: unity={unityResult.y}, rust={rustResult.y}");
            Assert.AreEqual(unityResult.z, rustResult.z, TOLERANCE, $"z: unity={unityResult.z}, rust={rustResult.z}");
        }

        [Test]
        public void CombinedRotation_ReproducesDrift() {
            float3 prevDirection = math.back();
            float3 prevNormal = math.down();
            float3 prevLateral = math.right();

            float normalAccel = 0.5f;
            float lateralAccel = 0.3f;
            float estimatedVelocity = 10f;
            float safeVelocity = 10f;
            float hz = 100f;

            quaternion unityQNormal = quaternion.AxisAngle(prevLateral, normalAccel / estimatedVelocity / hz);
            quaternion unityQLateral = quaternion.AxisAngle(prevNormal, -lateralAccel / safeVelocity / hz);
            float3 unityDirection = math.normalize(math.mul(math.mul(unityQNormal, unityQLateral), prevDirection));

            RustQuaternion rustQNormal = new RustQuaternion {
                x = unityQNormal.value.x,
                y = unityQNormal.value.y,
                z = unityQNormal.value.z,
                w = unityQNormal.value.w
            };
            RustQuaternion rustQLateral = new RustQuaternion {
                x = unityQLateral.value.x,
                y = unityQLateral.value.y,
                z = unityQLateral.value.z,
                w = unityQLateral.value.w
            };
            RustQuaternion rustCombined = RustQuaternion.Mul(rustQNormal, rustQLateral);
            float3 rustDirection = math.normalize(RustQuaternion.MulVec(rustCombined, prevDirection));

            float error = math.distance(unityDirection, rustDirection);
            UnityEngine.Debug.Log($"Direction error: {error:e} (unity={unityDirection}, rust={rustDirection})");
            UnityEngine.Debug.Log($"Unity combined: ({math.mul(unityQNormal, unityQLateral).value.x}, {math.mul(unityQNormal, unityQLateral).value.y}, {math.mul(unityQNormal, unityQLateral).value.z}, {math.mul(unityQNormal, unityQLateral).value.w})");
            UnityEngine.Debug.Log($"Rust combined: ({rustCombined.x}, {rustCombined.y}, {rustCombined.z}, {rustCombined.w})");

            Assert.AreEqual(unityDirection.x, rustDirection.x, 1e-5f, $"x: unity={unityDirection.x}, rust={rustDirection.x}");
            Assert.AreEqual(unityDirection.y, rustDirection.y, 1e-5f, $"y: unity={unityDirection.y}, rust={rustDirection.y}");
            Assert.AreEqual(unityDirection.z, rustDirection.z, 1e-5f, $"z: unity={unityDirection.z}, rust={rustDirection.z}");
        }
    }
}
