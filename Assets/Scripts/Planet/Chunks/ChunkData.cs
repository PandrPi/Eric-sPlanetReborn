using UnityEngine;

/// <summary>
/// Stores all the needed data about a chunk for the mesh generation
/// </summary>
[System.Serializable]
public class ChunkData
{
	public Mesh mesh;
	public Transform chunkTRS;
	public float meshSize;
	public int meshResolution;
	public MeshFilter meshFilter;
	public MeshRenderer meshRenderer;
	public MeshCollider meshCollider;

	public Matrix4x4 GetTransformationMatrix() => Matrix4x4.TRS(chunkTRS.position, chunkTRS.rotation, chunkTRS.localScale);
}
