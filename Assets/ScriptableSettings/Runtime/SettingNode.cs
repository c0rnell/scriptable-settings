using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Assertions;

#if UNITY_EDITOR
// No specific editor dependency needed here anymore unless for gizmos etc.
#endif

namespace Scriptable.Settings
{
    public class SettingNode
    {
        public Guid Guid { get; }
        public string Name { get; private set; }
        public Type SettingType { get; }
        public SettingNode Parent { get; private set; }
        public ScriptableObject Asset => _cache != null && _cache.TryGetTarget(out ScriptableObject so) ? so : null;
    
        private List<SettingNode> children = new List<SettingNode>();
        
        public List<string> Errors = new List<string>();
        
        public bool IsValid => Errors.Count == 0;
    
        [NonSerialized] private WeakReference<ScriptableObject> _cache; // Weak reference to the loaded asset
        public IReadOnlyList<SettingNode> Children => children;
    
        private ISettingLoader _settingLoader;
        
        

        // --- Constructor (Used by Editor) ---
        public SettingNode(string name, Type settingType, Guid assetGuid, ISettingLoader settingLoader)
        {
            _settingLoader = settingLoader;
            Assert.IsNotNull(_settingLoader);
        
            if (assetGuid == Guid.Empty)
                Errors.Add("Asset GUID cannot be empty for a new SettingNode!");

            Guid = assetGuid;
            Name = name;
        
            SettingType = settingType;
            if (SettingType == null)
            {
                Errors.Add($"Type of asset with {assetGuid} do not exists!");
                return;
            }  
            
            if(!typeof(ScriptableObject).IsAssignableFrom(this.SettingType))
                Errors.Add($"Type '{settingType.FullName}' must inherit from ScriptableObject!");
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

        internal void Rename(string newName)
        {
            Name = newName;
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
        public bool TryGetSetting(out ScriptableObject so) // Return ScriptableObject
        {
            so = null;
            if (_cache != null && _cache.TryGetTarget(out so))
            {
                if (so != null) return true; // Use Unity's null check
                else _cache = null;
            }

            if (_settingLoader == null) { /* ... error log ... */ return false; }

            so = _settingLoader.Load(this); // Load returns ScriptableObject
            if (CacheReference(so)) return true;
            return false;
        }
        
        public bool TryGetSetting<T>(out T settings) where T : ScriptableObject// Return ScriptableObject
        {
            settings = null;
            if(typeof(T).IsAssignableFrom(this.SettingType) == false)
            {
                Debug.LogError($"Trying to get setting of wrong type {Name}({SettingType}) by {typeof(T).Name} !");
                return false;
            }

            if (_cache != null && _cache.TryGetTarget(out var so))
            {
                if (so != null)
                {
                    settings = so as T;
                    return true; // Use Unity's null check
                }
                else
                {
                    _cache = null;
                }
            }

            if (_settingLoader == null) { /* ... error log ... */ return false; }

            so = _settingLoader.Load(this);
            settings = so as T;
            if (CacheReference(so)) return true;
            return false;
        }

        private bool CacheReference(ScriptableObject so)
        {
            if (so != null)
            {
                if(so is ISettingsObject settingsObject)
                {
                    settingsObject.OnLoaded(this); // Call OnLoaded if applicable
                }
            
                // No GUID check needed/possible on the loaded object itself
                _cache = new WeakReference<ScriptableObject>(so);
                return true;
            }

            return false;
        }

        // Load asynchronously
        public async Task<ScriptableObject> LoadAsync() // Return ScriptableObject
        {
            ScriptableObject so = null;
            if (_cache != null && _cache.TryGetTarget(out so))
            {
                if (so != null) return so;
                else _cache = null;
            }

            if (_settingLoader == null) { /* ... error log ... */ return null; }

            so = await _settingLoader.LoadAsync(this); // Load returns ScriptableObject
            if (CacheReference(so)) return so;
            return so;
        }
    
    }
}