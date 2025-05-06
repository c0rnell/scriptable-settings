using System.Threading.Tasks;
using UnityEngine;

// For ScriptableObject

namespace Scriptable.Settings
{
    public interface ISettingLoader
    {
        // Load returns base ScriptableObject
        ScriptableObject Load(SettingNode node);

        // LoadAsync returns base ScriptableObject
        Task<ScriptableObject> LoadAsync(SettingNode node);
    
        string NodeLoadPath(SettingNode node);
    
    
    }
}