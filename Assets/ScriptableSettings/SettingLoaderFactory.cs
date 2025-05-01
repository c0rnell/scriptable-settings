using UnityEngine;

namespace ScriptableSettings
{
    [CreateAssetMenu(fileName = "SettingLoaderFactory", menuName = "Scriptable/Settings/SettingLoaderFactory")]
    public sealed class SettingLoaderFactory  : ScriptableObject
    {
        public ISettingLoader CreateSettingLoader() => new ResourcesSettingLoader();
    }
}