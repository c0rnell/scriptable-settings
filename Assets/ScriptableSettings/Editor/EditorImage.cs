

using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Scriptable.Settings.Editor
{
    [UxmlElement]
    public partial class EditorImage : Image
    {

        [SerializeField]
        private string _iconName;
        [UxmlAttribute]
        internal string iconName
        {
            get => _iconName;
            set
            {
                _iconName = value;
                if(_iconName != null)
                    image = EditorGUIUtility.IconContent(_iconName).image;
            }
        }
    }
}