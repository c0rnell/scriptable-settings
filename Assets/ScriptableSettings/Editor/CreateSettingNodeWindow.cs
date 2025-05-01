using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System;
using System.Collections.Generic;
using System.Linq;
using DefaultNamespace; // Required for LINQ operations like .ToList()

namespace Scriptable.Settings.Editor
{
    public class CreateSettingNodeWindow : EditorWindow
    {
        private TextField nodeNameField;
        private SelectorPopupField<Type> nodeTypeField; // Or PopupField depending on preference
        private Button createButton;
        private Button cancelButton;

        // Action to call when the user confirms creation.
        // Takes the selected type and node name as arguments.
        private Action<Type, string> onCreateConfirmed;

        private Dictionary<string, Type> typeMapping = new Dictionary<string, Type>();

        private SettingNode _parent;
        
        // Method to show the window and set the callback
        public static void ShowWindow(Action<Type, string> onCreateCallback, SettingNode nodeParent, VisualElement positionParent)
        {
            CreateSettingNodeWindow window =  ScriptableObject.CreateInstance<CreateSettingNodeWindow>();
            window.onCreateConfirmed = onCreateCallback;
            window._parent = nodeParent;
            //window.Show();
            window.ShowAsDropDown(GUIUtility.GUIToScreenRect(positionParent.worldBound), new Vector2(250, 150));
        }

        public void CreateGUI()
        {
            /*typeMapping.Clear();

            foreach (var settingType in FindSettingTypes())
            {
                // Add each setting type to the mapping
                typeMapping.Add(settingType.Name, settingType);
            }*/

            // Get a reference to the root of the window.
            VisualElement root = rootVisualElement;

            // --- Style Sheet (Optional but Recommended) ---
            // You can create a .uss file and load it here for styling.
            // Example: var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Editor/CreateNodeWindow.uss");
            // root.styleSheets.Add(styleSheet);


            // --- Title Label ---
            Label titleLabel = new Label("Enter Node Details");
            titleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            titleLabel.style.fontSize = 18;
            titleLabel.style.marginBottom = 10;
            root.Add(titleLabel);

            // --- Name Field ---
            nodeNameField = new TextField();
            nodeNameField.value = "New Node"; // Default name
            nodeNameField.style.marginBottom = 5;
            root.Add(nodeNameField);


            nodeTypeField = new TypeSelectorDropdown("Type", FindSettingTypes) { style = { flexGrow = 1}}; // Select the first item by default
            nodeTypeField.style.marginBottom = 10;
            nodeTypeField.RegisterValueChangedCallback(evt =>
            {
                // Update the selected type when the dropdown value changes
                Type selectedType = evt.newValue;
                nodeTypeField.value = selectedType;
            });
            root.Add(nodeTypeField);

            // --- Buttons Container ---
            VisualElement buttonContainer = new VisualElement();
            buttonContainer.style.flexDirection = FlexDirection.Row;
            buttonContainer.style.justifyContent = Justify.FlexEnd; // Align buttons to the right
            buttonContainer.style.marginTop = 10;
            root.Add(buttonContainer);

            // --- Cancel Button ---
            cancelButton = new Button(Close);
            cancelButton.text = "Cancel";
            cancelButton.style.marginRight = 5; // Add some space between buttons
            buttonContainer.Add(cancelButton);

            // --- Create Button ---
            createButton = new Button(() =>
            {
                // Call the callback action with the selected type and name
                onCreateConfirmed?.Invoke(nodeTypeField.value, nodeNameField.value);

                Close(); // Close the window after creation
            });
            createButton.text = "Create";
            buttonContainer.Add(createButton);

            // --- Basic Layout/Padding ---
            root.style.paddingTop = 10;
            root.style.paddingBottom = 10;
            root.style.paddingLeft = 10;
            root.style.paddingRight = 10;
        }

        // Helper method to find types inheriting from a base type (Example)
        // You might want to call this when populating the dropdown
        private List<Type> FindSettingTypes()
        {
            // Replace BaseSettingType with the actual base class for your settings nodes
            List<Type> settingTypes = new List<Type>();

            if (_parent != null && _parent.TryGetSetting(out var setting) && setting is ISettingCollection collection)
            {
                settingTypes.AddRange(collection.GetSettingTypes());
            }
            else
            {
                settingTypes.Add(typeof(ScriptableObject));
            }
            
            // Or your specific base type
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