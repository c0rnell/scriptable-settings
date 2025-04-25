using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

#if UNITY_EDITOR
// No specific editor dependency needed here anymore unless for gizmos etc.
#endif

public class SettingNode
{
    public Guid Guid { get; }
    public string Name { get;  }
    public Type SettingType { get; }
    public SettingNode Parent { get; private set; }
    
    public ScriptableObject Asset => _cache != null && _cache.TryGetTarget(out ScriptableObject so) ? so : null;
    
    private List<SettingNode> children = new List<SettingNode>();
    
    [NonSerialized] private WeakReference<ScriptableObject> _cache; // Weak reference to the loaded asset
    public IReadOnlyList<SettingNode> Children => children;

    // --- Constructor (Used by Editor) ---
    public SettingNode(string name, Type settingType, Guid assetGuid)
    {
        if (assetGuid == Guid.Empty)
            throw new ArgumentException("Asset GUID cannot be empty for a new SettingNode.", nameof(assetGuid));

        Guid = assetGuid;
        Name = name;
        
        SettingType = settingType ?? throw new ArgumentNullException(nameof(settingType));
        if (!typeof(ScriptableObject).IsAssignableFrom(this.SettingType))
             throw new ArgumentException($"Type '{settingType.FullName}' must inherit from ScriptableObject.", nameof(settingType));
    }

    // --- Runtime Methods ---
    internal void AddChild(SettingNode child) // Made internal as it's manager's responsibility
    {
        if (child != null)
        {
            child.Parent = this;
            if (!children.Contains(child)) // Avoid duplicates if called multiple times
            {
                children.Add(child);
            }
        }
    }

    // Internal method to remove a child reference
    internal bool RemoveChild(SettingNode child)
    {
        if (children.Remove(child))
        {
            child.Parent = null;
            return true;
        }

        return false;
    }
    // Try to get a cached instance or load via loader
    public bool TryGetSetting(ISettingLoader loader, out ScriptableObject so) // Return ScriptableObject
    {
        so = null;
        if (_cache != null && _cache.TryGetTarget(out so))
        {
             if (so != null) return true; // Use Unity's null check
             else _cache = null;
        }

        if (loader == null) { /* ... error log ... */ return false; }

        so = loader.Load(this); // Load returns ScriptableObject
        if (so != null)
        {
            // No GUID check needed/possible on the loaded object itself
            _cache = new WeakReference<ScriptableObject>(so);
            return true;
        }
        return false;
    }

    // Load asynchronously
    public async Task<ScriptableObject> LoadAsync(ISettingLoader loader) // Return ScriptableObject
    {
        ScriptableObject so = null;
        if (_cache != null && _cache.TryGetTarget(out so))
        {
             if (so != null) return so;
             else _cache = null;
        }

        if (loader == null) { /* ... error log ... */ return null; }

        so = await loader.LoadAsync(this); // Load returns ScriptableObject
        if (so != null)
        {
            // No GUID check needed/possible
            _cache = new WeakReference<ScriptableObject>(so);
        }
        return so;
    }
    
}