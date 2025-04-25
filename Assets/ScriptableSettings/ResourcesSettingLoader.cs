using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class ResourcesSettingLoader : ISettingLoader
{
    public ScriptableObject Load(SettingNode node)
    {
        var path = NodeLoadPath(node);
        // Load as base ScriptableObject
        var so = Resources.Load<ScriptableObject>(path);
        if (so != null) {
            // Cannot verify GUID on 'so' itself anymore
            return so;
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