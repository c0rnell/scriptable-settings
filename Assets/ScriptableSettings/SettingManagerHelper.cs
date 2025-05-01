using UnityEngine;

namespace ScriptableSettings
{
    public static class SettingManagerHelper
    {
        private static SettingsManager _instance;
        
        public static SettingsManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<SettingsManager>("SettingsManager");
                }

                return _instance;
            }
        }
    }
}