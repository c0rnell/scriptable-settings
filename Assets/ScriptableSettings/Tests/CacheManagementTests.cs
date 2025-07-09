using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Scriptable.Settings;

namespace Scriptable.Settings.Tests
{
    /// <summary>
    /// Comprehensive tests for cache management functionality in the ScriptableSettings system.
    /// Tests weak reference behavior, cache hits/misses, type mismatches, and cache clearing.
    /// </summary>
    public class CacheManagementTests : ScriptableSettingsTestBase
    {
        private SettingNode testNode;
        private TestAudioSettings testAsset;

        [SetUp]
        public override void Setup()
        {
            base.Setup();
            
            // Create a test node with specific GUID
            testNode = new SettingNode("TestNode", typeof(TestAudioSettings), Guid.NewGuid(), mockLoader);
            
            // Create and setup test asset
            testAsset = CreateTestAsset<TestAudioSettings>("TestAudioAsset");
            testAsset.volume = 0.75f;
            testAsset.muted = false;
            
            // Setup mock loader with the asset
            mockLoader.SetupAsset(mockLoader.NodeLoadPath(testNode), testAsset);
        }

        #region Weak Reference Behavior Tests

        [Test]
        [UnityEngine.TestTools.UnityPlatform(exclude = new[] { RuntimePlatform.WindowsEditor, RuntimePlatform.OSXEditor, RuntimePlatform.LinuxEditor })]
        public void WeakReference_AllowsGarbageCollection()
        {
            // Note: This test may fail in Unity Editor due to Unity keeping internal references
            // First load to populate cache
            Assert.IsTrue(testNode.TryGetSetting(out ScriptableObject loaded));
            Assert.AreEqual(testAsset, loaded);
            Assert.AreEqual(1, mockLoader.LoadCallCount);

            // Verify cached
            Assert.IsNotNull(testNode.Asset);
            
            // Clear strong references
            loaded = null;
            testAsset = null;
            
            // Force garbage collection
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            // Asset should be null after GC (weak reference cleared)
            Assert.IsNull(testNode.Asset);
            
            // Next load should hit the loader again
            Assert.IsTrue(testNode.TryGetSetting(out loaded));
            Assert.AreEqual(2, mockLoader.LoadCallCount);
        }

        [Test]
        public void WeakReference_PreservesAssetWhileInUse()
        {
            // Load and keep strong reference
            Assert.IsTrue(testNode.TryGetSetting(out ScriptableObject loaded));
            Assert.AreEqual(testAsset, loaded);
            
            // Force garbage collection while keeping strong reference
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            // Asset should still be cached due to strong reference
            Assert.IsNotNull(testNode.Asset);
            Assert.AreEqual(testAsset, testNode.Asset);
            
            // Second load should use cache
            Assert.IsTrue(testNode.TryGetSetting(out ScriptableObject loaded2));
            Assert.AreEqual(loaded, loaded2);
            Assert.AreEqual(1, mockLoader.LoadCallCount); // No additional load
        }

        #endregion

        #region Cache Hit and Miss Tests

        [Test]
        public void CacheHit_ReturnssamInstance_WithoutReloading()
        {
            // First load
            Assert.IsTrue(testNode.TryGetSetting(out ScriptableObject first));
            Assert.AreEqual(1, mockLoader.LoadCallCount);
            
            // Second load - should hit cache
            Assert.IsTrue(testNode.TryGetSetting(out ScriptableObject second));
            Assert.AreEqual(1, mockLoader.LoadCallCount); // No additional load
            
            // Should be exact same instance
            Assert.AreSame(first, second);
        }

        [Test]
        public void CacheHit_GenericMethod_ReturnsSameInstance()
        {
            // First load with generic method
            Assert.IsTrue(testNode.TryGetSetting<TestAudioSettings>(out var first));
            Assert.AreEqual(1, mockLoader.LoadCallCount);
            
            // Second load - should hit cache
            Assert.IsTrue(testNode.TryGetSetting<TestAudioSettings>(out var second));
            Assert.AreEqual(1, mockLoader.LoadCallCount); // No additional load
            
            // Should be exact same instance
            Assert.AreSame(first, second);
            
            // Values should match
            Assert.AreEqual(first.volume, second.volume);
            Assert.AreEqual(first.muted, second.muted);
        }

        [Test]
        public void CacheMiss_WhenAssetNotFound_ReloadsEachTime()
        {
            // Setup node without asset
            var emptyNode = CreateTestNode("EmptyNode", typeof(TestSettings));
            
            // First attempt
            Assert.IsFalse(emptyNode.TryGetSetting(out ScriptableObject first));
            Assert.IsNull(first);
            Assert.AreEqual(1, mockLoader.LoadCallCount);
            
            // Second attempt - no cache, so loader called again
            Assert.IsFalse(emptyNode.TryGetSetting(out ScriptableObject second));
            Assert.IsNull(second);
            Assert.AreEqual(2, mockLoader.LoadCallCount);
        }

        [Test]
        [UnityEngine.TestTools.UnityPlatform(exclude = new[] { RuntimePlatform.WindowsEditor, RuntimePlatform.OSXEditor, RuntimePlatform.LinuxEditor })]
        public void CacheMiss_AfterWeakReferenceCleared_ReloadsAsset()
        {
            // Note: This test may fail in Unity Editor due to Unity keeping internal references
            // Load and verify cached
            Assert.IsTrue(testNode.TryGetSetting(out ScriptableObject loaded));
            Assert.AreEqual(1, mockLoader.LoadCallCount);
            
            // Clear references and force GC
            loaded = null;
            testAsset = null;
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            // Next load should miss cache and reload
            Assert.IsTrue(testNode.TryGetSetting(out loaded));
            Assert.AreEqual(2, mockLoader.LoadCallCount);
        }

        #endregion

        #region Type Mismatch Tests

        [Test]
        public void TypeMismatch_Generic_ReturnsFalse_WithError()
        {
            // Expect the error log
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error, "Trying to get setting of wrong type TestNode(ScriptableSettings.Tests.TestAudioSettings) by TestGameSettings !");
            
            // Try to get as wrong type
            Assert.IsFalse(testNode.TryGetSetting<TestGameSettings>(out var settings));
            Assert.IsNull(settings);
            
            // Should not have loaded from loader
            Assert.AreEqual(0, mockLoader.LoadCallCount);
        }

        [Test]
        public void TypeMismatch_DoesNotAffectCache()
        {
            // First load correct type
            Assert.IsTrue(testNode.TryGetSetting<TestAudioSettings>(out var audio));
            Assert.AreEqual(1, mockLoader.LoadCallCount);
            
            // Expect the error log for wrong type
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error, "Trying to get setting of wrong type TestNode(ScriptableSettings.Tests.TestAudioSettings) by TestGameSettings !");
            
            // Try wrong type - should fail but not clear cache
            Assert.IsFalse(testNode.TryGetSetting<TestGameSettings>(out var game));
            Assert.IsNull(game);
            
            // Original type should still work from cache
            Assert.IsTrue(testNode.TryGetSetting<TestAudioSettings>(out var audio2));
            Assert.AreSame(audio, audio2);
            Assert.AreEqual(1, mockLoader.LoadCallCount); // No additional load
        }

        [Test]
        public void TypeHierarchy_DerivedType_WorksCorrectly()
        {
            // TestAudioSettings derives from TestSettings
            // Should be able to get as base type
            Assert.IsTrue(testNode.TryGetSetting<TestSettings>(out var baseSettings));
            Assert.IsNotNull(baseSettings);
            Assert.IsInstanceOf<TestAudioSettings>(baseSettings);
            
            // Should also work as derived type
            Assert.IsTrue(testNode.TryGetSetting<TestAudioSettings>(out var derivedSettings));
            Assert.AreSame(baseSettings, derivedSettings);
            
            // Only one load should have occurred
            Assert.AreEqual(1, mockLoader.LoadCallCount);
        }

        #endregion

        #region Cache Clearing and Invalidation Tests

        [Test]
        public void Cache_ClearedWhenAssetDestroyed()
        {
            // Load and cache
            Assert.IsTrue(testNode.TryGetSetting(out ScriptableObject loaded));
            Assert.IsNotNull(testNode.Asset);
            
            // Destroy the asset
            UnityEngine.Object.DestroyImmediate(loaded);
            
            // Cache should detect destroyed object (Unity's null check)
            // Unity objects can be "fake null" after destruction
            Assert.IsTrue(testNode.Asset == null);
            
            // Next load should use loader
            mockLoader.ReturnNullOnLoad = true; // Simulate asset no longer available
            Assert.IsFalse(testNode.TryGetSetting(out loaded));
            Assert.AreEqual(2, mockLoader.LoadCallCount);
        }

        [Test]
        public void Cache_HandlesNullWeakReference()
        {
            // Create node and immediately try to get setting without any setup
            var newNode = new SettingNode("NewNode", typeof(TestSettings), Guid.NewGuid(), mockLoader);
            
            // Should handle null cache gracefully
            Assert.IsNull(newNode.Asset);
            Assert.IsFalse(newNode.TryGetSetting(out ScriptableObject loaded));
            Assert.IsNull(loaded);
        }

        [Test]
        public void MultipleNodes_MaintainSeparateCaches()
        {
            // Create second node with different asset
            var node2 = CreateTestNode("Node2", typeof(TestGameSettings));
            var asset2 = CreateTestAsset<TestGameSettings>("TestGameAsset");
            asset2.difficulty = 5;
            mockLoader.SetupAsset(mockLoader.NodeLoadPath(node2), asset2);
            
            // Load both
            Assert.IsTrue(testNode.TryGetSetting(out ScriptableObject loaded1));
            Assert.IsTrue(node2.TryGetSetting(out ScriptableObject loaded2));
            
            // Verify different instances
            Assert.AreNotSame(loaded1, loaded2);
            Assert.IsInstanceOf<TestAudioSettings>(loaded1);
            Assert.IsInstanceOf<TestGameSettings>(loaded2);
            
            // Clear one cache shouldn't affect other
            loaded1 = null;
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            // Node2 should still have cache
            Assert.IsNotNull(node2.Asset);
            Assert.IsTrue(node2.TryGetSetting(out loaded2));
            Assert.AreEqual(2, mockLoader.LoadCallCount); // Only one reload for node1
        }

        #endregion

        #region Async Loading Tests

        [Test]
        public async System.Threading.Tasks.Task AsyncLoad_UsesCacheOnSubsequentCalls()
        {
            // First async load
            var first = await testNode.LoadAsync();
            Assert.IsNotNull(first);
            Assert.AreEqual(1, mockLoader.LoadCallCount);
            
            // Second async load - should use cache
            var second = await testNode.LoadAsync();
            Assert.AreSame(first, second);
            Assert.AreEqual(1, mockLoader.LoadCallCount); // No additional load
            
            // Sync load should also use same cache
            Assert.IsTrue(testNode.TryGetSetting(out ScriptableObject third));
            Assert.AreSame(first, third);
            Assert.AreEqual(1, mockLoader.LoadCallCount); // Still no additional load
        }

        [Test]
        public async System.Threading.Tasks.Task AsyncLoad_HandlesNullCache()
        {
            // Setup loader to return null
            var nullNode = CreateTestNode("NullNode", typeof(TestSettings));
            
            var result = await nullNode.LoadAsync();
            Assert.IsNull(result);
            Assert.IsNull(nullNode.Asset);
        }

        #endregion

        #region ISettingsObject Integration Tests

        [Test]
        public void Cache_CallsOnLoaded_WhenImplemented()
        {
            // Create a node for tracking asset
            var trackingNode = CreateTestNode("TrackingNode", typeof(TrackingSettingsObject));
            
            // Create a mock settings object that tracks OnLoaded calls
            var trackingAsset = CreateTestAsset<TrackingSettingsObject>("TrackingAsset");
            mockLoader.SetupAsset(mockLoader.NodeLoadPath(trackingNode), trackingAsset);
            
            // Load should trigger OnLoaded
            Assert.IsTrue(trackingNode.TryGetSetting(out _));
            Assert.IsTrue(trackingAsset.OnLoadedCalled);
            Assert.AreEqual(trackingNode, trackingAsset.LoadedNode);
            
            // Second load from cache should not trigger OnLoaded again
            trackingAsset.OnLoadedCalled = false;
            Assert.IsTrue(trackingNode.TryGetSetting(out _));
            Assert.IsFalse(trackingAsset.OnLoadedCalled);
        }

        #endregion

        #region Performance and Stress Tests

        [Test]
        public void Cache_Performance_MultipleRapidAccess()
        {
            // Measure performance of cached vs non-cached access
            const int iterations = 1000;
            
            // First load to populate cache
            Assert.IsTrue(testNode.TryGetSetting(out ScriptableObject _));
            
            // Rapid access test - all should hit cache
            var startTime = DateTime.Now;
            for (int i = 0; i < iterations; i++)
            {
                Assert.IsTrue(testNode.TryGetSetting(out ScriptableObject loaded));
                Assert.IsNotNull(loaded);
            }
            var cacheTime = (DateTime.Now - startTime).TotalMilliseconds;
            
            // Should only have loaded once
            Assert.AreEqual(1, mockLoader.LoadCallCount);
            
            // Cache access should be very fast (less than 1ms per access on average)
            Assert.Less(cacheTime / iterations, 1.0);
        }

        [Test]
        [UnityEngine.TestTools.UnityPlatform(exclude = new[] { RuntimePlatform.WindowsEditor, RuntimePlatform.OSXEditor, RuntimePlatform.LinuxEditor })]
        public void Cache_StressTest_ManyNodesWithGC()
        {
            // Note: This test may fail in Unity Editor due to Unity keeping internal references
            var nodes = new List<SettingNode>();
            var assets = new List<ScriptableObject>();
            
            // Create many nodes
            for (int i = 0; i < 100; i++)
            {
                var node = CreateTestNode($"Node{i}", typeof(TestSettings));
                var asset = CreateTestAsset<TestSettings>($"Asset{i}");
                mockLoader.SetupAsset(mockLoader.NodeLoadPath(node), asset);
                
                nodes.Add(node);
                assets.Add(asset);
                
                // Load to populate cache
                Assert.IsTrue(node.TryGetSetting(out _));
            }
            
            // Clear half the references
            for (int i = 0; i < 50; i++)
            {
                assets[i] = null;
            }
            
            // Force GC
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            // Check cache state
            for (int i = 0; i < 100; i++)
            {
                if (i < 50)
                {
                    // Should have been collected
                    Assert.IsNull(nodes[i].Asset);
                }
                else
                {
                    // Should still be cached
                    Assert.IsNotNull(nodes[i].Asset);
                }
            }
        }

        #endregion

        // Helper class for testing ISettingsObject
        private class TrackingSettingsObject : ScriptableObject, ISettingsObject
        {
            public bool OnLoadedCalled { get; set; }
            public SettingNode LoadedNode { get; private set; }
            
            public void OnCreated()
            {
                // Not used in cache tests
            }
            
            public void OnLoaded(SettingNode node)
            {
                OnLoadedCalled = true;
                LoadedNode = node;
            }
            
            public void OnUnloaded(SettingNode node)
            {
                // Not used in cache tests
            }
        }
    }
}