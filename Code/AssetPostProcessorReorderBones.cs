using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

#if UNITY_EDITOR

/// <summary>
/// Sorts bone indexes in imported meshes.<br>
/// SkinnedMeshRenderer requires bone indexes to be sorted based on hierarchy.
/// </summary>
public class AssetPostProcessorReorderBones : AssetPostprocessor
{
	void OnPostprocessModel(GameObject g)
	{
        this.Process(g);
	}

	void Process(GameObject g)
	{
		SkinnedMeshRenderer smr = g.GetComponentInChildren<SkinnedMeshRenderer>();
		if (smr == null)
		{
			Debug.LogWarning("Unable to find Renderer" + smr.name);
			return;
		}

		//list of bones
		List<Transform> boneTransforms = smr.bones.ToList();

		//sort based on hierarchy
		boneTransforms.Sort(CompareTransform);

		//record bone index mappings (richardf advice)
		//build a Dictionary<int, int> that records the old bone index => new bone index mappings,
		//then run through every vertex and just do boneIndexN = dict[boneIndexN] for each weight on each vertex.
		var remap = new Dictionary<int, int>();
		for (int i = 0; i < smr.bones.Length; i++)
		{
			remap[i] = boneTransforms.IndexOf(smr.bones[i]);
		}

		//remap bone weight indexes
		BoneWeight[] bw = smr.sharedMesh.boneWeights;
		for (int i = 0; i < bw.Length; i++)
		{
			bw[i].boneIndex0 = remap[bw[i].boneIndex0];
			bw[i].boneIndex1 = remap[bw[i].boneIndex1];
			bw[i].boneIndex2 = remap[bw[i].boneIndex2];
			bw[i].boneIndex3 = remap[bw[i].boneIndex3];
		}

		//remap bindposes
		var bp = new Matrix4x4[smr.sharedMesh.bindposes.Length];
		for (int i = 0; i < bp.Length; i++)
		{
			bp[remap[i]] = smr.sharedMesh.bindposes[i];
		}

		//assign new data
		smr.bones = boneTransforms.ToArray();
		smr.sharedMesh.boneWeights = bw;
		smr.sharedMesh.bindposes = bp;
	}

	private static int CompareTransform(Transform A, Transform B)
	{
		if (B.IsChildOf(A))
        {
            return -1;
        }

        if (A.IsChildOf(B))
        {
            return -1;
        }

        return 0;
	}
}

#endif