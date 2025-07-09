using System;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System.IO;
using System.Linq;
using Scriptable.Settings.Editor.Tools;
using ObjectField = UnityEditor.UIElements.ObjectField;

namespace Scriptable.Settings.Editor
{
    public class SettingsManagerWindow : EditorWindow
    {
        public ScriptableSettings scriptableSettings => _scriptableSettings;
        
        [SerializeField] private VisualTreeAsset visualTree; // Assign UXML file in Inspector
        [SerializeField] private StyleSheet styleSheet;

        [SerializeField] private VisualTreeAsset settingItem; // Assign USS file (Optional)

        //[SerializeField][HideInInspector] 
        private ScriptableSettings _scriptableSettings;
        
        private SettingNodesTreeView _treeView;
        private SettingNodeVisual _inspectorPanel; 
        private ObjectField _managerField;
        private Button _newManagerButton;
        private ToolbarButton _addButton;
        private ToolbarButton _removeButton;
        private VisualElement _focusedOut;
        private VisualElement _leftPane;
        
        private SettingNode _clipboardNode;
        private ISettingNodeHistory _nodeHistory;
        private HistoryBar _historyBar;

        [MenuItem("Window/Settings Manager Editor")]
        public static void ShowWindow()
        {
            GetOrCreateWindow();
        }
        public static void GoToSettingsNode(SettingNode value)
        {
            SaveSelection(value);
            var window = GetOrCreateWindow();
            window.Refresh();
        }

        private static SettingsManagerWindow GetOrCreateWindow()
        {
            SettingsManagerWindow wnd = GetWindow<SettingsManagerWindow>();
            wnd.titleContent = new GUIContent("Settings Manager");
            return wnd;
        }

        private void OnDestroy()
        {
            CleanTools(_scriptableSettings);
        }

        private void CleanTools(ScriptableSettings scriptableSettings)
        {
            if (!scriptableSettings)
                return;
            
            if (scriptableSettings && scriptableSettings.Tools == null)
                return;

            foreach (var tool in scriptableSettings.Tools)
            {
                if(tool is ISettingEditorTool settingTool)
                {
                    settingTool.OnDestroySettingManagerWindow();
                }
            }
        }
        
        private void SetupTools(ScriptableSettings scriptableSettings)
        {
            if (scriptableSettings == null)
                return;

            foreach (var tool in scriptableSettings.Tools)
            {
                if(tool is ISettingEditorTool settingTool)
                {
                    settingTool.OnCreateSettingManagerWindow(this);
                }
            }
        }

        public void CreateGUI()
        {
            // Each editor window contains a root VisualElement object
            VisualElement root = rootVisualElement;

            
            // Instantiate UXML
            if (visualTree == null)
            {
                Debug.LogError("VisualTreeAsset is not assigned to SettingsManagerWindow script!");
                root.Add(new Label("Error: VisualTreeAsset not assigned."));
                return;
            }

            visualTree.CloneTree(root);

            // Apply Stylesheet (Optional)
            if (styleSheet != null)
            {
                root.styleSheets.Add(styleSheet);
            }

            // Get references to UI elements
            _treeView = new SettingNodesTreeView(settingItem);
            _inspectorPanel = new SettingNodeVisual();
            var rightPane = root.Q<VisualElement>("right-pane");
            _leftPane = root.Q<VisualElement>("left-pane");
            _leftPane.Add(_treeView);
            rightPane.Add(_inspectorPanel);
            
            _managerField = root.Q<ObjectField>("manager-field");
            _managerField.objectType = typeof(ScriptableSettings);

            var historyContainer = root.Q<VisualElement>("history");
            _historyBar = new HistoryBar();
            _historyBar.OnHistorySelection += SelectHistoryNode;
            historyContainer.Add(_historyBar);
            
            // Setup Object Field for Manager Asset
            // Setup Button Callbacks
            _treeView.selectionChanged += TreeSelectionChanged;
            _treeView.NodeRenamed += RenameNode;
            _treeView.NodeMoved += MoveNode;
            _treeView.AssetsAdded += AddExistingAssets;

            _addButton = _leftPane.Q<ToolbarButton>("Add");
            _addButton.RegisterCallback<ClickEvent>(CreateNode);
            _removeButton = _leftPane.Q<ToolbarButton>("Remove");
            _removeButton.RegisterCallback<ClickEvent>(RemoveNode);
            
            _treeView.RegisterCallback<KeyDownEvent>(OnKeyDown);
            root.RegisterCallback<FocusOutEvent>(OnLoseTreeFocus);
            root.RegisterCallback<FocusInEvent>(OnGainFocus);

            var search = _leftPane.Q<ToolbarSearchField>("TreeSearch");
            search.RegisterValueChangedCallback((val) =>
            {
                _treeView.PopulateTreeView(val.newValue);
                if (string.IsNullOrEmpty(val.newValue))
                    SelectLastNode();
            });

            _managerField.RegisterValueChangedCallback(evt =>
            {
                SetupManager(evt.newValue as ScriptableSettings);
                Refresh();
            });

            if (_scriptableSettings == null)
            {
                SelectManagerFromProjectSelection();
            }
            else
            {
                _managerField.SetValueWithoutNotify(_scriptableSettings);
                SetupManager(_scriptableSettings);
            }

            Refresh();
        }

        private void AddExistingAssets(VisualElement source, SettingNode parent, ScriptableObject[] assets)
        {
            foreach (var asset in assets)
            {
                CreateNode(parent, asset);
            }
            Refresh();
        }

        private void SetupManager(ScriptableSettings manager)
        {
            CleanTools(_scriptableSettings);
            _scriptableSettings = manager;
            _treeView.SetManager(_scriptableSettings);// Initialize loader if needed
            SetupTools(_scriptableSettings);
            
            // Initialize history
            if (_scriptableSettings != null)
            {
                _nodeHistory = new SettingNodeHistory(_scriptableSettings);
                _historyBar.SetHistory(_nodeHistory);
            }
            else
            {
                _nodeHistory = null;
            }
        }

        private void Refresh()
        {
            // Initial State
            _treeView.PopulateTreeView();// Populate based on initially loaded manager (if any)
            _inspectorPanel.ClearInspector();
            SelectLastNode();
        }

        private void SelectLastNode()
        {
            var savedSelection = EditorPrefs.GetString("SelectedNode", null);
            if(string.IsNullOrEmpty(savedSelection) == false
               && _scriptableSettings != null
               && Guid.TryParse(savedSelection, out var guid))
            {
                _treeView.SelectNode(_scriptableSettings.GetNodeById(guid));
            }
        }

        private void OnGainFocus(FocusInEvent evt)
        {
            if(evt.target == _leftPane && _focusedOut != null)
            {
                _treeView.Deselect(_focusedOut);
            }

            _focusedOut = null;
        }

        private void OnLoseTreeFocus(FocusOutEvent evt)
        {
            if ((evt.target is VisualElement visualElement && visualElement.name == SettingNodesTreeView.TreeItemName))
                _focusedOut = visualElement;
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Delete)
            {
                RemoveNode(null);
                evt.StopPropagation();
            }
            
            if(evt.altKey && evt.keyCode == KeyCode.Return)
            {
                CreateNode(null);
                evt.StopPropagation();
            }
            
            // History navigation (Alt+Left/Right)
            if (evt.altKey && evt.keyCode == KeyCode.LeftBracket)
            {
                NavigateHistoryBack();
                evt.StopPropagation();
            }
            else if (evt.altKey && evt.keyCode == KeyCode.RightBracket)
            {
                NavigateHistoryForward();
                evt.StopPropagation();
            }
            
            // Copy (Ctrl+C)
            if (evt.ctrlKey && evt.keyCode == KeyCode.C)
            {
                if (_treeView.Selected != null)
                {
                    _clipboardNode = _treeView.Selected;
                    Debug.Log($"Copied Node: {_treeView.Selected.Name} ({_treeView.Selected.Guid})"); // Optional feedback
                    evt.StopPropagation();
                }
            }
            // Paste (Ctrl+V)
            else if (evt.ctrlKey && evt.keyCode == KeyCode.V)
            {
                if (_clipboardNode != null && _scriptableSettings != null)
                {
                    var nodeToCopy = _clipboardNode;
                    if (nodeToCopy != null && _treeView.Selected != null)
                    {
                        // Determine paste target: selected node or root if none selected
                        var pasteTargetParent = _treeView.Selected ?? null; // Paste under selected, or at root level

                        var result = _scriptableSettings.PasteNode(nodeToCopy, pasteTargetParent);
                        _treeView.PopulateTreeView(); 
                        _treeView.SelectNode(pasteTargetParent);// Refresh the view
                        // Optionally select the newly pasted node (requires PasteNode to return it)
                        Debug.Log($"Pasted Node: {nodeToCopy.Name}"); // Optional feedback
                        evt.StopPropagation();
                    }
                    else
                    {
                        _clipboardNode = null; // Clear clipboard if the source node doesn't exist anymore
                    }
                }
            }
            // Duplicate (Ctrl+D)
            else if (evt.ctrlKey && evt.keyCode == KeyCode.D)
            {
                if (_treeView.Selected != null && _scriptableSettings != null)
                {
                    var newNode = _scriptableSettings.DuplicateNode(_treeView.Selected);
                    _treeView.PopulateTreeView(); 
                    _treeView.SelectNode(newNode);
                    Debug.Log($"Duplicated Node: {_treeView.Selected.Name}"); // Optional feedback
                    evt.StopPropagation();
                }
            }
        }

        private void RemoveNode(ClickEvent evt)
        {
            var nodeToRemove = _treeView.Selected;
            if (_scriptableSettings == null || nodeToRemove == null) return;

            DeleteNodeWindow.ShowModal((deleteAsset) =>
            {
                if (_treeView.Selected == nodeToRemove)
                {
                    _inspectorPanel.ClearInspector();
                }

                var parent = nodeToRemove.Parent;
                _scriptableSettings.DeleteNode(nodeToRemove, deleteAsset);
                _treeView.PopulateTreeView();
                _treeView.SelectNode(parent);
                // Refresh tree
            }, _removeButton);
        }

        private void CreateNode(ClickEvent evt)
        {
            if (_scriptableSettings == null) return;

            var nodeParent = _treeView.Selected;
            CreateSettingNodeWindow.ShowWindow( nodeParent, _addButton,
                (type, newNodeName) => { CreateNode(nodeParent, newNodeName, type); },
                (existing) => { CreateNode(nodeParent, existing); });
                
        }

        private void MoveNode(VisualElement arg1, SettingNode moveNode, SettingNode targetNode)
        {
            _scriptableSettings.MoveNode(moveNode, targetNode);
            _treeView.PopulateTreeView();
            _treeView.SelectNode(targetNode);
        }

        private void RenameNode(VisualElement source, SettingNode node, string newName)
        {
            _scriptableSettings.RenameNode(node, newName);
        }
        
        private void CreateNode(SettingNode nodeParent, string newNodeName, Type type)
        {
            SettingNode newNode = _scriptableSettings.CreateNode(nodeParent, newNodeName, type); // Pass selected node as parent
            if (newNode != null)
            {
                _treeView.PopulateTreeView();
                _treeView.SelectNode(newNode);
            }
        }
        
        private void CreateNode(SettingNode nodeParent, ScriptableObject existingAsset)
        {
            SettingNode newNode = _scriptableSettings.CreateNode(nodeParent, existingAsset.name, existingAsset, ScriptableSettings.ExistingAssetOperation.Move); // Pass selected node as parent
            if (newNode != null)
            {
                _treeView.PopulateTreeView();
                _treeView.SelectNode(newNode);
            }
        }
        
        // --- Manager Selection ---

        private void SelectManagerFromProjectSelection()
        {
            if (Selection.activeObject is ScriptableSettings manager)
            {
                _managerField.value = manager;
            }
            
            _scriptableSettings = AssetDatabase.FindAssets("t:ScriptableSettings")
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .Select(path => AssetDatabase.LoadAssetAtPath<ScriptableSettings>(path))
                .FirstOrDefault(manager => manager != null);
            
            if (_scriptableSettings == null)
            {
                var newButton = new Button() { text = "New Manager" };
                _managerField.parent.Add(newButton);

                newButton.RegisterCallback<ClickEvent>(_ =>
                {
                    _scriptableSettings = ScriptableObject.CreateInstance<ScriptableSettings>();
                    _scriptableSettings.name = "ScriptableSettings";
                    _scriptableSettings.LoaderFactory = CreateInstance<SettingLoaderFactory>();
                    var folderPath = _scriptableSettings.SettingsPath + "/Resources";
                    if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

                    AssetDatabase.CreateAsset(_scriptableSettings, $"{folderPath}/ScriptableSettings.asset");
                    AssetDatabase.CreateAsset(_scriptableSettings.LoaderFactory, $"{_scriptableSettings.SettingsPath}/SettingsLoaderFactory.asset");
                    _managerField.value = _scriptableSettings;
                    newButton.RemoveFromHierarchy(); // Remove the button
                });
            }
            else
            {
                _managerField.value = _scriptableSettings;
            }
        }

        private void TreeSelectionChanged(SettingNode selected)
        {
            SelectNode(selected, _nodeHistory);
        }

        private void SelectNode(SettingNode selected, ISettingNodeHistory history = null)
        {
            if (selected != null)
            {
                _inspectorPanel.ShowNodeInInspector(_scriptableSettings, selected);
                
                // Add to history
                if (history != null)
                {
                    history.AddNode(selected);
                    _historyBar.RefreshBreadcrumbs();
                }
            }
            else
            {
                _inspectorPanel.ClearInspector();
            }

            SaveSelection(selected);
        }

        private static void SaveSelection(SettingNode selected)
        {
            if(selected == null)
                return;
            EditorPrefs.SetString("SelectedNode", selected?.Guid.ToString() ?? string.Empty);
        }
        
        private void NavigateHistoryBack()
        {
            if (_nodeHistory == null || !_nodeHistory.CanGoBack) return;
            
            _nodeHistory.NavigateBack();
            SelectHistoryNode();
            _historyBar.UpdateCurrentSelection();
        }
        
        private void NavigateHistoryForward()
        {
            if (_nodeHistory == null || !_nodeHistory.CanGoForward) return;
            
            _nodeHistory.NavigateForward();
            SelectHistoryNode();
            _historyBar.UpdateCurrentSelection();
        }

        private void SelectHistoryNode()
        {
            var node = _nodeHistory.CurrentNode;
            if (node != null)
            {
                _treeView.SelectNode(node, true);
                SelectNode(node);
                _historyBar.UpdateCurrentSelection();
            }
        }
    }
}