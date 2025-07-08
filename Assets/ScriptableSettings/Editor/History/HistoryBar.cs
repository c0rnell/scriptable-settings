#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Scriptable.Settings.Editor
{
    public class HistoryBar : VisualElement
    {
        public event Action OnHistorySelection;
        private readonly ToolbarBreadcrumbs m_breadcrumbBar;
        private ISettingNodeHistory m_history;
        private const int MaxNameLength = 20;

        public HistoryBar()
        {
            m_breadcrumbBar = new ToolbarBreadcrumbs();
            Add(m_breadcrumbBar);

            style.flexGrow = 1;
            style.flexDirection = new StyleEnum<FlexDirection>(FlexDirection.Row);
        }

        /// <summary>
        /// Set the history source for this history bar
        /// </summary>
        /// <param name="history">The history implementation to use</param>
        public void SetHistory(ISettingNodeHistory history)
        {
            m_history = history;
            RefreshBreadcrumbs();
        }

        /// <summary>
        /// Refresh the breadcrumb display from the current history
        /// </summary>
        public void RefreshBreadcrumbs()
        {
            if (m_history == null)
            {
                m_breadcrumbBar.Clear();
                return;
            }

            BuildBreadcrumbs();
        }

        private void BuildBreadcrumbs()
        {
            m_breadcrumbBar.Clear();

            var historyEntries = m_history.GetHistory();
            if (historyEntries == null || historyEntries.Count == 0) return;

            for (int i = 0; i < historyEntries.Count; i++)
            {
                var entry = historyEntries[i];
                if (entry?.NodeGuid == null) continue;

                int index = i;
                string displayName = GetDisplayName(entry);

                Action clickAction = () => GoToHistoryIndex(index);
                m_breadcrumbBar.PushItem(displayName, clickAction);
            }

            UpdateCurrentSelection();
        }

        private string GetDisplayName(SettingNodeHistoryEntry entry)
        {
            // Try to get the actual node from the history entry
            var currentNode = FindNodeByGuid(entry.NodeGuid);
            string displayName = currentNode?.Name ?? "Unknown";

            // Truncate long names
            if (displayName.Length > MaxNameLength)
            {
                displayName = displayName.Substring(0, MaxNameLength - 3) + "...";
            }

            return displayName;
        }

        private SettingNode FindNodeByGuid(string guidString)
        {
            // Use the history's FindNodeByGuid method to get the node
            return m_history?.FindNodeByGuid(guidString);
        }

        public void UpdateCurrentSelection()
        {
            if (m_history == null) return;

            // Get all breadcrumb items
            var breadcrumbItems = m_breadcrumbBar.Children().ToArray();
            
            for (int i = 0; i < breadcrumbItems.Length; i++)
            {
                var item = breadcrumbItems[i];
                
                // Highlight the current history index
                if (i == m_history.CurrentIndex)
                {
                    item.style.color = new StyleColor(new Color(0.0f, 1f, 0.2f, 1f));
                }
                else
                {
                    item.style.color = new StyleColor(Color.white);
                }
            }
        }

        private void GoToHistoryIndex(int index)
        {
            if (m_history == null) return;

            var historyEntries = m_history.GetHistory();
            if (index < 0 || index >= historyEntries.Count) return;

            // Navigate to the specific index
            int currentIndex = m_history.CurrentIndex;
            if (index == currentIndex) return;

            if (index < currentIndex)
            {
                // Navigate backward
                int steps = currentIndex - index;
                for (int i = 0; i < steps; i++)
                {
                    if (m_history.CanGoBack)
                        m_history.NavigateBack();
                    else
                        break;
                }
            }
            else
            {
                // Navigate forward
                int steps = index - currentIndex;
                for (int i = 0; i < steps; i++)
                {
                    if (m_history.CanGoForward)
                        m_history.NavigateForward();
                    else
                        break;
                }
            }
            
            UpdateCurrentSelection();
            
            OnHistorySelection?.Invoke();
        }
        
    }
}
#endif
