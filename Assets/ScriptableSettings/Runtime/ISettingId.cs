using System;
using UnityEngine;

namespace Scriptable.Settings
{
    public interface ISettingId<out T> 
    {
        Guid Id { get; }
        
        T GetSetting();
    }
}