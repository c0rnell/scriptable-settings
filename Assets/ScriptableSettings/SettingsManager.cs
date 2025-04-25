using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ScriptableSettings;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using System.IO;
#endif

[CreateAssetMenu(fileName = "SettingsManager", menuName = "Settings/Settings Manager")]
public class SettingsManager : ScriptableObject
{
    [SerializeField] private List<SettingNode> roots = new List<SettingNode>();

    // Non-Serialized fields - same as before
    [NonSerialized] private Dictionary<Guid, SettingNode> nodeIndex;
    [NonSerialized] private ISettingLoader loader;
    [NonSerialized] private bool indexBuilt = false;

    // Properties and Core Methods - same as before
    public IReadOnlyList<SettingNode> Roots => roots;
    public ISettingLoader Loader => loader;
    private void OnEnable() { BuildIndexAndParentsIfNeeded(); }
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
             root.Parent = null; // Explicitly set root parents to null
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
                      Debug.LogWarning($"Duplicate GUID detected in SettingsManager tree: {node.Guid} for node '{node.Name}'. The first node encountered was kept in the index.");
                      // Optionally decide how to handle duplicates - skip, overwrite, log error?
                 }
            }
             else
             {
                 Debug.LogError($"SettingNode '{node.Name}' has an invalid or empty GUID during index build. It cannot be indexed or reliably loaded.");
             }


            // Set parent references for children and enqueue them
            foreach (var child in node.Children)
            {
                 child.Parent = node;
                 nodesToProcess.Enqueue(child);
            }
        }
         indexBuilt = true;
         //Debug.Log($"SettingsManager Index built. {nodeIndex.Count} nodes indexed.");
    }

    // Get node by its GUID
     public SettingNode GetNodeByGuid(Guid guid)
     {
          BuildIndexAndParentsIfNeeded(); // Ensure index is ready
          nodeIndex.TryGetValue(guid, out SettingNode node);
          return node;
     }

     public void InitLoader(ISettingLoader loaderImpl)
     {
         if (loaderImpl == null)
             throw new ArgumentNullException(nameof(loaderImpl));
         loader = loaderImpl;
         Debug.Log($"SettingsManager initialized with Loader: {loaderImpl.GetType().Name}");
     }


    // Typed enumeration - Constraint changed to ScriptableObject
    public IEnumerable<SettingNode> GetNodesOfType<T>() where T : ScriptableObject // Constraint Changed
    {
        BuildIndexAndParentsIfNeeded();
        Type targetType = typeof(T);
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
        var setting = node.TryGetSetting(loader, out var so) ? so : null;
        
        if(setting is ISettingsObject settingsObject)
        {
            settingsObject.OnLoaded(node); // Call OnLoaded if applicable
        }

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
        var setting = await node.LoadAsync(loader);;
        
        if(setting is ISettingsObject settingsObject)
        {
            settingsObject.OnLoaded(node); // Call OnLoaded if applicable
        }

        return setting as T;
    }


    // --- Editor-Only CRUD Operations ---
#if UNITY_EDITOR
    // Create Node - Constraint changed, removed InitializeGUID call
    
    public SettingNode CreateNode(SettingNode parent, string name, Type type)
    {
         if (string.IsNullOrWhiteSpace(name))
        {
            Debug.LogError("Cannot create SettingNode with an empty name.");
            return null;
        }

        // 1. Determine folder path for the new asset
        string parentAssetFolderPath = ""; // Default path
        if (parent != null)
        {
            string parentGuidString = parent.Guid.ToString("N"); // Format for AssetDatabase
            parentAssetFolderPath = AssetDatabase.GUIDToAssetPath(parentGuidString).Replace(".asset", "");
        }

        string folderPath;
        if (!string.IsNullOrEmpty(parentAssetFolderPath))
        {
            if (!Directory.Exists(parentAssetFolderPath)) Directory.CreateDirectory(parentAssetFolderPath);
            folderPath = parentAssetFolderPath;
        }
        else
        {
            // Default location for root nodes (ensure this folder exists)
            folderPath = "Assets/Resources/Settings"; // Example path
            if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);
        }

        // Sanitize name to be a valid filename
        string sanitizedName = MakeValidFileName(name);
        if (string.IsNullOrEmpty(sanitizedName))
        {
            Debug.LogError($"Failed to create valid filename from '{name}'.");
            return null;
        }


        // 2. Create the ScriptableObject instance
        var asset = ScriptableObject.CreateInstance(type); // Use correct type T
        asset.name = sanitizedName;

        // 3. Create the asset file
        var assetPath = AssetDatabase.GenerateUniqueAssetPath($"{folderPath}/{sanitizedName}.asset");
        AssetDatabase.CreateAsset(asset, assetPath);

        // 4. Get the GUID
        var assetGuidString = AssetDatabase.AssetPathToGUID(assetPath);
        if (string.IsNullOrEmpty(assetGuidString)) { /* ... error log, cleanup ... */ return null; }
        var assetGuid = new Guid(assetGuidString);

        if(asset is ISettingsObject settingsObject)
        {
            settingsObject.OnCreated(); // Call OnCreated if applicable
        }
        
        // Ensure asset name matches node name if desired, or keep them separate
         EditorUtility.SetDirty(asset); // Mark asset dirty just in case

        // 6. Create the SettingNode (stores name, type T, and assetGuid)
        var node = new SettingNode(name, type, assetGuid);

        // 7. Insert node into the tree
         if (parent != null) { parent.AddChild(node); }
         else { roots.Add(node); }
         EditorUtility.SetDirty(this); // Mark manager dirty


        // 8. Rebuild index
        BuildIndexAndParentsIfNeeded();

        // 9. Save changes
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Created Setting Node '{node.Name}' linked to asset '{asset.name}' ({node.Guid}) under parent '{(parent?.Name ?? "Root")}'.", this);
        return node;
    }
    
    public SettingNode CreateNode<T>(SettingNode parent, string name) where T : ScriptableObject // Constraint Changed
    {
       return CreateNode(parent, name, typeof(T));
    }

    public void DeleteNode(SettingNode node)
    {
        if (node == null) return;

        // Recursively delete children first
        // Create a copy of the children list to iterate over, as we modify the original
        List<SettingNode> childrenCopy = new List<SettingNode>(node.Children);
        foreach (var child in childrenCopy)
        {
            DeleteNode(child);
        }


        // 1. Get the asset path from the node's GUID
        string guidString = node.Guid.ToString("N");
        var path = AssetDatabase.GUIDToAssetPath(guidString);


        // 2. Remove the node from its parent or the roots list
        bool removed = false;
        if (node.Parent != null)
        {
            removed = node.Parent.RemoveChild(node);
        }
        else
        {
            removed = roots.Remove(node);
        }

         if (!removed)
         {
             Debug.LogWarning($"Node '{node.Name}' not found in parent '{(node.Parent?.Name ?? "Root")}' or roots list during deletion.", this);
         }


        // 3. Delete the asset file
        if (!string.IsNullOrEmpty(path))
        {
            AssetDatabase.DeleteAsset(path);
            Debug.Log($"Deleted asset at path: {path}", this);
            
            var parentAssetFolderPath = path.Replace(".asset", "");
            if (Directory.Exists(parentAssetFolderPath) && Directory.GetFiles(parentAssetFolderPath).Length == 0)
            {
                AssetDatabase.DeleteAsset(parentAssetFolderPath);
                Debug.Log($"Deleted empty directory: {parentAssetFolderPath}", this);
            }
        }
         else {
             Debug.LogWarning($"Could not find asset path for GUID {guidString} (Node: '{node.Name}'). Node reference removed, but asset file might remain.", this);
         }

        // 4. Mark manager dirty and rebuild index
        EditorUtility.SetDirty(this);
        indexBuilt = false; // Force index rebuild on next access
        BuildIndexAndParentsIfNeeded();

        // 5. Save changes
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
         Debug.Log($"Deleted Setting Node '{node.Name}' and associated asset.", this);
    }

    // Moves a SettingNode (and its associated asset) to a new parent in the hierarchy.
    public void MoveNode(SettingNode node, SettingNode newParent)
    {
         if (node == null) return;
         if (node == newParent) return; // Cannot parent to self
         if (IsAncestor(node, newParent)) // Check for cyclic parenting
         {
              Debug.LogError($"Cannot move node '{node.Name}' under '{newParent.Name}' as it would create a cycle.", this);
              return;
         }


        SettingNode oldParent = node.Parent;

         // 1. Determine destination folder path
         string destFolderPath;
         if (newParent != null)
         {
             string parentGuidString = newParent.Guid.ToString("N");
             string parentAssetPath = AssetDatabase.GUIDToAssetPath(parentGuidString);
             if (string.IsNullOrEmpty(parentAssetPath)) {
                  Debug.LogError($"Cannot find asset path for new parent '{newParent.Name}' ({parentGuidString}). Aborting move.", this);
                  return;
             }
             destFolderPath = Path.GetDirectoryName(parentAssetPath);
         }
         else
         {
             // Moving to root - use default root location
             destFolderPath = "Assets/Resources/Settings"; // Match CreateNode logic
             if (!Directory.Exists(destFolderPath)) Directory.CreateDirectory(destFolderPath);
         }

         // 2. Get current asset path and file name
         string nodeGuidString = node.Guid.ToString("N");
         var oldPath = AssetDatabase.GUIDToAssetPath(nodeGuidString);
         if (string.IsNullOrEmpty(oldPath))
         {
             Debug.LogError($"Cannot find asset path for node '{node.Name}' ({nodeGuidString}). Aborting move.", this);
             return;
         }
         var fileName = Path.GetFileName(oldPath);
         var newPath = Path.Combine(destFolderPath, fileName);
          newPath = AssetDatabase.GenerateUniqueAssetPath(newPath); // Ensure unique path in destination


         // 3. Move the asset file if the path changes
         if (oldPath != newPath)
         {
              string moveResult = AssetDatabase.MoveAsset(oldPath, newPath);
              if (!string.IsNullOrEmpty(moveResult)) // MoveAsset returns error string on failure
              {
                  Debug.LogError($"Failed to move asset from '{oldPath}' to '{newPath}': {moveResult}", this);
                  return; // Abort if asset move failed
              }
               Debug.Log($"Moved asset from '{oldPath}' to '{newPath}'.", this);
         }


         // 4. Update the tree structure
         // Remove from old parent/roots
         if (oldParent != null)
         {
             oldParent.RemoveChild(node);
         }
         else
         {
             roots.Remove(node);
         }
         // Add to new parent/roots
         if (newParent != null)
         {
             newParent.AddChild(node);
             node.Parent = newParent; // Explicitly update parent reference
         }
         else
         {
             roots.Add(node);
             node.Parent = null; // Explicitly update parent reference
         }


        // 5. Mark manager dirty, rebuild index, save
        EditorUtility.SetDirty(this);
         indexBuilt = false; // Force index rebuild
         BuildIndexAndParentsIfNeeded();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Moved Node '{node.Name}' from parent '{(oldParent?.Name ?? "Root")}' to '{(newParent?.Name ?? "Root")}'.", this);
    }

    // Helper to check if a node is an ancestor of another (prevents cyclic moves)
    private bool IsAncestor(SettingNode potentialAncestor, SettingNode node)
    {
        if (node == null) return false;
        var current = node.Parent;
        while (current != null)
        {
            if (current == potentialAncestor) return true;
            current = current.Parent;
        }
        return false;
    }

    // Helper to create a valid file name from a string
     private static string MakeValidFileName(string name)
     {
          string invalidChars = System.Text.RegularExpressions.Regex.Escape(new string(Path.GetInvalidFileNameChars()));
          string invalidRegStr = string.Format(@"([{0}]*\.+$)|([{0}]+)", invalidChars);
          return System.Text.RegularExpressions.Regex.Replace(name, invalidRegStr, "_");
     }

#endif // UNITY_EDITOR
   
}