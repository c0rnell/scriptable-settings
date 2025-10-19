namespace Scriptable.Settings
{
    using UnityEngine;

    public static class SettingsExtensions
    {
        public static bool TyrGetSetting<T>(this ISettingId<T> id, out T value) 
            where T : ScriptableObject
        {
            if(id.Id == System.Guid.Empty)
            {
                value = null;
                return false;
            }

            value = null;//ScriptableSettings.GetSetting<T>(id);
            return true;
        }
    }
}