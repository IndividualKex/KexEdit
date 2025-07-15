using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace KexEdit {
    public class UIStateAuthoring : MonoBehaviour {
        private class Baker : Baker<UIStateAuthoring> {
            public override void Bake(UIStateAuthoring authoring) {
                var entity = GetEntity(TransformUsageFlags.None);

                float3 defaultPosition = new(6f, 6f, 6f);
                float3 defaultEuler = new(30f, -135f, 0f);

                AddComponent(entity, new UIState {
                    TimelineOffset = 0f,
                    TimelineZoom = 1f,
                    NodeGraphPan = float2.zero,
                    NodeGraphZoom = 1f,
                    CameraPosition = defaultPosition,
                    CameraTargetPosition = defaultPosition,
                    CameraDistance = math.length(defaultPosition),
                    CameraTargetDistance = math.length(defaultPosition),
                    CameraPitch = defaultEuler.x,
                    CameraTargetPitch = defaultEuler.x,
                    CameraYaw = defaultEuler.y,
                    CameraTargetYaw = defaultEuler.y,
                    CameraSpeedMultiplier = 1f
                });
            }
        }
    }
}
