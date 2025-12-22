using KexEdit.Legacy;
using KexEdit;
using NUnit.Framework;
using Unity.Entities;
using Unity.Entities.Tests;

namespace Tests {
    [TestFixture]
    [Category("Golden")]
    public class BuildCopyPathSectionSystemTests : ECSTestsFixture {
        private SystemHandle _buildSystem;
        private SystemHandle _ecbSystem;

        [SetUp]
        public override void Setup() {
            base.Setup();
            _ecbSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            _buildSystem = World.GetOrCreateSystem<BuildCopyPathSectionSystem>();
        }

        [Test]
        public void AllTypes_CopyPathSection1_MatchesGoldData() {
            var gold = GoldDataLoader.Load("Assets/Tests/TrackData/all_types.json");
            var section = GoldDataLoader.GetCopyPathSectionByIndex(gold, 0);

            var entity = CopyPathSectionEntityBuilder.Create(m_Manager, section);

            _buildSystem.Update(World.Unmanaged);
            _ecbSystem.Update(World.Unmanaged);

            var points = m_Manager.GetBuffer<CorePointBuffer>(entity);
            PointComparer.AssertPointsMatch(points, section.outputs.points);
        }

        [Test]
        public void AllTypes_CopyPathSection2_MatchesGoldData() {
            var gold = GoldDataLoader.Load("Assets/Tests/TrackData/all_types.json");
            var section = GoldDataLoader.GetCopyPathSectionByIndex(gold, 1);

            var entity = CopyPathSectionEntityBuilder.Create(m_Manager, section);

            _buildSystem.Update(World.Unmanaged);
            _ecbSystem.Update(World.Unmanaged);

            var points = m_Manager.GetBuffer<CorePointBuffer>(entity);
            PointComparer.AssertPointsMatch(points, section.outputs.points);
        }

        [Test]
        public void AllTypes_CopyPathSection3_MatchesGoldData() {
            var gold = GoldDataLoader.Load("Assets/Tests/TrackData/all_types.json");
            var section = GoldDataLoader.GetCopyPathSectionByIndex(gold, 2);

            var entity = CopyPathSectionEntityBuilder.Create(m_Manager, section);

            _buildSystem.Update(World.Unmanaged);
            _ecbSystem.Update(World.Unmanaged);

            var points = m_Manager.GetBuffer<CorePointBuffer>(entity);
            PointComparer.AssertPointsMatch(points, section.outputs.points);
        }
    }
}
