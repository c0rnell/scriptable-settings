using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Guid = System.Guid;

#if UNITY_EDITOR
// No specific editor dependency needed here anymore unless for gizmos etc.
#endif

[Serializable]
public class SettingNode : ISerializationCallbackReceiver
{
    // Serialized fields - These are saved
    [SerializeField, HideInInspector] private string id = ""; // Stores ShortGuid representation of the ScriptableObject asset's GUID
    [SerializeField, HideInInspector] private string name;
    [SerializeField, HideInInspector] private string typeName; // AssemblyQualifiedName for Type reconstruction
    [SerializeField] private List<SettingNode> children = new List<SettingNode>();

    // Non-Serialized fields - Reconstructed or temporary runtime data
    [NonSerialized] public Guid Guid; // The actual GUID of the referenced asset
    [NonSerialized] public SettingNode Parent; // Reference to parent node in the tree
    [NonSerialized] public Type SettingType; // The actual Type of the asset
    [NonSerialized] private WeakReference<ScriptableObject> _cache; // Weak reference to the loaded asset

    // Public accessors
    public string Id => id;
    public string Name => name;
    public Type Type => SettingType; // Expose the type
    public IReadOnlyList<SettingNode> Children => children;

    // --- Constructor (Used by Editor) ---
    public SettingNode(string name, Type settingType, Guid assetGuid)
    {
        if (assetGuid == Guid.Empty)
            throw new ArgumentException("Asset GUID cannot be empty for a new SettingNode.", nameof(assetGuid));

        this.Guid = assetGuid;
        this.id = ShortGuid.Encode(this.Guid);
        this.name = name;
        this.SettingType = settingType ?? throw new ArgumentNullException(nameof(settingType));
        if (!typeof(ScriptableObject).IsAssignableFrom(this.SettingType))
             throw new ArgumentException($"Type '{settingType.FullName}' must inherit from ScriptableObject.", nameof(settingType));
        this.typeName = settingType.AssemblyQualifiedName;
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
        return children.Remove(child);
    }
    public List<string> GetPathSegments()
    {
        var segments = new List<string>();
        var node = this;
        while (node != null)
        {
            // Insert at the beginning to avoid reversing later
            segments.Insert(0, node.name);
            node = node.Parent;
        }
        return segments;
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

        so = loader.Load(this.id, this.Guid, GetPathSegments()); // Load returns ScriptableObject
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

        so = await loader.LoadAsync(this.id, this.Guid, GetPathSegments()); // Load returns ScriptableObject
        if (so != null)
        {
            // No GUID check needed/possible
            _cache = new WeakReference<ScriptableObject>(so);
        }
        return so;
    }


    // --- ISerializationCallbackReceiver Implementation ---
    public void OnBeforeSerialize()
    {
        // Ensure serialized ID matches the current Guid
        id = ShortGuid.Encode(Guid);
        // Ensure type name is up-to-date
        if (SettingType != null) { typeName = SettingType.AssemblyQualifiedName; }
    }

    public void OnAfterDeserialize()
    {
        // Reconstruct Guid from serialized ID
        if (!string.IsNullOrEmpty(id)) {
            if (!ShortGuid.TryParse(id, out Guid parsedGuid)) { /* ... error log ... */ Guid = Guid.Empty; }
            else { Guid = parsedGuid; }
        } else {
             Guid = Guid.Empty; /* ... warning log ... */
        }

        // Reconstruct Type from serialized typeName
        SettingType = null; // Reset first
        if (!string.IsNullOrEmpty(typeName))
        {
            SettingType = Type.GetType(typeName);
            if (SettingType == null)
            {
                // Log clearly that the type couldn't be found AT LOAD TIME
                Debug.LogError($"SettingNode '{name}' (GUID: {id}): Could not find Type '{typeName}'. The class may have been renamed, moved, or deleted. Run 'Validate & Fix Node Types' on the SettingsManager asset.");
            }
            // ... (check if ScriptableObject) ...
        }
        else
        {
            Debug.LogWarning($"SettingNode '{name}' (GUID: {id}) has missing or empty typeName during deserialization.");
        }
        _cache = null;
    }
}