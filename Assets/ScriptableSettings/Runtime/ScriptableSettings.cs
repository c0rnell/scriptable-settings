using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace Scriptable.Settings
{
    [CreateAssetMenu(fileName = "SettingsManager", menuName = "Settings/Settings Manager")]
    public partial class ScriptableSettings : ScriptableObject, ISerializationCallbackReceiver
    {
        [SerializeField] internal List<SettingNodeData> serializedNodes = new List<SettingNodeData>();

        [SerializeField] private SettingLoaderFactory loaderFactory;

        [NonSerialized] internal List<SettingNode> roots;

        // Non-Serialized fields - same as before
        [NonSerialized] internal Dictionary<Guid, SettingNode> nodeIndex;
        [NonSerialized] internal Dictionary<int, SettingNode> nodeNameTypeIndex;
        [NonSerialized] internal ISettingLoader loader;
        [NonSerialized] internal bool indexBuilt = false;

        // Properties and Core Methods - same as before
        public IReadOnlyList<SettingNode> SettingTree => roots;
        public ISettingLoader Loader => loader ??= loaderFactory.CreateSettingLoader();

        [NonSerialized] internal bool indexChanged = false;

        private void BuildIndexAndParentsIfNeeded()
        {
            // Avoid rebuilding redundantly if OnEnable is called multiple times
            if (indexBuilt && (nodeIndex != null && nodeNameTypeIndex != null)) return;

            //Debug.Log("Building SettingsManager Index and Parent references...");
            nodeIndex = new Dictionary<Guid, SettingNode>();
            nodeNameTypeIndex = new Dictionary<int, SettingNode>();
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
                    if (nodeIndex.TryAdd(node.Guid, node))
                    {
                        nodeNameTypeIndex.Add(GetNodeHash(node.Name, node.SettingType), node);
                    }
                    else
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

        private static int GetNodeHash(string nodeName, Type settingType)
        {
            // Use a combination of name and type to create a unique hash
            return (nodeName: nodeName.GetHashCode(), settingType: settingType.GetHashCode()).GetHashCode();
        }

        public SettingNode GetNodeById(Guid settingId)
        {
            BuildIndexAndParentsIfNeeded(); // Ensure index is ready
            nodeIndex.TryGetValue(settingId, out SettingNode node);
            return node;
        }

        public SettingNode GetNodeById<T>(ScriptableSettingId<T> settingId) where T : ScriptableObject
        {
            return GetNodeById(settingId.Id);
        }
        
        internal SettingNode GetNodeByInstance<T>(T settingId) where T : ScriptableObject
        {
            var nodeHash = GetNodeHash(settingId.name, settingId.GetType());
            BuildIndexAndParentsIfNeeded(); // Ensure index is ready
            return nodeNameTypeIndex.GetValueOrDefault(nodeHash);
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
            
            if (nodeIndex == null)
            {
                serializedNodes.Clear();
                return;
            }

            var serialisedNodesData = new List<SettingNodeData>();
            foreach (var node in nodeIndex.Values)
            {
                if (node.IsValid)
                {
                    serialisedNodesData.Add(new SettingNodeData
                    {
                        i = ShortGuid.Encode(node.Guid),
                        n = node.Name,
                        t = node.SettingType.AssemblyQualifiedName,
                        ch = node.Children.Select(c => ShortGuid.Encode(c.Guid)).ToList()
                    });
                }
                else
                {
                    if (node.TryGetSetting(out _))
                    {
                        var previousSerialization = serializedNodes.FirstOrDefault(x => x.i == ShortGuid.Encode(node.Guid));
                        if (previousSerialization != null)
                            serialisedNodesData.Add(previousSerialization);
                    }
                }
            }
            
            serializedNodes = serialisedNodesData;

            indexChanged = false;
        }

        public void OnAfterDeserialize()
        {
            // Initialize roots if null
            if (roots == null)
                roots = new List<SettingNode>();
            
            // Clear existing data
            roots.Clear();
            
            // Initialize nodeIndex if null
            if (nodeIndex == null)
                nodeIndex = new Dictionary<Guid, SettingNode>();
            else
                nodeIndex.Clear();
            
            // Handle empty serialized data
            if (serializedNodes == null || serializedNodes.Count == 0)
            {
                indexBuilt = true;
                return;
            }

            // Rebuild runtime graph from the flat DTO list
            var dataWithId = serializedNodes
                .Where(x => !string.IsNullOrEmpty(x.i))
                .Select(x => 
                {
                    try 
                    {
                        return (id: ShortGuid.Decode(x.i), data: x, valid: true);
                    }
                    catch
                    {
                        Debug.LogError($"Invalid ShortGuid format: '{x.i}'");
                        return (id: Guid.Empty, data: x, valid: false);
                    }
                })
                .Where(x => x.valid && x.id != Guid.Empty)
                .ToList();

            // Create nodes first
            foreach (var item in dataWithId)
            {
                var node = new SettingNode(item.data.n, ConstructType(item.data), item.id, Loader);
                if (nodeIndex.TryAdd(item.id, node) == false)
                {
                    Debug.LogWarning($"Duplicate GUID {item.id} found during deserialization");
                }
            }

            // Then establish parent-child relationships
            foreach (var data in dataWithId)
            {
                if (nodeIndex.TryGetValue(data.id, out var parentNode))
                {
                    foreach (var childId in data.data.ch)
                    {
                        if (!string.IsNullOrEmpty(childId))
                        {
                            try
                            {
                                var childGuid = ShortGuid.Decode(childId);
                                if (nodeIndex.TryGetValue(childGuid, out var childNode))
                                {
                                    parentNode.AddChild(childNode);
                                }
                            }
                            catch
                            {
                                Debug.LogError($"Invalid child ShortGuid format: '{childId}'");
                            }
                        }
                    }
                }
            }
            
            // Find root nodes
            roots = nodeIndex.Values.Where(x => x.Parent == null).ToList();
            
            // Mark index as built
            indexBuilt = true;
        }

        private Type ConstructType(SettingNodeData data)
        {
            if (!string.IsNullOrEmpty(data.t))
            {
                try
                {
                    var type = Type.GetType(data.t);
                    if (type == null)
                    {
                        Debug.LogError($"SettingNode '{data.n}' (GUID: {data.i}): Could not find Type '{data.t}'. The class may have been renamed, moved, or deleted. Run 'Validate & Fix Node Types' on the SettingsManager asset.");
                    }

                    return type;
                }
                catch (Exception)
                {
                    Debug.LogError($"SettingNode '{data.n}' (GUID: {data.i}): Failed to load type '{data.t}'. The class may have been renamed, moved, or deleted. Run 'Validate & Fix Node Types' on the SettingsManager asset.");
                    return null;
                }
            }
            else
            {
                Debug.LogWarning(
                    $"SettingNode '{data.n}' (GUID: {ShortGuid.Decode(data.i)}) has missing or empty typeName during deserialization.");
            }

            return null;
        }


        // --- Editor-Only CRUD Operations ---
#if UNITY_EDITOR
        // Create Node - Constraint changed, removed InitializeGUID call

        

#endif // UNITY_EDITOR

    }
}