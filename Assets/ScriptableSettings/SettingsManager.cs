using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DefaultNamespace;
using Scriptable.Settings.Editor;
using ScriptableSettings;
using SharpCompress;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using System.IO;
#endif

namespace ScriptableSettings
{
    [CreateAssetMenu(fileName = "SettingsManager", menuName = "Settings/Settings Manager")]
    public partial class SettingsManager : ScriptableObject, ISerializationCallbackReceiver
    {
        [SerializeField] private List<SettingNodeData> serializedNodes = new List<SettingNodeData>();

        [SerializeField] private SettingLoaderFactory loaderFactory;

        [NonSerialized] private List<SettingNode> roots;

        // Non-Serialized fields - same as before
        [NonSerialized] private Dictionary<Guid, SettingNode> nodeIndex;
        [NonSerialized] private ISettingLoader loader;
        [NonSerialized] private bool indexBuilt = false;

        // Properties and Core Methods - same as before
        public IReadOnlyList<SettingNode> SettingTree => roots;
        public ISettingLoader Loader => loader ??= loaderFactory.CreateSettingLoader();

        [NonSerialized] private bool indexChanged = false;

        private void BuildIndexAndParentsIfNeeded()
        {
            // Avoid rebuilding redundantly if OnEnable is called multiple times
            if (indexBuilt && nodeIndex != null) return;

            //Debug.Log("Building SettingsManager Index and Parent references...");
            nodeIndex = new Dictionary<Guid, SettingNode>();
            Queue<SettingNode> nodesToProcess = new Queue<SettingNode>();

            // Initialize processing with root nodes
            foreach (var root in roots)
            {
                // Explicitly set root parents to null
                nodesToProcess.Enqueue(root);
            }

            // Process nodes iteratively to set parents and build index
            while (nodesToProcess.Count > 0)
            {
                var node = nodesToProcess.Dequeue();

                // Index the node
                if (node.Guid != Guid.Empty)
                {
                    if (!nodeIndex.TryAdd(node.Guid, node))
                    {
                        Debug.LogWarning(
                            $"Duplicate GUID detected in SettingsManager tree: {node.Guid} for node '{node.Name}'. The first node encountered was kept in the index.");
                        // Optionally decide how to handle duplicates - skip, overwrite, log error?
                    }
                }
                else
                {
                    Debug.LogError(
                        $"SettingNode '{node.Name}' has an invalid or empty GUID during index build. It cannot be indexed or reliably loaded.");
                }


                // Set parent references for children and enqueue them
                foreach (var child in node.Children)
                {
                    nodesToProcess.Enqueue(child);
                }
            }

            indexBuilt = indexChanged = true;
            //Debug.Log($"SettingsManager Index built. {nodeIndex.Count} nodes indexed.");
        }


        public T GetSettings<T>(ScriptableSettingId<T> settingId) where T : ScriptableObject
        {
            return LoadNode<T>(GetNodeById(settingId.Id));
        }

        public IEnumerable<T> GetAllSettings<T>() where T : ScriptableObject
        {
            return GetNodesOfType<T>().Select(LoadNode<T>);
        }

        public SettingNode GetNodeById(Guid settingId)
        {
            BuildIndexAndParentsIfNeeded(); // Ensure index is ready
            nodeIndex.TryGetValue(settingId, out SettingNode node);
            return node;
        }

        // Typed enumeration - Constraint changed to ScriptableObject
        public IEnumerable<SettingNode> GetNodesOfType<T>() where T : ScriptableObject // Constraint Changed
        {
            return GetNodesOfType(typeof(T));
        }

        public IEnumerable<SettingNode> GetNodesOfType(Type targetType) // Constraint Changed
        {
            BuildIndexAndParentsIfNeeded();
            foreach (var node in nodeIndex.Values)
            {
                // Check if the node's stored type is assignable to the requested type T
                if (node.SettingType != null && targetType.IsAssignableFrom(node.SettingType))
                {
                    yield return node;
                }
            }
        }

        // Load Node - Return type changed to ScriptableObject
        public T LoadNode<T>(SettingNode node) where T : ScriptableObject // Return type Changed
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            if (loader == null) throw new InvalidOperationException("SettingsManager Loader has not been initialized.");
//        if (node.SettingType != typeof(T)) throw new ArgumentException($"Expecting type {node.Type} for {node.Name} but trying to load {typeof(T).Name}");

            BuildIndexAndParentsIfNeeded();

            // TryGetSetting now returns ScriptableObject
            var setting = node.TryGetSetting(out var so) ? so : null;

            return setting as T;
        }

        // Load Node Async - Return type changed to Task<ScriptableObject>
        public async Task<T> LoadNodeAsync<T>(SettingNode node) where T : ScriptableObject // Return type Changed
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            if (loader == null) throw new InvalidOperationException("SettingsManager Loader has not been initialized.");
            //if (node.SettingType != typeof(T)) throw new ArgumentException($"Expecting type {node.Type} for {node.Name} but trying to load {typeof(T).Name}");
            BuildIndexAndParentsIfNeeded();

            // LoadAsync now returns Task<ScriptableObject>
            var setting = await node.LoadAsync();
            return setting as T;
        }

        public void OnBeforeSerialize()
        {
            if (indexChanged == false) return;
            // Flatten your runtime graph back into DTOs
            serializedNodes.Clear();
            if (nodeIndex == null) return;

            foreach (var node in nodeIndex.Values)
            {
                serializedNodes.Add(new SettingNodeData
                {
                    i = ShortGuid.Encode(node.Guid),
                    n = node.Name,
                    t = node.SettingType.AssemblyQualifiedName,
                    ch = node.Children.Select(c => ShortGuid.Encode(c.Guid)).ToList()
                });
            }

            indexChanged = false;
        }

        public void OnAfterDeserialize()
        {
            // Rebuild runtime graph from the flat DTO list
            var dataWithId = serializedNodes
                .Select(x => (id: ShortGuid.Decode(x.i), data: x)).ToList();

            nodeIndex = dataWithId
                .ToDictionary(
                    d => d.id,
                    d => new SettingNode(d.data.n, ConstructType(d.data), d.id, Loader)
                );

            foreach (var data in dataWithId)
            {
                var parentNode = nodeIndex[data.id];
                foreach (var childId in data.data.ch)
                {
                    var childNode = nodeIndex[ShortGuid.Decode(childId)];
                    parentNode.AddChild(childNode);
                }
            }

            roots = nodeIndex.Values.Where(x => x.Parent == null).ToList();
        }

        private Type ConstructType(SettingNodeData data)
        {
            if (!string.IsNullOrEmpty(data.t))
            {
                var type = Type.GetType(data.t);
                if (type == null)
                {
                    // Log clearly that the type couldn't be found AT LOAD TIME
                    Debug.LogError(
                        $"SettingNode '{name}' (GUID: {ShortGuid.Decode(data.i)}): Could not find Type '{data.t}'. The class may have been renamed, moved, or deleted. Run 'Validate & Fix Node Types' on the SettingsManager asset.");
                }

                return type;
                // ... (check if ScriptableObject) ...
            }
            else
            {
                Debug.LogWarning(
                    $"SettingNode '{name}' (GUID: {ShortGuid.Decode(data.i)}) has missing or empty typeName during deserialization.");
            }

            return null;
        }


        // --- Editor-Only CRUD Operations ---
#if UNITY_EDITOR
        // Create Node - Constraint changed, removed InitializeGUID call

        

#endif // UNITY_EDITOR

    }
}