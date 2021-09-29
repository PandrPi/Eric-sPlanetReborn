using Planet.Noises;
using System.Collections.Generic;
using Unity.Jobs;
using UnityEngine;

namespace Planet.Managers
{
	/// <summary>
	/// Manages the ChunkGenerator instance and gives it chunks to generate
	/// </summary>
	public class ChunkGenerationManager : MonoBehaviour
	{
		public static ChunkGenerationManager Instance { get; private set; }

		[SerializeField, Range(2, MaxMeshResolution)] private int maxChunkMeshResolution;
		[SerializeField] private bool completeImmediately;
		[SerializeField] private int chunksInQueue;
		[SerializeField] public ValueDerivativeNoise valueDerivativeNoise;
		[SerializeField] public SimplexNoise simplexNoise;
		[SerializeField] public RidgedNoise ridgedNoise;

		private ChunkGenerator chunkGenerator;
		private ChunkData currentChunkData;
		private JobHandle currentJobHandle;
		private readonly Queue<ChunkData> chunksToGenerate = new Queue<ChunkData>();


		private const int MaxMeshResolution = 250;

		private void Awake()
		{
			Instance = this;
			chunkGenerator = new ChunkGenerator(maxChunkMeshResolution);

		}

		private void Update()
		{
			// Chunks are generated one by one. We use Jobs to generate a mesh data in parallel, not the chunks in parallel, so 
			// there is no case when several chunks are generated simultaneously 
			if (chunkGenerator.IsFree == true)
			{
				if (chunksToGenerate.Count > 0)
				{
					// Here we can start generating the chunk
					// Extract the cunk data from the queue
					currentChunkData = chunksToGenerate.Dequeue();
					chunksInQueue--;
					// Start chunk generation
					currentJobHandle = chunkGenerator.StartChunkGeneration(currentChunkData);					

					// Check whether we want to complete all the jobs immediately. This will freeze the main thread
					// for as long as the chunk is generated, so we can see some peaks at the frame time graph in the Profiler.
					if (completeImmediately == true) CompleteChunkGeneration();
				}
			}
			else
			{
				// Here we can ckeck whether our currentJobHandle is completed
				if (currentJobHandle.IsCompleted == true)
				{
					CompleteChunkGeneration();
				}
			}
		}

		/// <summary>
		/// Completes the current chunk generation jobs
		/// </summary>
		private void CompleteChunkGeneration()
		{
			// Our generation jobs is completed but we have to manually call the Complete method
			currentJobHandle.Complete();
			chunkGenerator.AssignMeshData(currentChunkData);
			
			// Remove reference to the current ChunkData object
			currentChunkData = null;
		}

		/// <summary>
		/// Adds the specified ChunkData object to the generation queue
		/// </summary>
		/// <param name="chunkData">The ChunkData object that stores an actual data for generation</param>
		public void AddToQueue(ChunkData chunkData)
		{
			chunksToGenerate.Enqueue(chunkData);
			chunksInQueue++;
		}

		private void OnDestroy()
		{
			// Release resources here
			chunkGenerator.Dispose();
		}
	}

}