using UnityEngine;

namespace Scriptable.Settings
{
    public partial class ScriptableSettings
    {
        private static ScriptableSettings _instance;
        
        internal static ScriptableSettings Instance
        {
            get
            {
                if (!_instance)
                {
                    _instance = Resources.Load<ScriptableSettings>("ScriptableSettings");
                }

                return _instance;
            }
        }
        
        public static T GetSetting<TID, T>(TID id) where T : ScriptableObject where TID : ISettingId<T>
        {
            return Instance.LoadNode<T>(Instance.GetNodeById(id.Id));
        }
        
        public static SettingNode GetSettingNode<T>(T setting) where T : ScriptableObject
        {
            return Instance.GetNodeByInstance(setting);
        }
    }
}