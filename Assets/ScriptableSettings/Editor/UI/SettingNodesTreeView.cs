using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Search;
using UnityEngine;
using UnityEngine.UIElements;

namespace Scriptable.Settings.Editor
{
    public class SettingNodesTreeView : VisualElement
    {
        /*public event Action<VisualElement, SettingNode> AddChildNodeClicked;
        public event Action<VisualElement, SettingNode> RemoveNodeClicked;*/
        public event Action<VisualElement, SettingNode, string> NodeRenamed;
        public event Action<VisualElement, SettingNode, SettingNode> NodeMoved;
        public event Action<VisualElement, SettingNode, ScriptableObject[]> AssetsAdded;
        public event Action<SettingNode> selectionChanged;

        public SettingNode Selected => _selectedNode;
        public const string TreeItemName = "TreeItem";

        private readonly TreeView _treeView;
        private readonly List<TreeViewItemData<SettingNode>> _treeRootData = new List<TreeViewItemData<SettingNode>>();

        private VisualElement _renaming;
        private SettingsManager _settingsManager;
        private SettingNode _selectedNode;
        public SettingNodesTreeView(VisualTreeAsset settingItem) : base()
        {
            _treeView = new TreeView();
            _treeView.focusable = true;
            Add(_treeView);
            
            _treeView.makeItem = () =>
            {
                var ve = new VisualElement() { name = TreeItemName };
                settingItem.CloneTree(ve);
                ve.focusable = true;
                ve.RegisterCallback<KeyDownEvent, VisualElement>(OnKeyDown, ve);
                
                ve.AddManipulator(new TreeViewDragManipulator(ve, this.parent
                    , (from, node, targetNode) => NodeMoved?.Invoke(this, node, targetNode),
                    (from, assets, target) => AssetsAdded?.Invoke(from, target, assets.ToArray())
                 ).WithHoverClass("drag-hover"));

                return ve;
            }; // Create a simple Label for each tree item

            _treeView.bindItem = (element, index) =>
            {
                element.focusable = true;
                var label = element.Q<Label>();
                var addButton = element.Q<Button>("Add");
                var node = _treeView.GetItemDataForIndex<SettingNode>(index);
                element.userData = node;
                
                if (node != null)
                {
                    label.text = node.Name;
                    SetFileOrFolder(node, element);
                }
                else
                {
                    label.text = "Error: Node not found";
                }
            };

            _treeView.unbindItem = (element, index) =>
            {
            };
            
            _treeView.selectionChanged += OnSelectionChange;
            
            /*RegisterCallback<DragPerformEvent, VisualElement>((evt, elm) =>
                {
                    if (DragAndDrop.GetGenericData("UserData") is SettingNode draggedNode)
                    {
                        NodeMoved?.Invoke(elm, draggedNode, null);
                    }
                    DragAndDrop.AcceptDrag();
                    evt.StopPropagation();
                }, this);
            
            RegisterCallback<DragUpdatedEvent, VisualElement>((evt, elm)  =>
            {
                var draggedNode = DragAndDrop.GetGenericData("UserData") as SettingNode;
                if (draggedNode != null)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Move;
                    evt.StopPropagation();
                }
            }, this);*/
            
            //_treeView.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
        }

        /*private void SetupDragAndDrop(VisualElement ve)
        {
            ve.RegisterCallback<MouseDownEvent, VisualElement>((evt, elm)  =>
            {
                if (evt.button == (int)MouseButton.LeftMouse && evt.clickCount == 1)
                {
                    DragAndDrop.PrepareStartDrag();
                    DragAndDrop.SetGenericData("UserData", elm.userData);
                    
                }
            }, ve);
            ve.RegisterCallback<MouseUpEvent, VisualElement>((evt, elm)  =>
            {
                DragAndDrop.PrepareStartDrag();
                elm.RemoveFromClassList("drag-hover");
            }, ve);
                
            ve.RegisterCallback<DragEnterEvent, VisualElement>((evt, elm)  =>
            {
                DragAndDrop.StartDrag("Dragging Node");
                if(elm.userData != DragAndDrop.GetGenericData("UserData"))
                    elm.AddToClassList("drag-hover");
                DragAndDrop.visualMode = DragAndDropVisualMode.Move;
            }, ve);

            ve.RegisterCallback<DragLeaveEvent, VisualElement>((evt, elm)  =>
            {
                elm.RemoveFromClassList("drag-hover");
            }, ve);

            ve.RegisterCallback<DragUpdatedEvent, VisualElement>((evt, elm)  =>
            {
                var draggedNode = DragAndDrop.GetGenericData("UserData") as SettingNode;
                if (elm.userData is SettingNode targetNode && draggedNode != targetNode)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Move;
                    evt.StopPropagation();
                }
                else
                {
                    elm.RemoveFromClassList("drag-hover");
                }
            }, ve);
                
            ve.RegisterCallback<DragPerformEvent, VisualElement>((evt, elm)  =>
            {
                if (elm.userData is SettingNode targetNode)
                {
                    if (DragAndDrop.GetGenericData("UserData") is SettingNode draggedNode)
                    {
                        NodeMoved?.Invoke(elm, draggedNode, targetNode);
                    }

                    if (DragAndDrop.paths.Any())
                    {
                        var processableAssets = new List<ScriptableObject>();
                        foreach (var path in DragAndDrop.paths)
                        {
                            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                            if (asset != null)
                                processableAssets.Add(asset);
                        }

                        if (processableAssets.Count > 0)
                            AssetsAdded?.Invoke(elm, targetNode, processableAssets.ToArray());
                    }
                }
                
                elm.RemoveFromClassList("drag-hover");
                DragAndDrop.AcceptDrag();
                evt.StopPropagation();
            },ve);
        }*/

        private void OnSelectionChange(IEnumerable<object> obj)
        {
            _selectedNode = _treeView.GetItemDataForIndex<SettingNode>(_treeView.selectedIndex);
            var element =_treeView.GetRootElementForIndex(_treeView.selectedIndex);
            if(element != null)
                element.Q("Item").Focus();
             
            selectionChanged?.Invoke(_selectedNode);
        }

        private static void SetFileOrFolder(SettingNode node, VisualElement element)
        {
            var item = element.Q("Item");
            var icon = element.Q("Icon");
            item.RemoveFromClassList("setting-item-folder");
            item.RemoveFromClassList("setting-item-file");

            if (node.Children.Any())
            {
                item.AddToClassList("setting-item-folder");
                icon.style.backgroundImage = new StyleBackground((Texture2D)EditorIcons.Folder) ;
            }
            else
            {
                item.AddToClassList("setting-item-file");
                icon.style.backgroundImage = new StyleBackground((Texture2D)EditorIcons.EmptyFolder) ;
            }
        }

        public static TreeViewItemData<SettingNode> CreateTreeViewItemRecursive(SettingNode node)
        {
            int currentId = node.Name.GetHashCode(); 
            var childrenData = new List<TreeViewItemData<SettingNode>>();
            foreach (var childNode in node.Children)
            {
                childrenData.Add(CreateTreeViewItemRecursive(childNode));
            }

            return new TreeViewItemData<SettingNode>(currentId, node, childrenData);
        }
        
        public void PopulateTreeView(string filter = null)
        {
            _treeView.Clear();
            _treeRootData.Clear();
            
            if (_settingsManager == null)
            {
                _treeView.RefreshItems(); // Show empty tree
                return;
            }
            // Build tree view item data (IDs and mapping) recursively
            IEnumerable<SettingNode> nodesToDisplay =
                string.IsNullOrEmpty(filter) ?
                _settingsManager.SettingTree
                : _settingsManager.GetAllNodes().Where(x => FuzzySearch.FuzzyMatch(filter, x.Name));
            
            // Filter nodes based on search field
            
            foreach (var rootNode in nodesToDisplay)
            {
                _treeRootData.Add(CreateTreeViewItemRecursive(rootNode));
            }

            _treeView.SetRootItems(_treeRootData);
            _treeView.RefreshItems();
            
            if(_selectedNode != null)
                _treeView.ExpandAndSelect(_treeRootData, _selectedNode);
            
            // Important: Rebuild the visual tree
        }

        public void SelectNode(SettingNode node)
        {
            _selectedNode = node;
            _treeView.ExpandAndSelect(_treeRootData, _selectedNode, true);
        }

        public void Deselect(VisualElement visualElement)
        {
            if(visualElement != null)
                visualElement.RemoveFromClassList("drag-hover");
            _treeView.selectedIndex = -1;
        }
        
        void OnKeyDown(KeyDownEvent evt, VisualElement elem)
        {
            if (evt.keyCode == KeyCode.F2 && _treeView.selectedItem != null)
            {
                StartRename(elem);
                evt.StopPropagation();
            }
        }

        void StartRename(VisualElement itemElement)
        {
            if(_renaming != null)
            {
                if(_renaming == itemElement)
                    return;
                
                EndRename(_renaming, apply: false);
            }
            
            var label       = itemElement.Q<Label>();
            var renameField = itemElement.Q<TextField>();
            label.style.display       = DisplayStyle.None;
            renameField.style.display = DisplayStyle.Flex;
            renameField.value = label.text;
            renameField.focusable = true;
            renameField.Focus();
            renameField.SelectAll();

            itemElement.RegisterCallback<KeyDownEvent>(ProcessRenameKeys, TrickleDown.TrickleDown);

            _renaming = itemElement;
        }

        private void ProcessRenameKeys(KeyDownEvent evt)
        {
            if (evt.currentTarget is TextField rename)
            {
                Debug.Log("Rename field key event: " + evt.bubbles);
                return; 
            }
            
            var itemElement = evt.currentTarget as VisualElement;
            
            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
            {
                EndRename(itemElement, apply: true);
                evt.StopPropagation();
            }
            else if (evt.keyCode == KeyCode.Escape)
            {
                EndRename(itemElement, apply: false);
                evt.StopPropagation();
            }
        }

        void EndRename(VisualElement itemElement, bool apply)
        {
            var node        = (SettingNode) itemElement.userData;
            var label       = itemElement.Q<Label>();
            var renameField = itemElement.Q<TextField>();

            if (apply && renameField.value != node.Name)
            {
               NodeRenamed?.Invoke(itemElement, node, renameField.value);
            }

            // restore visuals
            label.text               = node.Name;
            renameField.value        = node.Name;
            renameField.style.display = DisplayStyle.None;
            label.style.display       = DisplayStyle.Flex;
            
            itemElement.UnregisterCallback<KeyDownEvent>(ProcessRenameKeys);

            _treeView.Focus();
            
            _renaming = null;
        }

        public void SetManager(SettingsManager settingsManager)
        {
            _settingsManager = settingsManager;
        }
    }
}