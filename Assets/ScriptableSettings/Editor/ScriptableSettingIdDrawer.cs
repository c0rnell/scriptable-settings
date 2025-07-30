using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Collections;
using UnityEditor;
using UnityEngine.UIElements;

namespace Scriptable.Settings.Editor
{
    [CustomPropertyDrawer(typeof(ISettingId<>), true)]
    public class ScriptableSettingIdDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var idProperty = property.FindPropertyRelative("i");

            Type targetType = GetGenericArgumentType(GetTypeViaReflection(property));
            

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
                            var collections = ScriptableSettings.Instance.GetNodesOfType(type).ToList();
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
                
                return ScriptableSettings.Instance.GetNodesOfType(targetType);
            }
            
            void OnSettingSelected(ChangeEvent<SettingNode> evt)
            {
                var guid = evt.newValue.Guid;
                idProperty.boxedValue = ShortGuid.Encode32(guid);
                idProperty.serializedObject.ApplyModifiedProperties();
            }

            if (string.IsNullOrEmpty(idProperty.boxedValue.ToString()) == false)
            {
                var guid  = ShortGuid.Decode((FixedString32Bytes)idProperty.boxedValue);
                var settings = ScriptableSettings.Instance.GetNodeById(guid);
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
            var ifaces = derivedType.GetInterfaces();
            Type baseType = 
                ifaces
                .FirstOrDefault(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(ISettingId<>));

            if (baseType != null)
                return baseType.GetGenericArguments()[0];
            
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
        
        public static Type GetTypeViaReflection(SerializedProperty property)
        {
            try
            {
                Type parentType = property.serializedObject.targetObject.GetType();
                string[] path = property.propertyPath.Replace(".Array.data[", "[").Split('.');
        
                FieldInfo field = null;
                Type currentType = parentType;
        
                foreach (string segment in path)
                {
                    if (segment.Contains("["))
                    {
                        // Handle array/list elements
                        string fieldName = segment.Substring(0, segment.IndexOf('['));
                        field = currentType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                
                        if (field != null)
                        {
                            Type fieldType = field.FieldType;
                            if (fieldType.IsArray)
                                currentType = fieldType.GetElementType();
                            else if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(List<>))
                                currentType = fieldType.GetGenericArguments()[0];
                            else
                                currentType = fieldType;
                        }
                    }
                    else
                    {
                        field = currentType.GetField(segment, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (field != null)
                            currentType = field.FieldType;
                    }
                }
        
                return currentType;
            }
            catch
            {
                return null; // Fail gracefully
            }
        }
    }
    
    
}