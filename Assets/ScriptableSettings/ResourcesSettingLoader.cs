using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class ResourcesSettingLoader : ISettingLoader
{
    public ScriptableObject Load(string shortId, Guid guid, IList<string> pathSegments)
    {
        var path = GetPath(pathSegments);
        // Load as base ScriptableObject
        var so = Resources.Load<ScriptableObject>(path);
        if (so != null) {
            // Cannot verify GUID on 'so' itself anymore
            return so;
        }

        // Fallback to loading by shortId as filename
        so = Resources.Load<ScriptableObject>(shortId);
        if (so != null) {
            return so;
        }

        Debug.LogWarning($"ResourcesSettingLoader failed to load setting with path '{path}' or ID '{shortId}'.");
        return null;
    }

    private static string GetPath(IList<string> pathSegments)
    {
        return "Settings/" + string.Join("/", pathSegments);
    }

    public async Task<ScriptableObject> LoadAsync(string shortId, Guid guid, IList<string> pathSegments)
    {
        var path = GetPath(pathSegments);
        // Load as base ScriptableObject
        ResourceRequest req = Resources.LoadAsync<ScriptableObject>(path);
        while (!req.isDone) { await Task.Yield(); }

        ScriptableObject so = req.asset as ScriptableObject;
        if (so != null) {
            return so;
        }

        // Fallback to loading by shortId async
        ResourceRequest req2 = Resources.LoadAsync<ScriptableObject>(shortId);
        while (!req2.isDone) { await Task.Yield(); }

        so = req2.asset as ScriptableObject;
        if (so != null) {
            return so;
        }

        Debug.LogWarning($"ResourcesSettingLoader failed to load setting asynchronously with path '{path}' or ID '{shortId}'.");
        return null;
    }
}