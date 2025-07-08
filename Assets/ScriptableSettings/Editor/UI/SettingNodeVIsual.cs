using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Scriptable.Settings.Editor
{
    public class SettingNodeVisual : VisualElement
    {
        private VisualElement _header;
        private ScrollView _scrollView;

        public SettingNodeVisual()
        {
            _header = new VisualElement() {style  ={ flexGrow = 1} };
            _scrollView = new ScrollView();
            Add(_header);
            Add(_scrollView);
        }

        public void ShowNodeInInspector(SettingsManager manager, SettingNode node)
        {
            _header.Clear();
            _scrollView.Clear();
            
             if (node == null) { ClearInspector(); return; }

             var title = new VisualElement() { style = { flexDirection = FlexDirection.Row, marginBottom = 5 , flexGrow = 1} };
             
             var refField = new UnityEditor.Search.ObjectField($"Node:") { value = node.Asset, style = { unityFontStyleAndWeight = FontStyle.Bold, flexGrow = 1} };
             title.Add(refField);
             var scriptButton = new Button(() => 
             {
                 if (node.Asset != null)
                 {
                     var assetGuids = AssetDatabase.FindAssets($"{node.Asset.GetType().Name} t:MonoScript");
                     var assetPath = assetGuids.Select(AssetDatabase.GUIDToAssetPath)
                         .Where(x => x.Contains($"{node.Asset.GetType().Name}.cs"));
                     var assets = assetPath.Select(AssetDatabase.LoadAssetAtPath<Object>).ToArray();
                     AssetDatabase.OpenAsset(assets.FirstOrDefault());
                 }
             }) { text = "Script" };
             title.Add(scriptButton);
             
             _header.Add(title);
             var guidElement = new VisualElement() { style = { flexGrow = 1, flexDirection = FlexDirection.RowReverse } };
             _header.Add(guidElement);
             guidElement.Add(new Label($"GUID: {node.Guid}"));

            // _header.Add(new TextElement() { text = $"Node Type: {node.SettingType.FullName}"});
             // Add a separator
             _header.Add(new VisualElement() { style = { height = 1, backgroundColor = new StyleColor(new Color(0.35f,.35f,.35f)), marginTop = 5, marginBottom = 5 } });


            // Try to load and display the actual ScriptableObject
            if (manager != null)
            {
                 ScriptableObject settingSO = manager.LoadNode<ScriptableObject>(node); // Use synchronous load for editor inspector

                 refField.value = settingSO;
                 
                 if (settingSO != null)
                 {
                     // Create an InspectorElement bound to the loaded SO
                     // This automatically draws the default inspector for that SO
                     InspectorElement inspector = new InspectorElement(settingSO);
                     _scrollView.Add(inspector);
                 }
                 else
                 {
                     _header.Add(new HelpBox($"Could not load the ScriptableObject asset for '{node.Name}'.\nLoader: {manager.Loader.GetType().Name}\nPath might be: {manager.Loader.NodeLoadPath(node)}\nID: {node.Guid}", HelpBoxMessageType.Warning));
                 }
            }
             else if (manager != null)
             {
                 _header.Add(new HelpBox("SettingsManager Loader is not initialized. Cannot load asset.", HelpBoxMessageType.Warning));
             }
             else
             {
                 _header.Add(new HelpBox("SettingsManager reference missing.", HelpBoxMessageType.Error));
             }

              // Reset scroll position after populating
              if (_scrollView != null) _scrollView.scrollOffset = Vector2.zero;
        }
        
        public void ClearInspector()
        {
            _header.Clear();
            _scrollView.Clear();
            
            _header.Add(new Label("Select a setting node in the tree."));
            
            if (_scrollView != null) _scrollView.scrollOffset = Vector2.zero;
        }
    }
}