namespace ScriptableSettings
{
    public interface ISettingsObject
    {
        void OnCreated();
        void OnLoaded(SettingNode node);
        void OnUnloaded(SettingNode node);
    }
}