using System;

namespace Scriptable.Settings
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Field | AttributeTargets.Property)]
    public class SettingSourceAttribute : Attribute
    {
        public Type[] SettingCollectionType { get; }
        public SettingSourceAttribute(params Type[] settingCollectionType)
        {
            SettingCollectionType = settingCollectionType;
        }
    }
}