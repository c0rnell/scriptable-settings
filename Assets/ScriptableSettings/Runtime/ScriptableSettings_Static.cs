using System.Linq;
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
        
        // concrete type of Id to avoid struct boxing
        public static T GetSetting<TID, T>(in TID id) 
            where TID : struct, ISettingId<T>
        {
            return Instance.LoadNode<T>(GetSettingNodeFromId<TID,T>(id));
        }
        
        public static SettingNode GetSettingNode<T>(T setting) where T : ScriptableObject
        {
            return Instance.GetNodeByInstance(setting);
        }
        
        // concrete type of Id to avoid struct boxing
        public static SettingNode GetSettingNodeFromId<TID, T>(in TID id) 
            where TID : struct, ISettingId<T>
        {
            return Instance.GetNodeById(id.Id);
        }
        
        public static bool TryGetSetting<T>(out T setting) 
            where T : ScriptableObject
        {
            var nodes = Instance.GetNodesOfType<T>().ToArray();
            if(nodes.Length > 0)
            {
                return nodes[0].TryGetSetting<T>(out setting);
            }
            setting     = null;
            return false;
        }
    }
}