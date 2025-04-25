using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine; // For ScriptableObject

public interface ISettingLoader
{
    // Load returns base ScriptableObject
    ScriptableObject Load(string shortId, Guid guid, IList<string> pathSegments);

    // LoadAsync returns base ScriptableObject
    Task<ScriptableObject> LoadAsync(string shortId, Guid guid, IList<string> pathSegments);
}