using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace KexEdit.UI {
    public class OrbitCameraController : MonoBehaviour {
        public CinemachineCamera CinemachineCamera;
        public CinemachineCamera RideCamera;
        public LayerMask DefaultCullingMask;
        public LayerMask RideCullingMask;
        public UIDocument UIDocument;
        public float OrbitSpeed = 100f;
        public float ZoomSpeed = 1f;
        public float MinDistance = 1f;
        public float MaxDistance = 100f;
        public float Dampening = 20f;
        public float PanSpeed = 1f;
        public float FreeLookSpeed = 100f;
        public float MovementSpeed = 10f;
        public float FastMovementMultiplier = 3f;
        public float MinSpeedMultiplier = 0.1f;
        public float MaxSpeedMultiplier = 10f;
        public float SpeedMultiplierStep = 0.1f;

        private VisualElement _gameView;
        private Camera _camera;
        private Transform _target;
        private Vector2 _lastMousePosition;
        private bool _isOrbiting;
        private bool _isPanning;
        private bool _isFreeLooking;
        private bool _isOverGameView;
        private bool _isRideCameraActive;

        private CameraState _currentState;
        private CameraState _initialState;

        public static OrbitCameraController Instance { get; private set; }
        public static bool IsRideCameraActive => Instance._isRideCameraActive;
        public static float SpeedMultiplier => Instance._currentState.SpeedMultiplier;
        public static System.Action<float> OnSpeedMultiplierChanged;

        private void Awake() {
            Instance = this;
        }

        private void Start() {
            var root = UIDocument.rootVisualElement;
            _gameView = root.Q<VisualElement>("GameView");

            _gameView.RegisterCallback<MouseEnterEvent>(_ => _isOverGameView = true);
            _gameView.RegisterCallback<MouseLeaveEvent>(_ => _isOverGameView = false);

            _camera = Camera.main;
            _target = new GameObject("Dummy").transform;

            _currentState = new CameraState {
                Position = _target.position,
                TargetPosition = _target.position,
                Distance = CinemachineCamera.transform.position.magnitude,
                TargetDistance = CinemachineCamera.transform.position.magnitude,
                Pitch = CinemachineCamera.transform.rotation.eulerAngles.x,
                TargetPitch = CinemachineCamera.transform.rotation.eulerAngles.x,
                Yaw = CinemachineCamera.transform.rotation.eulerAngles.y,
                TargetYaw = CinemachineCamera.transform.rotation.eulerAngles.y,
                SpeedMultiplier = 1f
            };

            _camera.cullingMask = DefaultCullingMask;
            _initialState = _currentState;
            UpdateCamera();
        }

        private void Update() {
            HandleInput();
            UpdateCamera();
        }

        private void HandleInput() {
            var mouse = Mouse.current;
            var keyboard = Keyboard.current;

            if (keyboard.rKey.wasPressedThisFrame) {
                ToggleRideCamera();
            }

            if (_isRideCameraActive) return;

            Vector2 currentMousePosition = mouse.position.ReadValue();
            Vector2 mouseDelta = Vector2.zero;

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
                _currentState.TargetYaw += mouseDelta.x * OrbitSpeed * 0.01f;
                _currentState.TargetPitch -= mouseDelta.y * OrbitSpeed * 0.01f;
                _currentState.TargetPitch = Mathf.Clamp(_currentState.TargetPitch, -89f, 89f);
            }

            if (_isFreeLooking) {
                _currentState.TargetYaw += mouseDelta.x * FreeLookSpeed * 0.01f;
                _currentState.TargetPitch -= mouseDelta.y * FreeLookSpeed * 0.01f;
                _currentState.TargetPitch = Mathf.Clamp(_currentState.TargetPitch, -89f, 89f);

                Quaternion rotation = Quaternion.Euler(_currentState.TargetPitch, _currentState.TargetYaw, 0f);
                Vector3 currentCameraPos = CinemachineCamera.transform.position;
                Vector3 forwardDirection = rotation * Vector3.forward;
                _currentState.TargetPosition = currentCameraPos + forwardDirection * _currentState.TargetDistance;

                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f) {
                    if (scroll > 0f) {
                        _currentState.SpeedMultiplier += SpeedMultiplierStep;
                    }
                    else {
                        _currentState.SpeedMultiplier -= SpeedMultiplierStep;
                    }
                    _currentState.SpeedMultiplier = Mathf.Clamp(_currentState.SpeedMultiplier, MinSpeedMultiplier, MaxSpeedMultiplier);
                    OnSpeedMultiplierChanged?.Invoke(_currentState.SpeedMultiplier);
                }

                Vector3 movement = Vector3.zero;
                if (keyboard.wKey.isPressed) movement += Vector3.forward;
                if (keyboard.sKey.isPressed) movement += Vector3.back;
                if (keyboard.aKey.isPressed) movement += Vector3.left;
                if (keyboard.dKey.isPressed) movement += Vector3.right;
                if (keyboard.qKey.isPressed) movement += Vector3.down;
                if (keyboard.eKey.isPressed) movement += Vector3.up;

                if (movement != Vector3.zero) {
                    Quaternion cameraRotation = Quaternion.Euler(_currentState.TargetPitch, _currentState.TargetYaw, 0f);
                    Vector3 worldMovement = cameraRotation * movement.normalized;

                    float currentSpeed = MovementSpeed * _currentState.SpeedMultiplier;
                    if (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed) {
                        currentSpeed *= FastMovementMultiplier;
                    }

                    _currentState.TargetPosition += currentSpeed * Time.unscaledDeltaTime * worldMovement;
                }
            }

            if (_isPanning) {
                Plane panPlane = new(CinemachineCamera.transform.forward, _currentState.TargetPosition);

                Ray currentRay = _camera.ScreenPointToRay(currentMousePosition);
                Ray previousRay = _camera.ScreenPointToRay(_lastMousePosition);

                if (panPlane.Raycast(currentRay, out float currentDist) &&
                    panPlane.Raycast(previousRay, out float prevDist)) {

                    Vector3 currentHitPoint = currentRay.GetPoint(currentDist);
                    Vector3 prevHitPoint = previousRay.GetPoint(prevDist);

                    Vector3 positionDelta = prevHitPoint - currentHitPoint;
                    _currentState.TargetPosition += positionDelta;
                }
            }

            if (_isOverGameView && !_isFreeLooking) {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f) {
                    float zoomAmount = scroll * ZoomSpeed * _currentState.TargetDistance;
                    _currentState.TargetDistance -= zoomAmount;
                    _currentState.TargetDistance = Mathf.Clamp(_currentState.TargetDistance, MinDistance, MaxDistance);
                }
            }

            _lastMousePosition = currentMousePosition;
        }

        private void UpdateCamera() {
            _currentState.UpdateSmooth(Dampening);
            _currentState.ApplyToCamera(CinemachineCamera);
        }

        private void FocusInternal(Bounds bounds) {
            _currentState.TargetPosition = bounds.center;

            float fov = _camera.fieldOfView;
            float aspect = _camera.aspect;

            const float padding = 2f;
            Vector3 extents = bounds.extents + new Vector3(padding, padding, padding);

            float radius = extents.magnitude;

            float verticalFovRad = Mathf.Deg2Rad * fov * 0.5f;
            float horizontalFovRad = Mathf.Atan(Mathf.Tan(verticalFovRad) * aspect);

            float minFovRad = Mathf.Min(verticalFovRad, horizontalFovRad);

            float requiredDistance = radius / Mathf.Sin(minFovRad);

            _currentState.TargetDistance = Mathf.Clamp(requiredDistance, MinDistance, MaxDistance);

            UpdateCamera();
        }

        public static void Focus(Bounds bounds) {
            Instance.FocusInternal(bounds);
        }

        private void ToggleRideCamera() {
            _isRideCameraActive = !_isRideCameraActive;

            if (_isRideCameraActive) {
                _isOrbiting = false;
                _isPanning = false;
                _isFreeLooking = false;

                RideCamera.Priority = 20;
                CinemachineCamera.Priority = 0;
                _camera.cullingMask = RideCullingMask;
            }
            else {
                RideCamera.Priority = 0;
                CinemachineCamera.Priority = 10;
                _camera.cullingMask = DefaultCullingMask;
            }
        }

        public void ResetState() {
            _currentState = _initialState;
            _isOrbiting = false;
            _isPanning = false;
            _isFreeLooking = false;

            if (_isRideCameraActive) {
                ToggleRideCamera();
            }

            UpdateCamera();
        }
    }

    public struct CameraState {
        public Vector3 Position;
        public Vector3 TargetPosition;
        public float Distance;
        public float TargetDistance;
        public float Pitch;
        public float TargetPitch;
        public float Yaw;
        public float TargetYaw;
        public float SpeedMultiplier;

        public void UpdateSmooth(float dampening) {
            float t = 1f - Mathf.Exp(-dampening * Time.unscaledDeltaTime);
            Pitch = Mathf.LerpAngle(Pitch, TargetPitch, t);
            Yaw = Mathf.LerpAngle(Yaw, TargetYaw, t);
            Distance = Mathf.Lerp(Distance, TargetDistance, t);
            Position = Vector3.Lerp(Position, TargetPosition, t);
        }

        public void ApplyToCamera(CinemachineCamera camera) {
            Quaternion rotation = Quaternion.Euler(Pitch, Yaw, 0f);
            Vector3 dir = rotation * Vector3.back;
            Vector3 pos = Position + dir * Distance;
            camera.transform.SetPositionAndRotation(pos, rotation);
        }
    }
}
