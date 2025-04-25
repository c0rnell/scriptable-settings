#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine.InputSystem.HID; // For Type

public class SettingsManagerWindow : EditorWindow
{
    [SerializeField] private VisualTreeAsset visualTree; // Assign UXML file in Inspector
    [SerializeField] private StyleSheet styleSheet; 
    
    [SerializeField] private VisualTreeAsset settingItem;// Assign USS file (Optional)

    private SettingsManager _settingsManager;
    private TreeView _treeView;
    private VisualElement _inspectorPanel;
    private ScrollView _rightPane; // Reference to ScrollView for resetting scroll
    private Button _createRootButton;
    private Button _createChildButton;
    private Button _deleteButton;
    private Button _validateButton;
    private ObjectField _managerField;

    private SettingNode _selectedNode;

    // Store TreeView item IDs mapped to SettingNodes
    private Dictionary<int, SettingNode> _nodeMap = new Dictionary<int, SettingNode>();
    private int _nextItemId = 0;


    [MenuItem("Window/Settings Manager Editor")]
    public static void ShowWindow()
    {
        SettingsManagerWindow wnd = GetWindow<SettingsManagerWindow>();
        wnd.titleContent = new GUIContent("Settings Manager");
    }

    // Called when the window is enabled or scripts recompile
    private void OnEnable()
    {
        // Attempt to find manager if one is selected
        if (_settingsManager == null) {
             SelectManagerFromProjectSelection();
        }
    }


    public void CreateGUI()
    {
        // Each editor window contains a root VisualElement object
        VisualElement root = rootVisualElement;

        // Instantiate UXML
        if (visualTree == null) {
            Debug.LogError("VisualTreeAsset is not assigned to SettingsManagerWindow script!");
            root.Add(new Label("Error: VisualTreeAsset not assigned."));
            return;
        }
        visualTree.CloneTree(root);

        // Apply Stylesheet (Optional)
        if (styleSheet != null) {
            root.styleSheets.Add(styleSheet);
        }


        // Get references to UI elements
        _treeView = root.Q<TreeView>("settings-tree");
        _inspectorPanel = root.Q<VisualElement>("inspector-panel");
        _rightPane = root.Q<ScrollView>("right-pane");
        _createRootButton = root.Q<Button>("create-root-button");
        _createChildButton = root.Q<Button>("create-child-button");
        _deleteButton = root.Q<Button>("delete-button");
        _validateButton = root.Q<Button>("validate-button");
        _managerField = root.Q<ObjectField>("manager-field");
        _managerField.objectType = typeof(SettingsManager);


        // Setup Object Field for Manager Asset
        _managerField.RegisterValueChangedCallback(evt => {
            _settingsManager = evt.newValue as SettingsManager;
            _settingsManager.InitLoader(new ResourcesSettingLoader()); // Initialize loader if needed
            PopulateTreeView();
            ClearInspector();
            UpdateButtons();
        });
        _managerField.value = _settingsManager; // Set initial value


        // Setup TreeView
        SetupTreeView();


        // Setup Button Callbacks
        _createRootButton.clicked += OnCreateRootClicked;
        _createChildButton.clicked += OnCreateChildClicked;
        _deleteButton.clicked += OnDeleteClicked;
        _validateButton.clicked += OnValidateClicked;


        // Initial State
        PopulateTreeView(); // Populate based on initially loaded manager (if any)
        ClearInspector();
        UpdateButtons();


         // Handle selection changes in the Project window
         // Note: This might conflict slightly with the ObjectField, choose one primary method
         // UnityEditor.Selection.selectionChanged += HandleProjectSelectionChange;
    }

     private void OnDisable()
     {
         // UnityEditor.Selection.selectionChanged -= HandleProjectSelectionChange;
         // Clean up callbacks if needed
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
     }

    // Optional: Update if selection changes in Project window
    // private void HandleProjectSelectionChange()
    // {
    //      if (Selection.activeObject is SettingsManager manager && manager != _settingsManager)
    //      {
    //          _settingsManager = manager;
    //          if (_managerField != null) _managerField.value = _settingsManager;
    //          PopulateTreeView();
    //          ClearInspector();
    //          UpdateButtons();
    //      }
    // }


    // --- TreeView Setup & Population ---

    private void SetupTreeView()
    {
        _treeView.makeItem = () =>
        {
            var ve = new VisualElement();
            settingItem.CloneTree(ve);
            
            return ve;
        }; // Create a simple Label for each tree item

        _treeView.bindItem = (element, index) =>
        {
            var label = element.Q<Label>();
            var addButton = element.Q<Button>();
            addButton.userData = index;
            addButton.RegisterCallback<ClickEvent>(OnSelectedAdd, TrickleDown.TrickleDown);
            // 'index' here is the item ID we assigned
            if (_nodeMap.TryGetValue(index, out SettingNode node))
            {
                label.text = node.Name;
                SetFileOrFolder(node, element);
                
            } else {
                label.text = "Error: Node not found";
            }
        };

        _treeView.unbindItem = (element, index) =>
        {
            var addButton = element.Q<Button>();
            addButton.UnregisterCallback<ClickEvent>(OnSelectedAdd);
        };

        /*// How to get children for a given node ID
        _treeView.getChildren = (index) => {
             List<int> childrenIds = new List<int>();
             if (_nodeMap.TryGetValue(index, out SettingNode node))
             {
                 foreach (var childNode in node.Children)
                 {
                      // Find the ID associated with the childNode
                      // This linear search is inefficient for large trees, consider a reverse map
                      foreach(var kvp in _nodeMap) {
                          if (kvp.Value == childNode) {
                              childrenIds.Add(kvp.Key);
                              break;
                          }
                      }
                 }
             }
             return childrenIds;
        };*/

        // Handle selection changes in the TreeView
#if UNITY_2022_1_OR_NEWER // Or the specific version selectionChanged was added
        _treeView.selectionChanged += OnTreeSelectionChanged;
#else
        // Older versions might need RegisterCallback or different event handling
        _treeView.RegisterCallback<ChangeEvent<IEnumerable<object>>>(evt => OnTreeSelectionChangedLegacy(evt.newValue));
#endif
    }

    private static void SetFileOrFolder(SettingNode node, VisualElement element)
    {
        element.RemoveFromClassList(".setting-item-folder");
        element.RemoveFromClassList(".setting-item-file");
        
        if (node.Children.Any())
        {
            element.AddToClassList(".setting-item-folder");
        }
        else
        {
            element.AddToClassList(".setting-item-file");
        }
    }

    private void OnSelectedAdd(ClickEvent evt)
    {
        if (evt.currentTarget is Button addButton)
        {
            Debug.Log($"+ called {_nodeMap[(int)addButton.userData].Name}");
        }
        
    }

    private void PopulateTreeView()
     {
         if (_treeView == null) return; // Not ready yet

         _treeView.Clear();
         _nodeMap.Clear();
         _nextItemId = 0; // Reset item ID counter

         if (_settingsManager == null) {
              _treeView.RefreshItems(); // Show empty tree
              return;
         }

         // Ensure the manager's internal structures are ready
         // This might require making BuildIndexAndParentsIfNeeded public or adding a public Refresh method
         // _settingsManager.BuildIndexAndParentsIfNeeded(); // If accessible


         // Build tree view item data (IDs and mapping) recursively
         List<TreeViewItemData<SettingNode>> treeRootItems = new List<TreeViewItemData<SettingNode>>();
         foreach (var rootNode in _settingsManager.Roots)
         {
             treeRootItems.Add(CreateTreeViewItemRecursive(rootNode));
         }

         _treeView.SetRootItems(treeRootItems);
         _treeView.RefreshItems(); // Important: Rebuild the visual tree
     }


     // Recursive helper to build the data structure for the tree view
     private TreeViewItemData<SettingNode> CreateTreeViewItemRecursive(SettingNode node)
     {
          int currentId = _nextItemId++;
          _nodeMap[currentId] = node; // Map ID -> Node

          var childrenData = new List<TreeViewItemData<SettingNode>>();
          foreach (var childNode in node.Children)
          {
              childrenData.Add(CreateTreeViewItemRecursive(childNode));
          }

          return new TreeViewItemData<SettingNode>(currentId, node, childrenData);
     }


    // --- Selection & Inspector ---

#if UNITY_2022_1_OR_NEWER
    private void OnTreeSelectionChanged(IEnumerable<object> selectedItems)
    {
         // We handle single selection for simplicity
         SettingNode selectedId = selectedItems.FirstOrDefault() as SettingNode;

         if (selectedId != null)
         {
              _selectedNode = selectedId;
              ShowNodeInInspector(selectedId);
         }
         else
         {
              _selectedNode = null;
              ClearInspector();
         }
         UpdateButtons();
    }
#else
     private void OnTreeSelectionChangedLegacy(IEnumerable<object> selectedItems) { /* Similar logic as above */ }
#endif


    private void ClearInspector()
    {
        _inspectorPanel.Clear();
        _inspectorPanel.Add(new Label("Select a setting node in the tree."));
        // Reset scroll position
         if (_rightPane != null) _rightPane.scrollOffset = Vector2.zero;
    }

    private void ShowNodeInInspector(SettingNode node)
    {
        _inspectorPanel.Clear();
         if (node == null) { ClearInspector(); return; }

        // Display Node Info (Read-only for now)
         _inspectorPanel.Add(new Label($"Selected Node: {node.Name}") { style = { unityFontStyleAndWeight = FontStyle.Bold } });
         _inspectorPanel.Add(new Label($"GUID: {node.Guid}"));
         _inspectorPanel.Add(new Label($"ShortID: {node.Id}"));
         _inspectorPanel.Add(new Label($"Type: {(node.SettingType != null ? node.SettingType.FullName : node.SettingType + " (Not Found)")}"));

         // Add a separator
         _inspectorPanel.Add(new VisualElement() { style = { height = 1, backgroundColor = Color.gray, marginTop = 5, marginBottom = 5 } });


        // Try to load and display the actual ScriptableObject
        if (_settingsManager != null && _settingsManager.Loader != null)
        {
             ScriptableObject settingSO = _settingsManager.LoadNode<ScriptableObject>(node); // Use synchronous load for editor inspector

             if (settingSO != null)
             {
                 // Create an InspectorElement bound to the loaded SO
                 // This automatically draws the default inspector for that SO
                 InspectorElement inspector = new InspectorElement(settingSO);
                 _inspectorPanel.Add(inspector);
             }
             else
             {
                 _inspectorPanel.Add(new HelpBox($"Could not load the ScriptableObject asset for '{node.Name}'.\nLoader: {_settingsManager.Loader.GetType().Name}\nPath might be: {string.Join("/", node.GetPathSegments())}\nID: {node.Id}", HelpBoxMessageType.Warning));
             }
        }
         else if (_settingsManager != null && _settingsManager.Loader == null)
         {
              _inspectorPanel.Add(new HelpBox("SettingsManager Loader is not initialized. Cannot load asset.", HelpBoxMessageType.Warning));
         }
         else
         {
              _inspectorPanel.Add(new HelpBox("SettingsManager reference missing.", HelpBoxMessageType.Error));
         }

          // Reset scroll position after populating
         if (_rightPane != null) _rightPane.scrollOffset = Vector2.zero;
    }


    // --- Button Actions ---

    private void UpdateButtons()
    {
         bool managerExists = _settingsManager != null;
         bool nodeSelected = _selectedNode != null;

         _createRootButton.SetEnabled(managerExists);
         _validateButton.SetEnabled(managerExists);
         _createChildButton.SetEnabled(managerExists && nodeSelected);
         _deleteButton.SetEnabled(managerExists && nodeSelected);
    }

    private void OnCreateRootClicked()
    {
        // Simple example: Create a new node of a predefined type (e.g., a basic ScriptableObject)
        // A better implementation would ask the user for the Type and Name
         if (_settingsManager == null) return;

         CreateSettingNodeWindow.ShowWindow((type, name) =>
         {
             SettingNode newNode = _settingsManager.CreateNode(null, name, type); // Use specific type here
             if (newNode != null) {
                 PopulateTreeView(); // Refresh tree
                 // TODO: Select the new node in the tree
             }
         });
         
    }
    private void OnCreateChildClicked()
    {
         if (_settingsManager == null || _selectedNode == null) return;

         CreateSettingNodeWindow.ShowWindow((type, name) =>
         {
             SettingNode newNode = _settingsManager.CreateNode(_selectedNode, name, type); // Pass selected node as parent
             if (newNode != null) {
                 PopulateTreeView();
                 _treeView.SetSelectionById(_nodeMap.FirstOrDefault(x => x.Value == newNode).Key); // Select new node
                 _treeView.ScrollToItemById(_nodeMap.FirstOrDefault(x => x.Value == newNode).Key); // Scroll to it
             }
         });
    }


    private void OnDeleteClicked()
    {
        if (_settingsManager == null || _selectedNode == null) return;

         if (EditorUtility.DisplayDialog("Delete Setting Node?",
              $"Are you sure you want to delete '{_selectedNode.Name}' and its associated asset file? This cannot be undone.",
              "Delete", "Cancel"))
         {
              SettingNode nodeToDelete = _selectedNode; // Store reference before clearing selection
              _selectedNode = null; // Deselect first
               ClearInspector();

              _settingsManager.DeleteNode(nodeToDelete);
              PopulateTreeView(); // Refresh tree
              UpdateButtons();
         }
    }


    private void OnValidateClicked()
    {
        // Reuse the validation logic (needs to be refactored from SettingsManagerEditor or duplicated)
         if (_settingsManager == null) return;
        Debug.Log("Validation logic needs to be implemented here or called from a shared utility.");
        // You would call the same core logic as in SettingsManagerEditor.ValidateAndFixNodeTypes
        // It's best to extract that logic into a static method accessible by both editors.
        SettingsManagerValidationUtility.ValidateAndFixNodeTypes(_settingsManager); // Assuming you created this utility
        PopulateTreeView(); // Type might have been fixed, refresh display
         if (_selectedNode != null) ShowNodeInInspector(_selectedNode); // Refresh inspector if node was selected
    }

     // --- Helper (Refactor from SettingsManagerEditor) ---
     public static class SettingsManagerValidationUtility {
         public static void ValidateAndFixNodeTypes(SettingsManager manager) {
             if (manager == null) return;
             Debug.Log($"Starting validation for {manager.name}...");
             // --- PASTE or CALL the validation logic from SettingsManagerEditor HERE ---
             // Make sure it uses Undo.RecordObject(manager, ...) and EditorUtility methods.
             int nodesProcessed = 0; // etc...
             // ... logic ...
              Debug.Log($"Validation complete for {manager.name}."); // Replace Dialog with Log for background use
             // Consider returning results instead of showing a dialog immediately.
         }
     }

     // Helper to ask for a node name (Example)
     public static class EditorUtilityExt // Example helper
     {
          public static string AskForName(string title) {
              // Simple Input Dialog - replace with a more robust UI if needed
               string name = "";
               // Simple popup - limitations apply (can't easily validate)
               // A proper InputWindow would be better for validation etc.
               name = EditorInputDialog.Show("Enter Name", title, "");
               return name;

               // Placeholder if you don't have EditorInputDialog
               // return EditorUtility.DisplayDialog(title, "Enter name:", "OK", "Cancel") ? "DefaultName" : null; // Very basic
          }
     }

     // Simple Input Dialog (Create this helper script Editor/EditorInputDialog.cs if needed)
     public class EditorInputDialog : EditorWindow { /* ... Implementation needed ... */
          public static string Show(string title, string prompt, string initialValue) {
              // Implement a simple window with a text field and OK/Cancel buttons
               // EditorInputDialog window = GetWindow<EditorInputDialog>(true, title);
               // ... setup UI ...
               // window.ShowModal(); // or ShowUtility()
               // return window.result;
               Debug.LogWarning("EditorInputDialog not fully implemented. Returning default.");
               return "Default Name"; // Placeholder
          }
     }
}

#endif