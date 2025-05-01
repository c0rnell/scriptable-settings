
#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ScriptableSettings
{
    public partial class SettingsManager
    {
        public SettingLoaderFactory LoaderFactory
        {
            get => loaderFactory; set => loaderFactory = value; 
        }
        
        public SettingManagerTool[] Tools;

        public string SettingsPath = "Assets/ScriptableSettings"; // Default path for root nodes
        public string DefaultPath => $"{SettingsPath}/Resources/Settings"; // Default path for root nodes
        public string TrashPath => $"{SettingsPath}/Trash"; // Default path for root nodes

        public IReadOnlyCollection<SettingNode> GetAllNodes()
        {
            BuildIndexAndParentsIfNeeded();
            return nodeIndex.Values.ToList().AsReadOnly();
        }

        public SettingNode CreateNode(SettingNode parent, string name, Type type)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                Debug.LogError("Cannot create SettingNode with an empty name.");
                return null;
            }

            // 1. Determine folder path for the new asset
            var folderPath = GetParentBasedFolder(parent);

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
            if (string.IsNullOrEmpty(assetGuidString))
            {
                /* ... error log, cleanup ... */
                return null;
            }

            var assetGuid = new Guid(assetGuidString);

            if (asset is ISettingsObject settingsObject)
            {
                settingsObject.OnCreated(); // Call OnCreated if applicable
            }

            // Ensure asset name matches node name if desired, or keep them separate
            EditorUtility.SetDirty(asset); // Mark asset dirty just in case

            // 6. Create the SettingNode (stores name, type T, and assetGuid)
            var node = new SettingNode(name, type, assetGuid, loader);

            // 7. Insert node into the tree
            if (parent != null)
            {
                parent.AddChild(node);
            }
            else
            {
                roots.Add(node);
            }

            indexBuilt = false;
            BuildIndexAndParentsIfNeeded();

            EditorUtility.SetDirty(this); // Mark manager dirty


            // 8. Rebuild index
            //BuildIndexAndParentsIfNeeded();

            // 9. Save changes
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"Created Setting Node '{node.Name}' linked to asset '{asset.name}' ({node.Guid}) under parent '{(parent?.Name ?? "Root")}'.",
                this);
            return node;
        }

        private string GetParentBasedFolder(SettingNode parent)
        {
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
                folderPath = DefaultPath; // Example path
                if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);
            }

            return folderPath;
        }

        public SettingNode CreateNode<T>(SettingNode parent, string name)
            where T : ScriptableObject // Constraint Changed
        {
            return CreateNode(parent, name, typeof(T));
        }

        public void DeleteNode(SettingNode node, bool deleteAsset = true)
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
                Debug.LogWarning(
                    $"Node '{node.Name}' not found in parent '{(node.Parent?.Name ?? "Root")}' or roots list during deletion.",
                    this);
            }

            // 3. Delete the asset file
            if (!string.IsNullOrEmpty(path))
            {
                if (deleteAsset)
                {
                    AssetDatabase.DeleteAsset(path);
                    Debug.Log($"Deleted asset at path: {path}", this);

                    var parentAssetFolderPath = path.Replace(".asset", "");
                    if (Directory.Exists(parentAssetFolderPath) &&
                        Directory.GetFiles(parentAssetFolderPath).Length == 0)
                    {
                        AssetDatabase.DeleteAsset(parentAssetFolderPath);
                        Debug.Log($"Deleted empty directory: {parentAssetFolderPath}", this);
                    }
                }
                else
                {
                    if (Directory.Exists(TrashPath) == false)
                        Directory.CreateDirectory(TrashPath);
                    AssetDatabase.MoveAsset(path, TrashPath + $"/{Path.GetFileName(path)}");
                }
            }
            else if (deleteAsset)
            {
                Debug.LogWarning(
                    $"Could not find asset path for GUID {guidString} (Node: '{node.Name}'). Node reference removed, but asset file might remain.",
                    this);
            }

            indexBuilt = false;
            BuildIndexAndParentsIfNeeded();

            // 4. Mark manager dirty and rebuild index
            EditorUtility.SetDirty(this);

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
                Debug.LogError($"Cannot move node '{node.Name}' under '{newParent.Name}' as it would create a cycle.",
                    this);
                return;
            }


            SettingNode oldParent = node.Parent;

            // 1. Determine destination folder path
            string destFolderPath = GetParentBasedFolder(newParent);

            // 2. Get current asset path and file name
            string nodeGuidString = node.Guid.ToString("N");
            var oldPath = AssetDatabase.GUIDToAssetPath(nodeGuidString);
            if (string.IsNullOrEmpty(oldPath))
            {
                Debug.LogError($"Cannot find asset path for node '{node.Name}' ({nodeGuidString}). Aborting move.",
                    this);
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
            }
            else
            {
                roots.Add(node);
            }

            indexBuilt = false;
            BuildIndexAndParentsIfNeeded();

            // 5. Mark manager dirty, rebuild index, save
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"Moved Node '{node.Name}' from parent '{(oldParent?.Name ?? "Root")}' to '{(newParent?.Name ?? "Root")}'.",
                this);
        }

        public void RenameNode(SettingNode node, string newName)
        {
            if (node == null)
            {
                Debug.LogError("Cannot rename a null node.");
                return;
            }

            if (string.IsNullOrWhiteSpace(newName))
            {
                Debug.LogError("New name must not be empty.");
                return;
            }

            // Sanitize for valid filename
            string sanitizedName = MakeValidFileName(newName);

            // Get current asset path from GUID
            string guidString = node.Guid.ToString("N");
            string assetPath = AssetDatabase.GUIDToAssetPath(guidString);
            if (string.IsNullOrEmpty(assetPath))
            {
                Debug.LogError($"Could not find asset path for node '{{node.Name}}' (GUID: {guidString}).");
                return;
            }

            // Rename the asset file on disk
            string error = AssetDatabase.RenameAsset(assetPath, sanitizedName);
            if (!string.IsNullOrEmpty(error))
            {
                Debug.LogError($"Failed to rename asset file from '{{assetPath}}' to '{{sanitizedName}}': {error}");
                return;
            }

            // Update the in-memory asset name
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
            if (asset != null)
            {
                asset.name = sanitizedName;
                EditorUtility.SetDirty(asset);
            }

            // Update Node Folder
            string folderPath = assetPath.Replace(".asset", "");
            if (Directory.Exists(folderPath))
            {
                AssetDatabase.RenameAsset(folderPath, sanitizedName.Replace(".asset", ""));
            }

            // Update the node name
            node.Rename(newName);
            indexChanged = true;

            // Mark SettingsManager dirty and save
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Renamed SettingNode '{{node.Name}}' and associated asset to '{{sanitizedName}}'.", this);
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
            string invalidChars =
                System.Text.RegularExpressions.Regex.Escape(new string(Path.GetInvalidFileNameChars()));
            string invalidRegStr = string.Format(@"([{0}]*\.+$)|([{0}]+)", invalidChars);
            return System.Text.RegularExpressions.Regex.Replace(name, invalidRegStr, "_");
        }
    }
}

#endif