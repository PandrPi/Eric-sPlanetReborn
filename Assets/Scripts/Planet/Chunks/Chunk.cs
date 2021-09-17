using UnityEngine;

public class Chunk : MonoBehaviour
{
	[SerializeField] private ChunkData chunkData;

    void Start()
    {
		InitializeChunkData();
    }

	private void InitializeChunkData()
	{
		chunkData.mesh = new Mesh();
		chunkData.chunkTRS = transform;
		//chunkData.meshSize = ;
		//chunkData.meshResolution = ;
		chunkData.meshFilter = GetComponent<MeshFilter>();
		chunkData.meshRenderer = GetComponent<MeshRenderer>();
		chunkData.meshCollider = GetComponent<MeshCollider>();
	}

    
    void Update()
    {
		if (Input.GetKeyDown(KeyCode.E)) ChunkGenerationPool.Instance.AddToQueue(chunkData);
	}
}
