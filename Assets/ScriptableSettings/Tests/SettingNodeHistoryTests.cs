using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Scriptable.Settings;
using Scriptable.Settings.Editor;
using UnityEditor;

namespace ScriptableSettings.Tests
{
    [TestFixture]
    public class SettingNodeHistoryTests : ScriptableSettingsTestBase
    {
        private SettingsManager manager;
        private SettingNodeHistory history;
        private SettingNode rootNode;
        private SettingNode childNode1;
        private SettingNode childNode2;
        private SettingNode grandchildNode;
        
        [SetUp]
        public override void Setup()
        {
            base.Setup();
            manager = CreateTestManager();
            history = new SettingNodeHistory(manager);
            
            // Create test nodes using test base methods
            rootNode = CreateTestNode("RootNode", typeof(TestSettings));
            childNode1 = CreateTestNode("ChildNode1", typeof(TestSettings));
            childNode2 = CreateTestNode("ChildNode2", typeof(TestSettings));
            grandchildNode = CreateTestNode("GrandchildNode", typeof(TestSettings));
            
            // Build the hierarchy
            AddChildToNode(rootNode, childNode1);
            AddChildToNode(rootNode, childNode2);
            AddChildToNode(childNode1, grandchildNode);
            
            // Add root to manager
            AddRootNodeToManager(manager, rootNode);
        }
        
        [TearDown]
        public override void TearDown()
        {
            base.TearDown();
            EditorPrefs.DeleteKey("ScriptableSettings.NodeHistory");
        }
        
        [Test]
        public void AddNode_AddsNodeToHistory()
        {
            history.AddNode(rootNode);
            
            var historyList = history.GetHistory();
            Assert.AreEqual(1, historyList.Count);
            Assert.AreEqual("RootNode", historyList[0].NodePath);
            Assert.AreEqual(rootNode.Guid.ToString(), historyList[0].NodeGuid);
        }
        
        [Test]
        public void AddNode_PreventsDuplicates()
        {
            history.AddNode(rootNode);
            history.AddNode(childNode1);
            history.AddNode(rootNode); // Add same node again
            
            var historyList = history.GetHistory();
            Assert.AreEqual(2, historyList.Count);
            Assert.AreEqual("RootNode/ChildNode1", historyList[0].NodePath);
            Assert.AreEqual("RootNode", historyList[1].NodePath); // rootNode should be most recent
        }
        
        [Test]
        public void History_LimitsToTenItems()
        {
            // Add 12 nodes
            for (int i = 0; i < 12; i++)
            {
                var node = CreateTestNode($"Node{i}", typeof(TestSettings));
                AddChildToNode(rootNode, node);
                history.AddNode(node);
            }
            
            var historyList = history.GetHistory();
            Assert.AreEqual(10, historyList.Count);
        }
        
        [Test]
        public void NavigateBack_WorksCorrectly()
        {
            history.AddNode(rootNode);
            history.AddNode(childNode1);
            history.AddNode(childNode2);
            
            Assert.IsTrue(history.CanGoBack);
            Assert.AreEqual(childNode2, history.CurrentNode);
            
            history.NavigateBack();
            Assert.AreEqual(childNode1, history.CurrentNode);
            
            history.NavigateBack();
            Assert.AreEqual(rootNode, history.CurrentNode);
            
            Assert.IsFalse(history.CanGoBack);
        }
        
        [Test]
        public void NavigateForward_WorksCorrectly()
        {
            history.AddNode(rootNode);
            history.AddNode(childNode1);
            history.AddNode(childNode2);
            
            history.NavigateBack();
            history.NavigateBack();
            
            Assert.IsTrue(history.CanGoForward);
            Assert.AreEqual(rootNode, history.CurrentNode);
            
            history.NavigateForward();
            Assert.AreEqual(childNode1, history.CurrentNode);
            
            history.NavigateForward();
            Assert.AreEqual(childNode2, history.CurrentNode);
            
            Assert.IsFalse(history.CanGoForward);
        }
        
        [Test]
        public void AddNode_ClearsForwardHistory()
        {
            history.AddNode(rootNode);
            history.AddNode(childNode1);
            history.AddNode(childNode2);
            
            history.NavigateBack();
            history.NavigateBack();
            
            // Add new node while in middle of history
            history.AddNode(grandchildNode);
            
            Assert.IsFalse(history.CanGoForward);
            Assert.AreEqual(grandchildNode, history.CurrentNode);
            
            var historyList = history.GetHistory();
            Assert.AreEqual(2, historyList.Count); // rootNode and grandchildNode
        }
        
        [Test]
        public void Clear_RemovesAllHistory()
        {
            history.AddNode(rootNode);
            history.AddNode(childNode1);
            history.AddNode(childNode2);
            
            history.Clear();
            
            var historyList = history.GetHistory();
            Assert.AreEqual(0, historyList.Count);
            Assert.IsFalse(history.CanGoBack);
            Assert.IsFalse(history.CanGoForward);
            Assert.IsNull(history.CurrentNode);
        }
        
        [Test]
        public void Persistence_SavesAndLoadsHistory()
        {
            // Clear any existing history first
            EditorPrefs.DeleteKey("ScriptableSettings.NodeHistory");
            
            // Create first history instance and add nodes
            var history1 = new SettingNodeHistory(manager);
            history1.AddNode(rootNode);
            history1.AddNode(childNode1);
            history1.AddNode(childNode2);
            
            // Check that we can access the history immediately
            var list1 = history1.GetHistory();
            Assert.AreEqual(3, list1.Count, "History should have 3 items after adding");
            
            // Check if the data was saved
            var savedData = EditorPrefs.GetString("ScriptableSettings.NodeHistory", "");
            Assert.IsNotEmpty(savedData, "History should be saved to EditorPrefs");
            
            // Ensure manager has the nodes indexed
            Assert.IsNotNull(manager.GetNodeById(rootNode.Guid), "Root node should be in manager");
            Assert.IsNotNull(manager.GetNodeById(childNode1.Guid), "Child1 should be in manager");
            Assert.IsNotNull(manager.GetNodeById(childNode2.Guid), "Child2 should be in manager");
            
            // Create new history instance to test loading
            var newHistory = new SettingNodeHistory(manager);
            
            var historyList = newHistory.GetHistory();
            Assert.AreEqual(3, historyList.Count, "New history instance should load 3 items from EditorPrefs");
            Assert.AreEqual(childNode2.Guid, newHistory.CurrentNode?.Guid);
        }
        
        [Test]
        public void Persistence_HandlesDeletedNodes()
        {
            // Clear any existing history first
            EditorPrefs.DeleteKey("ScriptableSettings.NodeHistory");
            
            // Create first history instance and add nodes
            var history1 = new SettingNodeHistory(manager);
            history1.AddNode(rootNode);
            history1.AddNode(childNode1);
            history1.AddNode(childNode2);
            
            // Remove the node from the manager's index to simulate deletion
            RemoveChildFromNode(rootNode, childNode1);
            manager.nodeIndex.Remove(childNode1.Guid);
            
            // Create new history instance to test loading with deleted node
            var newHistory = new SettingNodeHistory(manager);
            
            var historyList = newHistory.GetHistory();
            Assert.AreEqual(2, historyList.Count); // childNode1 should be filtered out
            Assert.AreEqual(childNode2.Guid, newHistory.CurrentNode?.Guid);
        }
        
        [Test]
        public void CurrentIndex_UpdatesCorrectly()
        {
            history.AddNode(rootNode);
            history.AddNode(childNode1);
            history.AddNode(childNode2);
            
            Assert.AreEqual(2, history.CurrentIndex);
            
            history.NavigateBack();
            Assert.AreEqual(1, history.CurrentIndex);
            
            history.NavigateBack();
            Assert.AreEqual(0, history.CurrentIndex);
            
            history.NavigateForward();
            Assert.AreEqual(1, history.CurrentIndex);
        }
    }
}