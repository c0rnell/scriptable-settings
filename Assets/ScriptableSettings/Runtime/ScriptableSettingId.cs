using System;
using UnityEngine;

namespace Scriptable.Settings
{
    [Serializable]
    public class ScriptableSettingId<T> : ISerializationCallbackReceiver where T : ScriptableObject
    {
        [SerializeField] private string i;
        public Guid Id { get; private set; }

        public ScriptableSettingId()
        {
            Id = Guid.Empty;
            i = ShortGuid.Encode(Id);
        }
        
        public ScriptableSettingId(Guid guid)
        {
            SetId(guid);
        }
        public void OnBeforeSerialize()
        {
        }

        public void OnAfterDeserialize()
        {
            Id = ShortGuid.Decode(i);
        }
        
        public void SetId(Guid guid)
        {
            Id = guid;
            i = ShortGuid.Encode(guid);
        }
    }
}