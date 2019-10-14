using UnityEngine;

/// <summary>
/// Replaces Unity's default linear skinning with DQ skinning
/// 
/// Add this component to a <a class="bold" href="https://docs.unity3d.com/ScriptReference/GameObject.html">GameObject</a> that has <a class="bold" href="https://docs.unity3d.com/ScriptReference/SkinnedMeshRenderer.html">SkinnedMeshRenderer</a> attached.<br>
/// Do not remove <a class="bold" href="https://docs.unity3d.com/ScriptReference/SkinnedMeshRenderer.html">SkinnedMeshRenderer</a> component!<br>
/// Make sure that all materials of the animated object are using shader \"<b>MadCake/Material/Standard hacked for DQ skinning</b>\"
/// 
/// <a class="bold" href="https://docs.unity3d.com/ScriptReference/SkinnedMeshRenderer.html">SkinnedMeshRenderer</a> is required to extract some information about the mesh during <b>Start()</b> and is destroyed immediately after. 
/// </summary>
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(SkinnedMeshRenderer))]
public class DualQuaternionSkinner : MonoBehaviour
{
	struct VertexInfo
	{
		// could use float3 instead of float4 but NVidia says structures not aligned to 128 bits are slow
		// https://developer.nvidia.com/content/understanding-structured-buffer-performance

		public Vector4 position;
		public Vector4 normal;
		public Vector4 tangent;

		public int boneIndex0;
		public int boneIndex1;
		public int boneIndex2;
		public int boneIndex3;

		public float weight0;
		public float weight1;
		public float weight2;
		public float weight3;
	}

	struct MorphDelta
	{
		// could use float3 instead of float4 but NVidia says structures not aligned to 128 bits are slow
		// https://developer.nvidia.com/content/understanding-structured-buffer-performance

		public Vector4 position;
		public Vector4 normal;
		public Vector4 tangent;
	}

	struct DualQuaternion
	{
		public Quaternion rotationQuaternion;
		public Vector4 position;
	}

	const int numthreads = 1024;    // must be same in compute shader code
	const int textureWidth = 1024;  // no need to adjust compute shaders

	public ComputeShader shaderComputeBoneDQ;
	public ComputeShader shaderDQBlend;
	public ComputeShader shaderApplyMorph;

	/// <summary>
	/// Indicates whether Start( ) has already been called.<br>
	/// When set to <b>true</b> indicates that <a class="bold" href="https://docs.unity3d.com/ScriptReference/SkinnedMeshRenderer.html">SkinnedMeshRenderer</a> component was already destroyed.
	/// </summary>
	public bool started { get; private set; } = false;

	DualQuaternion[] poseDualQuaternions;
	Matrix4x4[] poseMatrices;

	ComputeBuffer bufPoseMatrices;
	ComputeBuffer bufSkinnedDq;
	ComputeBuffer bufBindDq;

	ComputeBuffer bufVertInfo;
	ComputeBuffer bufMorphedVertInfo;
	ComputeBuffer bufMorphTemp;

	ComputeBuffer[] arrBufMorphDeltas;

	float[] morphWeights;

	MeshFilter mf;
	MeshRenderer mr;
	SkinnedMeshRenderer smr;

	MaterialPropertyBlock materialPropertyBlock;

	Transform[] bones;

	/*
		Vulkan and OpenGL only support ComputeBuffer in compute shaders
		passing data to the vertex and fragment shaders is done through RenderTextures

		using ComputeBuffers would improve the efficiency slightly but it would only work with Dx11

		layout is as such:
			rtSkinnedData_1			float4			vertex.xyz,	normal.x
			rtSkinnedData_2			float4			normal.yz,	tangent.xy
			rtSkinnedData_3			float2			tangent.zw
	*/
	RenderTexture rtSkinnedData_1;
	RenderTexture rtSkinnedData_2;
	RenderTexture rtSkinnedData_3;

	Material[] materials;

	int kernelHandleComputeBoneDQ;
	int kernelHandleDQBlend;
	int kernelHandleApplyMorph;

	/// <summary>
	/// Returns an array of currently applied blend shape weights.
	/// 
	/// Default range is 0-100. It is possible to apply negative weights or exceeding 100.
	/// </summary>
	/// <returns>Array of currently applied blend shape weights.</returns>
	public float[] GetBlendShapeWeights()
	{
		float[] weights = new float[this.morphWeights.Length];
		for (int i = 0; i < weights.Length; i++)
        {
            weights[i] = this.morphWeights[i] * 100f;
        }

        return weights;
	}

	/// <summary>
	/// Applies blend shape weights from the given array.
	/// 
	/// Default range is 0-100. It is possible to apply negative weights or exceeding 100.
	/// </summary>
	/// <param name="weights">An array of weights to be applied</param>
	public void SetBlendShapeWeights(float[] weights)
	{
		if (weights.Length != this.morphWeights.Length)
        {
            throw new System.ArgumentException(
				"An array of weights must contain the number of elements " +
				$"equal to the number of available blendshapes. Currently " +
				$"{this.morphWeights.Length} blendshapes ara available but {weights.Length} weights were passed."
			);
        }

        for (int i = 0; i < weights.Length; i++)
        {
            this.morphWeights[i] = weights[i] / 100f;
        }
    }

	/// <summary>
	/// Set weight for the blend shape with given index.
	/// 
	/// Default range is 0-100. It is possible to apply negative weights or exceeding 100.
	/// </summary>
	/// <param name="index">Index of the blend shape</param>
	/// <param name="weight">Weight to be applied</param>
	public void SetBlendShapeWeight(int index, float weight)
	{
		if (this.started == false)
		{
			this.GetComponent<SkinnedMeshRenderer>().SetBlendShapeWeight(index, weight);
			return;
		}

		if (index < 0 || index >= this.morphWeights.Length)
        {
            throw new System.IndexOutOfRangeException("Blend shape index out of range");
        }

        this.morphWeights[index] = weight / 100f;
	}

	/// <summary>
	/// Returns currently applied weight for the blend shape with given index.
	/// </summary>
	/// <param name="index">Index of the blend shape</param>
	/// <returns>Currently applied weight.</returns>
	public float GetBlendShapeWeight(int index)
	{
		if (this.started == false)
        {
            return this.GetComponent<SkinnedMeshRenderer>().GetBlendShapeWeight(index);
        }

        if (index < 0 || index >= this.morphWeights.Length)
        {
            throw new System.IndexOutOfRangeException("Blend shape index out of range");
        }

        return this.morphWeights[index] * 100f;
	}

	/// <summary>
	/// UnityEngine.<a class="bold" href="https://docs.unity3d.com/ScriptReference/Mesh.html">Mesh</a> that is currently being rendered.
	/// @see <a class="bold" href="https://docs.unity3d.com/ScriptReference/Mesh.GetBlendShapeName.html">Mesh.GetBlendShapeName(int shapeIndex)</a>
	/// @see <a class="bold" href="https://docs.unity3d.com/ScriptReference/Mesh.GetBlendShapeIndex.html">Mesh.GetBlendShapeIndex(string blendShapeName)</a>
	/// @see <a class="bold" href="https://docs.unity3d.com/ScriptReference/Mesh-blendShapeCount.html">Mesh.blendShapeCount</a>
	/// </summary>
	public Mesh mesh
	{
		get
		{
			if (this.started == false)
            {
                return this.GetComponent<SkinnedMeshRenderer>().sharedMesh;
            }

            return this.mf.mesh;
		}

		set
		{
			if (this.started == false)
            {
                throw new System.InvalidOperationException("DualQuaternion.Skinner.mesh can only be assigned to after Awake() was called");
            }

            this.mf.mesh = value;

			this.SetMesh(value);
		}
	}

	void SetMesh(Mesh mesh)
	{
		this.ReleaseBuffers();

		mesh.bounds = new Bounds(Vector3.zero, new Vector3(Mathf.Infinity, Mathf.Infinity, Mathf.Infinity)); // ToDo: avoid dirty hack

		this.mf.mesh = mesh;

		this.arrBufMorphDeltas = new ComputeBuffer[this.mf.mesh.blendShapeCount];

		this.morphWeights = new float[this.mf.mesh.blendShapeCount];

		var deltaVertices = new Vector3[this.mf.mesh.vertexCount];
		var deltaNormals = new Vector3[this.mf.mesh.vertexCount];
		var deltaTangents = new Vector3[this.mf.mesh.vertexCount];

		var deltaVertInfos = new MorphDelta[this.mf.mesh.vertexCount];

		for (int i = 0; i < this.mf.mesh.blendShapeCount; i++)
		{
			this.mf.mesh.GetBlendShapeFrameVertices(i, 0, deltaVertices, deltaNormals, deltaTangents);

			this.arrBufMorphDeltas[i] = new ComputeBuffer(this.mf.mesh.vertexCount, sizeof(float) * 12);

			for (int k = 0; k < this.mf.mesh.vertexCount; k++)
			{
				deltaVertInfos[k].position	= deltaVertices	!= null ? deltaVertices[k]	: Vector3.zero;
				deltaVertInfos[k].normal	= deltaNormals	!= null ? deltaNormals[k]	: Vector3.zero;
				deltaVertInfos[k].tangent	= deltaTangents	!= null ? deltaTangents[k]	: Vector3.zero;
			}

			this.arrBufMorphDeltas[i].SetData(deltaVertInfos);
		}

		this.mr = this.gameObject.GetComponent<MeshRenderer>();
		if (this.mr == null)
        {
            this.mr = this.gameObject.AddComponent<MeshRenderer>();
        }

        this.mr.materials = this.materials;  // bug workaround
		this.materials = this.mr.materials;  // bug workaround

		foreach (Material m in this.mr.materials)
		{
			m.SetInt("_DoSkinning", 1);
		}

		this.shaderDQBlend.SetInt("textureWidth", textureWidth);

		this.poseDualQuaternions = new DualQuaternion[this.mf.mesh.bindposes.Length];
		this.poseMatrices = new Matrix4x4[this.mf.mesh.bindposes.Length];

		// initiate textures and buffers

		int textureHeight = this.mf.mesh.vertexCount / textureWidth;
		if (this.mf.mesh.vertexCount % textureWidth != 0)
		{
			textureHeight++;
		}

        this.rtSkinnedData_1 = new RenderTexture(textureWidth, textureHeight, 0, RenderTextureFormat.ARGBFloat)
        {
            filterMode = FilterMode.Point,
            enableRandomWrite = true
        };
        this.rtSkinnedData_1.Create();
		this.shaderDQBlend.SetTexture(this.kernelHandleComputeBoneDQ, "skinned_data_1", this.rtSkinnedData_1);

        this.rtSkinnedData_2 = new RenderTexture(textureWidth, textureHeight, 0, RenderTextureFormat.ARGBFloat)
        {
            filterMode = FilterMode.Point,
            enableRandomWrite = true
        };
        this.rtSkinnedData_2.Create();
		this.shaderDQBlend.SetTexture(this.kernelHandleComputeBoneDQ, "skinned_data_2", this.rtSkinnedData_2);

        this.rtSkinnedData_3 = new RenderTexture(textureWidth, textureHeight, 0, RenderTextureFormat.RGFloat)
        {
            filterMode = FilterMode.Point,
            enableRandomWrite = true
        };
        this.rtSkinnedData_3.Create();
		this.shaderDQBlend.SetTexture(this.kernelHandleComputeBoneDQ, "skinned_data_3", this.rtSkinnedData_3);

		this.bufPoseMatrices = new ComputeBuffer(this.mf.mesh.bindposes.Length, sizeof(float) * 16);
		this.shaderComputeBoneDQ.SetBuffer(this.kernelHandleComputeBoneDQ, "pose_matrices", this.bufPoseMatrices);

		this.bufSkinnedDq = new ComputeBuffer(this.mf.mesh.bindposes.Length, sizeof(float) * 8);
		this.shaderComputeBoneDQ.SetBuffer(this.kernelHandleComputeBoneDQ, "skinned_dual_quaternions", this.bufSkinnedDq);
		this.shaderDQBlend.SetBuffer(this.kernelHandleComputeBoneDQ, "skinned_dual_quaternions", this.bufSkinnedDq);

		this.bufVertInfo = new ComputeBuffer(this.mf.mesh.vertexCount, sizeof(float) * 16 + sizeof(int) * 4);
		var vertInfos = new VertexInfo[this.mf.mesh.vertexCount];
		Vector3[] vertices = this.mf.mesh.vertices;
		Vector3[] normals = this.mf.mesh.normals;
		Vector4[] tangents = this.mf.mesh.tangents;
		BoneWeight[] boneWeights = this.mf.mesh.boneWeights;
		for (int i = 0; i < vertInfos.Length; i++)
		{
			vertInfos[i].position = vertices[i];
			vertInfos[i].normal = normals[i];
			vertInfos[i].tangent = tangents[i];

			vertInfos[i].boneIndex0 = boneWeights[i].boneIndex0;
			vertInfos[i].boneIndex1 = boneWeights[i].boneIndex1;
			vertInfos[i].boneIndex2 = boneWeights[i].boneIndex2;
			vertInfos[i].boneIndex3 = boneWeights[i].boneIndex3;

			vertInfos[i].weight0 = boneWeights[i].weight0;
			vertInfos[i].weight1 = boneWeights[i].weight1;
			vertInfos[i].weight2 = boneWeights[i].weight2;
			vertInfos[i].weight3 = boneWeights[i].weight3;
		}
		this.bufVertInfo.SetData(vertInfos);
		this.shaderDQBlend.SetBuffer(this.kernelHandleComputeBoneDQ, "vertex_infos", this.bufVertInfo);
		
		this.bufMorphedVertInfo = new ComputeBuffer(this.mf.mesh.vertexCount, sizeof(float) * 16 + sizeof(int) * 4);
		this.bufMorphTemp = new ComputeBuffer(this.mf.mesh.vertexCount, sizeof(float) * 16 + sizeof(int) * 4);

		// bind DQ buffer

		Matrix4x4[] bindPoses = this.mf.mesh.bindposes;
		var bindDqs = new DualQuaternion[bindPoses.Length];
		for (int i = 0; i < bindPoses.Length; i++)
		{
			bindDqs[i].rotationQuaternion	= bindPoses[i].ExtractRotation();
			bindDqs[i].position				= bindPoses[i].ExtractPosition();
		}

		this.bufBindDq = new ComputeBuffer(bindDqs.Length, sizeof(float) * 8);
		this.bufBindDq.SetData(bindDqs);
		this.shaderComputeBoneDQ.SetBuffer(this.kernelHandleComputeBoneDQ, "bind_dual_quaternions", this.bufBindDq);
	}

	void ApplyAllMorphs()
	{
		this.shaderDQBlend.SetBuffer(
			this.kernelHandleComputeBoneDQ,
			"vertex_infos",
			this.ApplyMorphs(
				this.bufVertInfo,
				ref this.bufMorphedVertInfo,
				ref this.bufMorphTemp,
				this.arrBufMorphDeltas,
				this.morphWeights
			)
		);
	}

	ComputeBuffer ApplyMorphs(ComputeBuffer bufOriginal, ref ComputeBuffer bufTarget, ref ComputeBuffer bufTemp, ComputeBuffer[] arrBufDelta, float[] weights)
	{
		ComputeBuffer bufSource = bufOriginal;

		for (int i = 0; i < weights.Length; i++)
		{
			if (weights[i] == 0)
            {
                continue;
            }

            if (arrBufDelta[i] == null)
            {
                continue;
            }

            this.shaderApplyMorph.SetBuffer(this.kernelHandleApplyMorph, "source", bufSource);
			this.shaderApplyMorph.SetBuffer(this.kernelHandleApplyMorph, "target", bufTarget);
			this.shaderApplyMorph.SetBuffer(this.kernelHandleApplyMorph, "delta", arrBufDelta[i]);
			this.shaderApplyMorph.SetFloat("weight", weights[i]);

			int numThreadGroups = bufSource.count / numthreads;
			if (bufSource.count % numthreads != 0)
			{
				numThreadGroups++;
			}

			this.shaderApplyMorph.Dispatch(this.kernelHandleApplyMorph, numThreadGroups, 1, 1);

			bufSource = bufTarget;
			bufTarget = bufTemp;
			bufTemp = bufSource;
		}

		if (bufSource == bufOriginal)
        {
            return bufOriginal;
        }

        bufSource = bufTarget;
		bufTarget = bufTemp;
		bufTemp = bufSource;

		return bufTarget;
	}

	void ReleaseBuffers()
	{
		this.bufBindDq?.Release();
		this.bufPoseMatrices?.Release();
		this.bufSkinnedDq?.Release();

		this.bufVertInfo?.Release();
		this.bufMorphedVertInfo?.Release();
		this.bufMorphTemp?.Release();

		if (this.arrBufMorphDeltas != null)
		{
			for (int i = 0; i < this.arrBufMorphDeltas.Length; i++)
			{
				this.arrBufMorphDeltas[i]?.Release();
			}
		}
	}

	void OnDestroy()
	{
		this.ReleaseBuffers();
	}

	// Use this for initialization
	void Start()
	{
		this.materialPropertyBlock = new MaterialPropertyBlock();

		this.shaderComputeBoneDQ = (ComputeShader)Instantiate(this.shaderComputeBoneDQ);    // bug workaround
		this.shaderDQBlend = (ComputeShader)Instantiate(this.shaderDQBlend);                // bug workaround
		this.shaderApplyMorph = (ComputeShader)Instantiate(this.shaderApplyMorph);          // bug workaround

		this.smr = this.gameObject.GetComponent<SkinnedMeshRenderer>();
		this.mf = this.GetComponent<MeshFilter>();

		this.kernelHandleComputeBoneDQ = this.shaderComputeBoneDQ.FindKernel("CSMain");
		this.kernelHandleDQBlend = this.shaderDQBlend.FindKernel("CSMain");
		this.kernelHandleApplyMorph = this.shaderApplyMorph.FindKernel("CSMain");

		this.materials = this.smr.materials;
		this.bones = this.smr.bones;

		this.SetMesh(this.smr.sharedMesh);

		for (int i = 0; i < this.morphWeights.Length; i++)
		{
			this.morphWeights[i] = this.smr.GetBlendShapeWeight(i) / 100f;
		}

		this.smr.enabled = false;
		this.started = true;
	}

	// Update is called once per frame
	void Update()
	{
		this.ApplyAllMorphs();

		this.mf.mesh.MarkDynamic();    // once or every frame? idk.
									   // at least it does not affect performance

		this.shaderComputeBoneDQ.SetMatrix(
			"self_matrix",
			this.transform.worldToLocalMatrix
		);

		for (int i = 0; i < this.bones.Length; i++)
		{
			this.poseDualQuaternions[i].rotationQuaternion = this.bones[i].rotation;

			Vector3 pos = this.bones[i].position;

			// could use float3 instead of float4 for position but NVidia says structures not aligned to 128 bits are slow
			// https://developer.nvidia.com/content/understanding-structured-buffer-performance
			this.poseDualQuaternions[i].position = new Vector4(
				pos.x,
				pos.y,
				pos.z,
				0
			);	// not a proper quaternion, just a position. shader handles the rest

			this.poseMatrices[i] = this.bones[i].localToWorldMatrix;
		}
		
		this.bufPoseMatrices.SetData(this.poseMatrices);

		// Calculate blended quaternions

		int numThreadGroups = this.bones.Length / numthreads;
		if (this.bones.Length % numthreads != 0)
		{
			numThreadGroups++;
		}

		this.shaderComputeBoneDQ.Dispatch(this.kernelHandleDQBlend, numThreadGroups, 1, 1);

		numThreadGroups = this.mf.mesh.vertexCount / numthreads;
		if (this.mf.mesh.vertexCount % numthreads != 0)
		{
			numThreadGroups++;
		}

		this.shaderDQBlend.Dispatch(this.kernelHandleDQBlend, numThreadGroups, 1, 1);

		this.materialPropertyBlock.SetTexture("skinned_data_1", this.rtSkinnedData_1);
		this.materialPropertyBlock.SetTexture("skinned_data_2", this.rtSkinnedData_2);
		this.materialPropertyBlock.SetTexture("skinned_data_3", this.rtSkinnedData_3);
		this.materialPropertyBlock.SetInt("skinned_tex_height", this.mf.mesh.vertexCount / textureWidth);
		this.materialPropertyBlock.SetInt("skinned_tex_width", textureWidth);

		this.mr.SetPropertyBlock(this.materialPropertyBlock);
	}
}

