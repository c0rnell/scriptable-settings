#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Scriptable.Settings.Editor
{
    public class SettingNodeHistory : ISettingNodeHistory
    {
        private const string EditorPrefsKey = "ScriptableSettings.NodeHistory";
        private const int MaxHistorySize = 10;
        private int historySize;
        
        private List<SettingNodeHistoryEntry> history;
        private int currentIndex = -1;
        private readonly ScriptableSettings _scriptableSettings;
        
        public bool CanGoBack => currentIndex > 0;
        public bool CanGoForward => currentIndex < history.Count - 1;
        public int CurrentIndex => currentIndex;
        
        public SettingNode CurrentNode
        {
            get
            {
                if (currentIndex >= 0 && currentIndex < history.Count)
                {
                    var entry = history[currentIndex];
                    if (Guid.TryParse(entry.NodeGuid, out var guid))
                    {
                        return _scriptableSettings.GetNodeById(guid);
                    }
                }
                return null;
            }
        }
        
        public SettingNodeHistory(ScriptableSettings manager, int size = 10)
        {
            _scriptableSettings = manager;
            history = new List<SettingNodeHistoryEntry>();
            historySize = Mathf.Max(size, MaxHistorySize);
            LoadHistory();
        }
        
        public void AddNode(SettingNode node)
        {
            if (node == null) return;
            
            var nodeGuid = node.Guid.ToString();
            var existingIndex = history.FindIndex(h => h.NodeGuid == nodeGuid);
            
            if (existingIndex >= 0)
            {
                var entry = history[existingIndex];
                entry.LastAccessTime = DateTime.Now;
                history.RemoveAt(existingIndex);
                
                if (existingIndex <= currentIndex)
                {
                    currentIndex--;
                }
            }
            else
            {
                if (currentIndex < history.Count - 1)
                {
                    history.RemoveRange(currentIndex + 1, history.Count - currentIndex - 1);
                }
            }
            
            history.Add(new SettingNodeHistoryEntry(node));
            
            if (history.Count > historySize)
            {
                history.RemoveAt(0);
            }
            
            currentIndex = history.Count - 1;
            SaveHistory();
        }
        
        public void NavigateBack()
        {
            if (CanGoBack)
            {
                currentIndex--;
            }
        }
        
        public void NavigateForward()
        {
            if (CanGoForward)
            {
                currentIndex++;
            }
        }
        
        public void Clear()
        {
            history.Clear();
            currentIndex = -1;
            SaveHistory();
        }
        
        public IReadOnlyList<SettingNodeHistoryEntry> GetHistory()
        {
            return history.AsReadOnly();
        }
        
        public SettingNode FindNodeByGuid(string guidString)
        {
            if (string.IsNullOrEmpty(guidString) || _scriptableSettings == null)
                return null;
                
            if (Guid.TryParse(guidString, out var guid))
            {
                return _scriptableSettings.GetNodeById(guid);
            }
            
            return null;
        }
        
        private void LoadHistory()
        {
            var json = EditorPrefs.GetString(EditorPrefsKey, string.Empty);
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    var data = JsonUtility.FromJson<HistoryData>(json);
                    if (data != null && data.entries != null)
                    {
                        history = data.entries.ToList();
                        currentIndex = Math.Min(data.currentIndex, history.Count - 1);
                        
                        history.RemoveAll(h => 
                        {
                            if (Guid.TryParse(h.NodeGuid, out var guid))
                            {
                                return _scriptableSettings.GetNodeById(guid) == null;
                            }
                            return true;
                        });
                        
                        if (currentIndex >= history.Count)
                        {
                            currentIndex = history.Count - 1;
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Failed to load setting node history: {e.Message}");
                    history.Clear();
                    currentIndex = -1;
                }
            }
        }
        
        private void SaveHistory()
        {
            try
            {
                var data = new HistoryData
                {
                    entries = history.ToArray(),
                    currentIndex = currentIndex
                };
                var json = JsonUtility.ToJson(data);
                EditorPrefs.SetString(EditorPrefsKey, json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to save setting node history: {e.Message}");
            }
        }
        
        [Serializable]
        private class HistoryData
        {
            public SettingNodeHistoryEntry[] entries;
            public int currentIndex;
        }
    }
}
#endif