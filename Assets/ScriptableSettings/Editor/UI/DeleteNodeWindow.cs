using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System;

namespace Scriptable.Settings.Editor
{
    public class DeleteNodeWindow : EditorWindow
    {
        /// <summary>
        /// Called with true = “delete asset too”, false = “only delete node”.
        /// </summary>
        private Action<bool> _onDeleteChoice;

        /// <summary>
        /// Show this window as a blocking modal.
        /// </summary>
        public static void ShowModal(Action<bool> onDeleteChoice, VisualElement positionParent)
        {
            var w = CreateInstance<DeleteNodeWindow>();
            w._onDeleteChoice = onDeleteChoice;
            w.ShowAsDropDown(GUIUtility.GUIToScreenRect(positionParent.worldBound), new Vector2(350, 100));
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.paddingTop    = 10;
            root.style.paddingBottom = 10;
            root.style.paddingLeft   = 10;
            root.style.paddingRight  = 10;

            // Prompt
            var label = new Label("What would you like to do with this node?");
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.fontSize       = 14;
            label.style.marginBottom   = 8;
            root.Add(label);

            // Buttons row
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.FlexEnd;

            // Cancel
            var btnCancel = new Button(() => Close()) { text = "Cancel" };
            row.Add(btnCancel);

            // Delete only the node
            var btnNodeOnly = new Button(() =>
            {
                _onDeleteChoice?.Invoke(false);
                Close();
            })
            { text = "Delete Node Only" };
            row.Add(btnNodeOnly);

            // Delete node + asset
            var btnBoth = new Button(() =>
            {
                _onDeleteChoice?.Invoke(true);
                Close();
            })
            { text = "Delete Node & Asset" };
            row.Add(btnBoth);

            root.Add(row);
        }
    }
}
