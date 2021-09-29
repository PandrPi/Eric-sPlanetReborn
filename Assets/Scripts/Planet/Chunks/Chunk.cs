using Planet.Managers;
using UnityEngine;

namespace Planet.Chunks
{
	/// <summary>
	/// Represents a QuadTree node of a planet
	/// </summary>
	public class Chunk : MonoBehaviour, IQuadTreeNode
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
			if (Input.GetKeyDown(KeyCode.E)) ChunkGenerationManager.Instance.AddToQueue(chunkData);
		}

		public IQuadTreeNode GetParent()
		{
			throw new System.NotImplementedException();
		}

		public IQuadTreeNode[] GetChildren()
		{
			throw new System.NotImplementedException();
		}

		public void Divide()
		{
			throw new System.NotImplementedException();
		}

		public void Initialize()
		{
			throw new System.NotImplementedException();
		}
	}
}