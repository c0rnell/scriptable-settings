using ScriptableSettings;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Scriptable.Settings.Editor.Tools
{
    [CreateAssetMenu(menuName = "SettingManager/Tools/ValidateTypesTool", fileName = "ValidateTypesTool", order = 0)]
    public class ValidateTypesTool : SettingManagerTool, ISettingEditorTool
    {
        private Button _validateButton;
        private SettingsManagerWindow _window;

        public void OnCreateSettingManagerWindow(SettingsManagerWindow window)
        {
            _window = window;
            var toolbar = window.rootVisualElement.Q<Toolbar>();
            _validateButton = new ToolbarButton()
            {
                text = "Validate Types",
                style =
                {
                    marginLeft = 5,
                    marginRight = 5,
                    width = 150
                }
            };
            toolbar.Add(_validateButton);
            
            _validateButton.clicked += OnValidateClicked;
        }
        
        public void OnDestroySettingManagerWindow()
        {
            if(_validateButton != null)
                _validateButton.clicked -= OnValidateClicked;
        }
        
        private void OnValidateClicked()
        {
            // Reuse the validation logic (needs to be refactored from SettingsManagerEditor or duplicated)
            if (_window.SettingsManager == null) return;
            Debug.Log("Validation logic needs to be implemented here or called from a shared utility.");
            // You would call the same core logic as in SettingsManagerEditor.ValidateAndFixNodeTypes
            // It's best to extract that logic into a static method accessible by both editors.
            ValidateAndFixNodeTypes(_window.SettingsManager); // Assuming you created this utility
            _window.rootVisualElement.Q<SettingNodesTreeView>().PopulateTreeView(); // Type might have been fixed, refresh display
            /*if (_selectedNode != null)
                _inspectorPanel.ShowNodeInInspector(_settingsManager, _selectedNode);*/ // Refresh inspector if node was selected
        }

        private void ValidateAndFixNodeTypes(SettingsManager windowSettingsManager)
        {
            //TODO implement validation logic
        }
    }
}