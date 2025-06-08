using System;
using System.Collections.Generic;
using UnityEngine.Serialization;

namespace Scriptable.Settings
{
    [Serializable]
    public class SettingNodeData {
        [FormerlySerializedAs("id")] public string i;
        [FormerlySerializedAs("name")] public string n;
        [FormerlySerializedAs("typeName")] public string t;
        [FormerlySerializedAs("childIds")] public List<string> ch;   // just GUID strings
    }
}