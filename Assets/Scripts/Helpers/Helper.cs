using Unity.Mathematics;
using UnityEngine;

public static class Helper
{
	/// <summary>
	/// Converts the specified Matrix4x4 value to a new float4x4 value
	/// </summary>
	/// <param name="m">The matrix value to convert</param>
	/// <returns></returns>
	public static float4x4 Matrix4x4ToFloat4x4(Matrix4x4 m)
	{
		return new float4x4(m.GetColumn(0), m.GetColumn(1), m.GetColumn(2), m.GetColumn(3));
	}

	/// <summary>
	/// Converts the specified Vector3 value to a new float3 value
	/// </summary>
	/// <param name="value">The vector value to convert</param>
	/// <returns></returns>
	public static float3 Vector3ToFloat3(Vector3 value)
	{
		return new float3(value.x, value.y, value.z);
	}
}
