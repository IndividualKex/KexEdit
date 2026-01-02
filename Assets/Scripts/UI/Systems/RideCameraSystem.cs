using KexEdit.Legacy;
using KexEdit.Trains;
using Unity.Cinemachine;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace KexEdit.UI {
    [UpdateInGroup(typeof(UIPresentationSystemGroup))]
    public partial class RideCameraSystem : SystemBase {
        private CinemachineCamera _rideCamera;

        protected override void OnCreate() {
            RequireForUpdate<SimFollowerSingleton>();
            RequireForUpdate<TrackSingleton>();
        }

        protected override void OnStartRunning() {
            _rideCamera = GameObject.Find("RideCamera").GetComponent<CinemachineCamera>();
            if (_rideCamera == null) {
                Debug.LogError("RideCamera not found");
            }
        }

        protected override void OnUpdate() {
            if (_rideCamera == null) return;

            var track = SystemAPI.GetSingleton<TrackSingleton>().Value;
            var follower = SystemAPI.GetSingleton<SimFollowerSingleton>().Follower;

            if (!TrainCarLogic.TryGetSplinePoint(in follower, in track, offset: 0f, out var sp)) return;

            quaternion baseRotation = quaternion.LookRotation(sp.Direction, -sp.Normal);

            quaternion userRotation = quaternion.EulerXYZ(
                math.radians(Preferences.RideCameraRotationX),
                math.radians(Preferences.RideCameraRotationY),
                math.radians(Preferences.RideCameraRotationZ)
            );

            quaternion finalRotation = math.mul(baseRotation, userRotation);

            float3 positionOffset = new(
                Preferences.RideCameraPositionX,
                Preferences.RideCameraPositionY,
                Preferences.RideCameraPositionZ
            );

            float3 worldOffset = math.mul(baseRotation, positionOffset);

            _rideCamera.transform.SetPositionAndRotation(sp.Position + worldOffset, finalRotation);
        }
    }
}
