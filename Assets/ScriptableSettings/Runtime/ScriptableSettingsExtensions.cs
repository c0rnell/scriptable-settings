using UnityEngine;

namespace Scriptable.Settings
{
    public static class ScriptableSettingsExtensions
    {
        public static T GetSettings<T>(this ScriptableSettingId<T> id) where T : ScriptableObject
        {
            return SettingManagerHelper.Instance.GetSettings<T>(id);
            
        }
    }
}