using KexEdit.Legacy;
using KexEdit;
using NUnit.Framework;
using Unity.Entities;
using Unity.Entities.Tests;

namespace Tests {
    [TestFixture]
    [Category("Golden")]
    public class BuildCurvedSectionSystemTests : ECSTestsFixture {
        private SystemHandle _buildSystem;
        private SystemHandle _ecbSystem;

        [SetUp]
        public override void Setup() {
            base.Setup();
            _ecbSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            _buildSystem = World.GetOrCreateSystem<BuildCurvedSectionSystem>();
        }

        [Test]
        public void Veloci_CurvedSection1_MatchesGoldData() {
            var gold = GoldDataLoader.Load("Assets/Tests/TrackData/veloci.json");
            var section = GoldDataLoader.GetCurvedSectionByIndex(gold, 0);

            var entity = CurvedSectionEntityBuilder.Create(m_Manager, section);

            _buildSystem.Update(World.Unmanaged);
            _ecbSystem.Update(World.Unmanaged);

            var points = m_Manager.GetBuffer<CorePointBuffer>(entity);
            PointComparer.AssertPointsMatch(points, section.outputs.points);
        }
    }
}
