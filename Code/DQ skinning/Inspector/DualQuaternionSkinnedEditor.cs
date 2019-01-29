using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(DualQuaternionSkinner))]
public class DualQuaternionSkinnedEditor : Editor
{
	SerializedProperty shaderComputeBoneDQ;
	SerializedProperty shaderDQBlend;
	SerializedProperty shaderApplyMorph;

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

		var dqs = (DualQuaternionSkinner)this.target;

		EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField("Mode: ", GUILayout.Width(80));
			EditorGUILayout.LabelField(Application.isPlaying ? "Play" : "Editor", GUILayout.Width(80));
		EditorGUILayout.EndHorizontal();

		EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField("DQ skinning: ", GUILayout.Width(80));
			EditorGUILayout.LabelField(dqs.started ? "ON" : "OFF", GUILayout.Width(80));
		EditorGUILayout.EndHorizontal();

		EditorGUILayout.Space();
		EditorGUILayout.Space();
		EditorGUILayout.Space();
		EditorGUILayout.Space();

		this.showBlendShapes = EditorGUILayout.Foldout(this.showBlendShapes, "Blend shapes");

		if (this.showBlendShapes)
		{
			if (dqs.started == false)
			{
				EditorGUI.BeginChangeCheck();
				Undo.RecordObject(dqs.gameObject.GetComponent<SkinnedMeshRenderer>(), "changed blendshape weights by DualQuaternionSkinner component");
			}

			EditorGUI.indentLevel = 1;
			
			for (int i = 0; i < dqs.mesh.blendShapeCount; i++)
			{
				float weight = EditorGUILayout.Slider(dqs.GetBlendShapeWeight(i), 0, 100);
				dqs.SetBlendShapeWeight(i, weight);
			}
		}

		EditorGUI.indentLevel = 0;

		EditorGUILayout.Space();
		EditorGUILayout.Space();
		EditorGUILayout.Space();
		EditorGUILayout.Space();
		EditorGUILayout.LabelField("Compute shader references");
		EditorGUILayout.LabelField("Do not change unless you know what you're doing");
		EditorGUILayout.Space();

		EditorGUILayout.PropertyField(this.shaderComputeBoneDQ);
		EditorGUILayout.PropertyField(this.shaderDQBlend);
		EditorGUILayout.PropertyField(this.shaderApplyMorph);
		this.serializedObject.ApplyModifiedProperties();
	}
}
