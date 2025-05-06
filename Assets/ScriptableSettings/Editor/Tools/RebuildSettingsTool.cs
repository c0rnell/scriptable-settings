using System.IO;
using System.Linq;
using Unity.Properties;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.WSA;

namespace Scriptable.Settings.Editor.Tools
{
    [CreateAssetMenu(menuName = "SettingManager/Tools/RebuildSettingsTool", fileName = "RebuildSettingsTool", order = 0)]
    public class RebuildSettingsTool : SettingManagerTool, ISettingEditorTool
    {
        public VisualTreeAsset visualTree;
        
        private Button _validateButton;
        private SettingsManagerWindow _window;
        public void OnCreateSettingManagerWindow(SettingsManagerWindow root)
        {
            _window = root;
            var toolbar = root.rootVisualElement.Q<Toolbar>();
            _validateButton = new ToolbarButton()
            {
                text = "Reload Settings",
                style =
                {
                    marginLeft = 5,
                    marginRight = 5,
                    width = 150
                }
            };
            toolbar.Add(_validateButton);
            
            _validateButton.clicked += OnRequestRebuild;
        }

        public void OnDestroySettingManagerWindow()
        {
            if(_validateButton != null)
                _validateButton.clicked -= OnRequestRebuild;
        }

        private void OnRequestRebuild()
        {
            RebuildSettingsWindow.ShowWindow(visualTree, _window, _validateButton);
        }
    }

    public class RebuildSettingsWindow : EditorWindow
    {
        
        public VisualTreeAsset visualTree;
        private SettingsManagerWindow _settingsWindow;
        
        private string _folderPath;
        private ObjectField _folderField;

        public static void ShowWindow(VisualTreeAsset visualTree, SettingsManagerWindow settingsWindow, VisualElement positionParent)
        {
            var window = CreateInstance<RebuildSettingsWindow>();
            window.visualTree = visualTree;
            window._settingsWindow = settingsWindow;
            window.position = GUIUtility.GUIToScreenRect(positionParent.worldBound);
            window.minSize = new Vector2(400, 500);
            window.Show();
            /*window.ShowAsDropDown(, new Vector2(400, 500));*/
        }

        public void CreateGUI()
        {
            // Get a reference to the root of the window.
            VisualElement root = rootVisualElement;

            visualTree.CloneTree(root);
            
            var rebuildButton = root.Q<Button>("Create");
            rebuildButton.RegisterCallback<ClickEvent>(OnCreateClicked);
            
            _folderField = root.Q<ObjectField>("Folder");
            _folderField.objectType = typeof(DefaultAsset);
            _folderField.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue != null)
                {
                    _folderPath = AssetDatabase.GetAssetPath(evt.newValue);
                }
            });
        }

        private void OnCreateClicked(ClickEvent evt)
        {
            _settingsWindow.SettingsManager.Nuke();
            _settingsWindow.SettingsManager.BuildNodesFromPath(_folderPath, null);
        }
    }
}