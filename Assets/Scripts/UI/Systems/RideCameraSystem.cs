using Unity.Cinemachine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace KexEdit.UI {
    [UpdateInGroup(typeof(UIPresentationSystemGroup))]
    public partial class RideCameraSystem : SystemBase {
        private CinemachineCamera _rideCamera;

        protected override void OnStartRunning() {
            _rideCamera = GameObject.Find("RideCamera").GetComponent<CinemachineCamera>();
            if (_rideCamera == null) {
                Debug.LogError("RideCamera not found");
            }
        }

        protected override void OnUpdate() {
            if (_rideCamera == null) return;

            foreach (var (cart, transform) in SystemAPI.Query<Cart, LocalTransform>()) {
                if (!cart.Active || cart.Kinematic) continue;
                quaternion rotation = math.mul(
                    transform.Rotation,
                    quaternion.RotateY(math.PI)
                );
                float3 offset = math.mul(rotation, new float3(
                    0f,
                    Preferences.RideCameraHeight,
                    0f
                ));
                _rideCamera.transform.SetPositionAndRotation(transform.Position + offset, rotation);
                break;
            }
        }
    }
}
