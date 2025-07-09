using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Scriptable.Settings;

namespace Scriptable.Settings.Tests
{
    /// <summary>
    /// Base class for all ScriptableSettings tests providing common test utilities and simplified setup using internal accessors.
    /// </summary>
    public abstract class ScriptableSettingsTestBase
    {
        /// <summary>
        /// Manual mock implementation of ISettingLoader for testing without file system dependencies.
        /// </summary>
        protected class MockSettingLoader : ISettingLoader
        {
            private readonly Dictionary<string, ScriptableObject> assets = new();
            
            public int LoadCallCount { get; private set; }
            public List<string> LoadedPaths { get; } = new();
            public bool ThrowOnLoad { get; set; }
            public bool ReturnNullOnLoad { get; set; }
            
            public void SetupAsset(string path, ScriptableObject asset)
            {
                assets[path] = asset;
            }
            
            public void RemoveAsset(string path)
            {
                assets.Remove(path);
            }
            
            public void ClearAssets()
            {
                assets.Clear();
            }
            
            public ScriptableObject Load(SettingNode node)
            {
                LoadCallCount++;
                var path = NodeLoadPath(node);
                LoadedPaths.Add(path);
                
                if (ThrowOnLoad)
                    throw new System.Exception("Mock loader exception");
                    
                if (ReturnNullOnLoad)
                    return null;
                
                return assets.TryGetValue(path, out var asset) ? asset : null;
            }
            
            public System.Threading.Tasks.Task<ScriptableObject> LoadAsync(SettingNode node)
            {
                var result = Load(node);
                return System.Threading.Tasks.Task.FromResult(result);
            }
            
            public string NodeLoadPath(SettingNode node)
            {
                // Mimic real path construction
                var parts = new List<string>();
                var current = node;
                while (current != null)
                {
                    parts.Insert(0, current.Name);
                    current = current.Parent;
                }
                return string.Join("/", parts);
            }
        }
        
        protected MockSettingLoader mockLoader;
        
        [SetUp]
        public virtual void Setup()
        {
            mockLoader = new MockSettingLoader();
        }
        
        [TearDown]
        public virtual void TearDown()
        {
            mockLoader?.ClearAssets();
            
            // Clean up any ScriptableObjects created during tests
            var allObjects = Resources.FindObjectsOfTypeAll<ScriptableObject>();
            foreach (var obj in allObjects)
            {
                if (obj != null && obj.hideFlags == HideFlags.HideAndDontSave && 
                    (obj is ScriptableSettings || obj is SettingLoaderFactory || 
                     obj is TestSettings || obj is TestAudioSettings || 
                     obj is TestGameSettings || obj is TestGraphicsSettings))
                {
                    UnityEngine.Object.DestroyImmediate(obj);
                }
            }
        }
        
        /// <summary>
        /// Creates a ScriptlableSettings for testing using simplified internal accessors.
        /// </summary>
        protected ScriptableSettings CreateTestManager()
        {
            var manager = ScriptableObject.CreateInstance<ScriptableSettings>();
            manager.hideFlags = HideFlags.HideAndDontSave;
            
            // Create and set the loader factory using the editor property
            var loaderFactory = ScriptableObject.CreateInstance<SettingLoaderFactory>();
            loaderFactory.hideFlags = HideFlags.HideAndDontSave;
            
#if UNITY_EDITOR
            // Use the editor property to set the loader factory
            manager.LoaderFactory = loaderFactory;
#else
            // Fallback for non-editor builds
            var loaderFactoryField = typeof(ScriptableSettings).GetField("loaderFactory", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            loaderFactoryField?.SetValue(manager, loaderFactory);
#endif
            
            // Set our mock loader directly using internal accessor
            manager.loader = mockLoader;
            
            return manager;
        }
        
        /// <summary>
        /// Creates a test ScriptableObject asset.
        /// </summary>
        protected T CreateTestAsset<T>(string name) where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            asset.name = name;
            asset.hideFlags = HideFlags.HideAndDontSave;
            return asset;
        }
        
        /// <summary>
        /// Creates a SettingNode with the specified parameters.
        /// </summary>
        protected SettingNode CreateTestNode(string name, Type type = null, Guid? id = null, ISettingLoader loader = null)
        {
            type ??= typeof(TestSettings);
            id ??= Guid.NewGuid();
            loader ??= mockLoader;
            
            var node = new SettingNode(name, type, id.Value, loader);
            return node;
        }
        
        /// <summary>
        /// Helper to add child to parent using internal accessor.
        /// </summary>
        protected void AddChildToNode(SettingNode parent, SettingNode child)
        {
            parent?.AddChild(child);
        }

        /// <summary>
        /// Helper to add a node directly to the manager using internal accessors.
        /// </summary>
        protected void AddRootNodeToManager(ScriptableSettings manager, SettingNode node)
        {
            // Initialize collections if needed
            if (manager.nodeIndex == null)
                manager.nodeIndex = new Dictionary<Guid, SettingNode>();
            
            if (manager.roots == null)
                manager.roots = new List<SettingNode>();
            
            // Recursively add node and all its children to the index
            AddNodeToIndexRecursively(node, manager.nodeIndex);
            
            // Only add to roots if it has no parent (is actually a root)
            if (node.Parent == null && !manager.roots.Contains(node))
            {
                manager.roots.Add(node);
            }
            
            // Mark index as built and changed
            manager.indexBuilt = true;
            manager.indexChanged = true;
        }
        
        private void AddNodeToIndexRecursively(SettingNode node, Dictionary<Guid, SettingNode> nodeIndex)
        {
            if (!nodeIndex.ContainsKey(node.Guid))
            {
                nodeIndex[node.Guid] = node;
            }
            
            foreach (var child in node.Children)
            {
                AddNodeToIndexRecursively(child, nodeIndex);
            }
        }
        
        /// <summary>
        /// Helper to remove child from parent using internal accessor.
        /// </summary>
        protected bool RemoveChildFromNode(SettingNode parent, SettingNode child)
        {
            return parent?.RemoveChild(child) ?? false;
        }
        
        /// <summary>
        /// Asserts that two node hierarchies are equivalent.
        /// </summary>
        protected void AssertNodeHierarchyEquals(SettingNode expected, SettingNode actual)
        {
            Assert.IsNotNull(actual);
            Assert.AreEqual(expected.Guid, actual.Guid);
            Assert.AreEqual(expected.Name, actual.Name);
            Assert.AreEqual(expected.SettingType, actual.SettingType);
            Assert.AreEqual(expected.Children.Count, actual.Children.Count);
            
            for (int i = 0; i < expected.Children.Count; i++)
            {
                AssertNodeHierarchyEquals(expected.Children[i], actual.Children[i]);
            }
        }
    }
    
    // Test ScriptableObject classes
    public class TestSettings : ScriptableObject { }
    public class TestAudioSettings : TestSettings 
    {
        public float volume = 1.0f;
        public bool muted = false;
    }
    public class TestGameSettings : TestSettings 
    {
        public int difficulty = 1;
        public string playerName = "Player";
    }
    public class TestGraphicsSettings : TestSettings 
    {
        public int resolution = 1080;
        public bool fullscreen = true;
    }
    public class TestInvalidSettings : MonoBehaviour { } // Not a ScriptableObject
}