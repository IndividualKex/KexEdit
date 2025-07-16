using Unity.Cinemachine;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace KexEdit.UI {
    [UpdateInGroup(typeof(SimulationSystemGroup))]
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

        public static OrbitCameraSystem Instance { get; private set; }
        public static bool IsRideCameraActive => Instance._isRideCameraActive;

        public static System.Action<float> OnSpeedMultiplierChanged;

        public OrbitCameraSystem() {
            Instance = this;
        }

        protected override void OnCreate() {
            RequireForUpdate<UIState>();
        }

        protected override void OnStartRunning() {
            var uiService = UIService.Instance;
            _cinemachineCamera = uiService.CinemachineCamera;
            _rideCamera = uiService.RideCamera;
            _defaultCullingMask = uiService.DefaultCullingMask;
            _rideCullingMask = uiService.RideCullingMask;

            var root = uiService.UIDocument.rootVisualElement;
            _gameView = root.Q<VisualElement>("GameView");

            _gameView.RegisterCallback<MouseEnterEvent>(_ => _isOverGameView = true);
            _gameView.RegisterCallback<MouseLeaveEvent>(_ => _isOverGameView = false);

            _camera = UnityEngine.Camera.main;
            _target = new UnityEngine.GameObject("CameraTarget").transform;

            var uiState = SystemAPI.GetSingleton<UIState>();
            _currentPosition = uiState.CameraTargetPosition;
            _currentDistance = uiState.CameraTargetDistance;
            _currentPitch = uiState.CameraTargetPitch;
            _currentYaw = uiState.CameraTargetYaw;

            _camera.cullingMask = _defaultCullingMask;
            UpdateCamera();
        }

        protected override void OnUpdate() {
            if (!UnityEngine.Application.isFocused) {
                _isOrbiting = false;
                _isPanning = false;
                _isFreeLooking = false;
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

            if (_isRideCameraActive) return;

            ref var uiState = ref SystemAPI.GetSingletonRW<UIState>().ValueRW;

            float2 currentMousePosition = mouse.position.ReadValue();
            float2 mouseDelta = float2.zero;

            if (_isOrbiting || _isPanning || _isFreeLooking) {
                mouseDelta = currentMousePosition - _lastMousePosition;
            }

            if (_isOverGameView) {
                bool altOrCmdPressed = keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed ||
                                      keyboard.leftCommandKey.isPressed || keyboard.rightCommandKey.isPressed;

                if (mouse.leftButton.wasPressedThisFrame && altOrCmdPressed) {
                    _isOrbiting = true;
                }

                if (mouse.rightButton.wasPressedThisFrame && !altOrCmdPressed) {
                    _isFreeLooking = true;
                }

                if (mouse.middleButton.wasPressedThisFrame ||
                    (mouse.rightButton.wasPressedThisFrame && altOrCmdPressed)) {
                    _isPanning = true;
                }
            }

            if (mouse.leftButton.wasReleasedThisFrame) {
                _isOrbiting = false;
            }
            if (mouse.rightButton.wasReleasedThisFrame) {
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
                uiState.CameraTargetYaw += mouseDelta.x * CameraProperties.OrbitSpeed * 0.01f;
                uiState.CameraTargetPitch -= mouseDelta.y * CameraProperties.OrbitSpeed * 0.01f;
                uiState.CameraTargetPitch = math.clamp(uiState.CameraTargetPitch, -89f, 89f);
            }

            if (_isFreeLooking) {
                uiState.CameraTargetYaw += mouseDelta.x * CameraProperties.FreeLookSpeed * 0.01f;
                uiState.CameraTargetPitch -= mouseDelta.y * CameraProperties.FreeLookSpeed * 0.01f;
                uiState.CameraTargetPitch = math.clamp(uiState.CameraTargetPitch, -89f, 89f);

                quaternion rotation = quaternion.Euler(math.radians(uiState.CameraTargetPitch), math.radians(uiState.CameraTargetYaw), 0f);
                float3 currentCameraPos = _cinemachineCamera.transform.position;
                float3 forwardDirection = math.mul(rotation, new float3(0, 0, 1));
                uiState.CameraTargetPosition = currentCameraPos + forwardDirection * uiState.CameraTargetDistance;

                float scroll = mouse.scroll.ReadValue().y;
                if (math.abs(scroll) > 0.01f) {
                    if (scroll > 0f) {
                        uiState.CameraSpeedMultiplier += CameraProperties.SpeedMultiplierStep;
                    }
                    else {
                        uiState.CameraSpeedMultiplier -= CameraProperties.SpeedMultiplierStep;
                    }
                    uiState.CameraSpeedMultiplier = math.clamp(uiState.CameraSpeedMultiplier, CameraProperties.MinSpeedMultiplier, CameraProperties.MaxSpeedMultiplier);
                    OnSpeedMultiplierChanged?.Invoke(uiState.CameraSpeedMultiplier);
                }

                float3 movement = float3.zero;
                if (keyboard.wKey.isPressed) movement += new float3(0, 0, 1);
                if (keyboard.sKey.isPressed) movement += new float3(0, 0, -1);
                if (keyboard.aKey.isPressed) movement += new float3(-1, 0, 0);
                if (keyboard.dKey.isPressed) movement += new float3(1, 0, 0);
                if (keyboard.qKey.isPressed) movement += new float3(0, -1, 0);
                if (keyboard.eKey.isPressed) movement += new float3(0, 1, 0);

                if (!movement.Equals(float3.zero)) {
                    quaternion cameraRotation = quaternion.Euler(math.radians(uiState.CameraTargetPitch), math.radians(uiState.CameraTargetYaw), 0f);
                    float3 worldMovement = math.mul(cameraRotation, math.normalize(movement));

                    float currentSpeed = CameraProperties.MovementSpeed * uiState.CameraSpeedMultiplier;
                    if (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed) {
                        currentSpeed *= CameraProperties.FastMovementMultiplier;
                    }

                    uiState.CameraTargetPosition += currentSpeed * UnityEngine.Time.unscaledDeltaTime * worldMovement;
                }
            }

            if (_isPanning) {
                UnityEngine.Plane panPlane = new(_cinemachineCamera.transform.forward, uiState.CameraTargetPosition);

                UnityEngine.Ray currentRay = _camera.ScreenPointToRay((UnityEngine.Vector2)currentMousePosition);
                UnityEngine.Ray previousRay = _camera.ScreenPointToRay((UnityEngine.Vector2)_lastMousePosition);

                if (panPlane.Raycast(currentRay, out float currentDist) &&
                    panPlane.Raycast(previousRay, out float prevDist)) {

                    float3 currentHitPoint = currentRay.GetPoint(currentDist);
                    float3 prevHitPoint = previousRay.GetPoint(prevDist);

                    float3 positionDelta = prevHitPoint - currentHitPoint;
                    uiState.CameraTargetPosition += positionDelta;
                }
            }

            if (_isOverGameView && !_isFreeLooking) {
                float scroll = mouse.scroll.ReadValue().y;
                if (math.abs(scroll) > 0.01f) {
                    float zoomAmount = scroll * CameraProperties.ZoomSpeed * uiState.CameraTargetDistance;
                    uiState.CameraTargetDistance -= zoomAmount;
                    uiState.CameraTargetDistance = math.clamp(uiState.CameraTargetDistance, CameraProperties.MinDistance, CameraProperties.MaxDistance);
                }
            }

            _lastMousePosition = currentMousePosition;
        }

        private void UpdateCamera() {
            ref var uiState = ref SystemAPI.GetSingletonRW<UIState>().ValueRW;

            float t = 1f - math.exp(-CameraProperties.Dampening * UnityEngine.Time.unscaledDeltaTime);
            _currentPitch = math.lerp(_currentPitch, uiState.CameraTargetPitch, t);
            _currentYaw = math.lerp(_currentYaw, uiState.CameraTargetYaw, t);
            _currentDistance = math.lerp(_currentDistance, uiState.CameraTargetDistance, t);
            _currentPosition = math.lerp(_currentPosition, uiState.CameraTargetPosition, t);

            quaternion rotation = quaternion.Euler(math.radians(_currentPitch), math.radians(_currentYaw), 0f);
            float3 dir = math.mul(rotation, new float3(0, 0, -1));
            float3 pos = _currentPosition + dir * _currentDistance;

            _cinemachineCamera.transform.SetPositionAndRotation(pos, rotation);
            _target.transform.position = _currentPosition;

            uiState.CameraPosition = pos;
            uiState.CameraPitch = _currentPitch;
            uiState.CameraYaw = _currentYaw;
            uiState.CameraDistance = _currentDistance;
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
            ref var uiState = ref SystemAPI.GetSingletonRW<UIState>().ValueRW;
            uiState.CameraTargetPosition = bounds.center;

            float fov = _camera.fieldOfView;
            float aspect = _camera.aspect;

            const float padding = 2f;
            float3 extents = (float3)bounds.extents + new float3(padding, padding, padding);

            float radius = math.length(extents);

            float verticalFovRad = math.radians(fov) * 0.5f;
            float horizontalFovRad = math.atan(math.tan(verticalFovRad) * aspect);

            float minFovRad = math.min(verticalFovRad, horizontalFovRad);

            float requiredDistance = radius / math.sin(minFovRad);

            uiState.CameraTargetDistance = math.clamp(requiredDistance, CameraProperties.MinDistance, CameraProperties.MaxDistance);
        }

        public static void Focus(UnityEngine.Bounds bounds) {
            Instance.FocusInternal(bounds);
        }

        private void ResetStateInternal() {
            ref var uiState = ref SystemAPI.GetSingletonRW<UIState>().ValueRW;
            uiState.CameraTargetPosition = _target.transform.position;
            uiState.CameraTargetDistance = _cinemachineCamera.transform.position.magnitude;
            uiState.CameraTargetPitch = _cinemachineCamera.transform.rotation.eulerAngles.x;
            uiState.CameraTargetYaw = _cinemachineCamera.transform.rotation.eulerAngles.y;
            uiState.CameraSpeedMultiplier = 1f;

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
    }
}
