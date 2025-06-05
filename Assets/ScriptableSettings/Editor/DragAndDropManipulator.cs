using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Scriptable.Settings.Editor
{
    public abstract class DragAndDropManipulator : PointerManipulator
    {
        private bool isPressed;
        private readonly Label floaterElement;
        private Vector2 targetStartPosition { get; set; }
        private Vector3 pointerStartPosition { get; set; }
        private bool enabled { get; set; }
        private bool IsDragging { get; set; }

        protected VisualElement root { get; }

        private VisualElement currentHoverTarget;
        private string hoverClass;
        public DragAndDropManipulator(VisualElement origin, VisualElement root)
        {
            this.target = origin;
            this.root = root;

            if (floaterElement == null) floaterElement = new Label();
            floaterElement.name = "Floater Drag";
            floaterElement.text = "";
            floaterElement.style.height = 0;
            floaterElement.style.width = 0;
            floaterElement.style.position = new StyleEnum<Position>(Position.Absolute);
            floaterElement.pickingMode = PickingMode.Ignore;

            floaterElement.SetEnabled(false);
        }

        public DragAndDropManipulator WithHoverClass(string className)
        {
            hoverClass = className;
            return this;
        }

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<PointerDownEvent>(PointerDownHandler);
            target.RegisterCallback<PointerMoveEvent>(PointerMoveHandler);
            target.RegisterCallback<PointerUpEvent>(PointerUpHandler);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<PointerDownEvent>(PointerDownHandler);
            target.UnregisterCallback<PointerMoveEvent>(PointerMoveHandler);
            target.UnregisterCallback<PointerUpEvent>(PointerUpHandler);
        }

        private void PointerDownHandler(PointerDownEvent evt)
        {
            targetStartPosition = target.transform.position;
            pointerStartPosition = evt.position;
            isPressed = true;
        }

        private void PointerMoveHandler(PointerMoveEvent evt)
        {
            if (!isPressed)
            {
                return;
            }

            if (!IsDragging && Vector2.Distance(evt.position, pointerStartPosition) > 3)
            {
                IsDragging = true;

                target.CapturePointer(evt.pointerId);
                root.Add(floaterElement);

                floaterElement.SetEnabled(true);
                UpdateDragLabelPosition(evt.position);
                floaterElement.text = target.Q<Label>().text;
                floaterElement.BringToFront();
            }

            if (IsDragging && target.HasPointerCapture(evt.pointerId))
            {
                UpdateDragLabelPosition(evt.position);
                VisualElement hovered = root.panel.Pick(evt.position);
                
                
                
                if (CanAcceptDrag(hovered))
                {
                    floaterElement.style.color = new StyleColor(Color.white);
                    floaterElement.style.unityFontStyleAndWeight = new StyleEnum<FontStyle>(FontStyle.Bold);
                    if (currentHoverTarget != null && hoverClass != null)
                        currentHoverTarget.RemoveFromClassList(hoverClass);
                    
                    currentHoverTarget = hovered;
                    
                    if(currentHoverTarget != null && hovered != null)
                        currentHoverTarget.AddToClassList(hoverClass);
                }
                else
                {
                    floaterElement.style.color = new StyleColor(Color.red);
                    floaterElement.style.unityFontStyleAndWeight = new StyleEnum<FontStyle>(FontStyle.Normal);
                    
                    if (currentHoverTarget != null && hoverClass != null)
                        currentHoverTarget.RemoveFromClassList(hoverClass);
                    
                    currentHoverTarget = null;
                }
            }
        }


        private void PointerUpHandler(PointerUpEvent evt)
        {
            if (root != null && floaterElement != null && floaterElement.parent == root)
                root.Remove(floaterElement);
            
            if (IsDragging && target.HasPointerCapture(evt.pointerId))
            {
                target.ReleasePointer(evt.pointerId);
                
                floaterElement?.SetEnabled(false);

                if (currentHoverTarget != null)
                {
                    if(hoverClass != null)
                        currentHoverTarget.RemoveFromClassList(hoverClass);
                    AcceptDrag(currentHoverTarget, target);
                }

                IsDragging = false;
            }
            
            
            isPressed = false;
        }
        
        private void UpdateDragLabelPosition(Vector2 pointerPosition)
        {
            // Convert pointer position to root container's local coordinates
            Vector2 localPosition = root.WorldToLocal(pointerPosition);
        
            // Add offset so label doesn't cover the cursor
            Vector2 labelOffset = new Vector2(10, 0);
            Vector2 finalPosition = localPosition + labelOffset;
        
            // Constrain to root container bounds
            finalPosition = UIToolkitUtils.ConstrainToContainer(finalPosition, floaterElement, root);
        
            // Apply position
            floaterElement.style.left = finalPosition.x;
            floaterElement.style.top = finalPosition.y;
        }

        protected abstract bool CanAcceptDrag(VisualElement hovered);
        protected abstract void AcceptDrag(VisualElement reciever, VisualElement drager);

    }
}