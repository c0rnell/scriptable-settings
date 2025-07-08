#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scriptable.Settings.Editor
{
    [Serializable]
    public class SettingNodeHistoryEntry
    {
        [SerializeField] public string NodeGuid;
        [SerializeField] public long LastAccessTimeTicks;
        
        public DateTime LastAccessTime
        {
            get => new DateTime(LastAccessTimeTicks);
            set => LastAccessTimeTicks = value.Ticks;
        }

        public SettingNodeHistoryEntry()
        {
        }

        public SettingNodeHistoryEntry(SettingNode node)
        {
            NodeGuid = node.Guid.ToString();
            LastAccessTime = DateTime.Now;
        }
    }
}
#endif