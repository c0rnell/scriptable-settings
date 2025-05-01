using System;
using System.Collections.Generic;
using System.Linq;
using Scriptable.Settings.Editor;
using UnityEditor;
using UnityEditor.Search;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// A reusable field control that lets users select a Type via a namespace-based tree view, suitable for embedding in other UI Toolkit windows.
/// </summary>
[UxmlElement]
public abstract partial class SelectorPopupField<T> : BaseField<T>
{
    private readonly Func<IEnumerable<T>> _itemProvider;
    
    private readonly VisualElement _arrowElement;
    private VisualElement _visualElement;
    protected readonly TextElement _textElement;


    private SelectorPopupWindow<T> _popupWindow;

    public SelectorPopupField() : base(null, null)
    {
    }
    
    public SelectorPopupField(string label, Func<IEnumerable<T>> itemProvider) : base(label, new VisualElement())
    { 
        _itemProvider = itemProvider;
        _visualElement = this.Query(null, BaseField<T>.inputUssClassName).First();
        AddToClassList(BasePopupField<T, T>.ussClassName);
        labelElement.AddToClassList(BasePopupField<T, T>.labelUssClassName);
        labelElement.style.minWidth = new StyleLength(60);
        TextElement popupTextElement = new TextElement();
        popupTextElement.pickingMode = PickingMode.Ignore;
        _textElement = (TextElement) popupTextElement;
        _textElement.AddToClassList(BasePopupField<T, T>.textUssClassName);
        _visualElement.AddToClassList(BasePopupField<T, T>.inputUssClassName);
        _visualElement.Add(_textElement);
        _arrowElement = new VisualElement();
        _arrowElement.AddToClassList(BasePopupField<T, T>.arrowUssClassName);
        _arrowElement.pickingMode = PickingMode.Ignore;
        _visualElement.Add(this._arrowElement);
        
        Add(_visualElement);
        RegisterCallback<PointerDownEvent>(new EventCallback<PointerDownEvent>(this.OnPointerDownEvent));
        RegisterCallback<PointerUpEvent>(new EventCallback<PointerUpEvent>(this.OnPointerUpEvent));
        RegisterCallback<PointerMoveEvent>(new EventCallback<PointerMoveEvent>(this.OnPointerMoveEvent));
        RegisterCallback<MouseDownEvent>((EventCallback<MouseDownEvent>) (e =>
        {
            if (e.button != 0)
                return;
            e.StopPropagation();
        }));
        this.RegisterCallback<NavigationSubmitEvent>(new EventCallback<NavigationSubmitEvent>(this.OnNavigationSubmit));
    }

    private void OnPointerDownEvent(PointerDownEvent evt)
    {
        this.ProcessPointerDown<PointerDownEvent>((PointerEventBase<PointerDownEvent>) evt);
    }

    private void OnPointerUpEvent(PointerUpEvent evt)
    {
        if (evt.button != 0 || !this.ContainsPointer((VisualElement)evt.target))
            return;
        evt.StopPropagation();
    }

    private void OnPointerMoveEvent(PointerMoveEvent evt)
    {
        if (evt.button != 0 || (evt.pressedButtons & 1) == 0)
            return;
        this.ProcessPointerDown<PointerMoveEvent>((PointerEventBase<PointerMoveEvent>) evt);
    }

    private bool ContainsPointer(VisualElement target)
    {
        VisualElement elementUnderPointer = target;
        return this == elementUnderPointer || _visualElement == target;
    }

    private void ProcessPointerDown<TEvent>(PointerEventBase<TEvent> evt) where TEvent : PointerEventBase<TEvent>, new()
    {
        if (evt.button != 0 || !this.ContainsPointer((VisualElement)evt.target))
            return;
        this.schedule.Execute(new Action(this.ShowMenu));
        evt.StopPropagation();
    }

    private void OnNavigationSubmit(NavigationSubmitEvent evt)
    {
        this.ShowMenu();
        evt.StopPropagation();
    }

    protected virtual void ShowMenu()
    {
        OpenPopup();
        /*IGenericMenu menu = this.createMenuCallback != null ? this.createMenuCallback() : this.elementPanel.CreateMenu();
        this.AddMenuItems(menu);
        menu.DropDown(this.visualInput.worldBound, (VisualElement) this, true);*/
    }

    private void OpenPopup()
    {
        var localRect = _arrowElement.worldBound;
        var screenPos = GUIUtility.GUIToScreenPoint(new Vector2(localRect.x, localRect.y));
        var rect = new Rect(screenPos.x, screenPos.y, localRect.width, localRect.height);

        _popupWindow = ShowSelectionWindow(ChooseSelection, _itemProvider, _arrowElement);
    }

    protected abstract SelectorPopupWindow<T> ShowSelectionWindow(Func<T, string, bool> onTypeChosen,
        Func<IEnumerable<T>> itemProvider, VisualElement positionParent);

    protected virtual bool ChooseSelection(T type, string name)
    {
        if (type == null)
            return false;

        // Set the selected type
        value = type;
        _textElement.text = name;
        return true;
    }
}
public abstract class SelectorPopupWindow<T> : EditorWindow
{
    public Func<T, string, bool> OnSelectionChosen;
    private ToolbarSearchField  _searchField;
    protected TreeView     _treeView;
    private List<TreeViewItemData<T>> _treeData;

    private T _selected;
    public static SelectorPopupWindow<T> ShowWindow<TWindow>(Func<T, string, bool> onTypeChosen, VisualElement positionParent, Action<TWindow> onSetup = null, T selected = default) where TWindow : SelectorPopupWindow<T>
    {
        var window = ScriptableObject.CreateInstance<TWindow>();
        window.OnSelectionChosen = onTypeChosen;
        onSetup?.Invoke(window);
        window._selected = selected;
        var pos = GUIUtility.GUIToScreenRect(positionParent.worldBound);
        pos.x -= 200;
        window.ShowAsDropDown(pos, new Vector2(400, 500));
        window.BuildTree(string.Empty);
        return window;
    }
    

    public void CreateGUI()
    {
        // make the window wide enough for long namespaces
        minSize = new Vector2(300, 400);

        var root = rootVisualElement;
        root.style.paddingLeft  = 4;
        root.style.paddingRight = 4;
        root.style.paddingTop   = 4;
        root.style.paddingBottom= 4;
        root.AddToClassList("unity-inspector__container");  // gives you the nice grey background

        // 1) SearchField
        _searchField = new ToolbarSearchField { name = "type-search-field", style = { marginBottom = 4, width = Length.Auto()} };
        _searchField.RegisterValueChangedCallback(evt =>
        {
            BuildTree(evt.newValue);
        });
        root.Add(_searchField);

        // 2) TreeView
        _treeView = new TreeView
        {
            name       = "type-tree-view",
            showBorder = false,
            selectionType = SelectionType.Single,
            style =
            {
                flexGrow = 1,
                backgroundColor = new StyleColor(new Color(0.18f, 0.18f, 0.18f)) // match project window
            }
        };

        // Make an item: HBox with icon + label
        _treeView.makeItem = () =>
        {
            var row = new VisualElement { name = "row", style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, paddingLeft = 2, height = 20 } };
            var icon = new Image { name = "icon", style = { width = 16, height = 16, marginRight = 4 } };
            var lbl  = new Label { name = "label" };
            row.Add(icon);
            row.Add(lbl);
            return row;
        };

        // Bind the Type to that row
        _treeView.bindItem = (element, index) =>
        {
            var item = _treeView.GetItemDataForIndex<T>(index);
            var icon = element.Q<Image>("icon");
            var lbl  = element.Q<Label>("label");

            FillItemView(index, icon, lbl, item);
        };

        // When the user clicks an item
        _treeView.selectionChanged += objs =>
        {
            var selectedIndex = (T)objs.First();
            if (OnSelectionChosen == null)
            {
                Close();
                return;
            }

            if(OnSelectionChosen.Invoke(selectedIndex, FormatSelectionItem(selectedIndex)))
                Close();
            else
            {
                _treeView.SetSelectionWithoutNotify(Enumerable.Empty<int>());
            }
        };

        root.Add(_treeView);
        
    }

    protected virtual void FillItemView(int index, Image icon, Label lbl, T item)
    {
        icon.image = EditorIcons.ScriptableObject;
        lbl.text = FormatSelectionItem(item);
    }

    protected virtual void BuildTree(string filter)
    {
        // 1) pull all types
        var allItems = GetAllItems();
        var scoredList = new List<(T type, long score)>();

        if (string.IsNullOrEmpty(filter))
        {
            // no filter → score=0 for all
            scoredList.AddRange(allItems.Select(t => (t, 0L)));
        }
        else
        {
            // fuzzy‐match each type name and namespace
            foreach (var item in allItems)
            {
                DoSearch(filter, item, scoredList);
            }
        }

        // 2) sort by descending score, then namespace/name
        var flat = scoredList
            .OrderByDescending(x => x.score)
            .ThenBy(x => FormatSelectionItem(x.type))
            .Select(x => x.type)
            .ToList();

        _treeData = GroupItemsToTree(flat);
        _treeView.Clear();
        _treeView.SetRootItems(_treeData);
        _treeView.RefreshItems();
        _treeView.ExpandAndSelect(_treeData, _selected);
    }

    protected virtual List<TreeViewItemData<T>> GroupItemsToTree(List<T> flat)
    {
        return flat
            .Select(t => new TreeViewItemData<T>(t.GetHashCode(), t))
            .ToList();
    }

    protected virtual void DoSearch(string filter, T item, List<(T type, long score)> scoredList)
    {
        long score = 0;
        bool matchName = FuzzySearch.FuzzyMatch(filter, FormatSelectionItem(item), ref score);
        if (matchName)
        {
            // keep the better of the two scores
            scoredList.Add((item, score));
        }
    }

    protected virtual string FormatSelectionItem(T item)
    {
        return item.ToString();
    }

    protected abstract IEnumerable<T> GetAllItems();
}