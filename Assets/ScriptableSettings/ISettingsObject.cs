namespace Scriptable.Settings
{
    public interface ISettingsObject
    {
        void OnCreated();
        void OnLoaded(SettingNode node);
        void OnUnloaded(SettingNode node);
    }
}