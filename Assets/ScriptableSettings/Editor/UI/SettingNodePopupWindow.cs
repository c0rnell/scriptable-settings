using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace Scriptable.Settings.Editor
{
    public class SettingNodePopupWindow : SelectorPopupWindow<SettingNode>
    {
        Func<IEnumerable<SettingNode>> _itemProvider;

        private bool _wasSearched;
        
        public void SetProvider(Func<IEnumerable<SettingNode>> itemProvider)
        {
            _itemProvider = itemProvider;
        }

        protected override IEnumerable<SettingNode> GetAllItems()
        {
            _wasSearched = false;
            return _itemProvider();
        }

        protected override List<TreeViewItemData<SettingNode>> GroupItemsToTree(List<SettingNode> flat)
        {
            var rootItems = new List<TreeViewItemData<SettingNode>>();
            
            if (flat == null || flat.Count == 0)
                return rootItems;
            
            if(flat.All(x => x.Parent == flat[0].Parent))
                return flat.Select(SettingNodesTreeView.CreateTreeViewItemRecursive).ToList();
            
            if (_wasSearched)
            {
                foreach (var rootNode in flat)
                {
                    rootItems.Add(SettingNodesTreeView.CreateTreeViewItemRecursive(rootNode));
                }
            }
            else
            {
                var existing = new List<SettingNode>();
                var paths = new List<List<SettingNode>>();
                foreach (var node in flat)
                {
                    var path = new List<SettingNode>();
                    var current = node;
                    while (current != null)
                    {
                        path.Insert(0, current); // reverse order: root -> leaf
                        current = current.Parent;
                    }
                    paths.Add(path);
                }

// Find the minimum common depth
                int minCommonLength = paths.Min(p => p.Count);
                int commonIndex = 0;

                for (int i = 0; i < minCommonLength; i++)
                {
                    var reference = paths[0][i];
                    if (paths.All(p => p[i] == reference))
                        commonIndex = i + 1;
                    else
                        break;
                }

// Group by first differing node after the common path
                var groups = new Dictionary<SettingNode, List<SettingNode>>();

                foreach (var path in paths)
                {
                    // If path is shorter than divergence point, skip
                    if (path.Count <= commonIndex)
                        continue;

                    var groupKey = path[commonIndex]; // First differing parent
                    if (!groups.ContainsKey(groupKey))
                        groups[groupKey] = new List<SettingNode>();

                    groups[groupKey].Add(path.Last());
                }

// Now use groups to build your tree
                foreach (var kvp in groups)
                {
                    var groupRoot = kvp.Key;
                    var children = kvp.Value;
                    
                    if (!existing.Contains(groupRoot))
                    {
                        existing.Add(groupRoot);
                        rootItems.Add(SettingNodesTreeView.CreateTreeViewItemRecursive(groupRoot));
                    }
                }
            }

            return rootItems;
        }

        protected override void DoSearch(string filter, SettingNode item, List<(SettingNode type, long score)> scoredList)
        {
            _wasSearched = true;
            base.DoSearch(filter, item, scoredList);
        }


        protected override string FormatSelectionItem(SettingNode item)
        {
            return item.Name;
        }
    }
}