using Unity.Mathematics;
using UnityEngine;

public static class Helper
{
	public static float4x4 Matrix4x4ToFloat4x4(Matrix4x4 m)
	{
		return new float4x4(m.GetColumn(0), m.GetColumn(1), m.GetColumn(2), m.GetColumn(3));
	}
}
