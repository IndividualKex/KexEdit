using KexEdit.Legacy;
using KexEdit;
using NUnit.Framework;
using Unity.Entities;
using Unity.Entities.Tests;

namespace Tests {
    [TestFixture]
    [Category("Golden")]
    public class BuildBridgeSectionSystemTests : ECSTestsFixture {
        private SystemHandle _buildSystem;
        private SystemHandle _ecbSystem;

        [SetUp]
        public override void Setup() {
            base.Setup();
            _ecbSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            _buildSystem = World.GetOrCreateSystem<BuildBridgeSystem>();
        }

        [Test]
        public void AllTypes_BridgeSection1_MatchesGoldData() {
            var gold = GoldDataLoader.Load("Assets/Tests/TrackData/all_types.json");
            var bridgeSections = GoldDataLoader.GetBridgeSections(gold);

            if (bridgeSections.Count == 0) {
                Assert.Ignore("No BridgeSection found in all_types.json. Export gold data with a bridge section first.");
                return;
            }

            var section = bridgeSections[0];
            var entity = BridgeSectionEntityBuilder.Create(m_Manager, section);

            _buildSystem.Update(World.Unmanaged);
            _ecbSystem.Update(World.Unmanaged);

            var points = m_Manager.GetBuffer<CorePointBuffer>(entity);
            PointComparer.AssertPointsMatch(points, section.outputs.points);
        }
    }
}
