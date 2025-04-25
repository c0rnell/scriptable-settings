using ScriptableSettings;
using UnityEngine;

// Example Setting Type
using UnityEngine;
public class MyCustomSetting : ScriptableObject, ISettingsObject // Inherit directly from ScriptableObject
{
    public string someValue;
    public int anotherValue;

    public void OnCreated()
    {
        // Initialize your settings here
        someValue = "Default Value";
        anotherValue = 42;
    }

    public void OnLoaded(SettingNode node)
    {
        Debug.Log($"Me Loaded {node.Name} - {node.Guid}");
    }

    public void OnUnloaded(SettingNode node)
    {
        Debug.Log($"Me UnLoaded {node.Name} - {node.Guid}");
    }
}
