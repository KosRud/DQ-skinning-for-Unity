using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

#if UNITY_EDITOR

/// <summary>
/// Provides custom inspector for {@link DualQuaternionSkinner}
/// </summary>
[CustomEditor(typeof(DualQuaternionSkinner))]
public class DualQuaternionSkinnerEditor : Editor
{
    private SerializedProperty m_shaderComputeBoneDq;
    private SerializedProperty m_shaderDqBlend;
    private SerializedProperty m_shaderApplyMorph;
    private SerializedProperty m_bulgeCompensation;

    private DualQuaternionSkinner m_dqs;

    private SkinnedMeshRenderer Smr
    {
        get
        {
            if (m_smr == null)
            {
                m_smr = ((DualQuaternionSkinner)target).gameObject
                    .GetComponent<SkinnedMeshRenderer>();
            }

            return m_smr;
        }
    }

    private SkinnedMeshRenderer m_smr;

    private bool m_showBlendShapes;

    private enum BoneOrientation
    {
        X,
        Y,
        Z,
        negativeX,
        negativeY,
        negativeZ
    }

    private readonly Dictionary<BoneOrientation, Vector3> boneOrientationVectors = new();

    private void OnEnable()
    {
        m_shaderComputeBoneDq = serializedObject.FindProperty("m_shaderComputeBoneDQ");
        m_shaderDqBlend       = serializedObject.FindProperty("m_shaderDQBlend");
        m_shaderApplyMorph    = serializedObject.FindProperty("m_shaderApplyMorph");
        m_bulgeCompensation   = serializedObject.FindProperty("m_bulgeCompensation");

        boneOrientationVectors.Add(BoneOrientation.X, new Vector3(1, 0, 0));
        boneOrientationVectors.Add(BoneOrientation.Y, new Vector3(0, 1, 0));
        boneOrientationVectors.Add(BoneOrientation.Z, new Vector3(0, 0, 1));
        boneOrientationVectors.Add(BoneOrientation.negativeX, new Vector3(-1, 0, 0));
        boneOrientationVectors.Add(BoneOrientation.negativeY, new Vector3(0, -1, 0));
        boneOrientationVectors.Add(BoneOrientation.negativeZ, new Vector3(0, 0, -1));
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        m_dqs = (DualQuaternionSkinner)target;

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Mode: ", GUILayout.Width(80));
        EditorGUILayout.LabelField(
            Application.isPlaying ? "Play" : "Editor",
            GUILayout.Width(80));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("DQ skinning: ", GUILayout.Width(80));
        EditorGUILayout.LabelField(m_dqs.Started ? "ON" : "OFF", GUILayout.Width(80));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        m_dqs.SetViewFrustumCulling(
            EditorGUILayout.Toggle(
                "View frustrum culling: ",
                m_dqs.GetViewFrustumCulling()));
        EditorGUILayout.Space();

        var currentOrientation = BoneOrientation.X;
        foreach (var orientation in boneOrientationVectors.Keys)
            if (m_dqs.m_boneOrientationVector == boneOrientationVectors[orientation])
            {
                currentOrientation = orientation;
                break;
            }
        var newOrientation = (BoneOrientation)EditorGUILayout.EnumPopup(
            "Bone orientation: ",
            currentOrientation);
        if (m_dqs.m_boneOrientationVector != boneOrientationVectors[newOrientation])
        {
            m_dqs.m_boneOrientationVector = boneOrientationVectors[newOrientation];
            m_dqs.UpdatePerVertexCompensationCoef();
        }

        EditorGUILayout.PropertyField(m_bulgeCompensation);
        EditorGUILayout.Space();

        m_showBlendShapes = EditorGUILayout.Foldout(m_showBlendShapes, "Blend shapes");

        if (m_showBlendShapes)
        {
            if (m_dqs.Started == false)
            {
                EditorGUI.BeginChangeCheck();
                Undo.RecordObject(
                    m_dqs.gameObject.GetComponent<SkinnedMeshRenderer>(),
                    "changed blendshape weights by DualQuaternionSkinner component");
            }

            for (int i = 0; i < m_dqs.Mesh.blendShapeCount; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(
                    "   " + m_dqs.Mesh.GetBlendShapeName(i),
                    GUILayout.Width(EditorGUIUtility.labelWidth - 10));
                float weight = EditorGUILayout.Slider(m_dqs.GetBlendShapeWeight(i), 0, 100);
                EditorGUILayout.EndHorizontal();
                m_dqs.SetBlendShapeWeight(i, weight);
            }
        }

        EditorGUILayout.Space();

        EditorGUILayout.PropertyField(m_shaderComputeBoneDq);
        EditorGUILayout.PropertyField(m_shaderDqBlend);
        EditorGUILayout.PropertyField(m_shaderApplyMorph);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Problems: ");

        if (CheckProblems() == false)
        {
            EditorGUILayout.LabelField("not detected (this is good)");
        }

        serializedObject.ApplyModifiedProperties();
    }

    private bool CheckProblems()
    {
        var wrapStyle = new GUIStyle { wordWrap = true };

        if (Smr.sharedMesh.isReadable == false)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                "Skinned mesh must be read/write enabled (check import settings)",
                wrapStyle);
            return true;
        }

        if (Smr.rootBone.parent != m_dqs.gameObject.transform.parent)
        {
            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(
                "Skinned object and root bone must be children of the same parent",
                wrapStyle);
            if (GUILayout.Button("auto fix"))
            {
                Undo.SetTransformParent(
                    Smr.rootBone,
                    m_dqs.gameObject.transform.parent,
                    "Changed root bone's parent by Dual Quaternion Skinner (auto fix)");
                EditorApplication.RepaintHierarchyWindow();
            }
            EditorGUILayout.EndHorizontal();

            return true;
        }

        foreach (var bone in Smr.bones)
            if (bone.localScale != Vector3.one)
            {
                EditorGUILayout.Space();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(
                    string.Format("Bone scaling not supported: {0}", bone.name),
                    wrapStyle);
                if (GUILayout.Button("auto fix"))
                {
                    Undo.RecordObject(
                        bone,
                        "Set bone scale to (1,1,1) by Dual Quaternion Skinner (auto fix)");
                    bone.localScale = Vector3.one;
                }
                EditorGUILayout.EndHorizontal();

                return true;
            }

        return false;
    }
}

#endif
