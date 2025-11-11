namespace Scriptable.Settings
{
    public interface ISettingsName
    {
        public string DisplayName { get; }
    }
    
    public partial struct SettingNameId : ISettingId<ISettingsName>
    {
        
    }
}