using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System;
using System.Collections.Generic;
using System.Linq; // Required for LINQ operations like .ToList()

namespace Scriptable.Settings.Editor
{
    public class CreateSettingNodeWindow : EditorWindow
    {
        public VisualTreeAsset visualTree;
        
        private TextField nodeNameField;
        private TypeSelectorDropdown nodeTypeField; // Or PopupField depending on preference
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
            window.ShowAsDropDown(GUIUtility.GUIToScreenRect(positionParent.worldBound), new Vector2(350, 150));
        }

        public void CreateGUI()
        {
            // Get a reference to the root of the window.
            VisualElement root = rootVisualElement;

            visualTree.CloneTree(root);
           
            var parentField = root.Q<TextField>("Parent");
            parentField.style.display = _parent == null ? DisplayStyle.None : DisplayStyle.Flex;
            parentField.value = _parent?.Name;

            nodeNameField = root.Q<TextField>("Name");
            
            nodeTypeField = root.Q<TypeSelectorDropdown>();
            nodeTypeField.SetItemProvider(FindSettingTypes);
            var typeOptions = FindSettingTypes();
            if (typeOptions.Count == 1)
            {
                nodeTypeField.SetValueWithoutNotify(typeOptions[0]);
            }
            
            
            root.Q<Button>("Create").RegisterCallback<ClickEvent>((click) =>
            {
                onCreateConfirmed?.Invoke(nodeTypeField.value, nodeNameField.value);
                Close();
            });
            
            root.Q<Button>("Cancel").RegisterCallback<ClickEvent>((click) => Close());
            
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