using System.IO;
using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using KexEdit.UI.Timeline;

namespace KexEdit.UI {
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class NavigationMenuSystem : SystemBase {
        private VisualElement _root;
        private Timeline.Timeline _timeline;
        private Label _titleLabel;
        private bool _bypassUnsavedChangesCheck;

        private bool _initialized;

        protected override void OnStartRunning() {
            if (_initialized) return;

            _root = UIService.Instance.UIDocument.rootVisualElement;

            _timeline = _root.Q<Timeline.Timeline>();

            var menuContainer = _root.Q<VisualElement>("Top");

            if (menuContainer != null) {
                var menuBar = new MenuBar();
                menuContainer.Add(menuBar);

                AddFileMenu(menuBar);
                AddEditMenu(menuBar);
                AddViewMenu(menuBar);
                AddHelpMenu(menuBar);

                _titleLabel = new Label("Untitled") {
                    pickingMode = PickingMode.Ignore,
                    style = {
                        position = Position.Absolute,
                        left = 0,
                        right = 0,
                        top = 0,
                        bottom = 0,
                        unityTextAlign = TextAnchor.MiddleCenter,
                        fontSize = 12
                    }
                };
                menuContainer.Add(_titleLabel);

                ProjectOperations.FilePathChanged += _ => UpdateTitle();
                ProjectOperations.UnsavedChangesChanged += _ => UpdateTitle();

                Application.wantsToQuit += HandleApplicationWantsToQuit;

                UpdateTitle();
                _initialized = true;
            }
        }

        protected override void OnDestroy() {
            Application.wantsToQuit -= HandleApplicationWantsToQuit;
            ProjectOperations.FilePathChanged -= _ => UpdateTitle();
            ProjectOperations.UnsavedChangesChanged -= _ => UpdateTitle();
        }

        private void AddFileMenu(MenuBar menuBar) {
            menuBar.AddMenu("File", menu => {
                menu.AddItem("New", NewProject, "Ctrl+N".ToPlatformShortcut());
                menu.AddItem("Open", OpenProject, "Ctrl+O".ToPlatformShortcut());
                menu.AddItem("Save", SaveProject, "Ctrl+S".ToPlatformShortcut());
                menu.AddSeparator();
                menu.AddItem("Save As...", SaveProjectAs);
                menu.AddSeparator();
                menu.AddSubmenu("Open Recent", submenu => {
                    bool hasRecovery = !string.IsNullOrEmpty(ProjectOperations.GetLatestRecoveryFile());
                    submenu.AddItem("Recover Last Session", ProjectOperations.RecoverLastSession, enabled: hasRecovery);

                    string[] recentFiles = ProjectOperations.GetRecentValidFiles();
                    if (recentFiles.Length > 0) {
                        submenu.AddSeparator();
                        for (int i = 0; i < recentFiles.Length; i++) {
                            string filePath = recentFiles[i];
                            string fileName = Path.GetFileNameWithoutExtension(filePath);
                            submenu.AddItem(fileName, () => ProjectOperations.OpenProject(filePath));
                        }
                    }
                });
                menu.AddSeparator();
                menu.AddSubmenu("Export", submenu => {
                    submenu.AddItem("NoLimits 2", ShowExportDialog);
                    submenu.AddItem("Track Mesh", TrackMeshExporter.ExportTrackMesh);
                });
                menu.AddSeparator();
                menu.AddSubmenu("Units", submenu => {
                    submenu.AddItem("Metric", () => {
                        Preferences.DistanceUnits = DistanceUnitsType.Meters;
                        if (Preferences.SpeedUnits != SpeedUnitsType.MetersPerSecond &&
                            Preferences.SpeedUnits != SpeedUnitsType.KilometersPerHour) {
                            Preferences.SpeedUnits = SpeedUnitsType.MetersPerSecond;
                        }
                    });
                    submenu.AddItem("Imperial", () => {
                        Preferences.DistanceUnits = DistanceUnitsType.Feet;
                        Preferences.SpeedUnits = SpeedUnitsType.MilesPerHour;
                    });
                    submenu.AddSeparator();
                    submenu.AddSubmenu("Distance", submenu => {
                        submenu.AddItem("Meters", () => Preferences.DistanceUnits = DistanceUnitsType.Meters,
                            isChecked: Preferences.DistanceUnits == DistanceUnitsType.Meters);
                        submenu.AddItem("Feet", () => Preferences.DistanceUnits = DistanceUnitsType.Feet,
                            isChecked: Preferences.DistanceUnits == DistanceUnitsType.Feet);
                    });
                    submenu.AddSubmenu("Velocity", submenu => {
                        submenu.AddItem("Meters Per Second", () => Preferences.SpeedUnits = SpeedUnitsType.MetersPerSecond,
                            isChecked: Preferences.SpeedUnits == SpeedUnitsType.MetersPerSecond);
                        submenu.AddItem("Kilometers Per Hour", () => Preferences.SpeedUnits = SpeedUnitsType.KilometersPerHour,
                            isChecked: Preferences.SpeedUnits == SpeedUnitsType.KilometersPerHour);
                        submenu.AddItem("Miles Per Hour", () => Preferences.SpeedUnits = SpeedUnitsType.MilesPerHour,
                            isChecked: Preferences.SpeedUnits == SpeedUnitsType.MilesPerHour);
                    });
                    submenu.AddSubmenu("Angle", submenu => {
                        submenu.AddItem("Degrees", () => Preferences.AngleUnits = AngleUnitsType.Degrees,
                            isChecked: Preferences.AngleUnits == AngleUnitsType.Degrees);
                        submenu.AddItem("Radians", () => Preferences.AngleUnits = AngleUnitsType.Radians,
                            isChecked: Preferences.AngleUnits == AngleUnitsType.Radians);
                    });
                    submenu.AddSubmenu("Angle Change", submenu => {
                        submenu.AddItem("Degrees", () => Preferences.AngleChangeUnits = AngleChangeUnitsType.Degrees,
                            isChecked: Preferences.AngleChangeUnits == AngleChangeUnitsType.Degrees);
                        submenu.AddItem("Radians", () => Preferences.AngleChangeUnits = AngleChangeUnitsType.Radians,
                            isChecked: Preferences.AngleChangeUnits == AngleChangeUnitsType.Radians);
                    });
                    submenu.AddSeparator();
                    submenu.AddItem("Reset to Default", () => {
                        Preferences.DistanceUnits = DistanceUnitsType.Meters;
                        Preferences.SpeedUnits = SpeedUnitsType.MetersPerSecond;
                        Preferences.AngleUnits = AngleUnitsType.Degrees;
                        Preferences.AngleChangeUnits = AngleChangeUnitsType.Radians;
                    });
                });
                menu.AddSeparator();
                menu.AddItem("Quit", QuitWithConfirmation);
            });
        }

        private void AddEditMenu(MenuBar menuBar) {
            menuBar.AddMenu("Edit", menu => {
                bool canUndo = Undo.CanUndo, canRedo = Undo.CanRedo;
                bool canCopy = EditOperationsSystem.CanCopy, canPaste = EditOperationsSystem.CanPaste;
                bool canDelete = EditOperationsSystem.CanDelete, canCut = EditOperationsSystem.CanCut;
                bool canSelectAll = EditOperationsSystem.CanSelectAll, canDeselectAll = EditOperationsSystem.CanDeselectAll;

                menu.AddItem(canUndo ? "Undo" : "Can't Undo", Undo.Execute, "Ctrl+Z".ToPlatformShortcut(), enabled: canUndo);
                menu.AddItem(canRedo ? "Redo" : "Can't Redo", Undo.Redo, "Ctrl+Y".ToPlatformShortcut(), enabled: canRedo);
                menu.AddSeparator();
                menu.AddItem("Cut", EditOperationsSystem.HandleCut, "Ctrl+X".ToPlatformShortcut(), enabled: canCut);
                menu.AddItem("Copy", EditOperationsSystem.HandleCopy, "Ctrl+C".ToPlatformShortcut(), enabled: canCopy);
                menu.AddItem("Paste", EditOperationsSystem.HandlePaste, "Ctrl+V".ToPlatformShortcut(), enabled: canPaste);
                menu.AddItem("Delete", EditOperationsSystem.HandleDelete, "Del", enabled: canDelete);
                menu.AddSeparator();
                menu.AddItem("Select All", EditOperationsSystem.HandleSelectAll, "Ctrl+A".ToPlatformShortcut(), enabled: canSelectAll);
                menu.AddItem("Deselect All", EditOperationsSystem.HandleDeselectAll, "Alt+A".ToPlatformShortcut(), enabled: canDeselectAll);
                menu.AddSeparator();
                menu.AddItem("Sync Playback", ToggleSyncPlayback, "T", isChecked: Preferences.SyncPlayback);
            });
        }

        private void AddViewMenu(MenuBar menuBar) {
            menuBar.AddMenu("View", menu => {
                menu.AddItem("Full Screen", () => VideoControlSystem.Instance?.ToggleFullscreen(), "F11",
                    isChecked: VideoControlSystem.IsFullscreen);
                menu.AddItem("Zoom In", () => UIScaleSystem.Instance?.ZoomIn(), "Ctrl++".ToPlatformShortcut());
                menu.AddItem("Zoom Out", () => UIScaleSystem.Instance?.ZoomOut(), "Ctrl+-".ToPlatformShortcut());
                menu.AddItem("Reset Zoom", () => UIScaleSystem.Instance?.ResetZoom());
                menu.AddSeparator();
                menu.AddSubmenu("Camera", submenu => {
                    submenu.AddItem("Front View", () => OrbitCameraSystem.SetFrontView(), "Numpad 1");
                    submenu.AddItem("Back View", () => OrbitCameraSystem.SetBackView(), "Ctrl+Numpad 1".ToPlatformShortcut());
                    submenu.AddItem("Right View", () => OrbitCameraSystem.SetSideView(), "Numpad 3");
                    submenu.AddItem("Left View", () => OrbitCameraSystem.SetOtherSideView(), "Ctrl+Numpad 3".ToPlatformShortcut());
                    submenu.AddItem("Top View", () => OrbitCameraSystem.SetTopView(), "Numpad 7");
                    submenu.AddItem("Bottom View", () => OrbitCameraSystem.SetBottomView(), "Ctrl+Numpad 7".ToPlatformShortcut());
                    submenu.AddSeparator();
                    submenu.AddItem("Toggle Orthographic", () => OrbitCameraSystem.ToggleOrthographic(), "Numpad 5");
                    submenu.AddSeparator();
                    submenu.AddSubmenu("Ride Camera", rideSubmenu => {
                        rideSubmenu.AddItem("Toggle", () => OrbitCameraSystem.ToggleRideCamera(), "R",
                            isChecked: OrbitCameraSystem.IsRideCameraActive);
                        rideSubmenu.AddItem("Edit", ShowRideCameraDialog);
                    });
                });
                menu.AddSubmenu("Display", submenu => {
                    submenu.AddItem("Gizmos", ToggleShowGizmos, "F1",
                        isChecked: Preferences.ShowGizmos);
                    submenu.AddItem("Grid", () => GridSystem.Instance?.ToggleGrid(), "F2",
                        isChecked: GridSystem.Instance?.ShowGrid == true);
                    submenu.AddItem("Stats", ToggleShowStats, "F3",
                        isChecked: Preferences.ShowStats);
                    submenu.AddItem("Node Grid", ToggleNodeGridSnapping, "F4",
                        isChecked: Preferences.NodeGridSnapping);
                });
                menu.AddSeparator();
                menu.AddSubmenu("Visualization", submenu => {
                    var currentMode = VisualizationSystem.CurrentMode;
                    submenu.AddItem("Velocity", () => VisualizationSystem.SetMode(VisualizationMode.Velocity), "Ctrl+1".ToPlatformShortcut(),
                        isChecked: currentMode == VisualizationMode.Velocity);
                    submenu.AddItem("Curvature", () => VisualizationSystem.SetMode(VisualizationMode.Curvature), "Ctrl+2".ToPlatformShortcut(),
                        isChecked: currentMode == VisualizationMode.Curvature);
                    submenu.AddItem("Normal Force", () => VisualizationSystem.SetMode(VisualizationMode.NormalForce), "Ctrl+3".ToPlatformShortcut(),
                        isChecked: currentMode == VisualizationMode.NormalForce);
                    submenu.AddItem("Lateral Force", () => VisualizationSystem.SetMode(VisualizationMode.LateralForce), "Ctrl+4".ToPlatformShortcut(),
                        isChecked: currentMode == VisualizationMode.LateralForce);
                    submenu.AddItem("Roll Speed", () => VisualizationSystem.SetMode(VisualizationMode.RollSpeed), "Ctrl+5".ToPlatformShortcut(),
                        isChecked: currentMode == VisualizationMode.RollSpeed);
                    submenu.AddItem("Pitch Speed", () => VisualizationSystem.SetMode(VisualizationMode.PitchSpeed), "Ctrl+6".ToPlatformShortcut(),
                        isChecked: currentMode == VisualizationMode.PitchSpeed);
                    submenu.AddItem("Yaw Speed", () => VisualizationSystem.SetMode(VisualizationMode.YawSpeed), "Ctrl+7".ToPlatformShortcut(),
                        isChecked: currentMode == VisualizationMode.YawSpeed);
                });
                menu.AddSubmenu("Appearance", submenu => {
                    submenu.AddSubmenu("Track Style", trackSubmenu => {
                        var availableConfigs = TrackMeshConfigManager.GetAvailableConfigsWithNames();
                        string currentConfigName = TrackStylePreferences.CurrentTrackMesh;
                        if (availableConfigs.Length > 0) {
                            foreach (var configInfo in availableConfigs) {
                                bool isCurrentConfig = configInfo.FileName == currentConfigName;
                                trackSubmenu.AddItem(configInfo.DisplayName, () => TrackMeshConfigManager.LoadConfig(configInfo.FileName),
                                isChecked: isCurrentConfig);
                            }
                            trackSubmenu.AddSeparator();
                        }
                        trackSubmenu.AddItem("Edit Colors", ShowColorPicker);
                        trackSubmenu.AddItem("Auto Style", ToggleAutoStyle, isChecked: Preferences.AutoStyle);
                        trackSubmenu.AddSeparator();
                        trackSubmenu.AddItem("Open Folder", TrackMeshConfigManager.OpenTrackMeshFolder);
                    });
                    submenu.AddSubmenu("Cart Style", cartSubmenu => {
                        var availableConfigs = CartStyleConfigManager.GetAvailableConfigsWithNames();
                        string currentConfigName = CartStylePreferences.CurrentCartStyle;
                        if (availableConfigs.Count > 0) {
                            foreach (var configInfo in availableConfigs) {
                                bool isCurrentConfig = configInfo.fileName == currentConfigName;
                                cartSubmenu.AddItem(configInfo.displayName, () => CartStyleConfigManager.LoadConfig(configInfo.fileName),
                                isChecked: isCurrentConfig);
                            }
                            cartSubmenu.AddSeparator();
                        }
                        cartSubmenu.AddItem("Open Folder", CartStyleConfigManager.OpenCartStyleFolder);
                    });
                    submenu.AddSubmenu("Background", envSubmenu => {
                        var currentSkyType = Preferences.SkyType;
                        envSubmenu.AddItem("Solid", () => Preferences.SkyType = SkyType.Solid,
                            isChecked: currentSkyType == SkyType.Solid);
                        envSubmenu.AddItem("Sky", () => Preferences.SkyType = SkyType.Procedural,
                            isChecked: currentSkyType == SkyType.Procedural);
                    });
                });
            });
        }

        private void AddHelpMenu(MenuBar menuBar) {
            menuBar.AddMenu("Help", menu => {
                menu.AddItem("About", ShowAbout);
                menu.AddSeparator();
                menu.AddItem("Controls", ShowControls, "Ctrl+H".ToPlatformShortcut());
            });
        }

        private void ShowControls() {
            _root.ShowControlsDialog();
        }

        private void AddKeyframe() {
            _timeline.Send<AddKeyframeEvent>();
        }

        private void ShowAbout() {
            _root.ShowAboutDialog();
        }

        private void ShowExportDialog() {
            _root.ShowExportDialog(metersPerNode => NoLimits2Exporter.ExportTrack(metersPerNode));
        }

        private void ShowRideCameraDialog() {
            _root.ShowRideCameraDialog();
        }

        private void ShowColorPicker() {
            _root.ShowColorPickerDialog();
        }

        private void ToggleNodeGridSnapping() {
            Preferences.NodeGridSnapping = !Preferences.NodeGridSnapping;
        }

        private void ToggleSyncPlayback() {
            Preferences.SyncPlayback = !Preferences.SyncPlayback;
        }

        private void ToggleShowStats() {
            Preferences.ShowStats = !Preferences.ShowStats;
        }

        private void ToggleShowGizmos() {
            Preferences.ShowGizmos = !Preferences.ShowGizmos;
        }

        private void ToggleAutoStyle() {
            Preferences.AutoStyle = !Preferences.AutoStyle;

            var settings = SystemAPI.ManagedAPI.GetSingleton<TrackStyleSettings>();
            settings.Version++;
        }

        protected override void OnUpdate() {
            var kb = Keyboard.current;

            if (!kb.ctrlKey.isPressed && !kb.leftCommandKey.isPressed) {
                if (kb.f1Key.wasPressedThisFrame) ToggleShowGizmos();
                else if (kb.f2Key.wasPressedThisFrame) GridSystem.Instance?.ToggleGrid();
                else if (kb.f3Key.wasPressedThisFrame) ToggleShowStats();
                else if (kb.f4Key.wasPressedThisFrame) ToggleNodeGridSnapping();
                else if (kb.f11Key.wasPressedThisFrame) VideoControlSystem.Instance?.ToggleFullscreen();
                else if (kb.iKey.wasPressedThisFrame) AddKeyframe();
                else if (kb.tKey.wasPressedThisFrame) ToggleSyncPlayback();
                else if (!Extensions.IsTextInputActive() && kb.numpad1Key.wasPressedThisFrame) OrbitCameraSystem.SetFrontView();
                else if (!Extensions.IsTextInputActive() && kb.numpad3Key.wasPressedThisFrame) OrbitCameraSystem.SetSideView();
                else if (!Extensions.IsTextInputActive() && kb.numpad5Key.wasPressedThisFrame) OrbitCameraSystem.ToggleOrthographic();
                else if (!Extensions.IsTextInputActive() && kb.numpad7Key.wasPressedThisFrame) OrbitCameraSystem.SetTopView();
            }

            if (kb.ctrlKey.isPressed || kb.leftCommandKey.isPressed) {
                if (kb.zKey.wasPressedThisFrame) {
                    if (kb.shiftKey.isPressed) {
                        if (Undo.CanRedo) Undo.Redo();
                    }
                    else if (Undo.CanUndo) Undo.Execute();
                }
                else if (kb.yKey.wasPressedThisFrame && Undo.CanRedo) {
                    Undo.Redo();
                }
                else if (kb.nKey.wasPressedThisFrame) NewProject();
                else if (kb.oKey.wasPressedThisFrame) OpenProject();
                else if (kb.sKey.wasPressedThisFrame) SaveProject();
                else if (kb.hKey.wasPressedThisFrame) ShowControls();
                else if (kb.equalsKey.wasPressedThisFrame || kb.numpadPlusKey.wasPressedThisFrame) UIScaleSystem.Instance?.ZoomIn();
                else if (kb.minusKey.wasPressedThisFrame || kb.numpadMinusKey.wasPressedThisFrame) UIScaleSystem.Instance?.ZoomOut();
                else if (kb.digit1Key.wasPressedThisFrame) VisualizationSystem.SetMode(VisualizationMode.Velocity);
                else if (kb.digit2Key.wasPressedThisFrame) VisualizationSystem.SetMode(VisualizationMode.Curvature);
                else if (kb.digit3Key.wasPressedThisFrame) VisualizationSystem.SetMode(VisualizationMode.NormalForce);
                else if (kb.digit4Key.wasPressedThisFrame) VisualizationSystem.SetMode(VisualizationMode.LateralForce);
                else if (kb.digit5Key.wasPressedThisFrame) VisualizationSystem.SetMode(VisualizationMode.RollSpeed);
                else if (kb.digit6Key.wasPressedThisFrame) VisualizationSystem.SetMode(VisualizationMode.PitchSpeed);
                else if (kb.digit7Key.wasPressedThisFrame) VisualizationSystem.SetMode(VisualizationMode.YawSpeed);
                else if (!Extensions.IsTextInputActive() && kb.numpad1Key.wasPressedThisFrame) OrbitCameraSystem.SetBackView();
                else if (!Extensions.IsTextInputActive() && kb.numpad3Key.wasPressedThisFrame) OrbitCameraSystem.SetOtherSideView();
                else if (!Extensions.IsTextInputActive() && kb.numpad7Key.wasPressedThisFrame) OrbitCameraSystem.SetBottomView();
            }
        }

        private void NewProject() {
            if (ProjectOperations.HasUnsavedChanges) {
                ShowUnsavedChangesDialog(ProjectOperations.CreateNewProject);
            }
            else {
                ProjectOperations.CreateNewProject();
            }
        }

        private void OpenProject() {
            if (ProjectOperations.HasUnsavedChanges) {
                ShowUnsavedChangesDialog(DoOpenProject);
            }
            else {
                DoOpenProject();
            }
        }

        private void DoOpenProject() {
            string filePath = FileManager.ShowOpenFileDialog();
            if (!string.IsNullOrEmpty(filePath)) {
                ProjectOperations.OpenProject(filePath);
            }
        }

        private void SaveProject() {
            if (string.IsNullOrEmpty(ProjectOperations.CurrentFilePath)) {
                SaveProjectAs();
                return;
            }

            ProjectOperations.SaveProject();
        }

        private void SaveProjectAs() {
            string fileName = ProjectOperations.GetProjectDisplayName();
            string filePath = FileManager.ShowSaveFileDialog(fileName);
            if (!string.IsNullOrEmpty(filePath)) {
                ProjectOperations.SaveProject(filePath);
            }
        }

        private void UpdateTitle() {
            _titleLabel.text = ProjectOperations.GetProjectTitle();
        }

        private void QuitWithConfirmation() {
            if (ProjectOperations.HasUnsavedChanges) {
                ShowUnsavedChangesDialog(QuitApplication);
            }
            else {
                QuitApplication();
            }
        }

        private void ShowUnsavedChangesDialog(System.Action proceedAction) {
            _root.ShowUnsavedChangesDialog(
                onSave: () => {
                    try {
                        if (string.IsNullOrEmpty(ProjectOperations.CurrentFilePath)) {
                            SaveProjectAs();
                        }
                        else {
                            ProjectOperations.SaveProject();
                        }

                        if (!ProjectOperations.HasUnsavedChanges) {
                            proceedAction?.Invoke();
                        }
                    }
                    catch (System.Exception ex) {
                        Debug.LogError($"Failed to save project: {ex.Message}");
                    }
                },
                onDontSave: () => {
                    _bypassUnsavedChangesCheck = true;
                    proceedAction?.Invoke();
                }
            );
        }

        private static void QuitApplication() {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private bool HandleApplicationWantsToQuit() {
            if (_bypassUnsavedChangesCheck) {
                return true;
            }

            if (ProjectOperations.HasUnsavedChanges) {
                ShowUnsavedChangesDialog(QuitApplication);
                return false;
            }
            return true;
        }
    }
}
