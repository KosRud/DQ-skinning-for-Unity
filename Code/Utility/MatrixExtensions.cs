using UnityEngine;

public static class MatrixExtensions
{
	public static Quaternion ExtractRotation(this Matrix4x4 matrix)
	{
		return Quaternion.LookRotation(matrix.GetColumn(2), matrix.GetColumn(1));
	}

	public static Vector3 ExtractPosition(this Matrix4x4 matrix)
	{
		Vector4 position;
		position = matrix.GetColumn(3);
		position.w = 1;
		return position;
	}

	public static Vector3 ExtractScale(this Matrix4x4 matrix)
	{
		Vector3 scale;
		scale.x = new Vector4(matrix.m00, matrix.m10, matrix.m20, matrix.m30).magnitude;
		scale.y = new Vector4(matrix.m01, matrix.m11, matrix.m21, matrix.m31).magnitude;
		scale.z = new Vector4(matrix.m02, matrix.m12, matrix.m22, matrix.m32).magnitude;
		return scale;
	}
}