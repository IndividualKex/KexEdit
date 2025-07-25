using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;
using static KexEdit.UI.Constants;

namespace KexEdit.UI {
    public static class Extensions {
        private static readonly Dictionary<NodeType, string> s_NodeNames = new() {
            { NodeType.ForceSection, "Force Section" },
            { NodeType.GeometricSection, "Geometric Section" },
            { NodeType.CurvedSection, "Curved Section" },
            { NodeType.CopyPathSection, "Copy Path Section" },
            { NodeType.Anchor, "Anchor" },
            { NodeType.Bridge, "Bridge" },
            { NodeType.Reverse, "Reverse" },
            { NodeType.ReversePath, "Reverse Path" },
            { NodeType.Mesh, "Mesh" },
        };

        private static readonly Dictionary<PortType, string> s_InputPortNames = new() {
            { PortType.Anchor, "Input" },
            { PortType.Path, "Path" },
            { PortType.Duration, "Duration" },
            { PortType.Position, "Position" },
            { PortType.Roll, "Roll" },
            { PortType.Pitch, "Pitch" },
            { PortType.Yaw, "Yaw" },
            { PortType.Velocity, "Velocity" },
            { PortType.Heart, "Heart" },
            { PortType.Friction, "Friction" },
            { PortType.Resistance, "Resistance" },
            { PortType.Radius, "Radius" },
            { PortType.Arc, "Arc" },
            { PortType.Axis, "Axis" },
            { PortType.LeadIn, "Lead In" },
            { PortType.LeadOut, "Lead Out" },
            { PortType.Rotation, "Rotation" },
            { PortType.Scale, "Scale" },
        };

        private static readonly Dictionary<PortType, string> s_OutputPortNames = new() {
            { PortType.Anchor, "Output" },
            { PortType.Path, "Path" },
            { PortType.Duration, "Duration" },
            { PortType.Position, "Position" },
            { PortType.Roll, "Roll" },
            { PortType.Pitch, "Pitch" },
            { PortType.Yaw, "Yaw" },
            { PortType.Velocity, "Velocity" },
            { PortType.Heart, "Heart" },
            { PortType.Friction, "Friction" },
            { PortType.Resistance, "Resistance" },
            { PortType.Radius, "Radius" },
            { PortType.Arc, "Arc" },
            { PortType.Axis, "Axis" },
            { PortType.LeadIn, "Lead In" },
            { PortType.LeadOut, "Lead Out" },
            { PortType.Rotation, "Rotation" },
            { PortType.Scale, "Scale" },
        };

        private static readonly EasingType[] s_EasingTypes = {
            EasingType.Sine,
            EasingType.Quadratic,
            EasingType.Cubic,
            EasingType.Quartic,
            EasingType.Quintic,
            EasingType.Exponential
        };

        private static readonly bool s_IsMacOS = SystemInfo.operatingSystemFamily == OperatingSystemFamily.MacOSX;

        private static readonly Dictionary<string, string> s_PlatformShortcuts = s_IsMacOS ? new() {
            ["Ctrl+N"] = "Cmd+N",
            ["Ctrl+O"] = "Cmd+O",
            ["Ctrl+S"] = "Cmd+S",
            ["Ctrl+Z"] = "Cmd+Z",
            ["Ctrl+Y"] = "Cmd+Y",
            ["Ctrl+X"] = "Cmd+X",
            ["Ctrl+C"] = "Cmd+C",
            ["Ctrl+V"] = "Cmd+V",
            ["Ctrl+A"] = "Cmd+A",
            ["Ctrl+H"] = "Cmd+H",
            ["Ctrl++"] = "Cmd++",
            ["Ctrl+-"] = "Cmd+-",
            ["Ctrl+1"] = "Cmd+1",
            ["Ctrl+2"] = "Cmd+2",
            ["Ctrl+3"] = "Cmd+3",
            ["Ctrl+4"] = "Cmd+4",
            ["Ctrl+5"] = "Cmd+5",
            ["Ctrl+6"] = "Cmd+6",
            ["Ctrl+7"] = "Cmd+7",
            ["Alt+A"] = "Option+A",
            ["Alt + Mouse Drag"] = "Option + Mouse Drag",
            ["Alt + Right Mouse Drag"] = "Option + Right Mouse Drag"
        } : null;

        public static string ToPlatformShortcut(this string shortcut) {
            return s_PlatformShortcuts?.TryGetValue(shortcut, out var platformShortcut) == true
                ? platformShortcut
                : shortcut;
        }

        public static void AddPlatformItem(this ContextMenu menu, string text, Action action, string shortcut, bool enabled = true, bool isChecked = false) {
            menu.AddItem(text, action, shortcut.ToPlatformShortcut(), enabled: enabled, isChecked: isChecked);
        }

        public static string GetDisplayName(this NodeType nodeType) {
            return s_NodeNames[nodeType];
        }

        public static string GetDisplayName(this PortType portType, bool isInput, int index = 0) {
            if (portType == PortType.Anchor && isInput && index == 1) {
                return "Target";
            }
            return isInput ? s_InputPortNames[portType] : s_OutputPortNames[portType];
        }

        public static UnitsType GetUnits(this PropertyType propertyType, DurationType durationType) {
            return propertyType switch {
                PropertyType.FixedVelocity => UnitsType.Velocity,
                PropertyType.RollSpeed => durationType == DurationType.Time ? UnitsType.AnglePerTime : UnitsType.AnglePerDistance,
                PropertyType.NormalForce => UnitsType.Force,
                PropertyType.LateralForce => UnitsType.Force,
                PropertyType.PitchSpeed => durationType == DurationType.Time ? UnitsType.AnglePerTime : UnitsType.AnglePerDistance,
                PropertyType.YawSpeed => durationType == DurationType.Time ? UnitsType.AnglePerTime : UnitsType.AnglePerDistance,
                PropertyType.Heart => UnitsType.Distance,
                PropertyType.Friction => UnitsType.None,
                PropertyType.Resistance => UnitsType.Resistance,
                PropertyType.TrackStyle => UnitsType.None,
                _ => UnitsType.None
            };
        }

        public static string ToDisplayString(this UnitsType unitsType) {
            return unitsType switch {
                UnitsType.None => "",
                UnitsType.Time => s_UnitsSeconds,
                UnitsType.Distance => Units.GetDistanceUnitsString(),
                UnitsType.Angle => Units.GetAngleUnitsString(),
                UnitsType.AnglePerTime => Units.GetAnglePerTimeString(),
                UnitsType.AnglePerDistance => Units.GetAnglePerDistanceString(),
                UnitsType.Force => s_UnitsGs,
                UnitsType.Velocity => Units.GetSpeedUnitsString(),
                UnitsType.Resistance => s_UnitsOneOverMicrometers,
                _ => throw new System.ArgumentOutOfRangeException(nameof(unitsType), unitsType, "Unknown UnitsType")
            };
        }

        public static string ToDisplaySuffix(this UnitsType unitsType) {
            return unitsType switch {
                UnitsType.None => "",
                UnitsType.Time => s_UnitsSuffixSeconds,
                UnitsType.Distance => Units.GetDistanceUnitsSuffix(),
                UnitsType.Angle => Units.GetAngleUnitsSuffix(),
                UnitsType.AnglePerTime => Units.GetAnglePerTimeSuffix(),
                UnitsType.AnglePerDistance => Units.GetAnglePerDistanceSuffix(),
                UnitsType.Force => s_UnitsSuffixGs,
                UnitsType.Velocity => Units.GetSpeedUnitsSuffix(),
                UnitsType.Resistance => s_UnitsSuffixOneOverMicrometers,
                _ => throw new System.ArgumentOutOfRangeException(nameof(unitsType), unitsType, "Unknown UnitsType")
            };
        }

        public static float DisplayToValue(this UnitsType unitsType, float display) {
            return unitsType switch {
                UnitsType.Distance => Units.DisplayToDistance(display),
                UnitsType.Angle => Units.DisplayToAngle(display),
                UnitsType.AnglePerTime => Units.DisplayToAnglePerTime(display),
                UnitsType.AnglePerDistance => Units.DisplayToAnglePerDistance(display),
                UnitsType.Velocity => Units.DisplayToSpeed(display),
                _ => display,
            };
        }

        public static float ValueToDisplay(this UnitsType unitsType, float value) {
            return unitsType switch {
                UnitsType.Distance => Units.DistanceToDisplay(value),
                UnitsType.Angle => Units.AngleToDisplay(value),
                UnitsType.AnglePerTime => Units.AnglePerTimeToDisplay(value),
                UnitsType.AnglePerDistance => Units.AnglePerDistanceToDisplay(value),
                UnitsType.Velocity => Units.SpeedToDisplay(value),
                _ => value,
            };
        }

        public static UnitsType GetUnits(this PortType portType) {
            return portType switch {
                PortType.Duration => throw new System.Exception("Duration port type should not be used in GetUnits"),
                PortType.Position => UnitsType.Distance,
                PortType.Roll => UnitsType.Angle,
                PortType.Pitch => UnitsType.Angle,
                PortType.Yaw => UnitsType.Angle,
                PortType.Velocity => UnitsType.Velocity,
                PortType.Heart => UnitsType.Distance,
                PortType.Friction => UnitsType.None,
                PortType.Resistance => UnitsType.Resistance,
                PortType.Radius => UnitsType.Distance,
                PortType.Arc => UnitsType.Angle,
                PortType.Axis => UnitsType.Angle,
                PortType.LeadIn => UnitsType.Angle,
                PortType.LeadOut => UnitsType.Angle,
                PortType.Rotation => UnitsType.Angle,
                PortType.Scale => UnitsType.None,
                _ => UnitsType.None
            };
        }

        public static UnitsType GetUnits(this TargetValueType targetValueType) {
            return targetValueType switch {
                TargetValueType.Roll => UnitsType.Angle,
                TargetValueType.Pitch => UnitsType.Angle,
                TargetValueType.Yaw => UnitsType.Angle,
                TargetValueType.X => UnitsType.Distance,
                TargetValueType.Y => UnitsType.Distance,
                TargetValueType.Z => UnitsType.Distance,
                TargetValueType.NormalForce => UnitsType.Force,
                TargetValueType.LateralForce => UnitsType.Force,
                _ => UnitsType.None
            };
        }

        public static bool IsWithinElement(this VisualElement current, VisualElement element) {
            if (current == null) return false;
            while (current != null) {
                if (current == element) {
                    return true;
                }
                current = current.parent;
            }
            return false;
        }

        public static void ShowContextMenu(
            this VisualElement element,
            Vector2 position,
            Action<ContextMenu> configureMenu
        ) {
            var root = element.panel.visualTree.Q<TemplateContainer>();

            Vector2 worldPos = element.LocalToWorld(position);
            Vector2 rootLocalPos = root.WorldToLocal(worldPos);

            var menu = new ContextMenu();
            configureMenu(menu);

            var tempContainer = new VisualElement();
            tempContainer.style.position = Position.Absolute;
            tempContainer.style.left = -10000f;
            tempContainer.style.top = -10000f;
            tempContainer.Add(menu);
            root.Add(tempContainer);

            bool repositioned = false;
            void OnGeometryChanged(GeometryChangedEvent evt) {
                if (!repositioned && menu.resolvedStyle.width > 0 && menu.resolvedStyle.height > 0) {
                    repositioned = true;
                    menu.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);

                    var menuSize = new Vector2(menu.resolvedStyle.width, menu.resolvedStyle.height);
                    Vector2 adjustedPos = ContextMenu.CalculateMenuPosition(root, rootLocalPos, menuSize);

                    if (tempContainer.parent == root) {
                        tempContainer.Remove(menu);
                        root.Remove(tempContainer);
                    }

                    menu.style.left = adjustedPos.x;
                    menu.style.top = adjustedPos.y;
                    root.Add(menu);
                }
            }
            menu.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);

            void OnMouseDown(MouseDownEvent evt) {
                bool inMenu = IsMouseInMenuHierarchy(evt.target as VisualElement, menu);

                if (!inMenu && menu.parent != null) {
                    if (menu.userData is MenuBar menuBar) {
                        menuBar.ClearActiveItem();
                    }

                    if (menu.parent == root) {
                        root.Remove(menu);
                    }
                    root.UnregisterCallback((EventCallback<MouseDownEvent>)OnMouseDown, TrickleDown.TrickleDown);
                }
            }
            root.RegisterCallback((EventCallback<MouseDownEvent>)OnMouseDown, TrickleDown.TrickleDown);
        }

        public static void ShowConfirmationDialog(
            this VisualElement element,
            string message,
            string confirmText,
            string cancelText,
            Action onConfirm,
            Action onCancel = null
        ) {
            var root = element.panel.visualTree.Q<TemplateContainer>();

            var dialog = new VisualElement {
                style = {
                    position = Position.Absolute,
                    left = 0,
                    right = 0,
                    top = 0,
                    bottom = 0,
                    backgroundColor = new Color(0, 0, 0, 0.5f),
                    alignItems = Align.Center,
                    justifyContent = Justify.Center,
                }
            };

            var panel = new VisualElement {
                style = {
                    backgroundColor = s_BackgroundColor,
                    borderTopLeftRadius = 3f,
                    borderTopRightRadius = 3f,
                    borderBottomLeftRadius = 3f,
                    borderBottomRightRadius = 3f,
                    borderTopWidth = 1f,
                    borderRightWidth = 1f,
                    borderBottomWidth = 1f,
                    borderLeftWidth = 1f,
                    borderTopColor = s_BorderColor,
                    borderRightColor = s_BorderColor,
                    borderBottomColor = s_BorderColor,
                    borderLeftColor = s_BorderColor,
                    paddingTop = 16f,
                    paddingRight = 16f,
                    paddingBottom = 16f,
                    paddingLeft = 16f,
                }
            };

            var messageLabel = new Label(message) {
                style = {
                    whiteSpace = WhiteSpace.Normal,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    color = s_TextColor,
                    fontSize = 14,
                }
            };
            panel.Add(messageLabel);

            var buttonContainer = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    justifyContent = Justify.Center,
                    marginTop = 16f,
                }
            };
            panel.Add(buttonContainer);

            var confirmButton = new Label(confirmText) {
                style = {
                    marginRight = 8f,
                    paddingTop = 8f,
                    paddingRight = 8f,
                    paddingBottom = 8f,
                    paddingLeft = 8f,
                    backgroundColor = s_BackgroundColor,
                    color = s_TextColor,
                    unityTextAlign = TextAnchor.MiddleCenter
                }
            };

            var cancelButton = new Label(cancelText) {
                style = {
                    paddingTop = 8f,
                    paddingRight = 8f,
                    paddingBottom = 8f,
                    paddingLeft = 8f,
                    backgroundColor = s_BackgroundColor,
                    color = s_TextColor,
                    unityTextAlign = TextAnchor.MiddleCenter
                }
            };

            confirmButton.RegisterCallback<MouseEnterEvent>(_ => confirmButton.style.backgroundColor = s_HoverColor);
            confirmButton.RegisterCallback<MouseLeaveEvent>(_ => confirmButton.style.backgroundColor = s_BackgroundColor);
            cancelButton.RegisterCallback<MouseEnterEvent>(_ => cancelButton.style.backgroundColor = s_HoverColor);
            cancelButton.RegisterCallback<MouseLeaveEvent>(_ => cancelButton.style.backgroundColor = s_BackgroundColor);

            confirmButton.RegisterCallback<MouseDownEvent>(_ => {
                root.Remove(dialog);
                onConfirm?.Invoke();
            });

            cancelButton.RegisterCallback<MouseDownEvent>(_ => {
                root.Remove(dialog);
                onCancel?.Invoke();
            });

            buttonContainer.Add(confirmButton);
            buttonContainer.Add(cancelButton);

            dialog.Add(panel);

            panel.style.opacity = 0f;
            panel.style.transitionProperty = new List<StylePropertyName> { "opacity" };
            panel.style.transitionDuration = new List<TimeValue> { new(100, TimeUnit.Millisecond) };
            panel.style.transitionTimingFunction = new List<EasingFunction> { EasingMode.EaseOutCubic };

            root.Add(dialog);

            panel.schedule.Execute(() => panel.style.opacity = 1f);

            dialog.RegisterCallback<MouseDownEvent>(evt => {
                if (evt.target == dialog) {
                    root.Remove(dialog);
                    onCancel?.Invoke();
                    evt.StopPropagation();
                }
            });

            dialog.RegisterCallback<KeyDownEvent>(evt => {
                if (evt.keyCode == KeyCode.Escape) {
                    root.Remove(dialog);
                    onCancel?.Invoke();
                    evt.StopPropagation();
                }
            });

            dialog.focusable = true;
            dialog.Focus();
        }

        public static UnsavedChangesDialog ShowUnsavedChangesDialog(
            this VisualElement element,
            Action onSave,
            Action onDontSave
        ) {
            var root = element.panel.visualTree.Q<TemplateContainer>();
            KexTime.Pause();
            var dialog = new UnsavedChangesDialog(onSave, onDontSave, KexTime.Unpause);
            root.Add(dialog);
            return dialog;
        }

        public static ExportDialog ShowExportDialog(this VisualElement element, Action<float> onExport) {
            var root = element.panel.visualTree.Q<TemplateContainer>();
            KexTime.Pause();
            var dialog = new ExportDialog(onExport, KexTime.Unpause);
            root.Add(dialog);
            return dialog;
        }

        public static ControlsDialog ShowControlsDialog(this VisualElement element) {
            var root = element.panel.visualTree.Q<TemplateContainer>();
            KexTime.Pause();
            var dialog = new ControlsDialog(KexTime.Unpause);
            root.Add(dialog);
            return dialog;
        }

        public static AboutDialog ShowAboutDialog(this VisualElement element) {
            var root = element.panel.visualTree.Q<TemplateContainer>();
            KexTime.Pause();
            var dialog = new AboutDialog(KexTime.Unpause);
            root.Add(dialog);
            return dialog;
        }

        public static OptimizerDialog ShowOptimizerDialog(this VisualElement element, OptimizerData optimizerData) {
            var root = element.panel.visualTree.Q<TemplateContainer>();
            KexTime.Pause();
            var dialog = new OptimizerDialog(KexTime.Unpause, optimizerData);
            root.Add(dialog);
            return dialog;
        }

        public static RideCameraDialog ShowRideCameraDialog(this VisualElement element) {
            var root = element.panel.visualTree.Q<TemplateContainer>();
            KexTime.Pause();
            var dialog = new RideCameraDialog(KexTime.Unpause);
            root.Add(dialog);
            return dialog;
        }

        public static TrackColorPickerDialog ShowColorPickerDialog(this VisualElement element) {
            var root = element.panel.visualTree.Q<TemplateContainer>();
            KexTime.Pause();
            var dialog = new TrackColorPickerDialog(KexTime.Unpause);
            root.Add(dialog);
            return dialog;
        }

        public static FloatField ShowFloatFieldEditor(
            this VisualElement element,
            Vector2 position,
            float initialValue,
            Action<float> onSave,
            UnitsType unitsType = UnitsType.None,
            Action onCancel = null
        ) {
            float displayValue = unitsType.ValueToDisplay(initialValue);

            var floatField = new FloatField {
                value = displayValue,
                formatString = "0.###",
                isDelayed = true,
                style = {
                    position = Position.Absolute,
                    left = position.x,
                    top = position.y,
                    width = 60f,
                }
            };

            element.Add(floatField);
            floatField.Focus();

            void CloseEditor() {
                floatField.parent?.Remove(floatField);
            }

            void SaveValue() {
                float rawValue = unitsType.DisplayToValue(floatField.value);
                onSave?.Invoke(rawValue);
                CloseEditor();
            }

            void CancelEdit() {
                onCancel?.Invoke();
                CloseEditor();
            }

            floatField.RegisterCallback<KeyDownEvent>(evt => {
                if (evt.keyCode == UnityEngine.KeyCode.Return || evt.keyCode == UnityEngine.KeyCode.KeypadEnter) {
                    SaveValue();
                    evt.StopPropagation();
                }
                else if (evt.keyCode == UnityEngine.KeyCode.Escape) {
                    CancelEdit();
                    evt.StopPropagation();
                }
            });

            floatField.RegisterCallback<BlurEvent>(_ => {
                SaveValue();
            });

            return floatField;
        }

        public static bool IsMouseInMenuHierarchy(VisualElement target, ContextMenu rootMenu) {
            VisualElement current = target;
            while (current != null) {
                if (current == rootMenu) {
                    return true;
                }

                if (current is ContextMenu) {
                    return true;
                }

                current = current.parent;
            }
            return false;
        }

        public static EasingType? GetEasingFromWeights(float inWeight, float outWeight) {
            for (int i = 0; i < 6; i++) {
                var easing = (EasingType)i;
                easing.GetEasingHandles(out _, out float testOutWeight, out _, out float testInWeight);
                if (math.abs(outWeight - testOutWeight) < 1e-3f &&
                    math.abs(inWeight - testInWeight) < 1e-3f) {
                    return easing;
                }
            }
            return null;
        }

        public static EasingType? GetEasingFromWeight(float weight, bool isInWeight) {
            for (int i = 0; i < 6; i++) {
                var easing = (EasingType)i;
                easing.GetEasingHandles(out _, out float testOutWeight, out _, out float testInWeight);
                float testWeight = isInWeight ? testInWeight : testOutWeight;
                if (math.abs(weight - testWeight) < 1e-3f) {
                    return easing;
                }
            }
            return null;
        }

        public static void GetEasingHandles(this EasingType easing, out float outTangent, out float outWeight, out float inTangent, out float inWeight) {
            outTangent = 0f;
            inTangent = 0f;

            switch (easing) {
                case EasingType.Sine:
                    outWeight = 0.36f;
                    inWeight = 0.36f;
                    break;
                case EasingType.Quadratic:
                    outWeight = 0.48f;
                    inWeight = 0.48f;
                    break;
                case EasingType.Cubic:
                    outWeight = 0.66f;
                    inWeight = 0.66f;
                    break;
                case EasingType.Quartic:
                    outWeight = 0.76f;
                    inWeight = 0.76f;
                    break;
                case EasingType.Quintic:
                    outWeight = 0.84f;
                    inWeight = 0.84f;
                    break;
                case EasingType.Exponential:
                    outWeight = 0.9f;
                    inWeight = 0.9f;
                    break;
                default:
                    outWeight = 0.36f;
                    inWeight = 0.36f;
                    break;
            }
        }

        public static bool IsTextInputActive() {
            try {
                var root = UIService.Instance.UIDocument.rootVisualElement;
                var focusedElement = root.panel.focusController.focusedElement;
                return focusedElement is FloatField or IntegerField or TextField;
            }
            catch {
                return false;
            }
        }
    }
}
