using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System;
using System.Linq;
using System.Collections.Generic;

/// <summary>
/// A reusable field control that lets users select a Type via a namespace-based tree view, suitable for embedding in other UI Toolkit windows.
/// </summary>
public class TypeSelectorField : BaseField<Type>
{
    public new event Action<Type> OnTypeSelected;

    private readonly Label _labelElement;
    private readonly Button _arrowButton;
    private readonly Func<IEnumerable<Type>> _typeProvider;

    public new class UxmlFactory : UxmlFactory<TypeSelectorField, UxmlTraits> { }
    public new class UxmlTraits : BaseField<Type>.UxmlTraits { }

    /// <summary>
    /// Default constructor using all loaded class types.
    /// </summary>
    public TypeSelectorField() : this(GetAllClassTypes) { }

    /// <summary>
    /// Constructor that accepts a custom type provider.
    /// </summary>
    /// <param name="typeProvider">Function returning the list of Types to display.</param>
    public TypeSelectorField(Func<IEnumerable<Type>> typeProvider) : base(null, null)
    {
        _typeProvider = typeProvider ?? throw new ArgumentNullException(nameof(typeProvider));

        // Label to display current selection
        _labelElement = new Label("Select Type");
        _labelElement.AddToClassList("type-selector-label");

        // Arrow button to open the popup
        _arrowButton = new Button(OpenPopup) { text = "▼" };
        _arrowButton.AddToClassList("type-selector-arrow");

        // Layout container
        var container = new VisualElement();
        container.style.flexDirection = FlexDirection.Row;
        container.style.alignItems = Align.Center;
        container.Add(_labelElement);
        container.Add(_arrowButton);

        // Add to the input part of the field
        this.Add(container);
    }

    private static IEnumerable<Type> GetAllClassTypes()
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
            .Where(t => t.IsClass)
            .OrderBy(t => t.Namespace ?? string.Empty)
            .ThenBy(t => t.Name);
    }

    public override Type value
    {
        get => base.value;
        set
        {
            if (base.value == value) return;
            base.value = value;
            _labelElement.text = value != null ? value.Name : "Select Type";
            OnTypeSelected?.Invoke(value);
            using (var evt = ChangeEvent<Type>.GetPooled(value, value))
            {
                evt.target = this;
                SendEvent(evt);
            }
        }
    }

    private void OpenPopup()
    {
        var localRect = _arrowButton.worldBound;
        var screenPos = GUIUtility.GUIToScreenPoint(new Vector2(localRect.x, localRect.y));
        var rect = new Rect(screenPos.x, screenPos.y, localRect.width, localRect.height);

        var popup = ScriptableObject.CreateInstance<TypeSelectorPopupWindow>();
        popup.OnTypeChosen = ChooseType;
        popup.TypeProvider = _typeProvider;
        popup.ShowAsDropDown(rect, new Vector2(300, 400));
    }

    private void ChooseType(Type type)
    {
        value = type;
    }
}

/// <summary>
/// Internal popup window showing a searchable tree view of namespaces and types.
/// </summary>
internal class TypeSelectorPopupWindow : EditorWindow
{
    public Action<Type> OnTypeChosen;
    public Func<IEnumerable<Type>> TypeProvider;

    private string _searchText = string.Empty;
    private TextField _searchField;
    private ScrollView _scrollView;

    private void OnEnable()
    {
        var root = rootVisualElement;
        root.style.paddingLeft = 4;
        root.style.paddingRight = 4;
        root.style.paddingTop = 4;
        root.style.paddingBottom = 4;

        // Search field
        _searchField = new TextField { name = "type-search-field" };
        _searchField.RegisterValueChangedCallback(evt =>
        {
            _searchText = evt.newValue;
            RebuildTree();
        });
        root.Add(_searchField);

        // Scrollable tree view
        _scrollView = new ScrollView();
        root.Add(_scrollView);

        RebuildTree();
    }

    private void RebuildTree()
    {
        _scrollView.Clear();

        // Get filtered list
        var types = (TypeProvider?.Invoke() ?? Enumerable.Empty<Type>())
            .Where(t => string.IsNullOrEmpty(_searchText)
                       || t.Name.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0
                       || (t.Namespace?.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0))
            .OrderBy(t => t.Namespace ?? string.Empty)
            .ThenBy(t => t.Name).ToList();

        // Build namespace tree
        var rootNode = new NamespaceNode("<global>");
        foreach (var type in types)
        {
            var ns = type.Namespace ?? string.Empty;
            var segments = ns.Split(new[] {'.'}, StringSplitOptions.RemoveEmptyEntries);
            var current = rootNode;

            if (types.Count > 15)
            {
                foreach (var seg in segments)
                {
                    if (!current.Children.TryGetValue(seg, out var child))
                    {
                        child = new NamespaceNode(seg);
                        current.Children[seg] = child;
                    }
                    current = child;
                }
            }
            
            
            current.Types.Add(type);
        }

        // Render tree
        foreach (var child in rootNode.Children.Values)
            _scrollView.Add(CreateFoldout(child));
        
        foreach (var type in rootNode.Types)
        {
            var btn = new Button(() => SelectType(type)) { text = type.Name, tooltip = type.FullName};
            _scrollView.Add(btn);
        }
    }

    private Foldout CreateFoldout(NamespaceNode node)
    {
        var foldout = new Foldout { text = node.Name, value = false };
        foreach (var sub in node.Children.Values)
            foldout.Add(CreateFoldout(sub));
        foreach (var type in node.Types)
        {
            var btn = new Button(() => SelectType(type)) { text = type.Name, tooltip = type.FullName };
            foldout.Add(btn);
        }
        return foldout;
    }

    private void SelectType(Type type)
    {
        OnTypeChosen?.Invoke(type);
        Close();
    }

    [Serializable]
    private class NamespaceNode
    {
        public string Name;
        public Dictionary<string, NamespaceNode> Children = new Dictionary<string, NamespaceNode>();
        public List<Type> Types = new List<Type>();
        public NamespaceNode(string name) => Name = name;
    }
}
