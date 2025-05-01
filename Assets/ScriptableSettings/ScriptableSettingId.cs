using System;
using UnityEngine;

namespace Scriptable.Settings.Editor
{
    [Serializable]
    public class ScriptableSettingId<T> : ISerializationCallbackReceiver where T : ScriptableObject
    {
        [SerializeField] private string i;
        public Guid Id { get; private set; }

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