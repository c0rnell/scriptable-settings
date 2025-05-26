using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

            Type targetType = GetGenericArgumentType(property.boxedValue?.GetType());
            

            var dropDown = new SettingNodeDropdown(preferredLabel, GetAllSettingNodesOfType, 
                (selected) => targetType.IsAssignableFrom(selected.SettingType));
            dropDown.RegisterCallback<ChangeEvent<SettingNode>>(OnSettingSelected);
            
            
            IEnumerable<SettingNode> GetAllSettingNodesOfType()
            {
                var customAttributes = GetAttribute<SettingSourceAttribute>();
                if (customAttributes.Any())
                {
                    var nodes = new List<SettingNode>();

                    foreach (var customAttribute in customAttributes)
                    {
                        for (var index = 0; index < customAttribute.SettingCollectionType.Length; index++)
                        {
                            var type = customAttribute.SettingCollectionType[index];
                            var collections = SettingManagerHelper.Instance.GetNodesOfType(type).ToList();
                            if(collections.Count() == 1 && customAttribute.SettingCollectionType.Length == 1)
                                nodes.AddRange(collections.First().Children);
                            else
                            {
                                nodes.AddRange(collections);
                            }
                        }
                    }
                    

                    return nodes;
                }
                
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
        
        private List<T> GetAttribute<T>() where T : Attribute
        {
            var attributes = new List<T>();
            // 1. Check the field itself
            //    'fieldInfo' is a property of the base PropertyDrawer class
            T fieldAttribute = fieldInfo?.GetCustomAttribute<T>();
            if (fieldAttribute != null)
            {
                attributes.Add(fieldAttribute);
            }

            // 2. Check the field's type and its base types
            //    'fieldInfo.FieldType' gives you the System.Type of the field being drawn
            Type fieldType = fieldInfo?.FieldType;
            if (fieldType != null)
            {
                // The 'true' argument searches the inheritance chain
                T typeAttribute = fieldType.GetCustomAttribute<T>(true);
                if (typeAttribute != null)
                {
                    attributes.Add(typeAttribute);
                }
            }

            // Attribute not found
            return attributes;
        }
    }
    
    
}