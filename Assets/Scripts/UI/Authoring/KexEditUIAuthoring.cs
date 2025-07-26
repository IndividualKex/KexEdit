using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace KexEdit.UI {
    public class KexEditUIAuthoring : MonoBehaviour {
        private class Baker : Baker<KexEditUIAuthoring> {
            public override void Bake(KexEditUIAuthoring authoring) {
                var entity = GetEntity(TransformUsageFlags.None);

                float3 defaultPosition = new(6f, 6f, 6f);
                float3 defaultEuler = new(30f, -135f, 0f);

                AddComponent(entity, new TimelineState {
                    Offset = 0f,
                    Zoom = 1f
                });

                AddComponent(entity, new NodeGraphState {
                    Pan = float2.zero,
                    Zoom = 1f
                });

                AddComponent(entity, new CameraState {
                    Position = defaultPosition,
                    TargetPosition = defaultPosition,
                    Distance = math.length(defaultPosition),
                    TargetDistance = math.length(defaultPosition),
                    Pitch = defaultEuler.x,
                    TargetPitch = defaultEuler.x,
                    Yaw = defaultEuler.y,
                    TargetYaw = defaultEuler.y,
                    SpeedMultiplier = 1f,
                    OrthographicSize = 1f,
                    TargetOrthographicSize = 1f,
                    Orthographic = false,
                    TargetOrthographic = false
                });

                AddComponent<Gizmos>(entity);
            }
        }
    }
}
