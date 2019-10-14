using UnityEngine;
using UnityEditor;

/// <summary>
/// Provides custom inspector for {@link DualQuaternionSkinner}
/// </summary>
[CustomEditor(typeof(DualQuaternionSkinner))]
public class DualQuaternionSkinnerEditor : Editor
{
	SerializedProperty shaderComputeBoneDQ;
	SerializedProperty shaderDQBlend;
	SerializedProperty shaderApplyMorph;

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

	private void OnEnable()
	{
		this.shaderComputeBoneDQ	= this.serializedObject.FindProperty("shaderComputeBoneDQ");
		this.shaderDQBlend			= this.serializedObject.FindProperty("shaderDQBlend");
		this.shaderApplyMorph		= this.serializedObject.FindProperty("shaderApplyMorph");
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
