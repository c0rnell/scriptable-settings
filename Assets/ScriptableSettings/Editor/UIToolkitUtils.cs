using UnityEngine;
using UnityEngine.UIElements;

public static class UIToolkitUtils
{
    public static Vector2 ConvertPosition( Vector2 position, VisualElement from, VisualElement to)
    {
        // Convert from 'from' element's local space to 'to' element's local space
        Vector2 worldPos = from.LocalToWorld(position);
        return to.WorldToLocal(worldPos);
    }
    
    public static Vector2 ConstrainToContainer(Vector2 position, VisualElement element, VisualElement container)
    {
        Vector2 elementSize = new Vector2(element.resolvedStyle.width, element.resolvedStyle.height);
        Vector2 containerSize = new Vector2(container.resolvedStyle.width, container.resolvedStyle.height);
        
        float maxX = containerSize.x - elementSize.x;
        float maxY = containerSize.y - elementSize.y;
        
        return new Vector2(
            Mathf.Clamp(position.x, 0, maxX),
            Mathf.Clamp(position.y, 0, maxY)
        );
    }
}