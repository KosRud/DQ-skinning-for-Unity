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

float4 QuaternionApplyRotation(float4 v, float4 rotQ)
{
	v = QuaternionMultiply(rotQ, v);
	return QuaternionMultiply(v, QuaternionInvert(rotQ));
}

inline float signNoZero(float x)
{
	float s = sign(x);
	if (s)
		return s;
	return 1;
}

struct dual_quaternion DualQuaternionFromMatrix4x4(float4x4 m)
{
	struct  dual_quaternion dq;

	// http://www.euclideanspace.com/maths/geometry/rotations/conversions/matrixToQuaternion/index.htm
	// Alternative Method by Christian
	dq.rotation_quaternion.w = sqrt(max(0, 1.0 + m[0][0] + m[1][1] + m[2][2])) / 2.0;
	dq.rotation_quaternion.x = sqrt(max(0, 1.0 + m[0][0] - m[1][1] - m[2][2])) / 2.0;
	dq.rotation_quaternion.y = sqrt(max(0, 1.0 - m[0][0] + m[1][1] - m[2][2])) / 2.0;
	dq.rotation_quaternion.z = sqrt(max(0, 1.0 - m[0][0] - m[1][1] + m[2][2])) / 2.0;
	dq.rotation_quaternion.x *= signNoZero(m[2][1] - m[1][2]);
	dq.rotation_quaternion.y *= signNoZero(m[0][2] - m[2][0]);
	dq.rotation_quaternion.z *= signNoZero(m[1][0] - m[0][1]);

	dq.rotation_quaternion = normalize(dq.rotation_quaternion);	// ensure unit quaternion

	dq.translation_quaternion = float4(m[0][3], m[1][3], m[2][3], 0);
	dq.translation_quaternion = QuaternionMultiply(dq.translation_quaternion, dq.rotation_quaternion) * 0.5;

	return dq;
}