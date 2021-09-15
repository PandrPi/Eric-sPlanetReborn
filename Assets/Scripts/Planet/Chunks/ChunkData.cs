using UnityEngine;

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
}
