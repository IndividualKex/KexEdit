using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using System.IO;
using KexEdit.UI.Serialization;
using KexEdit.UI.Timeline;

namespace KexEdit.UI {
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class NavigationMenuSystem : SystemBase {
        private static IEditableHandler _activeHandler;
        private static bool _menuInteractionActive;

        private VisualElement _root;
        private Timeline.Timeline _timeline;
        private Label _titleLabel;

        private string _currentFilePath;
        private bool _initialized;
        private bool _hasUnsavedChanges;

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

                Undo.Recorded += () => {
                    _hasUnsavedChanges = true;
                    UpdateTitle();
                };

                Application.wantsToQuit += HandleApplicationWantsToQuit;

                _initialized = true;
            }
        }

        protected override void OnDestroy() {
            Application.wantsToQuit -= HandleApplicationWantsToQuit;
        }

        private void AddFileMenu(MenuBar menuBar) {
            menuBar.AddMenu("File", menu => {
                menu.AddItem("New", NewProject, "Ctrl+N".ToPlatformShortcut());
                menu.AddItem("Open", OpenProject, "Ctrl+O".ToPlatformShortcut());
                menu.AddItem("Save", SaveProject, "Ctrl+S".ToPlatformShortcut());
                menu.AddSeparator();
                menu.AddItem("Save As...", SaveProjectAs);
                menu.AddSeparator();
                menu.AddSubmenu("Export", submenu => {
                    submenu.AddItem("NoLimits 2", ShowExportDialog);
                });
                menu.AddSeparator();
                menu.AddSubmenu("Preferences", submenu => {
                    submenu.AddItem("Node Grid", ToggleNodeGridSnapping,
                        isChecked: PreferencesSystem.NodeGridSnapping);
                });
                menu.AddSeparator();
                menu.AddItem("Quit", QuitWithConfirmation);
            });
        }

        private void AddEditMenu(MenuBar menuBar) {
            menuBar.AddMenu("Edit", menu => {
                bool canUndo = Undo.CanUndo, canRedo = Undo.CanRedo;
                bool canCopy = CanCopy, canPaste = CanPaste;
                bool canDelete = CanDelete, canCut = CanCut;
                bool canSelectAll = CanSelectAll, canDeselectAll = CanDeselectAll;

                menu.AddItem(canUndo ? "Undo" : "Can't Undo", Undo.Execute, "Ctrl+Z".ToPlatformShortcut(), enabled: canUndo);
                menu.AddItem(canRedo ? "Redo" : "Can't Redo", Undo.Redo, "Ctrl+Y".ToPlatformShortcut(), enabled: canRedo);
                menu.AddSeparator();
                menu.AddItem("Cut", HandleCut, "Ctrl+X".ToPlatformShortcut(), enabled: canCut);
                menu.AddItem("Copy", HandleCopy, "Ctrl+C".ToPlatformShortcut(), enabled: canCopy);
                menu.AddItem("Paste", HandlePaste, "Ctrl+V".ToPlatformShortcut(), enabled: canPaste);
                menu.AddItem("Delete", HandleDelete, "Del", enabled: canDelete);
                menu.AddSeparator();
                menu.AddItem("Select All", HandleSelectAll, "Ctrl+A".ToPlatformShortcut(), enabled: canSelectAll);
                menu.AddItem("Deselect All", HandleDeselectAll, "Alt+A".ToPlatformShortcut(), enabled: canDeselectAll);
            });
        }

        private void AddViewMenu(MenuBar menuBar) {
            menuBar.AddMenu("View", menu => {
                menu.AddItem("Zoom In", () => UIScaleSystem.Instance?.ZoomIn(), "Ctrl++".ToPlatformShortcut());
                menu.AddItem("Zoom Out", () => UIScaleSystem.Instance?.ZoomOut(), "Ctrl+-".ToPlatformShortcut());
                menu.AddItem("Reset Zoom", () => UIScaleSystem.Instance?.ResetZoom());
                menu.AddSeparator();
                menu.AddItem("Grid", () => GridSystem.Instance?.ToggleGrid(), "F2",
                    isChecked: GridSystem.Instance?.ShowGrid == true);
                menu.AddItem("Stats", ToggleShowStats, "F3",
                    isChecked: PreferencesSystem.ShowStats);
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

        private void ToggleNodeGridSnapping() {
            PreferencesSystem.NodeGridSnapping = !PreferencesSystem.NodeGridSnapping;
        }

        private void ToggleShowStats() {
            PreferencesSystem.ShowStats = !PreferencesSystem.ShowStats;
        }

        protected override void OnUpdate() {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.f2Key.wasPressedThisFrame) GridSystem.Instance?.ToggleGrid();
            else if (kb.f3Key.wasPressedThisFrame) ToggleShowStats();
            else if (kb.fKey.wasPressedThisFrame) HandleFocus();
            else if (kb.deleteKey.wasPressedThisFrame) HandleDelete();
            else if (kb.iKey.wasPressedThisFrame) AddKeyframe();

            if (kb.ctrlKey.isPressed || kb.leftCommandKey.isPressed) {
                if (kb.zKey.wasPressedThisFrame) {
                    if (kb.shiftKey.isPressed) {
                        if (Undo.CanRedo) Undo.Redo();
                    }
                    else if (Undo.CanUndo) Undo.Execute();

                    _hasUnsavedChanges = true;
                    UpdateTitle();
                }
                else if (kb.yKey.wasPressedThisFrame && Undo.CanRedo) {
                    Undo.Redo();
                    _hasUnsavedChanges = true;
                    UpdateTitle();
                }
                else if (kb.xKey.wasPressedThisFrame) HandleCut();
                else if (kb.cKey.wasPressedThisFrame) HandleCopy();
                else if (kb.vKey.wasPressedThisFrame) HandlePaste();
                else if (kb.aKey.wasPressedThisFrame) HandleSelectAll();
                else if (kb.nKey.wasPressedThisFrame) NewProject();
                else if (kb.oKey.wasPressedThisFrame) OpenProject();
                else if (kb.sKey.wasPressedThisFrame) SaveProject();
                else if (kb.hKey.wasPressedThisFrame) ShowControls();
                else if (kb.equalsKey.wasPressedThisFrame || kb.numpadPlusKey.wasPressedThisFrame) UIScaleSystem.Instance?.ZoomIn();
                else if (kb.minusKey.wasPressedThisFrame || kb.numpadMinusKey.wasPressedThisFrame) UIScaleSystem.Instance?.ZoomOut();
            }
            else if (kb.altKey.isPressed || kb.leftCommandKey.isPressed || kb.rightCommandKey.isPressed) {
                if (kb.aKey.wasPressedThisFrame) HandleDeselectAll();
            }
        }

        private void NewProject() {
            if (_hasUnsavedChanges) {
                ShowUnsavedChangesDialog(() => {
                    CreateNewProject();
                });
            }
            else {
                CreateNewProject();
            }
        }

        private void CreateNewProject() {
            _currentFilePath = null;
            SerializationSystem.Instance.DeserializeGraph(new byte[0]);
            Undo.Clear();
            _hasUnsavedChanges = false;
            UpdateTitle();
        }

        private void OpenProject() {
            if (_hasUnsavedChanges) {
                ShowUnsavedChangesDialog(DoOpenProject);
            }
            else {
                DoOpenProject();
            }
        }

        private void DoOpenProject() {
            string filePath = FileManager.ShowOpenFileDialog();
            if (string.IsNullOrEmpty(filePath)) return;

            try {
                byte[] graphData = FileManager.LoadGraph(filePath);
                if (graphData.Length == 0) return;

                SerializationSystem.Instance.DeserializeGraph(graphData);
                _currentFilePath = filePath;
                Undo.Clear();
                _hasUnsavedChanges = false;
                UpdateTitle();
            }
            catch (System.Exception ex) {
                Debug.LogError($"Failed to open project: {ex.Message}");
            }
        }

        private void SaveProject() {
            if (string.IsNullOrEmpty(_currentFilePath)) {
                SaveProjectAs();
                return;
            }

            try {
                FileManager.SaveGraph(
                    SerializationSystem.Instance.SerializeGraph(),
                    _currentFilePath
                );
                _hasUnsavedChanges = false;
                UpdateTitle();
            }
            catch (System.Exception ex) {
                Debug.LogError($"Failed to save project: {ex.Message}");
            }
        }

        private void SaveProjectAs() {
            string fileName = Path.GetFileNameWithoutExtension(_currentFilePath);
            string filePath = FileManager.ShowSaveFileDialog(fileName);
            if (string.IsNullOrEmpty(filePath)) return;

            try {
                FileManager.SaveGraph(
                    SerializationSystem.Instance.SerializeGraph(),
                    filePath
                );
                _currentFilePath = filePath;
                _hasUnsavedChanges = false;
                UpdateTitle();
            }
            catch (System.Exception ex) {
                Debug.LogError($"Failed to save project: {ex.Message}");
            }
        }

        private void UpdateTitle() {
            if (_titleLabel == null) return;

            _titleLabel.text = string.IsNullOrEmpty(_currentFilePath)
                ? "Untitled"
                : Path.GetFileNameWithoutExtension(_currentFilePath);

            if (_hasUnsavedChanges) _titleLabel.text += "*";
        }

        private void QuitWithConfirmation() {
            if (_hasUnsavedChanges) {
                ShowUnsavedChangesDialog(QuitApplication);
            }
            else {
                QuitApplication();
            }
        }

        private void ShowUnsavedChangesDialog(System.Action proceedAction) {
            _root.ShowUnsavedChangesDialog(
                onSave: () => {
                    SaveProject();
                    if (!_hasUnsavedChanges) proceedAction?.Invoke();
                },
                onDontSave: () => {
                    _hasUnsavedChanges = false;
                    proceedAction?.Invoke();
                }
            );
        }

        public static void SetActiveHandler(IEditableHandler handler) => _activeHandler = handler;

        public static void ClearActiveHandler(IEditableHandler handler) {
            if (_activeHandler == handler && !_menuInteractionActive) _activeHandler = null;
        }

        private static void BeginMenuInteraction() => _menuInteractionActive = true;
        private static void EndMenuInteraction() => _menuInteractionActive = false;

        public static void HandleCopy() {
            if (CanCopy) {
                BeginMenuInteraction();
                _activeHandler.Copy();
                EndMenuInteraction();
            }
        }

        public static void HandlePaste() => HandlePaste(null);

        public static void HandlePaste(Vector2? worldPosition) {
            if (CanPaste) {
                BeginMenuInteraction();
                Undo.Record();
                _activeHandler.Paste(worldPosition);
                EndMenuInteraction();
            }
        }

        public static void HandleDelete() {
            if (CanDelete) {
                BeginMenuInteraction();
                Undo.Record();
                _activeHandler.Delete();
                EndMenuInteraction();
            }
        }

        public static void HandleCut() {
            if (CanCut) {
                BeginMenuInteraction();
                Undo.Record();
                _activeHandler.Cut();
                EndMenuInteraction();
            }
        }

        public static void HandleSelectAll() {
            if (CanSelectAll) {
                BeginMenuInteraction();
                _activeHandler.SelectAll();
                EndMenuInteraction();
            }
        }

        public static void HandleDeselectAll() {
            if (CanDeselectAll) {
                BeginMenuInteraction();
                _activeHandler.DeselectAll();
                EndMenuInteraction();
            }
        }

        public static void HandleFocus() {
            if (CanFocus) {
                BeginMenuInteraction();
                _activeHandler.Focus();
                EndMenuInteraction();
            }
        }

        private static void QuitApplication() {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public static bool CanCopy => _activeHandler?.CanCopy() == true;
        public static bool CanPaste => _activeHandler?.CanPaste() == true;
        public static bool CanDelete => _activeHandler?.CanDelete() == true;
        public static bool CanCut => _activeHandler?.CanCut() == true;
        public static bool CanSelectAll => _activeHandler?.CanSelectAll() == true;
        public static bool CanDeselectAll => _activeHandler?.CanDeselectAll() == true;
        public static bool CanFocus => _activeHandler?.CanFocus() == true;

        private bool HandleApplicationWantsToQuit() {
            if (_hasUnsavedChanges) {
                ShowUnsavedChangesDialog(QuitApplication);
                return false;
            }
            return true;
        }
    }
}
