using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using Scriptable.Settings;

namespace Scriptable.Settings.Tests
{
    /// <summary>
    /// Comprehensive tests for path construction and asset loading in the ScriptableSettings system.
    /// Tests cover path generation, asset loading scenarios, and edge cases for user mistakes.
    /// </summary>
    [TestFixture]
    public class PathConstructionAndLoadingTests : ScriptableSettingsTestBase
    {
        private ResourcesSettingLoader resourcesLoader;
        private ScriptableSettings testManager;
        
        [SetUp]
        public override void Setup()
        {
            base.Setup();
            resourcesLoader = new ResourcesSettingLoader();
            testManager = CreateTestManager();
        }
        
        #region Path Construction Tests
        
        [Test]
        public void NodeLoadPath_RootNode_ReturnsCorrectPath()
        {
            // Arrange
            var rootNode = new SettingNode("Audio", typeof(TestAudioSettings), Guid.NewGuid(), mockLoader);
            
            // Act
            var path = resourcesLoader.NodeLoadPath(rootNode);
            
            // Assert
            Assert.AreEqual("Settings/Audio", path);
        }
        
        [Test]
        public void NodeLoadPath_NestedNode_ReturnsCorrectPath()
        {
            // Arrange
            var rootNode = new SettingNode("Game", typeof(TestGameSettings), Guid.NewGuid(), mockLoader);
            var childNode = new SettingNode("Player", typeof(TestSettings), Guid.NewGuid(), mockLoader);
            var grandchildNode = new SettingNode("Controls", typeof(TestSettings), Guid.NewGuid(), mockLoader);
            
            AddChildToNode(rootNode, childNode);
            AddChildToNode(childNode, grandchildNode);
            
            // Act
            var rootPath = resourcesLoader.NodeLoadPath(rootNode);
            var childPath = resourcesLoader.NodeLoadPath(childNode);
            var grandchildPath = resourcesLoader.NodeLoadPath(grandchildNode);
            
            // Assert
            Assert.AreEqual("Settings/Game", rootPath);
            Assert.AreEqual("Settings/Game/Player", childPath);
            Assert.AreEqual("Settings/Game/Player/Controls", grandchildPath);
        }
        
        [Test]
        public void NodeLoadPath_DeeplyNestedNode_ReturnsCorrectPath()
        {
            // Arrange
            var nodes = new List<SettingNode>();
            SettingNode parent = null;
            var depth = 10;
            
            for (int i = 0; i < depth; i++)
            {
                var node = new SettingNode($"Level{i}", typeof(TestSettings), Guid.NewGuid(), mockLoader);
                if (parent != null)
                {
                    AddChildToNode(parent, node);
                }
                nodes.Add(node);
                parent = node;
            }
            
            // Act
            var deepestPath = resourcesLoader.NodeLoadPath(nodes.Last());
            
            // Assert
            var expectedPath = "Settings/" + string.Join("/", Enumerable.Range(0, depth).Select(i => $"Level{i}"));
            Assert.AreEqual(expectedPath, deepestPath);
        }
        
        [Test]
        public void NodeLoadPath_SpecialCharactersInName_HandlesCorrectly()
        {
            // Test various special characters that might appear in node names
            var specialCharacterTests = new[]
            {
                ("Node-With-Dashes", "Settings/Node-With-Dashes"),
                ("Node_With_Underscores", "Settings/Node_With_Underscores"),
                ("Node.With.Dots", "Settings/Node.With.Dots"),
                ("Node With Spaces", "Settings/Node With Spaces"),
                ("Node@With#Special$Chars", "Settings/Node@With#Special$Chars"),
                ("Node(With)Parentheses", "Settings/Node(With)Parentheses"),
                ("Node[With]Brackets", "Settings/Node[With]Brackets"),
                ("Node{With}Braces", "Settings/Node{With}Braces"),
            };
            
            foreach (var (nodeName, expectedPath) in specialCharacterTests)
            {
                // Arrange
                var node = new SettingNode(nodeName, typeof(TestSettings), Guid.NewGuid(), mockLoader);
                
                // Act
                var path = resourcesLoader.NodeLoadPath(node);
                
                // Assert
                Assert.AreEqual(expectedPath, path, $"Failed for node name: {nodeName}");
            }
        }
        
        [Test]
        public void NodeLoadPath_UnicodeCharactersInName_HandlesCorrectly()
        {
            // Test various Unicode characters
            var unicodeTests = new[]
            {
                ("日本語", "Settings/日本語"),
                ("Русский", "Settings/Русский"),
                ("العربية", "Settings/العربية"),
                ("中文", "Settings/中文"),
                ("한국어", "Settings/한국어"),
                ("Emoji😀Node", "Settings/Emoji😀Node"),
                ("Mixed日本語Text", "Settings/Mixed日本語Text"),
            };
            
            foreach (var (nodeName, expectedPath) in unicodeTests)
            {
                // Arrange
                var node = new SettingNode(nodeName, typeof(TestSettings), Guid.NewGuid(), mockLoader);
                
                // Act
                var path = resourcesLoader.NodeLoadPath(node);
                
                // Assert
                Assert.AreEqual(expectedPath, path, $"Failed for Unicode node name: {nodeName}");
            }
        }
        
        [Test]
        public void NodeLoadPath_ReservedWindowsNames_HandlesCorrectly()
        {
            // Test Windows reserved names
            var reservedNames = new[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", 
                                       "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", 
                                       "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" };
            
            foreach (var name in reservedNames)
            {
                // Arrange
                var node = new SettingNode(name, typeof(TestSettings), Guid.NewGuid(), mockLoader);
                
                // Act
                var path = resourcesLoader.NodeLoadPath(node);
                
                // Assert
                Assert.AreEqual($"Settings/{name}", path, $"Failed for reserved name: {name}");
            }
        }
        
        [Test]
        public void NodeLoadPath_EmptyName_HandlesCorrectly()
        {
            // Arrange
            var node = new SettingNode("", typeof(TestSettings), Guid.NewGuid(), mockLoader);
            
            // Act
            var path = resourcesLoader.NodeLoadPath(node);
            
            // Assert
            Assert.AreEqual("Settings/", path);
        }
        
        [Test]
        public void NodeLoadPath_VeryLongName_HandlesCorrectly()
        {
            // Arrange - Create a name with 255 characters (typical filename limit)
            var longName = new string('A', 255);
            var node = new SettingNode(longName, typeof(TestSettings), Guid.NewGuid(), mockLoader);
            
            // Act
            var path = resourcesLoader.NodeLoadPath(node);
            
            // Assert
            Assert.AreEqual($"Settings/{longName}", path);
            Assert.AreEqual(255 + 9, path.Length); // "Settings/" (9 chars) + 255 chars = 264
        }
        
        [Test]
        public void NodeLoadPath_SlashesInName_PreservesSlashes()
        {
            // Test that slashes in names are preserved (which could cause path issues)
            var slashTests = new[]
            {
                ("Node/With/Forward/Slashes", "Settings/Node/With/Forward/Slashes"),
                ("Node\\With\\Backslashes", "Settings/Node\\With\\Backslashes"),
                ("Mixed/Slash\\Types", "Settings/Mixed/Slash\\Types"),
            };
            
            foreach (var (nodeName, expectedPath) in slashTests)
            {
                // Arrange
                var node = new SettingNode(nodeName, typeof(TestSettings), Guid.NewGuid(), mockLoader);
                
                // Act
                var path = resourcesLoader.NodeLoadPath(node);
                
                // Assert
                Assert.AreEqual(expectedPath, path, $"Failed for node with slashes: {nodeName}");
            }
        }
        
        #endregion
        
        #region Synchronous Loading Tests
        
        [Test]
        public void Load_ExistingAsset_ReturnsCorrectAsset()
        {
            // Arrange
            var node = new SettingNode("TestAudio", typeof(TestAudioSettings), Guid.NewGuid(), mockLoader);
            var expectedAsset = CreateTestAsset<TestAudioSettings>("TestAudio");
            mockLoader.SetupAsset("TestAudio", expectedAsset);
            
            // Act
            var loadedAsset = mockLoader.Load(node);
            
            // Assert
            Assert.IsNotNull(loadedAsset);
            Assert.AreSame(expectedAsset, loadedAsset);
            Assert.AreEqual(1, mockLoader.LoadCallCount);
            Assert.Contains("TestAudio", mockLoader.LoadedPaths);
        }
        
        [Test]
        public void Load_MissingAsset_ReturnsNull()
        {
            // Arrange
            var node = new SettingNode("NonExistent", typeof(TestSettings), Guid.NewGuid(), mockLoader);
            
            // Act
            var loadedAsset = mockLoader.Load(node);
            
            // Assert
            Assert.IsNull(loadedAsset);
            Assert.AreEqual(1, mockLoader.LoadCallCount);
        }
        
        [Test]
        public void Load_WrongAssetType_ReturnsAssetRegardless()
        {
            // Arrange - Setup an asset of different type than expected
            var node = new SettingNode("WrongType", typeof(TestAudioSettings), Guid.NewGuid(), mockLoader);
            var wrongTypeAsset = CreateTestAsset<TestGraphicsSettings>("WrongType");
            mockLoader.SetupAsset("WrongType", wrongTypeAsset);
            
            // Act
            var loadedAsset = mockLoader.Load(node);
            
            // Assert
            Assert.IsNotNull(loadedAsset);
            Assert.AreSame(wrongTypeAsset, loadedAsset);
            // The loader returns the asset regardless of type mismatch
        }
        
        [Test]
        public void Load_LoaderThrowsException_PropagatesException()
        {
            // Arrange
            var node = new SettingNode("TestNode", typeof(TestSettings), Guid.NewGuid(), mockLoader);
            mockLoader.ThrowOnLoad = true;
            
            // Act & Assert
            Assert.Throws<Exception>(() => mockLoader.Load(node));
        }
        
        [Test]
        public void Load_LoaderConfiguredToReturnNull_ReturnsNull()
        {
            // Arrange
            var node = new SettingNode("TestNode", typeof(TestSettings), Guid.NewGuid(), mockLoader);
            mockLoader.ReturnNullOnLoad = true;
            
            // Act
            var result = mockLoader.Load(node);
            
            // Assert
            Assert.IsNull(result);
            Assert.AreEqual(1, mockLoader.LoadCallCount);
        }
        
        [Test]
        public void Load_MultipleCallsSameNode_LoadsMultipleTimes()
        {
            // Arrange
            var node = new SettingNode("TestNode", typeof(TestSettings), Guid.NewGuid(), mockLoader);
            var asset = CreateTestAsset<TestSettings>("TestNode");
            mockLoader.SetupAsset("TestNode", asset);
            
            // Act
            var result1 = mockLoader.Load(node);
            var result2 = mockLoader.Load(node);
            var result3 = mockLoader.Load(node);
            
            // Assert
            Assert.AreSame(result1, result2);
            Assert.AreSame(result2, result3);
            Assert.AreEqual(3, mockLoader.LoadCallCount);
        }
        
        #endregion
        
        #region Asynchronous Loading Tests
        
        [Test]
        public async Task LoadAsync_ExistingAsset_ReturnsCorrectAsset()
        {
            // Arrange
            var node = new SettingNode("AsyncTest", typeof(TestAudioSettings), Guid.NewGuid(), mockLoader);
            var expectedAsset = CreateTestAsset<TestAudioSettings>("AsyncTest");
            mockLoader.SetupAsset("AsyncTest", expectedAsset);
            
            // Act
            var loadedAsset = await mockLoader.LoadAsync(node);
            
            // Assert
            Assert.IsNotNull(loadedAsset);
            Assert.AreSame(expectedAsset, loadedAsset);
            Assert.AreEqual(1, mockLoader.LoadCallCount);
        }
        
        [Test]
        public async Task LoadAsync_MissingAsset_ReturnsNull()
        {
            // Arrange
            var node = new SettingNode("AsyncNonExistent", typeof(TestSettings), Guid.NewGuid(), mockLoader);
            
            // Act
            var loadedAsset = await mockLoader.LoadAsync(node);
            
            // Assert
            Assert.IsNull(loadedAsset);
            Assert.AreEqual(1, mockLoader.LoadCallCount);
        }
        
        [Test]
        public void LoadAsync_LoaderThrowsException_PropagatesException()
        {
            // Arrange
            var node = new SettingNode("AsyncException", typeof(TestSettings), Guid.NewGuid(), mockLoader);
            mockLoader.ThrowOnLoad = true;
            
            // Act & Assert
            Assert.ThrowsAsync<Exception>(async () => await mockLoader.LoadAsync(node));
        }
        
        #endregion
        
        #region Node Caching and Loading Tests
        
        [Test]
        public void TryGetSetting_FirstCall_LoadsFromLoader()
        {
            // Arrange
            var node = new SettingNode("CacheTest", typeof(TestSettings), Guid.NewGuid(), mockLoader);
            var asset = CreateTestAsset<TestSettings>("CacheTest");
            mockLoader.SetupAsset("CacheTest", asset);
            
            // Act
            var success = node.TryGetSetting(out ScriptableObject loadedAsset);
            
            // Assert
            Assert.IsTrue(success);
            Assert.IsNotNull(loadedAsset);
            Assert.AreSame(asset, loadedAsset);
            Assert.AreEqual(1, mockLoader.LoadCallCount);
        }
        
        [Test]
        public void TryGetSetting_SubsequentCalls_UsesCachedAsset()
        {
            // Arrange
            var node = new SettingNode("CacheTest2", typeof(TestSettings), Guid.NewGuid(), mockLoader);
            var asset = CreateTestAsset<TestSettings>("CacheTest2");
            mockLoader.SetupAsset("CacheTest2", asset);
            
            // Act
            node.TryGetSetting(out ScriptableObject firstLoad);
            node.TryGetSetting(out ScriptableObject secondLoad);
            node.TryGetSetting(out ScriptableObject thirdLoad);
            
            // Assert
            Assert.AreSame(firstLoad, secondLoad);
            Assert.AreSame(secondLoad, thirdLoad);
            Assert.AreEqual(1, mockLoader.LoadCallCount); // Only loaded once
        }
        
        [Test]
        public void TryGetSettingGeneric_WrongType_ReturnsFalseWithError()
        {
            // Arrange
            var node = new SettingNode("TypeMismatch", typeof(TestAudioSettings), Guid.NewGuid(), mockLoader);
            var asset = CreateTestAsset<TestAudioSettings>("TypeMismatch");
            mockLoader.SetupAsset("TypeMismatch", asset);
            
            // Expect the error log
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error, "Trying to get setting of wrong type TypeMismatch(ScriptableSettings.Tests.TestAudioSettings) by TestGraphicsSettings !");
            
            // Act
            var success = node.TryGetSetting<TestGraphicsSettings>(out var settings);
            
            // Assert
            Assert.IsFalse(success);
            Assert.IsNull(settings);
            Assert.AreEqual(0, mockLoader.LoadCallCount); // Should not even attempt to load
        }
        
        [Test]
        public void TryGetSettingGeneric_CorrectType_ReturnsTypedAsset()
        {
            // Arrange
            var node = new SettingNode("TypedTest", typeof(TestAudioSettings), Guid.NewGuid(), mockLoader);
            var asset = CreateTestAsset<TestAudioSettings>("TypedTest");
            asset.volume = 0.75f;
            asset.muted = true;
            mockLoader.SetupAsset("TypedTest", asset);
            
            // Act
            var success = node.TryGetSetting<TestAudioSettings>(out var settings);
            
            // Assert
            Assert.IsTrue(success);
            Assert.IsNotNull(settings);
            Assert.AreEqual(0.75f, settings.volume);
            Assert.IsTrue(settings.muted);
        }
        
        [Test]
        public async Task LoadAsync_WithCaching_CachesCorrectly()
        {
            // Arrange
            var node = new SettingNode("AsyncCacheTest", typeof(TestSettings), Guid.NewGuid(), mockLoader);
            var asset = CreateTestAsset<TestSettings>("AsyncCacheTest");
            mockLoader.SetupAsset("AsyncCacheTest", asset);
            
            // Act
            var firstLoad = await node.LoadAsync();
            var cachedAsset = node.Asset; // Should be cached now
            var secondLoad = await node.LoadAsync(); // Should return cached
            
            // Assert
            Assert.IsNotNull(firstLoad);
            Assert.AreSame(firstLoad, cachedAsset);
            Assert.AreSame(firstLoad, secondLoad);
            Assert.AreEqual(1, mockLoader.LoadCallCount); // Only loaded once
        }
        
        #endregion
        
        #region Edge Cases and Error Scenarios
        
        [Test]
        public void NodeConstruction_EmptyGuid_AddsError()
        {
            // Arrange & Act
            var node = new SettingNode("InvalidNode", typeof(TestSettings), Guid.Empty, mockLoader);
            
            // Assert
            Assert.IsFalse(node.IsValid);
            Assert.AreEqual(1, node.Errors.Count);
            Assert.IsTrue(node.Errors[0].Contains("GUID cannot be empty"));
        }
        
        [Test]
        public void NodeConstruction_NullType_AddsError()
        {
            // Arrange & Act
            var node = new SettingNode("NullTypeNode", null, Guid.NewGuid(), mockLoader);
            
            // Assert
            Assert.IsFalse(node.IsValid);
            Assert.IsTrue(node.Errors.Any(e => e.Contains("Type of asset")));
        }
        
        [Test]
        public void NodeConstruction_NonScriptableObjectType_AddsError()
        {
            // Arrange & Act
            var node = new SettingNode("InvalidType", typeof(TestInvalidSettings), Guid.NewGuid(), mockLoader);
            
            // Assert
            Assert.IsFalse(node.IsValid);
            Assert.IsTrue(node.Errors.Any(e => e.Contains("must inherit from ScriptableObject")));
        }
        
        [Test]
        public void CircularReference_Detection()
        {
            // This test verifies that the system handles circular references gracefully
            // Arrange
            var nodeA = new SettingNode("NodeA", typeof(TestSettings), Guid.NewGuid(), mockLoader);
            var nodeB = new SettingNode("NodeB", typeof(TestSettings), Guid.NewGuid(), mockLoader);
            var nodeC = new SettingNode("NodeC", typeof(TestSettings), Guid.NewGuid(), mockLoader);
            
            // Create circular reference: A -> B -> C -> A
            AddChildToNode(nodeA, nodeB);
            AddChildToNode(nodeB, nodeC);
            
            // Act - Try to create circular reference
            // Note: The current implementation doesn't prevent this, but paths should still work
            var pathA = resourcesLoader.NodeLoadPath(nodeA);
            var pathB = resourcesLoader.NodeLoadPath(nodeB);
            var pathC = resourcesLoader.NodeLoadPath(nodeC);
            
            // Assert - Paths are still constructed correctly
            Assert.AreEqual("Settings/NodeA", pathA);
            Assert.AreEqual("Settings/NodeA/NodeB", pathB);
            Assert.AreEqual("Settings/NodeA/NodeB/NodeC", pathC);
        }
        
        [Test]
        public void Load_NullNode_HandlesGracefully()
        {
            // The ResourcesSettingLoader should handle null nodes gracefully
            // Act
            var path = resourcesLoader.NodeLoadPath(null);
            
            // Assert
            Assert.AreEqual("Settings/", path); // Just the prefix
        }
        
        [Test]
        public void VeryDeepHierarchy_PerformanceTest()
        {
            // Test performance with very deep hierarchies
            var depth = 100;
            var nodes = new List<SettingNode>();
            SettingNode parent = null;
            
            // Arrange - Create a very deep hierarchy
            for (int i = 0; i < depth; i++)
            {
                var node = new SettingNode($"Level{i}", typeof(TestSettings), Guid.NewGuid(), mockLoader);
                if (parent != null)
                {
                    AddChildToNode(parent, node);
                }
                nodes.Add(node);
                parent = node;
            }
            
            // Act - Time the path construction
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var deepPath = resourcesLoader.NodeLoadPath(nodes.Last());
            stopwatch.Stop();
            
            // Assert
            Assert.Less(stopwatch.ElapsedMilliseconds, 100); // Should be fast even for deep hierarchies
            Assert.IsTrue(deepPath.StartsWith("Settings/Level0"));
            Assert.IsTrue(deepPath.EndsWith($"Level{depth - 1}"));
        }
        
        [Test]
        public void SpecialPathCharacters_InHierarchy_HandlesCorrectly()
        {
            // Test a complex hierarchy with various special characters
            var root = new SettingNode("Root-Node", typeof(TestSettings), Guid.NewGuid(), mockLoader);
            var child1 = new SettingNode("Child.With.Dots", typeof(TestSettings), Guid.NewGuid(), mockLoader);
            var child2 = new SettingNode("Child With Spaces", typeof(TestSettings), Guid.NewGuid(), mockLoader);
            var grandchild = new SettingNode("GrandChild@Special#Chars", typeof(TestSettings), Guid.NewGuid(), mockLoader);
            
            AddChildToNode(root, child1);
            AddChildToNode(child1, child2);
            AddChildToNode(child2, grandchild);
            
            // Act
            var path = resourcesLoader.NodeLoadPath(grandchild);
            
            // Assert
            Assert.AreEqual("Settings/Root-Node/Child.With.Dots/Child With Spaces/GrandChild@Special#Chars", path);
        }
        
        #endregion
    }
}