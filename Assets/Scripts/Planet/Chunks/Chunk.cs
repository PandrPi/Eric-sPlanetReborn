using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Chunk : MonoBehaviour
{
	[SerializeField] private ChunkData chunkData;

    void Start()
    {
		chunkData.mesh = new Mesh();
    }

    
    void Update()
    {
		if (Input.GetKeyDown(KeyCode.E)) ChunkGenerationPool.Instance.AddToQueue(chunkData);
	}
}
