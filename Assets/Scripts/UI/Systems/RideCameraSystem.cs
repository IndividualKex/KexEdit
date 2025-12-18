using Unity.Cinemachine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

using KexEdit.Legacy;
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

            foreach (var (train, coaster, transform) in SystemAPI.Query<Train, CoasterReference, LocalTransform>()) {
                if (!train.Enabled || train.Kinematic || !SystemAPI.HasComponent<EditorCoasterTag>(coaster)) continue;

                quaternion trainRotation = math.mul(
                    transform.Rotation,
                    quaternion.RotateY(math.PI)
                );

                quaternion userRotation = quaternion.EulerXYZ(
                    math.radians(Preferences.RideCameraRotationX),
                    math.radians(Preferences.RideCameraRotationY),
                    math.radians(Preferences.RideCameraRotationZ)
                );

                quaternion finalRotation = math.mul(trainRotation, userRotation);

                float3 positionOffset = new(
                    Preferences.RideCameraPositionX,
                    Preferences.RideCameraPositionY,
                    Preferences.RideCameraPositionZ
                );

                float3 worldOffset = math.mul(trainRotation, positionOffset);

                _rideCamera.transform.SetPositionAndRotation(transform.Position + worldOffset, finalRotation);
                break;
            }
        }
    }
}
