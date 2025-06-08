using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Scriptable.Settings.Editor
{
    public class TreeViewDragManipulator : DragAndDropManipulator
    {
        public delegate void AcceptNode(VisualElement from, SettingNode node, SettingNode targetNode);
        public delegate void AddNodes(VisualElement from, List<ScriptableObject> node, SettingNode targetNode);
        
        public AcceptNode AcceptNodeAction;
        public AddNodes AddNodeAction;
        
        public TreeViewDragManipulator(VisualElement origin, VisualElement root, AcceptNode acceptAction, AddNodes addNodes) : base(origin, root)
        {
            AcceptNodeAction = acceptAction;
            AddNodeAction = addNodes;
        }

        protected override void RegisterCallbacksOnTarget()
        {
            base.RegisterCallbacksOnTarget();
            target.RegisterCallback<DragUpdatedEvent>(OnOutsideDragEnter);
            target.RegisterCallback<DragPerformEvent>(OnOutsideDragPerformed);
        }

        

        protected override void UnregisterCallbacksFromTarget()
        {
            base.UnregisterCallbacksFromTarget();
            target.UnregisterCallback<DragUpdatedEvent>(OnOutsideDragEnter);
            target.UnregisterCallback<DragPerformEvent>(OnOutsideDragPerformed);
        }
        
        private void OnOutsideDragEnter(DragUpdatedEvent evt)
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Move;
        }
        
        private void OnOutsideDragPerformed(DragPerformEvent evt)
        {
            if (DragAndDrop.paths.Any() && target.userData is SettingNode targetNode)
            {
                var processableAssets = new List<ScriptableObject>();
                foreach (var path in DragAndDrop.paths)
                {
                    var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                    if (asset != null)
                        processableAssets.Add(asset);
                }

                if (processableAssets.Count > 0)
                    AddNodeAction?.Invoke(target, processableAssets, targetNode);
            }
        }

        protected override bool CanAcceptDrag(VisualElement hovered)
        {
            if (hovered == null)
                return false;
            
            return 
                hovered == root 
                || hovered.userData is SettingNode targetNode && targetNode != target.userData as SettingNode;
        }

        protected override void AcceptDrag(VisualElement reciever, VisualElement drager)
        {
            if (reciever == root)
            {
                AcceptNodeAction?.Invoke(drager, target.userData as SettingNode, null);
            }
            
            AcceptNodeAction?.Invoke(drager, target.userData as SettingNode, reciever.userData as SettingNode);
        }
    }
}