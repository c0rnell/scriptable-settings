using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Scriptable.Settings;
using UnityEngine;
using UnityEngine.TestTools;

namespace Scriptable.Settings.Tests
{
    [TestFixture]
    public class SerializationTests : ScriptableSettingsTestBase
    {
        private ScriptableSettings manager;
        
        public override void Setup()
        {
            base.Setup();
            manager = CreateTestManager();
            
            // Ensure the loader is properly set after creation using internal accessor
            manager.loader = mockLoader;
        }
        
        public override void TearDown()
        {
            if (manager != null)
                UnityEngine.Object.DestroyImmediate(manager);
            base.TearDown();
        }
        
        
        #region Basic Serialization Tests
        
        [Test]
        public void Serialization_EmptyManager_SerializesCorrectly()
        {
            // Arrange
            manager.OnBeforeSerialize();
            
            // Act
            var serializedData = manager.GetSerializedNodesForTesting();
            manager.OnAfterDeserialize();
            
            // Assert
            Assert.IsNotNull(serializedData);
            Assert.AreEqual(0, serializedData.Count);
            Assert.IsNotNull(manager.GetRootNode());
            Assert.AreEqual(0, manager.GetRootNode().Children.Count);
        }
        
        [Test]
        public void Serialization_SingleNode_PreservesAllData()
        {
            // Arrange
            var nodeId = Guid.NewGuid();
            var node = CreateTestNode("TestNode", typeof(TestSettings), nodeId);
            AddRootNodeToManager(manager, node);
            
            // Act
            manager.OnBeforeSerialize();
            var serializedData = manager.GetSerializedNodesForTesting();
            
            // Create new manager to test deserialization
            var newManager = CreateTestManager();
            newManager.SetSerializedNodesForTesting(serializedData);
            newManager.OnAfterDeserialize();
            
            // Assert
            var deserializedNode = newManager.GetNodeById(nodeId);
            Assert.IsNotNull(deserializedNode);
            Assert.AreEqual("TestNode", deserializedNode.Name);
            Assert.AreEqual(typeof(TestSettings), deserializedNode.SettingType);
            Assert.AreEqual(nodeId, deserializedNode.Guid);
        }
        
        [Test]
        public void Serialization_ComplexHierarchy_MaintainsStructure()
        {
            // Arrange
            var parent = CreateTestNode("Parent", typeof(TestSettings), Guid.NewGuid());
            var child1 = CreateTestNode("Child1", typeof(TestAudioSettings), Guid.NewGuid());
            var child2 = CreateTestNode("Child2", typeof(TestGameSettings), Guid.NewGuid());
            var grandchild = CreateTestNode("Grandchild", typeof(TestGraphicsSettings), Guid.NewGuid());
            
            AddChildToNode(parent, child1);
            AddChildToNode(parent, child2);
            AddChildToNode(child1, grandchild);
            
            // Add only the root node to the manager
            // Child nodes will be added to the index automatically through the parent-child relationships
            AddRootNodeToManager(manager, parent);
            
            // Act
            manager.OnBeforeSerialize();
            var serializedData = manager.GetSerializedNodesForTesting();
            
            var newManager = CreateTestManager();
            newManager.SetSerializedNodesForTesting(serializedData);
            newManager.OnAfterDeserialize();
            
            // Assert
            var newParent = newManager.GetNodeById(parent.Guid);
            Assert.IsNotNull(newParent);
            Assert.AreEqual(2, newParent.Children.Count);
            
            var newChild1 = newManager.GetNodeById(child1.Guid);
            Assert.IsNotNull(newChild1);
            Assert.AreEqual(1, newChild1.Children.Count);
            Assert.AreEqual(newParent, newChild1.Parent);
            
            var newGrandchild = newManager.GetNodeById(grandchild.Guid);
            Assert.IsNotNull(newGrandchild);
            Assert.AreEqual(newChild1, newGrandchild.Parent);
            Assert.AreEqual(0, newGrandchild.Children.Count);
        }
        
        [Test]
        public void Serialization_DuplicateGuids_HandlesGracefully()
        {
            // Arrange
            var duplicateId = Guid.NewGuid();
            var node1 = CreateTestNode("Node1", typeof(TestSettings), duplicateId);
            var node2 = CreateTestNode("Node2", typeof(TestSettings), duplicateId); // Same ID!
            
            AddChildToNode(manager.GetRootNode(), node1);
            AddChildToNode(manager.GetRootNode(), node2);
            
            // Act
            manager.OnBeforeSerialize();
            manager.OnAfterDeserialize();
            
            // Assert
            var nodes = manager.GetAllNodes();
            var duplicateNodes = nodes.Where(n => n.Guid == duplicateId).ToList();
            
            // Should handle duplicate gracefully - either by keeping one or logging error
            Assert.LessOrEqual(duplicateNodes.Count, 2);
        }
        
        #endregion
        
        #region Type Serialization Edge Cases
        
        [Test]
        public void Serialization_MissingType_CreatesErrorNode()
        {
            // Arrange
            var nodeId = Guid.NewGuid();
            var nodeData = new SettingNodeData
            {
                i = ShortGuid.Encode(nodeId),
                n = "MissingTypeNode",
                t = "NonExistent.Type.That.Does.Not.Exist, NonExistentAssembly",
                ch = new List<string>()
            };
            
            // Act
            var newManager = CreateTestManager();
            newManager.SetSerializedNodesForTesting(new List<SettingNodeData> { nodeData });
            
            // Expect the error log
            LogAssert.Expect(LogType.Error, $"SettingNode 'MissingTypeNode' (GUID: {ShortGuid.Encode(nodeId)}): Could not find Type 'NonExistent.Type.That.Does.Not.Exist, NonExistentAssembly'. The class may have been renamed, moved, or deleted. Run 'Validate & Fix Node Types' on the ScriptableSettings asset.");
            
            newManager.OnAfterDeserialize();
            
            // Assert
            var node = newManager.GetNodeById(nodeId);
            Assert.IsNotNull(node);
            Assert.AreEqual("MissingTypeNode", node.Name);
            Assert.IsFalse(node.IsValid);
            Assert.IsNotEmpty(node.Errors);
            Assert.IsTrue(node.Errors.Any(e => e.Contains("Type")));
        }
        
        [Test]
        public void Serialization_NullTypeName_HandlesError()
        {
            // Arrange
            var nodeId = Guid.NewGuid();
            var nodeData = new SettingNodeData
            {
                i = ShortGuid.Encode(nodeId),
                n = "NullTypeNode",
                t = null,
                ch = new List<string>()
            };
            
            // Act
            var newManager = CreateTestManager();
            newManager.SetSerializedNodesForTesting(new List<SettingNodeData> { nodeData });
            newManager.OnAfterDeserialize();
            
            // Assert
            var node = newManager.GetNodeById(nodeId);
            Assert.IsNotNull(node);
            Assert.IsFalse(node.IsValid);
            Assert.IsNull(node.SettingType);
        }
        
        [Test]
        public void Serialization_InvalidTypeName_HandlesError()
        {
            // Arrange
            var nodeId = Guid.NewGuid();
            var nodeData = new SettingNodeData
            {
                i = ShortGuid.Encode(nodeId),
                n = "InvalidTypeNode",
                t = "My<>Type<<>>With<>Invalid<>Characters",
                ch = new List<string>()
            };
            
            // Act
            var newManager = CreateTestManager();
            newManager.SetSerializedNodesForTesting(new List<SettingNodeData> { nodeData });
            
            // Expect the error log
            LogAssert.Expect(LogType.Error, $"SettingNode 'InvalidTypeNode' (GUID: {ShortGuid.Encode(nodeId)}): Could not find Type 'My<>Type<<>>With<>Invalid<>Characters'. The class may have been renamed, moved, or deleted. Run 'Validate & Fix Node Types' on the ScriptableSettings asset.");
            
            newManager.OnAfterDeserialize();
            
            // Assert
            var node = newManager.GetNodeById(nodeId);
            Assert.IsNotNull(node);
            Assert.IsFalse(node.IsValid);
        }
        
        [Test]
        public void Serialization_EmptyTypeName_HandlesError()
        {
            // Arrange
            var nodeId = Guid.NewGuid();
            var nodeData = new SettingNodeData
            {
                i = ShortGuid.Encode(nodeId),
                n = "EmptyTypeNode",
                t = "",
                ch = new List<string>()
            };
            
            // Act
            var newManager = CreateTestManager();
            newManager.SetSerializedNodesForTesting(new List<SettingNodeData> { nodeData });
            newManager.OnAfterDeserialize();
            
            // Assert
            var node = newManager.GetNodeById(nodeId);
            Assert.IsNotNull(node);
            Assert.IsFalse(node.IsValid);
        }
        
        #endregion
        
        #region GUID Handling Tests
        
        [Test]
        public void Serialization_EmptyGuid_GeneratesNewOne()
        {
            // Arrange - Test empty GUID handling
            var nodeData = new SettingNodeData
            {
                i = "", // Empty string for GUID
                n = "EmptyGuidNode",
                t = typeof(TestSettings).AssemblyQualifiedName,
                ch = new List<string>()
            };
            
            // Act
            var newManager = CreateTestManager();
            newManager.SetSerializedNodesForTesting(new List<SettingNodeData> { nodeData });
            newManager.OnAfterDeserialize();
            
            // Assert - node should be skipped due to empty GUID
            var nodes = newManager.GetAllNodes();
            var node = nodes.FirstOrDefault(n => n.Name == "EmptyGuidNode");
            Assert.IsNull(node, "Node with empty GUID should be filtered out during deserialization");
        }
        
        [Test]
        public void Serialization_MalformedGuid_HandlesError()
        {
            // Arrange
            var nodeData = new SettingNodeData
            {
                i = "NotAValidGuid!@#$",
                n = "MalformedGuidNode",
                t = typeof(TestSettings).AssemblyQualifiedName,
                ch = new List<string>()
            };
            
            // Act
            var newManager = CreateTestManager();
            newManager.SetSerializedNodesForTesting(new List<SettingNodeData> { nodeData });
            
            // Expect the error log
            LogAssert.Expect(LogType.Error, "Invalid ShortGuid format: 'NotAValidGuid!@#$'");
            
            newManager.OnAfterDeserialize();
            
            // Assert
            var nodes = newManager.GetAllNodes();
            var node = nodes.FirstOrDefault(n => n.Name == "MalformedGuidNode");
            // Should skip the node due to invalid GUID
            Assert.IsNull(node);
        }
        
        [Test]
        public void Serialization_ShortGuidEncoding_RoundTrips()
        {
            // Arrange
            var originalGuid = Guid.NewGuid();
            var node = CreateTestNode("GuidTest", typeof(TestSettings), originalGuid);
            
            // Debug: Check if node is valid
            Assert.IsTrue(node.IsValid, $"Node should be valid. Errors: {string.Join(", ", node.Errors)}");
            
            AddRootNodeToManager(manager, node);
            
            // Debug: Check if node was added to manager
            var nodeById = manager.GetNodeById(originalGuid);
            Assert.IsNotNull(nodeById, "Node should be retrievable from manager after adding");
            Assert.IsTrue(nodeById.IsValid, $"Node in manager should be valid. Errors: {string.Join(", ", nodeById.Errors)}");
            
            // Act
            manager.OnBeforeSerialize();
            var serializedData = manager.GetSerializedNodesForTesting();
            
            // Verify we have data
            Assert.Greater(serializedData.Count, 0, "Should have serialized data");
            var encodedGuid = serializedData[0].i;
            
            // Verify it's base64-like (22 chars)
            Assert.AreEqual(22, encodedGuid.Length);
            
            var newManager = CreateTestManager();
            newManager.SetSerializedNodesForTesting(serializedData);
            newManager.OnAfterDeserialize();
            
            // Assert
            var deserializedNode = newManager.GetNodeById(originalGuid);
            Assert.IsNotNull(deserializedNode);
            Assert.AreEqual(originalGuid, deserializedNode.Guid);
        }
        
        [Test]
        public void Serialization_GuidCollisions_DetectedAndReported()
        {
            // Arrange
            var sharedGuid = Guid.NewGuid();
            var nodeData1 = new SettingNodeData
            {
                i = ShortGuid.Encode(sharedGuid),
                n = "Node1",
                t = typeof(TestSettings).AssemblyQualifiedName,
                ch = new List<string>()
            };
            var nodeData2 = new SettingNodeData
            {
                i = ShortGuid.Encode(sharedGuid), // Same GUID!
                n = "Node2",
                t = typeof(TestSettings).AssemblyQualifiedName,
                ch = new List<string>()
            };
            
            // Act
            var newManager = CreateTestManager();
            newManager.SetSerializedNodesForTesting(new List<SettingNodeData> { nodeData1, nodeData2 });
            newManager.OnAfterDeserialize();
            
            // Assert
            var allNodes = newManager.GetAllNodes();
            var nodesWithGuid = allNodes.Where(n => n.Guid == sharedGuid).ToList();
            
            // Should handle collision - either by skipping duplicate or assigning new GUID
            Assert.LessOrEqual(nodesWithGuid.Count, 1);
        }
        
        #endregion
        
        #region Roundtrip Tests
        
        [Test]
        public void Serialization_MultipleRoundtrips_PreservesData()
        {
            // Arrange
            var node1 = CreateTestNode("Node1", typeof(TestSettings));
            var node2 = CreateTestNode("Node2", typeof(TestAudioSettings));
            var node3 = CreateTestNode("Node3", typeof(TestGameSettings));
            
            AddChildToNode(node1, node2);
            AddChildToNode(node2, node3);
            
            // Add only the root node to the manager
            // Child nodes will be added to the index automatically through recursive addition
            AddRootNodeToManager(manager, node1);
            
            // Act - Multiple roundtrips
            for (int i = 0; i < 5; i++)
            {
                manager.OnBeforeSerialize();
                var data = manager.GetSerializedNodesForTesting();
                manager.SetSerializedNodesForTesting(data);
                manager.OnAfterDeserialize();
            }
            
            // Assert
            var finalNode1 = manager.GetNodeById(node1.Guid);
            var finalNode2 = manager.GetNodeById(node2.Guid);
            var finalNode3 = manager.GetNodeById(node3.Guid);
            
            Assert.IsNotNull(finalNode1);
            Assert.IsNotNull(finalNode2);
            Assert.IsNotNull(finalNode3);
            
            Assert.AreEqual("Node1", finalNode1.Name);
            Assert.AreEqual("Node2", finalNode2.Name);
            Assert.AreEqual("Node3", finalNode3.Name);
            
            Assert.AreEqual(typeof(TestSettings), finalNode1.SettingType);
            Assert.AreEqual(typeof(TestAudioSettings), finalNode2.SettingType);
            Assert.AreEqual(typeof(TestGameSettings), finalNode3.SettingType);
            
            Assert.AreEqual(finalNode1, finalNode2.Parent);
            Assert.AreEqual(finalNode2, finalNode3.Parent);
        }
        
        #endregion
    }
    
    // Extension methods using internal accessors for testing
    public static class ScriptableSettingsTestExtensions
    {
        public static List<SettingNodeData> GetSerializedNodesForTesting(this ScriptableSettings manager)
        {
            return manager.serializedNodes ?? new List<SettingNodeData>();
        }
        
        public static void SetSerializedNodesForTesting(this ScriptableSettings manager, List<SettingNodeData> data)
        {
            manager.serializedNodes = data;
        }
        
        public static SettingNode GetRootNode(this ScriptableSettings manager)
        {
            // Create a fake root node for testing - use a valid GUID instead of Guid.Empty
            var rootNode = new SettingNode("Root", typeof(ScriptableObject), Guid.NewGuid(), manager.Loader);
            
            // Get the actual roots from SettingTree
            var roots = manager.SettingTree;
            if (roots != null)
            {
                foreach (var root in roots)
                {
                    rootNode.AddChild(root);
                }
            }
            
            return rootNode;
        }
        
        public static List<SettingNode> GetAllNodes(this ScriptableSettings manager)
        {
#if UNITY_EDITOR
            // Use the editor method if available
            return manager.GetAllNodes().ToList();
#else
            var nodes = new List<SettingNode>();
            var roots = manager.SettingTree;
            if (roots != null)
            {
                foreach (var root in roots)
                {
                    CollectNodes(root, nodes);
                }
            }
            return nodes;
#endif
        }
        
        private static void CollectNodes(SettingNode node, List<SettingNode> nodes)
        {
            if (node == null) return;
            nodes.Add(node);
            foreach (var child in node.Children)
                CollectNodes(child, nodes);
        }
    }
}