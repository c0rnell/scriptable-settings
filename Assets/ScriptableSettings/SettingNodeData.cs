using System;
using System.Collections.Generic;

namespace ScriptableSettings
{
    [Serializable]
    public class SettingNodeData {
        public string id;
        public string name;
        public string typeName;
        public string parentId;         // empty or null for root
        public List<string> childIds;   // just GUID strings
    }
}