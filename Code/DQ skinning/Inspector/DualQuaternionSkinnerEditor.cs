using UnityEngine;
using UnityEditor;

using System.Collections.Generic;

#if UNITY_EDITOR

/// <summary>
/// Provides custom inspector for {@link DualQuaternionSkinner}
/// </summary>
[CustomEditor(typeof(DualQuaternionSkinner))]
public class DualQuaternionSkinnerEditor : Editor
{
	SerializedProperty shaderComputeBoneDQ;
	SerializedProperty shaderDQBlend;
	SerializedProperty shaderApplyMorph;
	SerializedProperty bulgeCompensation;

	DualQuaternionSkinner dqs;
	SkinnedMeshRenderer smr
	{
		get
		{
			if (this._smr == null)
			{
				this._smr = ((DualQuaternionSkinner)this.target).gameObject.GetComponent<SkinnedMeshRenderer>();
			}

			return this._smr;
		}
	}
	SkinnedMeshRenderer _smr;

	bool showBlendShapes = false;

	enum BoneOrientation {X, Y, Z, negativeX, negativeY, negativeZ}
	readonly Dictionary<BoneOrientation, Vector3> boneOrientationVectors = new Dictionary<BoneOrientation, Vector3>();

	private void OnEnable()
	{
		this.shaderComputeBoneDQ	= this.serializedObject.FindProperty("shaderComputeBoneDQ");
		this.shaderDQBlend			= this.serializedObject.FindProperty("shaderDQBlend");
		this.shaderApplyMorph		= this.serializedObject.FindProperty("shaderApplyMorph");
		this.bulgeCompensation		= this.serializedObject.FindProperty("bulgeCompensation");

		this.boneOrientationVectors.Add(BoneOrientation.X, new Vector3(1,0,0));
		this.boneOrientationVectors.Add(BoneOrientation.Y, new Vector3(0,1,0));
		this.boneOrientationVectors.Add(BoneOrientation.Z, new Vector3(0,0,1));
		this.boneOrientationVectors.Add(BoneOrientation.negativeX, new Vector3(-1,0,0));
		this.boneOrientationVectors.Add(BoneOrientation.negativeY, new Vector3(0,-1,0));
		this.boneOrientationVectors.Add(BoneOrientation.negativeZ, new Vector3(0,0,-1));
	}

	public override void OnInspectorGUI()
	{
		this.serializedObject.Update();

		this.dqs = (DualQuaternionSkinner)this.target;

		EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField("Mode: ", GUILayout.Width(80));
			EditorGUILayout.LabelField(Application.isPlaying ? "Play" : "Editor", GUILayout.Width(80));
		EditorGUILayout.EndHorizontal();

		EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField("DQ skinning: ", GUILayout.Width(80));
			EditorGUILayout.LabelField(this.dqs.started ? "ON" : "OFF", GUILayout.Width(80));
		EditorGUILayout.EndHorizontal();

		EditorGUILayout.Space();
		this.dqs.SetViewFrustrumCulling(EditorGUILayout.Toggle("View frustrum culling: ", this.dqs.viewFrustrumCulling));
		EditorGUILayout.Space();

		BoneOrientation currentOrientation = BoneOrientation.X;
		foreach(BoneOrientation orientation in this.boneOrientationVectors.Keys)
		{
			if (this.dqs.boneOrientationVector == this.boneOrientationVectors[orientation])
			{
				currentOrientation = orientation;
				break;
			}
		}
		var newOrientation = (BoneOrientation)EditorGUILayout.EnumPopup("Bone orientation: ", currentOrientation);
		if (this.dqs.boneOrientationVector != this.boneOrientationVectors[newOrientation])
		{
			this.dqs.boneOrientationVector = this.boneOrientationVectors[newOrientation];
			this.dqs.UpdatePerVertexCompensationCoef();
		}

		EditorGUILayout.PropertyField(this.bulgeCompensation);
		EditorGUILayout.Space();

		this.showBlendShapes = EditorGUILayout.Foldout(this.showBlendShapes, "Blend shapes");

		if (this.showBlendShapes)
		{
			if (this.dqs.started == false)
			{
				EditorGUI.BeginChangeCheck();
				Undo.RecordObject(this.dqs.gameObject.GetComponent<SkinnedMeshRenderer>(), "changed blendshape weights by DualQuaternionSkinner component");
			}
			
			for (int i = 0; i < this.dqs.mesh.blendShapeCount; i++)
			{
				EditorGUILayout.BeginHorizontal();
				EditorGUILayout.LabelField("   " + this.dqs.mesh.GetBlendShapeName(i), GUILayout.Width(EditorGUIUtility.labelWidth - 10));
				float weight = EditorGUILayout.Slider(this.dqs.GetBlendShapeWeight(i), 0, 100);
				EditorGUILayout.EndHorizontal();
				this.dqs.SetBlendShapeWeight(i, weight);
			}
		}

		EditorGUILayout.Space();

		EditorGUILayout.PropertyField(this.shaderComputeBoneDQ);
		EditorGUILayout.PropertyField(this.shaderDQBlend);
		EditorGUILayout.PropertyField(this.shaderApplyMorph);

		EditorGUILayout.Space();
		EditorGUILayout.LabelField("Problems: ");

		if (this.CheckProblems() == false)
		{
			EditorGUILayout.LabelField("not detected (this is good)");
		}

		this.serializedObject.ApplyModifiedProperties();
	}

	bool CheckProblems()
	{
		var wrapStyle = new GUIStyle() { wordWrap = true };

		if (this.smr.sharedMesh.isReadable == false)
		{
			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Skinned mesh must be read/write enabled (check import settings)", wrapStyle);
			return true;
		}

		if (this.smr.rootBone.parent != this.dqs.gameObject.transform.parent)
		{
			EditorGUILayout.Space();

			EditorGUILayout.BeginHorizontal();
				EditorGUILayout.LabelField("Skinned object and root bone must be children of the same parent", wrapStyle);
				if (GUILayout.Button("auto fix"))
				{
					Undo.SetTransformParent(
						this.smr.rootBone,
						this.dqs.gameObject.transform.parent,
						"Changed root bone's parent by Dual Quaternion Skinner (auto fix)"
					);
					EditorApplication.RepaintHierarchyWindow();
				}
			EditorGUILayout.EndHorizontal();

			return true;
		}

		foreach(Transform bone in this.smr.bones)
		{
			if (bone.localScale != Vector3.one)
			{
				EditorGUILayout.Space();

				EditorGUILayout.BeginHorizontal();
					EditorGUILayout.LabelField(string.Format("Bone scaling not supported: {0}", bone.name), wrapStyle);
					if (GUILayout.Button("auto fix"))
					{
						Undo.RecordObject(bone, "Set bone scale to (1,1,1) by Dual Quaternion Skinner (auto fix)");
						bone.localScale = Vector3.one;
					}
				EditorGUILayout.EndHorizontal();

				return true;
			}
		}

		return false;
	}
}

#endif