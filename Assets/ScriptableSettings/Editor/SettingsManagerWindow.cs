using System;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Scriptable.Settings.Editor.Tools;
using ScriptableSettings;
using UnityEditor.Search;
using ObjectField = UnityEditor.UIElements.ObjectField;

namespace Scriptable.Settings.Editor
{
    public class SettingsManagerWindow : EditorWindow
    {
        public SettingsManager SettingsManager => _settingsManager;
        
        [SerializeField] private VisualTreeAsset visualTree; // Assign UXML file in Inspector
        [SerializeField] private StyleSheet styleSheet;

        [SerializeField] private VisualTreeAsset settingItem; // Assign USS file (Optional)

        private SettingsManager _settingsManager;
        private SettingNodesTreeView _treeView;
        private SettingNodeVisual _inspectorPanel; 
        private ObjectField _managerField;
        private Button _newManagerButton;

        [MenuItem("Window/Settings Manager Editor")]
        public static void ShowWindow()
        {
            SettingsManagerWindow wnd = GetWindow<SettingsManagerWindow>();
            wnd.titleContent = new GUIContent("Settings Manager");
        }

        private void OnDestroy()
        {
            CleanTools(_settingsManager);
        }

        private void CleanTools(SettingsManager settingsManager)
        {
            if (settingsManager == null)
                return;

            foreach (var tool in settingsManager.Tools)
            {
                if(tool is ISettingEditorTool settingTool)
                {
                    settingTool.OnDestroySettingManagerWindow();
                }
            }
        }
        
        private void SetupTools(SettingsManager settingsManager)
        {
            if (settingsManager == null)
                return;

            foreach (var tool in settingsManager.Tools)
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
            var leftPane = root.Q<VisualElement>("left-pane");
            leftPane.Add(_treeView);
            rightPane.Add(_inspectorPanel);
            
            _managerField = root.Q<ObjectField>("manager-field");
            _managerField.objectType = typeof(SettingsManager);


            // Setup Object Field for Manager Asset
            _managerField.RegisterValueChangedCallback(evt =>
            {
                CleanTools(_settingsManager);
                _settingsManager = evt.newValue as SettingsManager;
                _treeView.SetManager(_settingsManager);// Initialize loader if needed
                _treeView.PopulateTreeView();
                _inspectorPanel.ClearInspector();
                SetupTools(_settingsManager);
            });
            // Set initial value

            // Setup Button Callbacks
            _treeView.selectionChanged += SelectNode;
            _treeView.RemoveNodeClicked += RemoveNode;
            _treeView.AddChildNodeClicked += CreateNode;
            _treeView.NodeRenamed += RenameNode;
            _treeView.NodeMoved += MoveNode;

            if (_settingsManager == null)
            {
                SelectManagerFromProjectSelection();
            }

            _managerField.value = _settingsManager;

            // Initial State
            _treeView.PopulateTreeView();// Populate based on initially loaded manager (if any)
            _inspectorPanel.ClearInspector();

            // Handle selection changes in the Project window
            // Note: This might conflict slightly with the ObjectField, choose one primary method
            // UnityEditor.Selection.selectionChanged += HandleProjectSelectionChange;
        }

        private void MoveNode(VisualElement arg1, SettingNode moveNode, SettingNode targetNode)
        {
            _settingsManager.MoveNode(moveNode, targetNode);
            _treeView.PopulateTreeView();
            _treeView.SelectNode(targetNode);
        }

        private void RenameNode(VisualElement source, SettingNode node, string newName)
        {
            _settingsManager.RenameNode(node, newName);
        }

        private void CreateNode(VisualElement visualElement, SettingNode nodeParent)
        {
            if (_settingsManager == null) return;

            CreateSettingNodeWindow.ShowWindow((type, newNodeName) => { CreateNode(nodeParent, newNodeName, type); }, nodeParent, visualElement);
        }

        private void CreateNode(SettingNode nodeParent, string newNodeName, Type type)
        {
            SettingNode newNode = _settingsManager.CreateNode(nodeParent, newNodeName, type); // Pass selected node as parent
            if (newNode != null)
            {
                _treeView.PopulateTreeView();
                _treeView.SelectNode(newNode);
            }
        }

        private void RemoveNode(VisualElement visualElement, SettingNode nodeToRemove)
        {
            if (_settingsManager == null || nodeToRemove == null) return;

            DeleteNodeWindow.ShowModal((deleteAsset) =>
            {
                if (_treeView.Selected == nodeToRemove)
                {
                    _inspectorPanel.ClearInspector();
                }

                _settingsManager.DeleteNode(nodeToRemove, deleteAsset);
                _treeView.PopulateTreeView();// Refresh tree
            }, visualElement);
            
        }

        // --- Manager Selection ---

        private void SelectManagerFromProjectSelection()
        {
            if (Selection.activeObject is SettingsManager manager)
            {
                _settingsManager = manager;
                if (_managerField != null) _managerField.value = _settingsManager; // Update UI field
                // Don't populate tree here, CreateGUI might not be finished
            }

            _settingsManager = AssetDatabase.FindAssets("t:SettingsManager")
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .Select(path => AssetDatabase.LoadAssetAtPath<SettingsManager>(path))
                .FirstOrDefault(manager => manager != null);

            if (_settingsManager == null)
            {
                var newButton = new Button() { text = "New Manager" };
                _managerField.parent.Add(newButton);

                newButton.RegisterCallback<ClickEvent>(_ =>
                {
                    _settingsManager = ScriptableObject.CreateInstance<SettingsManager>();
                    _settingsManager.name = "SettingsManager";
                    _settingsManager.LoaderFactory = CreateInstance<SettingLoaderFactory>();
                    var folderPath = _settingsManager.SettingsPath + "/Resources";
                    if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

                    AssetDatabase.CreateAsset(_settingsManager, $"{folderPath}/SettingsManager.asset");
                    AssetDatabase.CreateAsset(_settingsManager.LoaderFactory, $"{_settingsManager.SettingsPath}/SettingsLoaderFactory.asset");
                    _managerField.value = _settingsManager;
                    newButton.RemoveFromHierarchy(); // Remove the button
                });
            }
        }

        // Recursive helper to build the data structure for the tree view
        private void OnTreeSelectionChanged(IEnumerable<object> selectedItems)
        {
            SettingNode selectedId = selectedItems.FirstOrDefault() as SettingNode;
            SelectNode(selectedId);
        }

        private void SelectNode(SettingNode selected)
        {
            if (selected != null)
                _inspectorPanel.ShowNodeInInspector(_settingsManager, selected);
            else
                _inspectorPanel.ClearInspector();
        }
        // --- Helper (Refactor from SettingsManagerEditor) ---
    }
}