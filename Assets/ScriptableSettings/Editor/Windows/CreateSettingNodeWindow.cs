using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.UIElements; // Required for LINQ operations like .ToList()

namespace Scriptable.Settings.Editor
{
    public class CreateSettingNodeWindow : EditorWindow
    {
        public VisualTreeAsset visualTree;

        // Fields from original "Create New"
        private TextField nodeNameField;
        private TypeSelectorDropdown nodeTypeField; // Your custom TypeSelectorDropdown
        private Button createAssetButton;

        // Fields for "Add Existing"
        private ObjectField existingAssetField;
        private Button addExistingAssetButton;

        // Tab handling
        private Button createNewTabButton;
        private Button addExistingTabButton;
        private VisualElement createNewTabContent;
        private VisualElement addExistingTabContent;

        // Common
        private Button cancelButton;
        private TextField parentField;

        // Actions
        // Action for creating a new asset
        private Action<Type, string> onCreateConfirmed;
        // Action for adding an existing asset
        private Action<ScriptableObject> onAddExistingConfirmed; // UnityEngine.Object is more general

        private SettingNode _parent;

        private CreateSettingNodeWindow _window;

        // Updated ShowWindow method to accept both callbacks
        public static void ShowWindow(SettingNode nodeParent, VisualElement positionParent,
            Action<Type, string> createCallback, 
            Action<ScriptableObject> addExistingCallback)
        {
            CreateSettingNodeWindow _window = ScriptableObject.CreateInstance<CreateSettingNodeWindow>();
            _window.onCreateConfirmed = createCallback;
            _window.onAddExistingConfirmed = addExistingCallback;
            _window._parent = nodeParent;
            _window.position = GUIUtility.GUIToScreenRect(positionParent.worldBound);
            _window.minSize = new Vector2(400, 250);

            // Adjust size if needed to accommodate tabs
            _window.Show(); // Increased height
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            visualTree.CloneTree(root);

            // --- Query UI Elements ---
            parentField = root.Q<TextField>("Parent");
            parentField.style.display = _parent == null ? DisplayStyle.None : DisplayStyle.Flex;
            if (_parent != null)
            {
                parentField.value = _parent.Name;
            }

            // Tab Buttons
            createNewTabButton = root.Q<Button>("CreateNewTabButton");
            addExistingTabButton = root.Q<Button>("AddExistingTabButton");

            // Tab Content Panes
            createNewTabContent = root.Q<VisualElement>("CreateNewTabContent");
            addExistingTabContent = root.Q<VisualElement>("AddExistingTabContent");

            // "Create New" Tab Elements
            nodeNameField = createNewTabContent.Q<TextField>("Name"); // Ensure "Name" is within CreateNewTabContent in UXML
            nodeTypeField = createNewTabContent.Q<TypeSelectorDropdown>(); // Ensure "TypeSelector" is within CreateNewTabContent
            createAssetButton = createNewTabContent.Q<Button>("CreateAssetButton");

            // "Add Existing" Tab Elements
            existingAssetField = addExistingTabContent.Q<ObjectField>("ExistingAssetField");
            if (existingAssetField != null)
            {
                 // By default, ObjectField allows any UnityEngine.Object. 
                 // If you want to restrict it to ScriptableObject or a derivative:
                existingAssetField.objectType = typeof(ScriptableObject);
            }
            addExistingAssetButton = addExistingTabContent.Q<Button>("AddExistingAssetButton");
            
            // Common Button
            cancelButton = root.Q<Button>("CancelButton");


            // --- Setup Initial State & Callbacks ---
            if (nodeTypeField != null)
            {
                nodeTypeField.SetItemProvider(FindSettingTypes); // Your existing method
                var typeOptions = FindSettingTypes();
                if (typeOptions.Count == 1)
                {
                    nodeTypeField.SetValueWithoutNotify(typeOptions[0]);
                }
            }

            // Tab Switching Logic
            createNewTabButton?.RegisterCallback<ClickEvent>(evt => SwitchTab(true));
            addExistingTabButton?.RegisterCallback<ClickEvent>(evt => SwitchTab(false));

            // Action Button Callbacks
            createAssetButton?.RegisterCallback<ClickEvent>((click) =>
            {
                if (nodeTypeField != null && nodeNameField != null)
                {
                    onCreateConfirmed?.Invoke(nodeTypeField.value, nodeNameField.value);
                }
                Close();
            });

            addExistingAssetButton?.RegisterCallback<ClickEvent>((click) =>
            {
                if (existingAssetField != null && existingAssetField.value != null)
                {
                    onAddExistingConfirmed?.Invoke(existingAssetField.value as ScriptableObject);
                }
                Close();
            });

            cancelButton?.RegisterCallback<ClickEvent>((click) => Close());

            // Initialize to the "Create New" tab
            SwitchTab(true);
        }

        private void SwitchTab(bool showCreateNew)
        {
            if (createNewTabContent != null && addExistingTabContent != null)
            {
                createNewTabContent.style.display = showCreateNew ? DisplayStyle.Flex : DisplayStyle.None;
                addExistingTabContent.style.display = showCreateNew ? DisplayStyle.None : DisplayStyle.Flex;
            }

            // Optionally, change tab button styles to indicate selection
            if (createNewTabButton != null && addExistingTabButton != null)
            {
                createNewTabButton.style.backgroundColor = showCreateNew ? new StyleColor(new Color(0.31f, 0.31f, 0.31f)) : new StyleColor(new Color(0.23f, 0.23f, 0.23f)); // Darker gray for selected
                addExistingTabButton.style.backgroundColor = !showCreateNew ? new StyleColor(new Color(0.31f, 0.31f, 0.31f)) : new StyleColor(new Color(0.23f, 0.23f, 0.23f));
            }
        }

        // Your existing FindSettingTypes method
        private List<Type> FindSettingTypes()
        {
            // ... (your existing implementation from CreateSettingNodeWindow.cs)
            List<Type> settingTypes = new List<Type>();

            if (_parent != null && _parent.TryGetSetting(out var setting) && setting is ISettingCollection collection)
            {
                settingTypes.AddRange(collection.GetSettingTypes()); 
            }
            else
            {
                settingTypes.Add(typeof(ScriptableObject));
            }
            
            var nonUnityAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly =>
                {
                    var name = assembly.GetName().Name;
                    return !name.StartsWith("Unity") &&
                           !name.StartsWith("UnityEditor") &&
                           !name.StartsWith("UnityEngine") &&
                           !name.StartsWith("Mono") &&
                           !name.StartsWith("System") &&
                           !name.StartsWith("Microsoft");
                })
                .ToList();

            List<Type> types = nonUnityAssemblies
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type => type is { IsClass: true, IsAbstract: false, BaseType: { Namespace: not null } }
                               && !type.BaseType.Namespace.StartsWith("UnityEditor")
                               && settingTypes.Any(x => x.IsAssignableFrom(type)))
                .Distinct()
                .ToList();

            return types;
        }
    }
}