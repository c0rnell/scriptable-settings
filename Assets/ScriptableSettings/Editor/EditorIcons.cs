using UnityEditor;
using UnityEngine;

namespace Scriptable.Settings.Editor
{
    public static class EditorIcons
    {
        public static Texture Folder => EditorGUIUtility.IconContent("d_Folder Icon").image;
        public static Texture ScriptableObject => EditorGUIUtility.IconContent("ScriptableObject Icon").image;
        public static Texture Script => EditorGUIUtility.IconContent("d_cs Script Icon").image;
    }
}