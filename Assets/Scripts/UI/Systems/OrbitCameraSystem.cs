using Unity.Cinemachine;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace KexEdit.UI {
    [UpdateInGroup(typeof(UISimulationSystemGroup))]
    public partial class OrbitCameraSystem : SystemBase {
        private CinemachineCamera _cinemachineCamera;
        private CinemachineCamera _rideCamera;
        private UnityEngine.LayerMask _defaultCullingMask;
        private UnityEngine.LayerMask _rideCullingMask;

        private VisualElement _gameView;
        private UnityEngine.Camera _camera;
        private UnityEngine.Transform _target;
        private float2 _lastMousePosition;
        private bool _isOrbiting;
        private bool _isPanning;
        private bool _isFreeLooking;
        private bool _isOverGameView;
        private bool _isRideCameraActive;

        private float3 _currentPosition;
        private float _currentDistance;
        private float _currentPitch;
        private float _currentYaw;
        private bool _currentIsOrthographic;
        private float _currentOrthographicSize;

        private float3 _freeLookPosition;
        private float _freeLookPitch;
        private float _freeLookYaw;

        public static OrbitCameraSystem Instance { get; private set; }
        public static bool IsRideCameraActive => Instance._isRideCameraActive;

        public OrbitCameraSystem() {
            Instance = this;
        }

        protected override void OnCreate() {
            RequireForUpdate<CameraState>();
        }

        protected override void OnStartRunning() {
            var uiService = UIService.Instance;
            _cinemachineCamera = uiService.CinemachineCamera;
            _rideCamera = uiService.RideCamera;
            _defaultCullingMask = uiService.DefaultCullingMask;
            _rideCullingMask = uiService.RideCullingMask;

            var root = uiService.UIDocument.rootVisualElement;
            _gameView = root.Q<GameView>();

            _gameView.RegisterCallback<MouseEnterEvent>(_ => _isOverGameView = true);
            _gameView.RegisterCallback<MouseLeaveEvent>(_ => _isOverGameView = false);

            float2 mousePosition = Mouse.current.position.ReadValue();
            float uiScale = UIScaleSystem.Instance.CurrentScale;
            mousePosition /= uiScale;
            mousePosition.y = _gameView.worldBound.height - mousePosition.y;
            _isOverGameView = _gameView.worldBound.Contains(mousePosition);

            _camera = UnityEngine.Camera.main;
            _target = new UnityEngine.GameObject("CameraTarget").transform;

            var cameraState = SystemAPI.GetSingleton<CameraState>();
            _currentPosition = cameraState.TargetPosition;
            _currentDistance = cameraState.TargetDistance;
            _currentPitch = cameraState.TargetPitch;
            _currentYaw = cameraState.TargetYaw;
            _currentIsOrthographic = cameraState.TargetOrthographic;
            _currentOrthographicSize = cameraState.TargetOrthographicSize;

            _freeLookPosition = _cinemachineCamera.transform.position;
            _freeLookPitch = _currentPitch;
            _freeLookYaw = _currentYaw;

            _camera.cullingMask = _defaultCullingMask;
            _camera.orthographic = _currentIsOrthographic;

            _cinemachineCamera.Lens.ModeOverride = _currentIsOrthographic ?
                LensSettings.OverrideModes.Orthographic :
                LensSettings.OverrideModes.None;

            if (_currentIsOrthographic) {
                _camera.orthographicSize = _currentOrthographicSize;
                _cinemachineCamera.Lens.OrthographicSize = _currentOrthographicSize;
            }

            UpdateCamera();
        }

        protected override void OnUpdate() {
            if (!UnityEngine.Application.isFocused) {
                _isOrbiting = false;
                _isPanning = false;
                _isFreeLooking = false;
                return;
            }

            HandleInput();
            UpdateCamera();
        }

        private void HandleInput() {
            var mouse = Mouse.current;
            var keyboard = Keyboard.current;

            if (keyboard.rKey.wasPressedThisFrame) {
                ToggleRideCameraInternal();
            }

            if (!Extensions.IsTextInputActive()) {
                if (keyboard.numpad1Key.wasPressedThisFrame) {
                    SetFrontViewInternal();
                }
                if (keyboard.numpad3Key.wasPressedThisFrame) {
                    SetSideViewInternal();
                }
                if (keyboard.numpad7Key.wasPressedThisFrame) {
                    SetTopViewInternal();
                }
            }

            if (_isRideCameraActive) return;

            ref var cameraState = ref SystemAPI.GetSingletonRW<CameraState>().ValueRW;

            float2 currentMousePosition = mouse.position.ReadValue();
            float2 mouseDelta = float2.zero;

            if (_isOrbiting || _isPanning || _isFreeLooking) {
                mouseDelta = currentMousePosition - _lastMousePosition;
                mouseDelta = Preferences.AdjustPointerDelta(mouseDelta);
            }

            if (_isOverGameView) {
                bool altOrCmdPressed = keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed ||
                                      keyboard.leftCommandKey.isPressed || keyboard.rightCommandKey.isPressed;

                if (mouse.leftButton.wasPressedThisFrame && altOrCmdPressed) {
                    _isOrbiting = true;
                    cameraState.TargetOrthographic = false;
                    _lastMousePosition = currentMousePosition;
                }

                if (mouse.rightButton.wasPressedThisFrame && !altOrCmdPressed) {
                    _isFreeLooking = true;
                    cameraState.TargetOrthographic = false;

                    _freeLookPosition = _cinemachineCamera.transform.position;
                    _freeLookPitch = _currentPitch;
                    _freeLookYaw = _currentYaw;
                    _lastMousePosition = currentMousePosition;
                }

                if (mouse.middleButton.wasPressedThisFrame ||
                    (mouse.rightButton.wasPressedThisFrame && altOrCmdPressed)) {
                    _isPanning = true;
                    _lastMousePosition = currentMousePosition;
                }
            }

            if (mouse.leftButton.wasReleasedThisFrame) {
                _isOrbiting = false;
            }
            if (mouse.rightButton.wasReleasedThisFrame) {
                if (_isFreeLooking) {
                    cameraState.TargetPosition = _currentPosition;
                    cameraState.TargetPitch = _currentPitch;
                    cameraState.TargetYaw = _currentYaw;
                }
                _isFreeLooking = false;
                _isPanning = false;
            }
            if (mouse.middleButton.wasReleasedThisFrame) {
                _isPanning = false;
            }

            bool processInput = _isOrbiting || _isPanning || _isFreeLooking || _isOverGameView;
            if (!processInput) {
                _lastMousePosition = currentMousePosition;
                return;
            }

            if (_isOrbiting) {
                cameraState.TargetYaw += mouseDelta.x * CameraProperties.OrbitSpeed * 0.01f;
                cameraState.TargetPitch -= mouseDelta.y * CameraProperties.OrbitSpeed * 0.01f;
                cameraState.TargetPitch = math.clamp(cameraState.TargetPitch, -89f, 89f);
            }

            if (_isFreeLooking) {
                _freeLookYaw += mouseDelta.x * CameraProperties.FreeLookSpeed * 0.01f;
                _freeLookPitch -= mouseDelta.y * CameraProperties.FreeLookSpeed * 0.01f;
                _freeLookPitch = math.clamp(_freeLookPitch, -89f, 89f);

                float scroll = Preferences.AdjustScroll(mouse.scroll.ReadValue().y);
                if (math.abs(scroll) > 0.01f) {
                    if (scroll > 0f) {
                        cameraState.SpeedMultiplier += CameraProperties.SpeedMultiplierStep;
                    }
                    else {
                        cameraState.SpeedMultiplier -= CameraProperties.SpeedMultiplierStep;
                    }
                    cameraState.SpeedMultiplier = math.clamp(
                        cameraState.SpeedMultiplier, CameraProperties.MinSpeedMultiplier, CameraProperties.MaxSpeedMultiplier);
                    NotificationSystem.ShowNotification($"Fly Speed: {cameraState.SpeedMultiplier:F1}x");
                }

                float3 movement = float3.zero;
                if (keyboard.wKey.isPressed) movement += new float3(0, 0, 1);
                if (keyboard.sKey.isPressed) movement += new float3(0, 0, -1);
                if (keyboard.aKey.isPressed) movement += new float3(-1, 0, 0);
                if (keyboard.dKey.isPressed) movement += new float3(1, 0, 0);
                if (keyboard.qKey.isPressed) movement += new float3(0, -1, 0);
                if (keyboard.eKey.isPressed) movement += new float3(0, 1, 0);

                if (!movement.Equals(float3.zero)) {
                    quaternion cameraRotation = quaternion.Euler(math.radians(_freeLookPitch), math.radians(_freeLookYaw), 0f);
                    float3 worldMovement = math.mul(cameraRotation, math.normalize(movement));

                    float currentSpeed = CameraProperties.MovementSpeed * cameraState.SpeedMultiplier;
                    if (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed) {
                        currentSpeed *= CameraProperties.FastMovementMultiplier;
                    }

                    _freeLookPosition += currentSpeed * UnityEngine.Time.unscaledDeltaTime * worldMovement;
                }
            }

            if (_isPanning) {
                UnityEngine.Plane panPlane = new(_cinemachineCamera.transform.forward, cameraState.TargetPosition);

                UnityEngine.Ray currentRay = _camera.ScreenPointToRay((UnityEngine.Vector2)currentMousePosition);
                UnityEngine.Ray previousRay = _camera.ScreenPointToRay((UnityEngine.Vector2)_lastMousePosition);

                if (panPlane.Raycast(currentRay, out float currentDist) &&
                    panPlane.Raycast(previousRay, out float prevDist)) {

                    float3 currentHitPoint = currentRay.GetPoint(currentDist);
                    float3 prevHitPoint = previousRay.GetPoint(prevDist);

                    float3 positionDelta = prevHitPoint - currentHitPoint;
                    cameraState.TargetPosition += positionDelta;
                }
            }

            if (_isOverGameView && !_isFreeLooking) {
                float scroll = Preferences.AdjustScroll(mouse.scroll.ReadValue().y);
                if (math.abs(scroll) > 0.01f) {
                    float zoomAmount = scroll * CameraProperties.ZoomSpeed * cameraState.TargetDistance;
                    cameraState.TargetDistance -= zoomAmount;
                    cameraState.TargetDistance = math.clamp(cameraState.TargetDistance, CameraProperties.MinDistance, CameraProperties.MaxDistance);

                    if (cameraState.TargetOrthographic) {
                        cameraState.TargetOrthographicSize = cameraState.TargetDistance * 0.6f;
                    }
                }
            }

            _lastMousePosition = currentMousePosition;
        }

        private void UpdateCamera() {
            ref var cameraState = ref SystemAPI.GetSingletonRW<CameraState>().ValueRW;

            float t = 1f - math.exp(-CameraProperties.Dampening * UnityEngine.Time.unscaledDeltaTime);

            if (_isFreeLooking) {
                _currentPitch = math.lerp(_currentPitch, _freeLookPitch, t);
                _currentYaw = math.lerp(_currentYaw, _freeLookYaw, t);

                float3 currentFreeLookPos = _cinemachineCamera.transform.position;
                float3 smoothedPos = math.lerp(currentFreeLookPos, _freeLookPosition, t);

                quaternion rotation = quaternion.Euler(math.radians(_currentPitch), math.radians(_currentYaw), 0f);
                _cinemachineCamera.transform.SetPositionAndRotation(smoothedPos, rotation);

                float3 forwardDirection = math.mul(rotation, new float3(0, 0, 1));
                _target.transform.position = smoothedPos + forwardDirection * _currentDistance;

                _currentPosition = _target.transform.position;
                _currentIsOrthographic = false;
            }
            else {
                _currentPitch = math.lerp(_currentPitch, cameraState.TargetPitch, t);
                _currentYaw = math.lerp(_currentYaw, cameraState.TargetYaw, t);
                _currentDistance = math.lerp(_currentDistance, cameraState.TargetDistance, t);
                _currentPosition = math.lerp(_currentPosition, cameraState.TargetPosition, t);
                _currentIsOrthographic = _isRideCameraActive ? false : cameraState.TargetOrthographic;
                _currentOrthographicSize = math.lerp(_currentOrthographicSize, cameraState.TargetOrthographicSize, t);

                if (_currentIsOrthographic) {
                    _currentOrthographicSize = _currentDistance * 0.6f;
                }

                quaternion rotation = quaternion.Euler(math.radians(_currentPitch), math.radians(_currentYaw), 0f);
                float3 dir = math.mul(rotation, new float3(0, 0, -1));
                float3 pos = _currentPosition + dir * _currentDistance;

                _cinemachineCamera.transform.SetPositionAndRotation(pos, rotation);
                _target.transform.position = _currentPosition;
            }

            _camera.orthographic = _currentIsOrthographic;
            _cinemachineCamera.Lens.NearClipPlane = _currentIsOrthographic ? CameraProperties.OrthographicNearClip : CameraProperties.PerspectiveNearClip;

            _cinemachineCamera.Lens.ModeOverride = _currentIsOrthographic ?
                LensSettings.OverrideModes.Orthographic :
                LensSettings.OverrideModes.None;

            if (_currentIsOrthographic) {
                _camera.orthographicSize = _currentOrthographicSize;
                _cinemachineCamera.Lens.OrthographicSize = _currentOrthographicSize;
            }

            cameraState.Position = _cinemachineCamera.transform.position;
            cameraState.Pitch = _currentPitch;
            cameraState.Yaw = _currentYaw;
            cameraState.Distance = _currentDistance;
            cameraState.Orthographic = _currentIsOrthographic;
            cameraState.OrthographicSize = _currentOrthographicSize;
        }

        private void ToggleRideCameraInternal() {
            _isRideCameraActive = !_isRideCameraActive;

            if (_isRideCameraActive) {
                _isOrbiting = false;
                _isPanning = false;
                _isFreeLooking = false;

                _rideCamera.Priority = 20;
                _cinemachineCamera.Priority = 0;
                _camera.cullingMask = _rideCullingMask;
            }
            else {
                _rideCamera.Priority = 0;
                _cinemachineCamera.Priority = 10;
                _camera.cullingMask = _defaultCullingMask;
            }
        }

        private void FocusInternal(UnityEngine.Bounds bounds) {
            ref var cameraState = ref SystemAPI.GetSingletonRW<CameraState>().ValueRW;
            cameraState.TargetPosition = bounds.center;

            float fov = _camera.fieldOfView;
            float aspect = _camera.aspect;

            const float padding = 2f;
            float3 extents = (float3)bounds.extents + new float3(padding, padding, padding);

            float radius = math.length(extents);

            float verticalFovRad = math.radians(fov) * 0.5f;
            float horizontalFovRad = math.atan(math.tan(verticalFovRad) * aspect);

            float minFovRad = math.min(verticalFovRad, horizontalFovRad);

            float requiredDistance = radius / math.sin(minFovRad);

            cameraState.TargetDistance = math.clamp(requiredDistance, CameraProperties.MinDistance, CameraProperties.MaxDistance);
        }

        public static void Focus(UnityEngine.Bounds bounds) {
            Instance.FocusInternal(bounds);
        }

        private void ResetStateInternal() {
            ref var cameraState = ref SystemAPI.GetSingletonRW<CameraState>().ValueRW;
            cameraState.TargetPosition = _target.transform.position;
            cameraState.TargetDistance = _cinemachineCamera.transform.position.magnitude;
            cameraState.TargetPitch = _cinemachineCamera.transform.rotation.eulerAngles.x;
            cameraState.TargetYaw = _cinemachineCamera.transform.rotation.eulerAngles.y;
            cameraState.SpeedMultiplier = 1f;

            _isOrbiting = false;
            _isPanning = false;
            _isFreeLooking = false;

            if (_isRideCameraActive) {
                ToggleRideCamera();
            }

            UpdateCamera();
        }

        public static void ResetState() {
            Instance.ResetStateInternal();
        }

        public static void ToggleRideCamera() {
            Instance.ToggleRideCameraInternal();
        }

        private void SetFrontViewInternal() {
            if (_isRideCameraActive) return;
            ref var cameraState = ref SystemAPI.GetSingletonRW<CameraState>().ValueRW;
            cameraState.TargetPitch = 0f;
            cameraState.TargetYaw = 0f;
            cameraState.TargetOrthographic = true;
            if (cameraState.TargetOrthographicSize <= 0.1f) {
                cameraState.TargetOrthographicSize = cameraState.TargetDistance * 0.6f;
            }
            NotificationSystem.ShowNotification("Front View");
        }

        private void SetSideViewInternal() {
            if (_isRideCameraActive) return;
            ref var cameraState = ref SystemAPI.GetSingletonRW<CameraState>().ValueRW;
            cameraState.TargetPitch = 0f;
            cameraState.TargetYaw = -90f;
            cameraState.TargetOrthographic = true;
            if (cameraState.TargetOrthographicSize <= 0.1f) {
                cameraState.TargetOrthographicSize = cameraState.TargetDistance * 0.6f;
            }
            NotificationSystem.ShowNotification("Right View");
        }

        private void SetTopViewInternal() {
            if (_isRideCameraActive) return;
            ref var cameraState = ref SystemAPI.GetSingletonRW<CameraState>().ValueRW;
            cameraState.TargetPitch = 90f;
            cameraState.TargetYaw = 0f;
            cameraState.TargetOrthographic = true;
            if (cameraState.TargetOrthographicSize <= 0.1f) {
                cameraState.TargetOrthographicSize = cameraState.TargetDistance * 0.6f;
            }
            NotificationSystem.ShowNotification("Top View");
        }

        private void SetBackViewInternal() {
            if (_isRideCameraActive) return;
            ref var cameraState = ref SystemAPI.GetSingletonRW<CameraState>().ValueRW;
            cameraState.TargetPitch = 0f;
            cameraState.TargetYaw = 180f;
            cameraState.TargetOrthographic = true;
            if (cameraState.TargetOrthographicSize <= 0.1f) {
                cameraState.TargetOrthographicSize = cameraState.TargetDistance * 0.6f;
            }
            NotificationSystem.ShowNotification("Back View");
        }

        private void SetOtherSideViewInternal() {
            if (_isRideCameraActive) return;
            ref var cameraState = ref SystemAPI.GetSingletonRW<CameraState>().ValueRW;
            cameraState.TargetPitch = 0f;
            cameraState.TargetYaw = 90f;
            cameraState.TargetOrthographic = true;
            if (cameraState.TargetOrthographicSize <= 0.1f) {
                cameraState.TargetOrthographicSize = cameraState.TargetDistance * 0.6f;
            }
            NotificationSystem.ShowNotification("Left View");
        }

        private void SetBottomViewInternal() {
            if (_isRideCameraActive) return;
            ref var cameraState = ref SystemAPI.GetSingletonRW<CameraState>().ValueRW;
            cameraState.TargetPitch = -90f;
            cameraState.TargetYaw = 0f;
            cameraState.TargetOrthographic = true;
            if (cameraState.TargetOrthographicSize <= 0.1f) {
                cameraState.TargetOrthographicSize = cameraState.TargetDistance * 0.6f;
            }
            NotificationSystem.ShowNotification("Bottom View");
        }

        public static void SetFrontView() {
            Instance.SetFrontViewInternal();
        }

        public static void SetSideView() {
            Instance.SetSideViewInternal();
        }

        public static void SetTopView() {
            Instance.SetTopViewInternal();
        }

        public static void SetBackView() {
            Instance.SetBackViewInternal();
        }

        public static void SetOtherSideView() {
            Instance.SetOtherSideViewInternal();
        }

        public static void SetBottomView() {
            Instance.SetBottomViewInternal();
        }

        private void ToggleOrthographicInternal() {
            if (_isRideCameraActive) return;
            ref var cameraState = ref SystemAPI.GetSingletonRW<CameraState>().ValueRW;
            cameraState.TargetOrthographic = !cameraState.TargetOrthographic;

            if (cameraState.TargetOrthographic && cameraState.TargetOrthographicSize <= 0.1f) {
                cameraState.TargetOrthographicSize = cameraState.TargetDistance * 0.6f;
            }

            string mode = cameraState.TargetOrthographic ? "Orthographic" : "Perspective";
            NotificationSystem.ShowNotification($"{mode} View");
        }

        public static void ToggleOrthographic() {
            Instance.ToggleOrthographicInternal();
        }
    }
}
