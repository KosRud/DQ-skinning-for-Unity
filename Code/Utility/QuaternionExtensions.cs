using UnityEngine;

public static class QuaternionExtensions
{
	public static Vector4 ToVector4(this Quaternion quaternion)
	{
		return new Vector4(quaternion.x, quaternion.y, quaternion.z, quaternion.w);
	}
}