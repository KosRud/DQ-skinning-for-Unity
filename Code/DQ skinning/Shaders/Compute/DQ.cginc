struct dual_quaternion
{
	float4 rotation_quaternion;
	float4 translation_quaternion;
};

float4 QuaternionInvert(float4 q)
{
	q.xyz *= -1;
	return q;
}

float4 QuaternionMultiply(float4 q1, float4 q2)
{
	float w = q1.w * q2.w - dot(q1.xyz, q2.xyz);
	q1.xyz = q2.xyz * q1.w + q1.xyz * q2.w + cross(q1.xyz, q2.xyz);
	q1.w = w;
	return q1;
}

struct dual_quaternion DualQuaternionMultiply(struct dual_quaternion dq1, struct dual_quaternion dq2)
{
	struct dual_quaternion result;

	result.translation_quaternion = QuaternionMultiply(dq1.rotation_quaternion,		dq2.translation_quaternion) + 
									QuaternionMultiply(dq1.translation_quaternion,	dq2.rotation_quaternion);
	
	result.rotation_quaternion = QuaternionMultiply(dq1.rotation_quaternion, dq2.rotation_quaternion);

	float mag = length(result.rotation_quaternion);
	result.rotation_quaternion /= mag;
	result.translation_quaternion /= mag;

	return result;
}

struct dual_quaternion DualQuaternionShortestPath(struct dual_quaternion dq1, struct dual_quaternion dq2)
{
	bool isBadPath = dot(dq1.rotation_quaternion, dq2.rotation_quaternion) < 0;
	dq1.rotation_quaternion		= isBadPath ? -dq1.rotation_quaternion		: dq1.rotation_quaternion;
	dq1.translation_quaternion	= isBadPath ? -dq1.translation_quaternion	: dq1.translation_quaternion;
	return dq1;
}