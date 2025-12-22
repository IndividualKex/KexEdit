using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Unity.PerformanceTesting;
using KexEdit.Core;
using KexEdit.Nodes;
using KexEdit.Nodes.Force;
using Keyframe = KexEdit.Core.Keyframe;

#if USE_RUST_BACKEND_BACKEND_BACKEND
using KexEdit.Native.RustCore;
#else
using Unity.Burst;
#endif

namespace Tests.Performance {
    [Category("Performance")]
#if !USE_RUST_BACKEND_BACKEND
    [BurstCompile]
#endif
    public class ForceSectionPerformanceTests {
        private Point CreateAnchorPoint() {
            Frame initialFrame = Frame.FromEuler(0f, 0f, 0f);
            float3 pos = float3.zero;
            float3 dir = initialFrame.Direction;
            float3 norm = initialFrame.Normal;
            float3 lat = initialFrame.Lateral;
            return new Point(
                in pos,
                in dir,
                in norm,
                in lat,
                velocity: 20f,
                energy: 0f,
                normalForce: 1f,
                lateralForce: 0f,
                heartArc: 0f,
                spineArc: 0f,
                heartAdvance: 0f,
                frictionOrigin: 0f,
                rollSpeed: 0f,
                heartOffset: 1f,
                friction: 0.005f,
                resistance: 0.0001f
            );
        }

        private NativeArray<Keyframe> CreateEmptyKeyframes() {
            return new NativeArray<Keyframe>(0, Allocator.Temp);
        }

        private NativeArray<Keyframe> CreateSimpleRollSpeedKeyframes() {
            var keyframes = new NativeArray<Keyframe>(2, Allocator.Temp);
            keyframes[0] = new Keyframe(0f, 0f, InterpolationType.Linear, InterpolationType.Linear);
            keyframes[1] = new Keyframe(5f, 360f, InterpolationType.Linear, InterpolationType.Linear);
            return keyframes;
        }

        private NativeArray<Keyframe> CreateComplexForceKeyframes() {
            var keyframes = new NativeArray<Keyframe>(4, Allocator.Temp);
            keyframes[0] = new Keyframe(0f, 1f, InterpolationType.Bezier, InterpolationType.Bezier, 0f, 2f, 0.5f, 0.5f);
            keyframes[1] = new Keyframe(2f, 4f, InterpolationType.Bezier, InterpolationType.Bezier, 1f, -1f, 0.5f, 0.5f);
            keyframes[2] = new Keyframe(4f, 2f, InterpolationType.Bezier, InterpolationType.Bezier, -0.5f, 0.5f, 0.5f, 0.5f);
            keyframes[3] = new Keyframe(6f, 1f, InterpolationType.Bezier, InterpolationType.Bezier, -1f, 0f, 0.5f, 0.5f);
            return keyframes;
        }

#if USE_RUST_BACKEND_BACKEND
        [Test, Performance]
        public void RustBuildForceSection_SimpleTime() {
            Point anchor = CreateAnchorPoint();
            var emptyKf = CreateEmptyKeyframes();
            var result = new NativeList<Point>(Allocator.Temp);

            Measure.Method(() => {
                int returnCode = RustForceNode.Build(
                    in anchor,
                    duration: 5f,
                    durationType: (int)DurationType.Time,
                    driven: false,
                    rollSpeed: emptyKf,
                    normalForce: emptyKf,
                    lateralForce: emptyKf,
                    drivenVelocity: emptyKf,
                    heartOffset: emptyKf,
                    friction: emptyKf,
                    resistance: emptyKf,
                    anchorHeart: 1f,
                    anchorFriction: 0.005f,
                    anchorResistance: 0.0001f,
                    ref result
                );
                Assert.AreEqual(0, returnCode, "Rust build should succeed");
            })
            .WarmupCount(2)
            .MeasurementCount(10)
            .CleanUp(() => result.Clear())
            .Run();

            result.Dispose();
            emptyKf.Dispose();
        }

        [Test, Performance]
        public void RustBuildForceSection_ComplexTime() {
            Point anchor = CreateAnchorPoint();
            var rollSpeedKf = CreateSimpleRollSpeedKeyframes();
            var normalForceKf = CreateComplexForceKeyframes();
            var lateralForceKf = CreateComplexForceKeyframes();
            var emptyKf = CreateEmptyKeyframes();
            var result = new NativeList<Point>(Allocator.Temp);

            Measure.Method(() => {
                int returnCode = RustForceNode.Build(
                    in anchor,
                    duration: 6f,
                    durationType: (int)DurationType.Time,
                    driven: false,
                    rollSpeed: rollSpeedKf,
                    normalForce: normalForceKf,
                    lateralForce: lateralForceKf,
                    drivenVelocity: emptyKf,
                    heartOffset: emptyKf,
                    friction: emptyKf,
                    resistance: emptyKf,
                    anchorHeart: 1f,
                    anchorFriction: 0.005f,
                    anchorResistance: 0.0001f,
                    ref result
                );
                Assert.AreEqual(0, returnCode, "Rust build should succeed");
            })
            .WarmupCount(2)
            .MeasurementCount(10)
            .CleanUp(() => result.Clear())
            .Run();

            result.Dispose();
            emptyKf.Dispose();
            rollSpeedKf.Dispose();
            normalForceKf.Dispose();
            lateralForceKf.Dispose();
        }

        [Test, Performance]
        public void RustBuildForceSection_SimpleDistance() {
            Point anchor = CreateAnchorPoint();
            var emptyKf = CreateEmptyKeyframes();
            var result = new NativeList<Point>(Allocator.Temp);

            Measure.Method(() => {
                int returnCode = RustForceNode.Build(
                    in anchor,
                    duration: 100f,
                    durationType: (int)DurationType.Distance,
                    driven: false,
                    rollSpeed: emptyKf,
                    normalForce: emptyKf,
                    lateralForce: emptyKf,
                    drivenVelocity: emptyKf,
                    heartOffset: emptyKf,
                    friction: emptyKf,
                    resistance: emptyKf,
                    anchorHeart: 1f,
                    anchorFriction: 0.005f,
                    anchorResistance: 0.0001f,
                    ref result
                );
                Assert.AreEqual(0, returnCode, "Rust build should succeed");
            })
            .WarmupCount(2)
            .MeasurementCount(10)
            .CleanUp(() => result.Clear())
            .Run();

            result.Dispose();
            emptyKf.Dispose();
        }

        [Test, Performance]
        public void RustBuildForceSection_ComplexDistance() {
            Point anchor = CreateAnchorPoint();
            var rollSpeedKf = CreateSimpleRollSpeedKeyframes();
            var normalForceKf = CreateComplexForceKeyframes();
            var lateralForceKf = CreateComplexForceKeyframes();
            var emptyKf = CreateEmptyKeyframes();
            var result = new NativeList<Point>(Allocator.Temp);

            Measure.Method(() => {
                int returnCode = RustForceNode.Build(
                    in anchor,
                    duration: 120f,
                    durationType: (int)DurationType.Distance,
                    driven: false,
                    rollSpeed: rollSpeedKf,
                    normalForce: normalForceKf,
                    lateralForce: lateralForceKf,
                    drivenVelocity: emptyKf,
                    heartOffset: emptyKf,
                    friction: emptyKf,
                    resistance: emptyKf,
                    anchorHeart: 1f,
                    anchorFriction: 0.005f,
                    anchorResistance: 0.0001f,
                    ref result
                );
                Assert.AreEqual(0, returnCode, "Rust build should succeed");
            })
            .WarmupCount(2)
            .MeasurementCount(10)
            .CleanUp(() => result.Clear())
            .Run();

            result.Dispose();
            emptyKf.Dispose();
            rollSpeedKf.Dispose();
            normalForceKf.Dispose();
            lateralForceKf.Dispose();
        }
#else
        private delegate void BuildForceSectionDelegate(
            in Point anchor,
            in IterationConfig config,
            bool driven,
            in NativeArray<Keyframe> rollSpeed,
            in NativeArray<Keyframe> normalForce,
            in NativeArray<Keyframe> lateralForce,
            in NativeArray<Keyframe> drivenVelocity,
            in NativeArray<Keyframe> heartOffset,
            in NativeArray<Keyframe> friction,
            in NativeArray<Keyframe> resistance,
            float anchorHeart,
            float anchorFriction,
            float anchorResistance,
            ref NativeList<Point> result
        );

        [BurstCompile]
        private static void BuildForceSectionBurst(
            in Point anchor,
            in IterationConfig config,
            bool driven,
            in NativeArray<Keyframe> rollSpeed,
            in NativeArray<Keyframe> normalForce,
            in NativeArray<Keyframe> lateralForce,
            in NativeArray<Keyframe> drivenVelocity,
            in NativeArray<Keyframe> heartOffset,
            in NativeArray<Keyframe> friction,
            in NativeArray<Keyframe> resistance,
            float anchorHeart,
            float anchorFriction,
            float anchorResistance,
            ref NativeList<Point> result
        ) {
            ForceNode.Build(
                in anchor, in config, driven,
                in rollSpeed, in normalForce, in lateralForce,
                in drivenVelocity, in heartOffset, in friction, in resistance,
                anchorHeart, anchorFriction, anchorResistance,
                ref result
            );
        }

        [Test, Performance]
        public void BurstBuildForceSection_SimpleTime() {
            var burstFunc = BurstCompiler.CompileFunctionPointer<BuildForceSectionDelegate>(BuildForceSectionBurst);
            Point anchor = CreateAnchorPoint();
            var config = new IterationConfig(5f, DurationType.Time);
            var emptyKf = CreateEmptyKeyframes();
            var result = new NativeList<Point>(Allocator.Temp);

            Measure.Method(() => {
                burstFunc.Invoke(
                    in anchor, in config, false,
                    in emptyKf, in emptyKf, in emptyKf,
                    in emptyKf, in emptyKf, in emptyKf, in emptyKf,
                    1f, 0.005f, 0.0001f,
                    ref result
                );
            })
            .WarmupCount(2)
            .MeasurementCount(10)
            .CleanUp(() => result.Clear())
            .Run();

            result.Dispose();
            emptyKf.Dispose();
        }

        [Test, Performance]
        public void BurstBuildForceSection_ComplexTime() {
            var burstFunc = BurstCompiler.CompileFunctionPointer<BuildForceSectionDelegate>(BuildForceSectionBurst);
            Point anchor = CreateAnchorPoint();
            var config = new IterationConfig(6f, DurationType.Time);
            var rollSpeedKf = CreateSimpleRollSpeedKeyframes();
            var normalForceKf = CreateComplexForceKeyframes();
            var lateralForceKf = CreateComplexForceKeyframes();
            var emptyKf = CreateEmptyKeyframes();
            var result = new NativeList<Point>(Allocator.Temp);

            Measure.Method(() => {
                burstFunc.Invoke(
                    in anchor, in config, false,
                    in rollSpeedKf, in normalForceKf, in lateralForceKf,
                    in emptyKf, in emptyKf, in emptyKf, in emptyKf,
                    1f, 0.005f, 0.0001f,
                    ref result
                );
            })
            .WarmupCount(2)
            .MeasurementCount(10)
            .CleanUp(() => result.Clear())
            .Run();

            result.Dispose();
            emptyKf.Dispose();
            rollSpeedKf.Dispose();
            normalForceKf.Dispose();
            lateralForceKf.Dispose();
        }

        [Test, Performance]
        public void BurstBuildForceSection_SimpleDistance() {
            var burstFunc = BurstCompiler.CompileFunctionPointer<BuildForceSectionDelegate>(BuildForceSectionBurst);
            Point anchor = CreateAnchorPoint();
            var config = new IterationConfig(100f, DurationType.Distance);
            var emptyKf = CreateEmptyKeyframes();
            var result = new NativeList<Point>(Allocator.Temp);

            Measure.Method(() => {
                burstFunc.Invoke(
                    in anchor, in config, false,
                    in emptyKf, in emptyKf, in emptyKf,
                    in emptyKf, in emptyKf, in emptyKf, in emptyKf,
                    1f, 0.005f, 0.0001f,
                    ref result
                );
            })
            .WarmupCount(2)
            .MeasurementCount(10)
            .CleanUp(() => result.Clear())
            .Run();

            result.Dispose();
            emptyKf.Dispose();
        }

        [Test, Performance]
        public void BurstBuildForceSection_ComplexDistance() {
            var burstFunc = BurstCompiler.CompileFunctionPointer<BuildForceSectionDelegate>(BuildForceSectionBurst);
            Point anchor = CreateAnchorPoint();
            var config = new IterationConfig(120f, DurationType.Distance);
            var rollSpeedKf = CreateSimpleRollSpeedKeyframes();
            var normalForceKf = CreateComplexForceKeyframes();
            var lateralForceKf = CreateComplexForceKeyframes();
            var emptyKf = CreateEmptyKeyframes();
            var result = new NativeList<Point>(Allocator.Temp);

            Measure.Method(() => {
                burstFunc.Invoke(
                    in anchor, in config, false,
                    in rollSpeedKf, in normalForceKf, in lateralForceKf,
                    in emptyKf, in emptyKf, in emptyKf, in emptyKf,
                    1f, 0.005f, 0.0001f,
                    ref result
                );
            })
            .WarmupCount(2)
            .MeasurementCount(10)
            .CleanUp(() => result.Clear())
            .Run();

            result.Dispose();
            emptyKf.Dispose();
            rollSpeedKf.Dispose();
            normalForceKf.Dispose();
            lateralForceKf.Dispose();
        }
#endif

        [Test]
        public void VerifyCompilationFlags() {
#if USE_RUST_BACKEND_BACKEND
            Assert.Pass("Running with USE_RUST_BACKEND flag enabled. Rust performance tests are active. To test Burst performance, remove USE_RUST_BACKEND from Project Settings → Player → Scripting Define Symbols (ProjectSettings/ProjectSettings.asset)");
#else
            Assert.Pass("Running with Burst compilation (USE_RUST_BACKEND not defined). Burst performance tests are active. To test Rust performance, add USE_RUST_BACKEND to Project Settings → Player → Scripting Define Symbols (ProjectSettings/ProjectSettings.asset)");
#endif
        }
    }
}
