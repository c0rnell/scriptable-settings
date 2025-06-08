using UnityEngine;

namespace Scriptable.Settings
{
    [CreateAssetMenu(fileName = "SettingLoaderFactory", menuName = "Scriptable/Settings/SettingLoaderFactory")]
    public sealed class SettingLoaderFactory  : ScriptableObject
    {
        public ISettingLoader CreateSettingLoader() => new ResourcesSettingLoader();
    }
}