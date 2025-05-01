namespace Scriptable.Settings.Editor.Tools
{
    public interface ISettingEditorTool
    {
        void OnCreateSettingManagerWindow(SettingsManagerWindow root);

        void OnDestroySettingManagerWindow();
    }
}