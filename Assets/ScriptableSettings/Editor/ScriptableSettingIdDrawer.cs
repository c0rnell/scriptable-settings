using System;
using System.Collections.Generic;
using ScriptableSettings;
using UnityEditor;
using UnityEngine.UIElements;

namespace Scriptable.Settings.Editor
{
    [CustomPropertyDrawer(typeof(ScriptableSettingId<>), true)]
    public class ScriptableSettingIdDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var idProperty = property.FindPropertyRelative("i");

            Type targetType = GetGenericArgumentType(fieldInfo.FieldType);

            var dropDown = new SettingNodeDropdown(property.name, GetAllSettingNodesOfType, 
                (selected) => targetType.IsAssignableFrom(selected.SettingType));
            dropDown.RegisterCallback<ChangeEvent<SettingNode>>(OnSettingSelected);
            
            
            IEnumerable<SettingNode> GetAllSettingNodesOfType()
            {
                return SettingManagerHelper.Instance.GetNodesOfType(targetType);
            }
            
            void OnSettingSelected(ChangeEvent<SettingNode> evt)
            {
                var guid = evt.newValue.Guid;
                idProperty.stringValue = ShortGuid.Encode(guid);
                idProperty.serializedObject.ApplyModifiedProperties();
            }

            if (string.IsNullOrEmpty(idProperty.stringValue) == false)
            {
                var guid  = ShortGuid.Decode(idProperty.stringValue);
                var settings = SettingManagerHelper.Instance.GetNodeById(guid);
                if (settings == null)
                {
                    dropDown.SetError($"Missing setting {guid.ToString()}");
                }
                else
                {
                    dropDown.SetValueWithoutNotify(settings);
                }
                
            }
                
            return dropDown;
        }
        

        public static Type GetGenericArgumentType(Type derivedType)
        {
            Type baseType = derivedType.BaseType;

            while (baseType != null)
            {
                if (baseType.IsGenericType && baseType.GetGenericTypeDefinition() == typeof(ScriptableSettingId<>))
                {
                    return baseType.GetGenericArguments()[0];
                }
                baseType = baseType.BaseType;
            }

            throw new InvalidOperationException("Generic base type not found.");
        }
    }
}