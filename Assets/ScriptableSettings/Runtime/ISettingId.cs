using System;
using UnityEngine;

namespace Scriptable.Settings
{
    public interface ISettingId<out T> where T : ScriptableObject 
    {
        Guid Id { get; }
        
        T GetSetting();
    }
}