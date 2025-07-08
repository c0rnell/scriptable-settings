#if UNITY_EDITOR
using System.Collections.Generic;

namespace Scriptable.Settings.Editor
{
    public interface ISettingNodeHistory
    {
        void AddNode(SettingNode node);
        bool CanGoBack { get; }
        bool CanGoForward { get; }
        void NavigateBack();
        void NavigateForward();
        void Clear();
        IReadOnlyList<SettingNodeHistoryEntry> GetHistory();
        int CurrentIndex { get; }
        SettingNode CurrentNode { get; }
        SettingNode FindNodeByGuid(string guidString);
    }
}
#endif