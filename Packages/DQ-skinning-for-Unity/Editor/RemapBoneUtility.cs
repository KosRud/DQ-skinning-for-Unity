// 2023-02-07 BeXide by Y.Hayashi

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

/// <summary>
/// モデルPrefab（FBXから派生したPrefab）のSkinnedMeshRendererのボーンが狂ったときにリセットする
/// ユーティリティ。対象のPrefabを開き、修正したいメッシュオブジェクトを選択した状態で実行。
/// </summary>
public class RemapBoneUtility
{
    [MenuItem("BeXide/SkinnedMeshRenderer/Remap bones")]
    private static void RemapBones()
    {
        var skinnedMeshRenderers = Selection.gameObjects
            .Select(go => go.GetComponent<SkinnedMeshRenderer>())
            .Where(smr => smr != null)
            .ToList();

        if (skinnedMeshRenderers.Count <= 0)
        {
            Debug.LogWarning("RemapBones:no SkinnedMeshRenderer selection. skip.");
            return;
        }

        foreach (var smr in skinnedMeshRenderers)
        {
            RemapBonesInSkinnedMeshRenderer(smr);
        }
    }

    private static void RemapBonesInSkinnedMeshRenderer(SkinnedMeshRenderer smr)
    {
        var mesh = smr.sharedMesh;
        Debug.Log($"RemapBones:mesh=[{mesh}]");

        // search original SkinnedMeshRenderer
        string assetPath = AssetDatabase.GetAssetPath(mesh);
        Debug.Log($"  assetPath=[{assetPath}]");
        var originalAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        var originalSkinnedMeshRenderer = originalAssets
            .OfType<GameObject>()
            .Select(a => a.GetComponent<SkinnedMeshRenderer>())
            .Where(r => r != null)
            .FirstOrDefault(r => r.sharedMesh == mesh);

        if (originalSkinnedMeshRenderer == null)
        {
            UnityEngine.Debug.LogError("RemapBones:cannot find original data.");
            return;
        }
        
        // reset bones
        var root  = smr.transform.root;
        smr.bones = originalSkinnedMeshRenderer.bones
            .Select(bone => FindDeepChild(root, bone.name))
            .ToArray();

        EditorUtility.SetDirty(smr);
    }
    
    private static Transform FindDeepChild(Transform aParent, string aName)
    {
        foreach (Transform child in aParent)
        {
            if (child.name == aName)
                return child;
            var result = FindDeepChild(child, aName);
            if (result != null)
                return result;
        }
        return null;
    }

}
