using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class DualQuaternionSkinner : MonoBehaviour {

	struct DualQuaternion
	{
		public Vector4 rotationQuaternion;
		public Vector4 translationQuaternion;
	}

	struct BoneWeightInfo
	{
		public int boneIndex0;
		public int boneIndex1;
		public int boneIndex2;
		public int boneIndex3;

		public float weight0;
		public float weight1;
		public float weight2;
		public float weight3;
	}

	DualQuaternion[] poseDualQuaternions;
	
	const int numthreads = 1024;	// must be same in compute shader code
	const int textureWidth = 1024;  // no need to adjust compute shaders
	
	public ComputeShader shaderComputeBoneDQ;
	public ComputeShader shaderDQBlend;
	public ComputeShader shaderApplyMorph;

	public bool started { get; private set; } = false;

	ComputeBuffer bufPoseDq;
	ComputeBuffer bufSkinnedDq;
	ComputeBuffer bufOriginalVertices;
	ComputeBuffer bufOriginalNormals;
	ComputeBuffer bufOriginalTangents;
	ComputeBuffer bufBoneInfo;
	ComputeBuffer bufBindDq;
	ComputeBuffer bufMorphedVertices;
	ComputeBuffer bufMorphedNormals;
	ComputeBuffer bufMorphedTangents;
	ComputeBuffer bufMorphTemp;

	ComputeBuffer[] arrBufMorphDeltaVertices;
	ComputeBuffer[] arrBufMorphDeltaNormals;
	ComputeBuffer[] arrBufMorphDeltaTangents;

	float[] morphWeights;

	MeshFilter mf;

	Transform[] bones;

	/*
		Vulkan and OpenGL only support ComputeBuffer in compute shaders
		passing data to the vertex and fragment shaders is done through RenderTextures

		using ComputeBuffers would improve the efficiency slightly but it would only work with Dx11

		layout is as such:
			rtSkinnedData_1			float4			vertex.xyz,	normal.x
			rtSkinnedData_2			float4			normal.yz,	tangent.xy
			rtSkinnedData_3			float			tangent.z
	*/
	RenderTexture rtSkinnedData_1;
	RenderTexture rtSkinnedData_2;
	RenderTexture rtSkinnedData_3;

	Material[] materials;

	int kernelHandleComputeBoneDQ;
	int kernelHandleDQBlend;
	int kernelHandleApplyMorph;

	public float[] GetBlendShapeWeights()
	{
		float[] weights = new float[this.morphWeights.Length];
		for (int i = 0; i < weights.Length; i++)
			weights[i] = this.morphWeights[i] * 100f;
		return weights;
	}

	public void SetBlendShapeWeights(float[] weights)
	{
		if (weights.Length != this.morphWeights.Length)
			throw new System.ArgumentException(
				"An array of weights must contain the number of elements " +
				$"equal to the number of available blendshapes. Currently " +
				$"{this.morphWeights.Length} blendshapes ara available but {weights.Length} weights were passed."
			);

		for (int i = 0; i < weights.Length; i++)
			this.morphWeights[i] = weights[i] / 100f;
	}

	public void SetBlendShapeWeight(int index, float weight)
	{
		if (this.started == false)
		{
			this.GetComponent<SkinnedMeshRenderer>().SetBlendShapeWeight(index, weight);
			return;
		}

		if (index < 0 || index >= this.morphWeights.Length)
			throw new System.IndexOutOfRangeException("Blend shape index out of range");

		this.morphWeights[index] = weight / 100f;
	}

	public float GetBlendShapeWeight(int index)
	{
		if (this.started == false)
			return this.GetComponent<SkinnedMeshRenderer>().GetBlendShapeWeight(index);

		if (index < 0 || index >= this.morphWeights.Length)
			throw new System.IndexOutOfRangeException("Blend shape index out of range");

		return this.morphWeights[index] * 100f;
	}

	public Mesh mesh
	{
		get
		{
			if (this.started == false)
				return this.GetComponent<SkinnedMeshRenderer>().sharedMesh;

			return this.mf.mesh;
		}

		set
		{
			if (this.started == false)
				throw new System.InvalidOperationException("DualQuaternion.Skinner.mesh can only be assigned to after Awake() was called");

			this.mf.mesh = value;

			this.SetMesh(value);
		}
	}

	void SetMesh(Mesh mesh)
	{
		this.ReleaseBuffers();

		this.mf.mesh = mesh;

		this.arrBufMorphDeltaVertices = new ComputeBuffer[this.mf.mesh.blendShapeCount];
		this.arrBufMorphDeltaNormals = new ComputeBuffer[this.mf.mesh.blendShapeCount];
		this.arrBufMorphDeltaTangents = new ComputeBuffer[this.mf.mesh.blendShapeCount];

		this.morphWeights = new float[this.mf.mesh.blendShapeCount];

		var deltaVertices = new Vector3[this.mf.mesh.vertexCount];
		var deltaNormals = new Vector3[this.mf.mesh.vertexCount];
		var deltaTangents = new Vector3[this.mf.mesh.vertexCount];
		var tempVec4 = new Vector4[this.mf.mesh.vertexCount];

		for (int i = 0; i < this.mf.mesh.blendShapeCount; i++)
		{
			this.mf.mesh.GetBlendShapeFrameVertices(i, 0, deltaVertices, deltaNormals, deltaTangents);

			if (deltaVertices != null)
			{

				// could use float3 instead of float4 but NVidia says structures not aligned to 128 bits are slow
				// https://developer.nvidia.com/content/understanding-structured-buffer-performance
				this.arrBufMorphDeltaVertices[i] = new ComputeBuffer(this.mf.mesh.vertexCount, sizeof(float) * 4);
				for (int k = 0; k < this.mf.mesh.vertexCount; k++)
					tempVec4[k] = deltaVertices[k];
				this.arrBufMorphDeltaVertices[i].SetData(tempVec4);
			}

			if (deltaNormals != null)
			{
				// could use float3 instead of float4 but NVidia says structures not aligned to 128 bits are slow
				// https://developer.nvidia.com/content/understanding-structured-buffer-performance
				this.arrBufMorphDeltaNormals[i] = new ComputeBuffer(this.mf.mesh.vertexCount, sizeof(float) * 4);
				for (int k = 0; k < this.mf.mesh.vertexCount; k++)
					tempVec4[k] = deltaNormals[k];
				this.arrBufMorphDeltaNormals[i].SetData(tempVec4);
			}

			if (deltaTangents != null)
			{
				// could use float3 instead of float4 but NVidia says structures not aligned to 128 bits are slow
				// https://developer.nvidia.com/content/understanding-structured-buffer-performance
				this.arrBufMorphDeltaTangents[i] = new ComputeBuffer(this.mf.mesh.vertexCount, sizeof(float) *  4);
				for (int k = 0; k < this.mf.mesh.vertexCount; k++)
					tempVec4[k] = deltaTangents[k];
				this.arrBufMorphDeltaTangents[i].SetData(tempVec4);
			}
		}

		MeshRenderer mr = this.gameObject.GetComponent<MeshRenderer>();
		if (mr == null)
			mr = this.gameObject.AddComponent<MeshRenderer>();

		mr.materials = this.materials;  // this is NOT a mistake
		this.materials = mr.materials;      // this is NOT a mistake

		foreach (Material m in mr.materials)
		{
			m.SetInt("_DoSkinning", 1);
		}

		this.shaderDQBlend.SetInt("textureWidth", textureWidth);

		this.poseDualQuaternions = new DualQuaternion[this.mf.mesh.bindposes.Length];

		// initiate textures and buffers

		int textureHeight = this.mf.mesh.vertexCount / textureWidth;
		if (this.mf.mesh.vertexCount % textureWidth != 0)
		{
			textureHeight++;
		}

		this.rtSkinnedData_1 = new RenderTexture(textureWidth, textureHeight, 0, RenderTextureFormat.ARGBFloat);
		this.rtSkinnedData_1.filterMode = FilterMode.Point;
		this.rtSkinnedData_1.enableRandomWrite = true;
		this.rtSkinnedData_1.Create();
		this.shaderDQBlend.SetTexture(this.kernelHandleComputeBoneDQ, "skinned_data_1", this.rtSkinnedData_1);

		this.rtSkinnedData_2 = new RenderTexture(textureWidth, textureHeight, 0, RenderTextureFormat.ARGBFloat);
		this.rtSkinnedData_2.filterMode = FilterMode.Point;
		this.rtSkinnedData_2.enableRandomWrite = true;
		this.rtSkinnedData_2.Create();
		this.shaderDQBlend.SetTexture(this.kernelHandleComputeBoneDQ, "skinned_data_2", this.rtSkinnedData_2);

		this.rtSkinnedData_3 = new RenderTexture(textureWidth, textureHeight, 0, RenderTextureFormat.RFloat);
		this.rtSkinnedData_3.filterMode = FilterMode.Point;
		this.rtSkinnedData_3.enableRandomWrite = true;
		this.rtSkinnedData_3.Create();
		this.shaderDQBlend.SetTexture(this.kernelHandleComputeBoneDQ, "skinned_data_3", this.rtSkinnedData_3);

		this.bufPoseDq = new ComputeBuffer(this.mf.mesh.bindposes.Length, sizeof(float) * 8);
		this.shaderComputeBoneDQ.SetBuffer(this.kernelHandleComputeBoneDQ, "pose_dual_quaternions", this.bufPoseDq);

		this.bufSkinnedDq = new ComputeBuffer(this.mf.mesh.bindposes.Length, sizeof(float) * 8);
		this.shaderComputeBoneDQ.SetBuffer(this.kernelHandleComputeBoneDQ, "skinned_dual_quaternions", this.bufSkinnedDq);
		this.shaderDQBlend.SetBuffer(this.kernelHandleComputeBoneDQ, "skinned_dual_quaternions", this.bufSkinnedDq);

		// could use float3 instead of float4 but NVidia says structures not aligned to 128 bits are slow
		// https://developer.nvidia.com/content/understanding-structured-buffer-performance
		this.bufOriginalVertices = new ComputeBuffer(this.mf.mesh.vertexCount, sizeof(float) * 4);
		Vector3[] vertices = this.mf.mesh.vertices;
		for (int i = 0; i < this.mf.mesh.vertexCount; i++)
			tempVec4[i] = vertices[i];
		this.bufOriginalVertices.SetData(tempVec4);
		this.shaderDQBlend.SetBuffer(this.kernelHandleComputeBoneDQ, "original_vertices", this.bufOriginalVertices);

		// could use float3 instead of float4 but NVidia says structures not aligned to 128 bits are slow
		// https://developer.nvidia.com/content/understanding-structured-buffer-performance
		this.bufOriginalNormals = new ComputeBuffer(this.mf.mesh.vertexCount, sizeof(float) * 4);
		Vector3[] normals = this.mf.mesh.normals;
		for (int i = 0; i < this.mf.mesh.vertexCount; i++)
			tempVec4[i] = normals[i];
		this.bufOriginalNormals.SetData(tempVec4);
		this.shaderDQBlend.SetBuffer(this.kernelHandleComputeBoneDQ, "original_normals", this.bufOriginalNormals);

		// could use float3 instead of float4 but NVidia says structures not aligned to 128 bits are slow
		// https://developer.nvidia.com/content/understanding-structured-buffer-performance
		this.bufOriginalTangents = new ComputeBuffer(this.mf.mesh.vertexCount, sizeof(float) * 4);
		this.bufOriginalTangents.SetData(this.mf.mesh.tangents);
		this.shaderDQBlend.SetBuffer(this.kernelHandleComputeBoneDQ, "original_tangents", this.bufOriginalTangents);

		// could use float3 instead of float4 but NVidia says structures not aligned to 128 bits are slow
		// https://developer.nvidia.com/content/understanding-structured-buffer-performance
		this.bufMorphedVertices = new ComputeBuffer(this.mf.mesh.vertexCount, sizeof(float) * 4);
		this.bufMorphedNormals = new ComputeBuffer(this.mf.mesh.vertexCount, sizeof(float) * 4);
		this.bufMorphedTangents = new ComputeBuffer(this.mf.mesh.vertexCount, sizeof(float) * 4);
		this.bufMorphTemp = new ComputeBuffer(this.mf.mesh.vertexCount, sizeof(float) * 4);

		// bone info buffer

		BoneWeight[] boneWeights = this.mf.mesh.boneWeights;
		var boneWeightInfos = new BoneWeightInfo[boneWeights.Length];
		for (int i = 0; i < boneWeights.Length; i++)
		{
			// this.mf.mesh.boneWeights is an array of classes, we need structs

			boneWeightInfos[i].boneIndex0 = boneWeights[i].boneIndex0;
			boneWeightInfos[i].boneIndex1 = boneWeights[i].boneIndex1;
			boneWeightInfos[i].boneIndex2 = boneWeights[i].boneIndex2;
			boneWeightInfos[i].boneIndex3 = boneWeights[i].boneIndex3;

			boneWeightInfos[i].weight0 = boneWeights[i].weight0;
			boneWeightInfos[i].weight1 = boneWeights[i].weight1;
			boneWeightInfos[i].weight2 = boneWeights[i].weight2;
			boneWeightInfos[i].weight3 = boneWeights[i].weight3;
		}
		this.bufBoneInfo = new ComputeBuffer(boneWeightInfos.Length, sizeof(int) * 4 + sizeof(float) * 4);
		this.bufBoneInfo.SetData(boneWeightInfos);
		this.shaderDQBlend.SetBuffer(this.kernelHandleDQBlend, "bone_weights", this.bufBoneInfo);

		// bind DQ buffer

		Matrix4x4[] bindPoses = this.mf.mesh.bindposes;
		var bindDqs = new DualQuaternion[bindPoses.Length];
		for (int i = 0; i < bindPoses.Length; i++)
		{
			bindDqs[i].rotationQuaternion = bindPoses[i].ExtractRotation().ToVector4();

			Vector3 pos = bindPoses[i].ExtractPosition();
			bindDqs[i].translationQuaternion = new Vector4(pos.x, pos.y, pos.z, 1);
		}

		this.bufBindDq = new ComputeBuffer(bindDqs.Length, sizeof(float) * 8);
		this.bufBindDq.SetData(bindDqs);
		this.shaderComputeBoneDQ.SetBuffer(this.kernelHandleComputeBoneDQ, "bind_dual_quaternions", this.bufBindDq);
	}

	void ApplyAllMorphs()
	{
		this.shaderDQBlend.SetBuffer(
			this.kernelHandleComputeBoneDQ,
			"original_vertices",
			this.ApplyMorphs(
				this.bufOriginalVertices,
				ref this.bufMorphedVertices,
				ref this.bufMorphTemp,
				this.arrBufMorphDeltaVertices,
				this.morphWeights
			)
		);

		this.shaderDQBlend.SetBuffer(
			this.kernelHandleComputeBoneDQ,
			"original_normals",
			this.ApplyMorphs(
				this.bufOriginalNormals,
				ref this.bufMorphedNormals,
				ref this.bufMorphTemp,
				this.arrBufMorphDeltaNormals,
				this.morphWeights
			)
		);

		this.shaderDQBlend.SetBuffer(
			this.kernelHandleComputeBoneDQ,
			"original_tangents",
			this.ApplyMorphs(
				this.bufOriginalTangents,
				ref this.bufMorphedTangents,
				ref this.bufMorphTemp,
				this.arrBufMorphDeltaTangents,
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
				continue;

			if (arrBufDelta[i] == null)
				continue;

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
			return bufOriginal;

		bufSource = bufTarget;
		bufTarget = bufTemp;
		bufTemp = bufSource;

		return bufTarget;
	}

	void ReleaseBuffers()
	{
		this.bufBindDq?.Release();
		this.bufBoneInfo?.Release();
		this.bufOriginalNormals?.Release();
		this.bufOriginalVertices?.Release();
		this.bufOriginalTangents?.Release();
		this.bufPoseDq?.Release();
		this.bufSkinnedDq?.Release();
		this.bufMorphedNormals?.Release();
		this.bufMorphedVertices?.Release();
		this.bufMorphedTangents?.Release();
		this.bufMorphTemp?.Release();

		if (this.arrBufMorphDeltaVertices != null)
			for (int i = 0; i < this.arrBufMorphDeltaVertices.Length; i++)
				this.arrBufMorphDeltaVertices[i]?.Release();

		if (this.arrBufMorphDeltaNormals != null)
			for (int i = 0; i < this.arrBufMorphDeltaNormals.Length; i++)
				this.arrBufMorphDeltaNormals[i]?.Release();

		if (this.arrBufMorphDeltaTangents != null)
			for (int i = 0; i < this.arrBufMorphDeltaTangents.Length; i++)
				this.arrBufMorphDeltaTangents[i]?.Release();
	}

	void OnDestroy()
	{
		this.ReleaseBuffers();	
	}

	// Use this for initialization
	void Start()
	{
		this.shaderComputeBoneDQ = (ComputeShader)Instantiate(this.shaderComputeBoneDQ);	// bug workaround
		this.shaderDQBlend = (ComputeShader)Instantiate(this.shaderDQBlend);                // bug workaround
		this.shaderApplyMorph = (ComputeShader)Instantiate(this.shaderApplyMorph);          // bug workaround

		SkinnedMeshRenderer smr = this.gameObject.GetComponent<SkinnedMeshRenderer>();
		this.mf = this.GetComponent<MeshFilter>();

		this.kernelHandleComputeBoneDQ = this.shaderComputeBoneDQ.FindKernel("CSMain");
		this.kernelHandleDQBlend = this.shaderDQBlend.FindKernel("CSMain");
		this.kernelHandleApplyMorph = this.shaderApplyMorph.FindKernel("CSMain");

		if (smr == null)
		{
			throw new System.Exception("DualQuaternionSkinner requires skinned mesh renderer. It is used to extract some parameters and removed on start.");
		}

		this.materials = smr.materials;
		this.bones = smr.bones;

		this.SetMesh(smr.sharedMesh);

		for (int i = 0; i < this.morphWeights.Length; i++)
		{
			this.morphWeights[i] = smr.GetBlendShapeWeight(i) / 100f;
		}

		Destroy(smr);

		this.started = true;
	}

	// Update is called once per frame
	void Update () {
		this.ApplyAllMorphs();

		this.mf.mesh.MarkDynamic ();    // once or every frame? idk.
										// at least it does not affect performance

		this.shaderComputeBoneDQ.SetVector(
			"parent_rotation_quaternion",
			Quaternion.Inverse(this.transform.parent.rotation).ToVector4()
		);

		this.shaderComputeBoneDQ.SetVector (
			"parent_translation_quaternion", 
			new Vector4(
				- this.transform.parent.position.x,
				- this.transform.parent.position.y,
				- this.transform.parent.position.z,
				1
			)
		);

		this.shaderComputeBoneDQ.SetVector(
			"parent_scale",
			new Vector4(
				this.transform.parent.lossyScale.x,
				this.transform.parent.lossyScale.y,
				this.transform.parent.lossyScale.z,
				1
			)
		);

		for (int i = 0; i < this.bones.Length; i++)
		{
			this.poseDualQuaternions[i].rotationQuaternion = this.bones[i].rotation.ToVector4();

			Vector3 pos = this.bones[i].position;

			// could use float3 instead of float4 for position but NVidia says structures not aligned to 128 bits are slow
			// https://developer.nvidia.com/content/understanding-structured-buffer-performance
			this.poseDualQuaternions[i].translationQuaternion = new Vector4(pos.x, pos.y, pos.z, 1);
		}

		this.bufPoseDq.SetData(this.poseDualQuaternions);

		// Calculate blended quaternions

		int numThreadGroups = this.bones.Length / numthreads;
		if (this.bones.Length % numthreads != 0)
		{
			numThreadGroups ++;
		}

		this.shaderComputeBoneDQ.Dispatch(this.kernelHandleDQBlend, numThreadGroups, 1, 1);

		numThreadGroups = this.mf.mesh.vertexCount / numthreads;
		if (this.mf.mesh.vertexCount % numthreads != 0)
		{
			numThreadGroups++;
		}

		this.shaderDQBlend.Dispatch(this.kernelHandleDQBlend, numThreadGroups, 1, 1);

		foreach (Material m in this.materials)
		{
			m.SetTexture("skinned_data_1", this.rtSkinnedData_1);
			m.SetTexture("skinned_data_2", this.rtSkinnedData_2);
			m.SetTexture("skinned_data_3", this.rtSkinnedData_3);
			m.SetInt("skinned_tex_height", this.mf.mesh.vertexCount / textureWidth);
			m.SetInt("skinned_tex_width", textureWidth);
		}
	}
}

