using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Scriptable.Settings
{
    public class ResourcesSettingLoader : ISettingLoader
    {
        public ScriptableObject Load(SettingNode node)
        {
            // Use Unity editor aset databse because Resources.Load cannot be used in baking time
#if UNITY_EDITOR
            var assetPath = AssetDatabase.GUIDToAssetPath(new GUID(node.Guid.ToString("N")));
            return AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
#endif
            var path = NodeLoadPath(node);
            // Load as base ScriptableObject
            try
            {
                var so = Resources.Load<ScriptableObject>(path);
                if (so != null) {
                    // Cannot verify GUID on 'so' itself anymore
                    return so;
                }
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
            
            Debug.LogWarning($"ResourcesSettingLoader failed to load setting with path '{path}' or ID '{node.Guid}'.");
            return null;
        }
    
        public string NodeLoadPath(SettingNode node)
        {
            var segments = new List<string>();
            while (node != null)
            {
                // Insert at the beginning to avoid reversing later
                segments.Insert(0, node.Name);
                node = node.Parent;
            }
        
            return "Settings/" + string.Join("/", segments);
        }

        public async Task<ScriptableObject> LoadAsync(SettingNode node)
        {
            var path = NodeLoadPath(node);
            // Load as base ScriptableObject
            ResourceRequest req = Resources.LoadAsync<ScriptableObject>(path);
            while (!req.isDone) { await Task.Yield(); }

            ScriptableObject so = req.asset as ScriptableObject;
            if (so != null) {
                return so;
            }

            Debug.LogWarning($"ResourcesSettingLoader failed to load setting asynchronously with path '{path}' or ID '{node.Guid}'.");
            return null;
        }
    }
}