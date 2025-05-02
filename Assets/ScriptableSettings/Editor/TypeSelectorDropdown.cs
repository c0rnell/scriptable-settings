using UnityEditor;
using UnityEngine.UIElements;
using System;
using System.Linq;
using System.Collections.Generic;
using Scriptable.Settings.Editor;
using UnityEditor.Search;
using UnityEngine;

/// <summary>
/// Internal popup window showing a searchable tree view of namespaces and types.
/// </summary>


[UxmlElement]
public partial class TypeSelectorDropdown : SelectorPopupField<Type>
{
    
    [UxmlAttribute]
    [UxmlTypeReference(typeof(Type))]
    public Type type { get; set; }
    
    public TypeSelectorDropdown()
    {
        // Default to showing all class types
        _itemProvider = GetAllClassTypes;
    }
    
    public void SetItemProvider(Func<IEnumerable<Type>> itemProvider)
    {
        _itemProvider = itemProvider;
    }
    protected override SelectorPopupWindow<Type> ShowSelectionWindow(Func<Type, string, bool> onTypeChosen,
        Func<IEnumerable<Type>> itemProvider, VisualElement positionParent)
    {
        return TypeSelectorPopupWindow.ShowWindow<TypeSelectorPopupWindow>(onTypeChosen, positionParent, 
            (win) =>
            {
                win.SetProvider(itemProvider);
            }, value);
    }
    
    private IEnumerable<Type> GetAllClassTypes()
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
            .Where(t => t.IsClass && (type == null|| type.IsAssignableFrom(t)))
            .OrderBy(t => t.Namespace ?? string.Empty)
            .ThenBy(t => t.Name);
    }

    public override void SetValueWithoutNotify(Type newValue)
    {
        base.SetValueWithoutNotify(newValue);
        _textElement.text = newValue.Name;
    }
}

public class TypeSelectorPopupWindow : SelectorPopupWindow<Type>
{
    Func<IEnumerable<Type>> _itemProvider;
    
    public void SetProvider(Func<IEnumerable<Type>> itemProvider)
    {
        _itemProvider = itemProvider;
    }

    protected override IEnumerable<Type> GetAllItems()
    {
        return _itemProvider();
    }

    protected override List<TreeViewItemData<Type>> GroupItemsToTree(List<Type> flat)
    {
        if (flat.Count < 10)
            return base.GroupItemsToTree(flat);
        
        // 3) build your TreeViewItem list exactly as before,
        //    grouping by namespace, grabbing each type’s .GetHashCode() as its unique id, etc.
        var rootItems = flat
            .GroupBy(t => t.Namespace ?? "<global>")
            .Select(g =>
                new TreeViewItemData<Type>(
                    g.Key.GetHashCode(),
                    g.First(),
                    g.Select(t => new TreeViewItemData<Type>(t.GetHashCode(), t)).ToList()
                )
            )
            .ToList();

        return rootItems;
    }

    protected override void DoSearch(string filter, Type item, List<(Type type, long score)> scoredList)
    {
        long score = 0;
        long nsScore = 0;
        
        bool matchName = FuzzySearch.FuzzyMatch(filter, item.Name, ref score);
        bool matchNs = FuzzySearch.FuzzyMatch(filter, item.Namespace ?? "<global>", ref nsScore);
        if (matchName || matchNs)
        {
            // keep the better of the two scores
            scoredList.Add((item, Math.Max(score, nsScore)));
        }
    }

    protected override void FillItemView(int index, Image icon, Label lbl, Type item)
    {
        var category = _treeView.GetChildrenIdsForIndex(index).Any();
            
        // give it the C# script icon
        icon.image = category ? EditorIcons.Folder : EditorIcons.ScriptableObject;

        lbl.text   = category
            ? item.Namespace
            : $"{item.Name}";
    }

    protected override string FormatSelectionItem(Type item)
    {
        return item.Name;
    }
}


