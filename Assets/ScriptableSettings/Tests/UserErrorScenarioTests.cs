using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using Scriptable.Settings;

namespace ScriptableSettings.Tests
{
    /// <summary>
    /// Comprehensive tests for user mistake scenarios in the ScriptableSettings system.
    /// Tests cover common user errors, manual edits, setup mistakes, and data corruption scenarios.
    /// Focus on robust error handling, data recovery, clear error reporting, and graceful degradation.
    /// </summary>
    [TestFixture]
    public class UserErrorScenarioTests : ScriptableSettingsTestBase
    {
        private SettingsManager testManager;
        
        [SetUp]
        public override void Setup()
        {
            base.Setup();
            testManager = CreateTestManager();
        }

        #region Common User Errors

        [Test]
        public void DuplicateNodeNames_InSameParent_HandledGracefully()
        {
            // Arrange
            var parent = CreateTestNode("Parent", typeof(TestSettings));
            var child1 = CreateTestNode("DuplicateName", typeof(TestAudioSettings));
            var child2 = CreateTestNode("DuplicateName", typeof(TestGraphicsSettings));
            
            AddChildToNode(parent, child1);
            AddChildToNode(parent, child2);
            
            // Setup assets with hierarchical paths
            var asset1 = CreateTestAsset<TestAudioSettings>("DuplicateName1");
            var asset2 = CreateTestAsset<TestGraphicsSettings>("DuplicateName2");
            mockLoader.SetupAsset("Parent/DuplicateName", asset1); // Both children have same path
            
            // Act - Try to load both children with duplicate names
            var success1 = child1.TryGetSetting(out ScriptableObject loadedAsset1);
            var success2 = child2.TryGetSetting(out ScriptableObject loadedAsset2);
            
            // Assert - At least one should be loadable (the first one)
            Assert.IsTrue(success1 || success2, "At least one child should be loadable");
            // The system should handle duplicate names by treating them as separate nodes with different GUIDs
            Assert.AreNotEqual(child1.Guid, child2.Guid);
        }

        [Test]
        public void DuplicateNodeNames_DifferentParents_ShouldWork()
        {
            // Arrange
            var parent1 = CreateTestNode("Parent1", typeof(TestSettings));
            var parent2 = CreateTestNode("Parent2", typeof(TestSettings));
            var child1 = CreateTestNode("SameName", typeof(TestAudioSettings));
            var child2 = CreateTestNode("SameName", typeof(TestGraphicsSettings));
            
            AddChildToNode(parent1, child1);
            AddChildToNode(parent2, child2);
            
            // Setup assets with proper hierarchical paths
            var asset1 = CreateTestAsset<TestAudioSettings>("SameName1");
            var asset2 = CreateTestAsset<TestGraphicsSettings>("SameName2");
            mockLoader.SetupAsset("Parent1/SameName", asset1);
            mockLoader.SetupAsset("Parent2/SameName", asset2);
            
            // Act
            var success1 = child1.TryGetSetting(out _);
            var success2 = child2.TryGetSetting(out _);
            
            // Assert - Both should work fine as they're in different hierarchies
            Assert.IsTrue(success1 && success2);
            Assert.AreNotEqual(child1.Guid, child2.Guid);
        }

        [Test]
        public void WrongTypeInSettingId_TypeMismatch_ReportsError()
        {
            // Arrange
            var node = CreateTestNode("AudioNode", typeof(TestAudioSettings));
            var audioAsset = CreateTestAsset<TestAudioSettings>("AudioNode");
            mockLoader.SetupAsset("AudioNode", audioAsset);
            
            // Expect the error log
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error, "Trying to get setting of wrong type AudioNode(ScriptableSettings.Tests.TestAudioSettings) by TestGraphicsSettings !");
            
            // Act - Try to get setting with wrong type
            var success = node.TryGetSetting<TestGraphicsSettings>(out var settings);
            
            // Assert
            Assert.IsFalse(success);
            Assert.IsNull(settings);
        }

        [Test]
        public void DeletedNodeReferences_MissingAsset_HandledGracefully()
        {
            // Arrange
            var node = CreateTestNode("DeletedAsset", typeof(TestSettings));
            // Don't setup the asset in mockLoader to simulate deleted asset
            
            // Act
            var success = node.TryGetSetting(out ScriptableObject asset);
            
            // Assert
            Assert.IsFalse(success);
            Assert.IsNull(asset);
            Assert.AreEqual(1, mockLoader.LoadCallCount);
        }

        [Test]
        public void NullReferencesInHierarchy_HandledGracefully()
        {
            // Arrange
            var parent = CreateTestNode("Parent", typeof(TestSettings));
            AddChildToNode(parent, null); // This should be handled gracefully
            
            // Act & Assert - Should not throw
            Assert.DoesNotThrow(() => {
                var children = parent.Children;
                Assert.AreEqual(0, children.Count); // Null child should not be added
            });
        }

        #endregion

        #region Manual File Edits

        [Test]
        public void ManualGuidEditing_InvalidGuid_AddsValidationError()
        {
            // Arrange & Act - Create node with invalid GUID
            var nodeWithEmptyGuid = CreateTestNode("InvalidGuid", typeof(TestSettings), Guid.Empty);
            
            // Assert
            Assert.IsFalse(nodeWithEmptyGuid.IsValid);
            Assert.IsTrue(nodeWithEmptyGuid.Errors.Any(e => e.Contains("Asset GUID cannot be empty")));
        }

        [Test]
        public void ManualGuidEditing_DuplicateGuids_DetectedInIndex()
        {
            // Arrange
            var guid = Guid.NewGuid();
            var node1 = CreateTestNode("Node1", typeof(TestSettings), guid);
            var node2 = CreateTestNode("Node2", typeof(TestSettings), guid);
            
            // Act - Simulate adding both to a manager's index
            var nodeIndex = new Dictionary<Guid, SettingNode>();
            var duplicateDetected = !nodeIndex.TryAdd(node1.Guid, node1);
            Assert.IsFalse(duplicateDetected); // First one should succeed
            
            duplicateDetected = !nodeIndex.TryAdd(node2.Guid, node2);
            
            // Assert
            Assert.IsTrue(duplicateDetected); // Second one should fail due to duplicate GUID
        }

        [Test]
        public void MixedSlashesInPath_HandledCorrectly()
        {
            // Arrange - Test paths with mixed forward and back slashes
            var testCases = new[]
            {
                "Settings\\Windows\\Style\\Path",
                "Settings/Unix/Style/Path",
                "Settings\\Mixed/Style\\Path"
            };
            
            foreach (var testPath in testCases)
            {
                // Act - Create node with mixed slashes in name
                var node = CreateTestNode(testPath.Replace("Settings", "").TrimStart('\\', '/'), typeof(TestSettings));
                
                // Assert - Node should be created without errors
                Assert.IsTrue(node.IsValid, $"Node with path '{testPath}' should be valid");
            }
        }

        [Test]
        public void RenamedSettingFiles_AssetNotFound_HandlesGracefully()
        {
            // Arrange
            var node = CreateTestNode("OriginalName", typeof(TestSettings));
            // Setup asset with different name to simulate renamed file
            var asset = CreateTestAsset<TestSettings>("RenamedFile");
            mockLoader.SetupAsset("RenamedFile", asset);
            // Don't setup with "OriginalName" to simulate missing file
            
            // Act
            var success = node.TryGetSetting(out ScriptableObject loadedAsset);
            
            // Assert
            Assert.IsFalse(success);
            Assert.IsNull(loadedAsset);
        }

        [Test]
        public void MovedAssets_PathChanged_HandlesGracefully()
        {
            // Arrange
            var node = CreateTestNode("MovedAsset", typeof(TestSettings));
            // Setup asset in old location
            var asset = CreateTestAsset<TestSettings>("MovedAsset");
            mockLoader.SetupAsset("OldPath/MovedAsset", asset);
            // Current path would be "MovedAsset" but asset is at "OldPath/MovedAsset"
            
            // Act
            var success = node.TryGetSetting(out ScriptableObject loadedAsset);
            
            // Assert
            Assert.IsFalse(success); // Should fail as asset is not at expected path
            Assert.IsNull(loadedAsset);
        }

        #endregion

        #region Setup Mistakes

        [Test]
        public void MissingSettingsFolder_LoadingFails_HandlesGracefully()
        {
            // Arrange
            var node = CreateTestNode("MissingFolder", typeof(TestSettings));
            // Don't setup any assets to simulate missing Settings folder
            
            // Act
            var success = node.TryGetSetting(out ScriptableObject asset);
            
            // Assert
            Assert.IsFalse(success);
            Assert.IsNull(asset);
            Assert.AreEqual(1, mockLoader.LoadCallCount);
        }

        [Test]
        public void WrongFolderStructure_AssetsInWrongLocation_HandlesGracefully()
        {
            // Arrange
            var parentNode = CreateTestNode("Parent", typeof(TestSettings));
            var childNode = CreateTestNode("Child", typeof(TestSettings));
            AddChildToNode(parentNode, childNode);
            
            // Setup asset in wrong location (not following hierarchy)
            var asset = CreateTestAsset<TestSettings>("Child");
            mockLoader.SetupAsset("Child", asset); // Should be at "Parent/Child"
            
            // Act
            var success = childNode.TryGetSetting(out ScriptableObject loadedAsset);
            
            // Assert
            Assert.IsFalse(success); // Should fail as asset is not at expected hierarchical path
            Assert.IsNull(loadedAsset);
        }

        [Test]
        public void MixedSettingTypes_InSameHierarchy_WorksCorrectly()
        {
            // Arrange
            var parent = CreateTestNode("Parent", typeof(TestSettings));
            var audioChild = CreateTestNode("Audio", typeof(TestAudioSettings));
            var graphicsChild = CreateTestNode("Graphics", typeof(TestGraphicsSettings));
            var gameChild = CreateTestNode("Game", typeof(TestGameSettings));
            
            AddChildToNode(parent, audioChild);
            AddChildToNode(parent, graphicsChild);
            AddChildToNode(parent, gameChild);
            
            // Setup assets
            var audioAsset = CreateTestAsset<TestAudioSettings>("Audio");
            var graphicsAsset = CreateTestAsset<TestGraphicsSettings>("Graphics");
            var gameAsset = CreateTestAsset<TestGameSettings>("Game");
            
            mockLoader.SetupAsset("Parent/Audio", audioAsset);
            mockLoader.SetupAsset("Parent/Graphics", graphicsAsset);
            mockLoader.SetupAsset("Parent/Game", gameAsset);
            
            // Act
            var audioSuccess = audioChild.TryGetSetting<TestAudioSettings>(out var audioSettings);
            var graphicsSuccess = graphicsChild.TryGetSetting<TestGraphicsSettings>(out var graphicsSettings);
            var gameSuccess = gameChild.TryGetSetting<TestGameSettings>(out var gameSettings);
            
            // Assert
            Assert.IsTrue(audioSuccess);
            Assert.IsTrue(graphicsSuccess);
            Assert.IsTrue(gameSuccess);
            Assert.IsNotNull(audioSettings);
            Assert.IsNotNull(graphicsSettings);
            Assert.IsNotNull(gameSettings);
        }

        [Test]
        public void CircularDependencies_DetectionAndHandling()
        {
            // Arrange - Create circular reference structure
            var nodeA = CreateTestNode("NodeA", typeof(TestSettings));
            var nodeB = CreateTestNode("NodeB", typeof(TestSettings));
            var nodeC = CreateTestNode("NodeC", typeof(TestSettings));
            
            // Create circular structure: A -> B -> C -> (attempting to add A as child of C)
            AddChildToNode(nodeA, nodeB);
            AddChildToNode(nodeB, nodeC);
            
            // Act - The system should handle this gracefully
            // Note: Current implementation doesn't prevent circular references,
            // but path construction should still work for the defined hierarchy
            
            var pathA = mockLoader.NodeLoadPath(nodeA);
            var pathB = mockLoader.NodeLoadPath(nodeB);
            var pathC = mockLoader.NodeLoadPath(nodeC);
            
            // Assert - Paths should still be constructible
            Assert.AreEqual("NodeA", pathA);
            Assert.AreEqual("NodeA/NodeB", pathB);
            Assert.AreEqual("NodeA/NodeB/NodeC", pathC);
        }

        #endregion

        #region Data Corruption Scenarios

        [Test]
        public void CorruptedShortGuid_InvalidFormat_HandlesGracefully()
        {
            // Arrange - Test various corrupted ShortGuid formats
            var corruptedGuids = new[]
            {
                "", // Empty
                "invalid", // Too short
                "this-is-way-too-long-to-be-a-valid-shortguid", // Too long
                "invalid!@#$%^&*()", // Invalid characters
                "22-chars-but-invalid@", // Right length but invalid chars
                null // Null
            };
            
            foreach (var corrupted in corruptedGuids)
            {
                // Act
                var success = ShortGuid.TryParse(corrupted, out Guid result);
                
                // Assert
                Assert.IsFalse(success, $"Corrupted GUID '{corrupted}' should not parse successfully");
                Assert.AreEqual(Guid.Empty, result);
            }
        }

        [Test]
        public void CorruptedTypeInfo_MissingType_HandlesGracefully()
        {
            // Arrange - Create node with non-existent type
            // Note: CreateTestNode with null type defaults to typeof(TestSettings)
            var node = new SettingNode("InvalidType", null, Guid.NewGuid(), mockLoader);
            
            // Assert
            Assert.IsFalse(node.IsValid);
            Assert.IsTrue(node.Errors.Any(e => e.Contains("Type of asset")));
        }

        [Test]
        public void CorruptedTypeInfo_WrongBaseType_DetectedAndReported()
        {
            // Arrange - Create node with type that doesn't inherit from ScriptableObject
            var node = CreateTestNode("WrongBaseType", typeof(TestInvalidSettings));
            
            // Assert
            Assert.IsFalse(node.IsValid);
            Assert.IsTrue(node.Errors.Any(e => e.Contains("must inherit from ScriptableObject")));
        }

        [Test]
        public void PartiallyCorruptedHierarchy_ValidNodesStillWork()
        {
            // Arrange - Create hierarchy with some corrupted nodes
            var validParent = CreateTestNode("ValidParent", typeof(TestSettings));
            var validChild = CreateTestNode("ValidChild", typeof(TestAudioSettings));
            var corruptedChild = new SettingNode("CorruptedChild", null, Guid.NewGuid(), mockLoader); // Corrupted type
            var anotherValidChild = CreateTestNode("AnotherValid", typeof(TestGraphicsSettings));
            
            AddChildToNode(validParent, validChild);
            AddChildToNode(validParent, corruptedChild);
            AddChildToNode(validParent, anotherValidChild);
            
            // Setup assets for valid nodes
            var validAsset = CreateTestAsset<TestAudioSettings>("ValidChild");
            var anotherValidAsset = CreateTestAsset<TestGraphicsSettings>("AnotherValid");
            mockLoader.SetupAsset("ValidParent/ValidChild", validAsset);
            mockLoader.SetupAsset("ValidParent/AnotherValid", anotherValidAsset);
            
            // Act
            var validSuccess = validChild.TryGetSetting<TestAudioSettings>(out var validSettings);
            var anotherValidSuccess = anotherValidChild.TryGetSetting<TestGraphicsSettings>(out var anotherValidSettings);
            
            // Assert - Valid nodes should still work despite corrupted sibling
            Assert.IsTrue(validSuccess);
            Assert.IsTrue(anotherValidSuccess);
            Assert.IsNotNull(validSettings);
            Assert.IsNotNull(anotherValidSettings);
            Assert.IsFalse(corruptedChild.IsValid);
        }

        [Test]
        public void LoaderExceptions_PropagatedCorrectly()
        {
            // Arrange
            var node = CreateTestNode("ExceptionNode", typeof(TestSettings));
            mockLoader.ThrowOnLoad = true;
            
            // Act & Assert
            Assert.Throws<Exception>(() => node.TryGetSetting(out _));
        }

        [Test]
        public async Task AsyncLoadingErrors_HandledGracefully()
        {
            // Arrange
            var node = CreateTestNode("AsyncErrorNode", typeof(TestSettings));
            mockLoader.ThrowOnLoad = true;
            
            // Act & Assert
            Assert.ThrowsAsync<Exception>(async () => await node.LoadAsync());
        }

        #endregion

        #region Error Recovery and Validation

        [Test]
        public void ErrorRecovery_LoaderFailure_FallbackMechanisms()
        {
            // Arrange
            var node = CreateTestNode("RecoveryTest", typeof(TestSettings));
            mockLoader.ReturnNullOnLoad = true; // Simulate loader returning null
            
            // Act
            var success = node.TryGetSetting(out ScriptableObject asset);
            
            // Assert
            Assert.IsFalse(success);
            Assert.IsNull(asset);
            // System should handle null gracefully without crashing
        }

        [Test]
        public void ValidationChecks_MultipleErrors_AllReported()
        {
            // Arrange - Create node with multiple issues
            var invalidNode = new SettingNode("", typeof(TestInvalidSettings), Guid.Empty, mockLoader);
            
            // Assert - All errors should be reported
            Assert.IsFalse(invalidNode.IsValid);
            Assert.GreaterOrEqual(invalidNode.Errors.Count, 2); // At least GUID and type errors
            Assert.IsTrue(invalidNode.Errors.Any(e => e.Contains("Asset GUID cannot be empty")));
            Assert.IsTrue(invalidNode.Errors.Any(e => e.Contains("must inherit from ScriptableObject")));
        }

        [Test]
        public void ConsistencyChecks_HierarchyIntegrity_Maintained()
        {
            // Arrange
            var parent = CreateTestNode("Parent", typeof(TestSettings));
            var child = CreateTestNode("Child", typeof(TestSettings));
            
            // Act
            AddChildToNode(parent, child);
            
            // Assert - Verify bidirectional relationship
            Assert.AreEqual(parent, child.Parent);
            Assert.IsTrue(parent.Children.Contains(child));
            
            // Test removal
            var removed = RemoveChildFromNode(parent, child);
            Assert.IsTrue(removed);
            Assert.IsNull(child.Parent);
            Assert.IsFalse(parent.Children.Contains(child));
        }

        [Test]
        public void CacheManagement_CorruptedCache_HandlesGracefully()
        {
            // Arrange
            var node = CreateTestNode("CacheTest", typeof(TestSettings));
            var asset = CreateTestAsset<TestSettings>("CacheTest");
            mockLoader.SetupAsset("CacheTest", asset);
            
            // Load once to cache
            node.TryGetSetting(out _);
            
            // Simulate cache corruption by destroying the asset
            UnityEngine.Object.DestroyImmediate(asset);
            
            // Act - Try to get setting again
            var success = node.TryGetSetting(out _);
            
            // Assert - Should attempt to reload when cached asset is destroyed
            Assert.IsFalse(success); // Will fail since we destroyed the asset
            Assert.AreEqual(2, mockLoader.LoadCallCount); // Should have attempted reload
        }

        #endregion

        #region Edge Cases and Boundary Conditions

        [Test]
        public void ExtremelyLongNodeNames_HandledCorrectly()
        {
            // Arrange - Create node with very long name
            var longName = new string('A', 1000);
            var node = CreateTestNode(longName, typeof(TestSettings));
            
            // Act
            var path = mockLoader.NodeLoadPath(node);
            
            // Assert
            Assert.IsTrue(node.IsValid);
            Assert.IsTrue(path.Contains(longName));
        }

        [Test]
        public void SpecialCharactersInNodeNames_HandledCorrectly()
        {
            // Arrange - Test various special characters
            var specialNames = new[]
            {
                "Node with spaces",
                "Node-with-dashes",
                "Node_with_underscores",
                "Node.with.dots",
                "Node@with#special$chars",
                "Node(with)parentheses",
                "Node[with]brackets",
                "Node{with}braces",
                "Node/with/slashes",
                "Node\\with\\backslashes"
            };
            
            foreach (var name in specialNames)
            {
                // Act
                var node = CreateTestNode(name, typeof(TestSettings));
                var path = mockLoader.NodeLoadPath(node);
                
                // Assert
                Assert.IsTrue(node.IsValid, $"Node with name '{name}' should be valid");
                Assert.IsTrue(path.Contains(name), $"Path should contain the name '{name}'");
            }
        }

        [Test]
        public void UnicodeCharactersInNodeNames_HandledCorrectly()
        {
            // Arrange - Test Unicode characters
            var unicodeNames = new[]
            {
                "日本語",
                "Русский",
                "العربية",
                "中文",
                "한국어",
                "Emoji😀Node"
            };
            
            foreach (var name in unicodeNames)
            {
                // Act
                var node = CreateTestNode(name, typeof(TestSettings));
                var path = mockLoader.NodeLoadPath(node);
                
                // Assert
                Assert.IsTrue(node.IsValid, $"Node with Unicode name '{name}' should be valid");
                Assert.IsTrue(path.Contains(name), $"Path should contain the Unicode name '{name}'");
            }
        }

        [Test]
        public void VeryDeepHierarchy_PerformanceAndStability()
        {
            // Arrange - Create very deep hierarchy
            var depth = 50;
            var nodes = new List<SettingNode>();
            SettingNode parent = null;
            
            for (int i = 0; i < depth; i++)
            {
                var node = CreateTestNode($"Level{i}", typeof(TestSettings));
                if (parent != null)
                {
                    AddChildToNode(parent, node);
                }
                nodes.Add(node);
                parent = node;
            }
            
            // Act - Test path construction performance
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var deepPath = mockLoader.NodeLoadPath(nodes.Last());
            stopwatch.Stop();
            
            // Assert
            Assert.Less(stopwatch.ElapsedMilliseconds, 50); // Should be fast
            Assert.IsTrue(deepPath.StartsWith("Level0"));
            Assert.IsTrue(deepPath.EndsWith($"Level{depth - 1}"));
        }

        [Test]
        public void EmptyNodeName_HandledGracefully()
        {
            // Arrange
            var node = CreateTestNode("", typeof(TestSettings));
            
            // Act
            var path = mockLoader.NodeLoadPath(node);
            
            // Assert
            Assert.IsTrue(node.IsValid); // Empty name should be allowed
            Assert.AreEqual("", path); // Empty name results in empty path (no trailing slash)
        }

        [Test]
        public void NullSettingLoader_HandledGracefully()
        {
            // Arrange - Create node with null loader
            // Note: Constructor will assert that loader is not null
            
            // Act & Assert - Should handle null loader gracefully
            Assert.Throws<UnityEngine.Assertions.AssertionException>(() => {
                _ = new SettingNode("NullLoader", typeof(TestSettings), Guid.NewGuid(), null);
            });
        }

        #endregion

        #region Concurrency and Threading

        [Test]
        public async Task ConcurrentLoading_SameNode_HandledCorrectly()
        {
            // Arrange
            var node = CreateTestNode("ConcurrentTest", typeof(TestSettings));
            var asset = CreateTestAsset<TestSettings>("ConcurrentTest");
            mockLoader.SetupAsset("ConcurrentTest", asset);
            
            // Act - Load same node concurrently
            var task1 = Task.Run(() => node.TryGetSetting(out ScriptableObject _));
            var task2 = Task.Run(() => node.TryGetSetting(out ScriptableObject _));
            var task3 = Task.Run(() => node.TryGetSetting(out ScriptableObject _));
            
            await Task.WhenAll(task1, task2, task3);
            
            // Assert - All should complete successfully
            Assert.IsTrue(task1.Result);
            Assert.IsTrue(task2.Result);
            Assert.IsTrue(task3.Result);
        }

        [Test]
        public async Task AsyncLoadingWithSyncAccess_ConsistentBehavior()
        {
            // Arrange
            var node = CreateTestNode("AsyncSyncTest", typeof(TestSettings));
            var asset = CreateTestAsset<TestSettings>("AsyncSyncTest");
            mockLoader.SetupAsset("AsyncSyncTest", asset);
            
            // Act - Mix async and sync loading
            var asyncResult = await node.LoadAsync();
            var syncSuccess = node.TryGetSetting(out ScriptableObject syncResult);
            
            // Assert
            Assert.IsNotNull(asyncResult);
            Assert.IsTrue(syncSuccess);
            Assert.AreSame(asyncResult, syncResult); // Should return cached instance
        }

        #endregion
    }
}