using UnityEngine;
using KexEdit.Serialization;
using Unity.Entities;
using Unity.Collections;
using System;
using System.Collections;
using System.Collections.Generic;

namespace KexEdit {
    public class TrackLoader : MonoBehaviour {
        public Track Track;
        public GameObject CartRenderer;
        public TrackStyleData TrackStyle;

        private IEnumerator Start() {
            while (SerializationSystem.Instance == null) yield return null;

            SerializationSystem.Instance.DeserializeGraph(Track.Data, restoreUIState: false);

            if (CartRenderer != null) {
                var world = World.DefaultGameObjectInjectionWorld;
                var entityManager = world.EntityManager;
                using var ecb = new EntityCommandBuffer(Allocator.Temp);
                var entity = ecb.CreateEntity();
                ecb.AddComponent(entity, new LoadCartMeshEvent {
                    Cart = CartRenderer
                });
                ecb.SetName(entity, "Load Cart Mesh Event");
                ecb.Playback(entityManager);
            }

            if (TrackStyle != null) {
                var world = World.DefaultGameObjectInjectionWorld;
                var entityManager = world.EntityManager;
                using var ecb = new EntityCommandBuffer(Allocator.Temp);
                var entity = ecb.CreateEntity();
                ecb.AddComponent(entity, new LoadTrackStyleEvent { TrackStyle = TrackStyle });
                ecb.SetName(entity, "Load Track Style Event");
                ecb.Playback(entityManager);
            }
        }

        [Serializable]
        public class TrackStyleData {
            public List<TrackStyleMeshData> Styles = new();
            public int DefaultStyle;
            public bool AutoStyle;
        }

        [Serializable]
        public class TrackStyleMeshData {
            public List<DuplicationMeshSettings> DuplicationMeshes = new();
            public List<ExtrusionMeshSettings> ExtrusionMeshes = new();
            public List<CapMeshSettings> StartCapMeshes = new();
            public List<CapMeshSettings> EndCapMeshes = new();
            public float Spacing;
            public float Threshold;
        }
    }
}
