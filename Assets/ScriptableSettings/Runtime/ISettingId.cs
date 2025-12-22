using System;
using UnityEngine;

namespace Scriptable.Settings
{
    public interface ISettingId
    {
        Guid Id { get; }
    }
    public interface ISettingId<out T> : ISettingId
    {
        T GetSetting();
    }
}