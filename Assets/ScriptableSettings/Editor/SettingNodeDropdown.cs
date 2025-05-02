using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Scriptable.Settings.Editor
{
    public class SettingNodeDropdown : SelectorPopupField<SettingNode>
    {
        private readonly Func<SettingNode,bool> _typeValidityCheck;

        public SettingNodeDropdown(string label, Func<IEnumerable<SettingNode>> itemProvider, Func<SettingNode, bool> typeValidityCheck = null) : base()
        {
            base.label = label;
            base._itemProvider = itemProvider;
            _typeValidityCheck = typeValidityCheck;
        }

        protected override SelectorPopupWindow<SettingNode> ShowSelectionWindow(Func<SettingNode, string, bool> onTypeChosen,
            Func<IEnumerable<SettingNode>> itemProvider,
            VisualElement positionParent)
        {
            return SettingNodePopupWindow.ShowWindow<SettingNodePopupWindow>(onTypeChosen, positionParent,
                (win) =>
                {
                    win.SetProvider(itemProvider);
                }, value);
        }

        private static IEnumerable<Type> GetAllClassTypes()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a =>
                {
                    try
                    {
                        return a.GetTypes();
                    }
                    catch
                    {
                        return Array.Empty<Type>();
                    }
                })
                .Where(t => t.IsClass)
                .OrderBy(t => t.Namespace ?? string.Empty)
                .ThenBy(t => t.Name);
        }

        public override void SetValueWithoutNotify(SettingNode newValue)
        {
            base.SetValueWithoutNotify(newValue);
            SetError(null);
           _textElement.text = newValue.Name;
        }
        
        public void SetError(string error)
        {
            if (string.IsNullOrEmpty(error))
            {
                _textElement.style.backgroundColor = Color.clear;
            }
            else
            {
                _textElement.style.backgroundColor = Color.red;
                _textElement.text = error;
            }
        }

        protected override bool ChooseSelection(SettingNode type, string name)
        {
            if(_typeValidityCheck == null)
                return base.ChooseSelection(type, name);
            
            if (_typeValidityCheck(type))
                return base.ChooseSelection(type, name);

            return false;
        }
    }
}