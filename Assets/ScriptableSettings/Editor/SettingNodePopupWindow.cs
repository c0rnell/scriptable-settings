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
                foreach (var node in flat)
                {
                    var rootNode = node;
                    while (rootNode.Parent != null)
                    {
                        rootNode = rootNode.Parent;
                    }
                    
                    if(existing.Contains(rootNode))
                        continue;
                    
                    existing.Add(rootNode);
                    rootItems.Add(SettingNodesTreeView.CreateTreeViewItemRecursive(rootNode));
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