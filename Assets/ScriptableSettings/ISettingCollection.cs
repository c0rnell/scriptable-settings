using System;
using UnityEngine;

namespace Scriptable.Settings
{
    public interface ISettingCollection
    {
        public Type[] GetSettingTypes() => new []{ typeof(ScriptableObject) };
    }

    public interface ISettingCollection<T> : ISettingCollection where T : ScriptableObject
    {
        Type[] ISettingCollection.GetSettingTypes() => new[] { typeof(T) };
    }
    
    public interface ISettingCollection<T, U> : ISettingCollection 
        where T : ScriptableObject
        where U : ScriptableObject
    {
        Type[] ISettingCollection.GetSettingTypes() => new[] { typeof(T), typeof(U) };
    }
    
    public interface ISettingCollection<T, U, V> : ISettingCollection 
        where T : ScriptableObject
        where U : ScriptableObject
        where V : ScriptableObject
    {
        Type[] ISettingCollection.GetSettingTypes() => new[] { typeof(T), typeof(U), typeof(V) };
    }
    
    public interface ISettingCollection<T, U, V, W> : ISettingCollection 
        where T : ScriptableObject
        where U : ScriptableObject
        where V : ScriptableObject
        where W : ScriptableObject
    {
        Type[] ISettingCollection.GetSettingTypes() => new[] { typeof(T), typeof(U), typeof(V), typeof(W) };
    }
}