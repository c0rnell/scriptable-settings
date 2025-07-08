using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Scriptable.Settings.Editor
{
    public static class TreeExtensions
    {
        public static TreeViewItemData<T>? FindItemByData<T>(this IEnumerable<TreeViewItemData<T>> items, T targetData, List<TreeViewItemData<T>> path)
        {
            foreach (var item in items)
            {
                if (EqualityComparer<T>.Default.Equals(item.data, targetData))
                {
                    path.Add(item);
                    return item;
                }

                var found = FindItemByData(item.children, targetData, path);
                if (found.HasValue)
                {
                    path.Add(item);
                    return found;
                }
            }
            return null;
        }

        public static void ExpandAndSelect<T>(this TreeView tree, IEnumerable<TreeViewItemData<T>> rootItems, T item, bool select = false)
        {
            if (rootItems == null || item == null)
                return;
        
            var pathToItem = new List<TreeViewItemData<T>>();
            var treeItem = rootItems.FindItemByData(item, pathToItem);
            if (treeItem.HasValue)
            {
                pathToItem.Reverse();
                foreach (var parent in pathToItem)
                {
                    tree.ExpandItem(parent.id);
                }
            
                if(select)
                    tree.SetSelectionById(new List<int> { treeItem.Value.id });
                else
                    tree.SetSelectionByIdWithoutNotify(new List<int> { treeItem.Value.id });
                
                tree.ScrollToItemById(treeItem.Value.id);
            }
        }
    }
}