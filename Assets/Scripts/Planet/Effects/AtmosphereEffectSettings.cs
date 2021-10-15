using Planet.Generation.Jobs;
using Unity.Mathematics;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using Planet.Managers;

[System.Serializable]
[PostProcess(typeof(AtmosphereEffectRenderer), PostProcessEvent.BeforeStack, "Custom/AtmosphereMedium")]
public sealed class AtmosphereEffectSettings : PostProcessEffectSettings
{
	[Range(0f, 1f)]
	public FloatParameter AtmosphereScale = new FloatParameter { value = 1f };
	[Range(1f, 1000f)]
	public FloatParameter PlanetRadius = new FloatParameter { value = 50f };
	[Range(0.01f, 50f)]
	public FloatParameter DensityFalloff = new FloatParameter { value = 1f };
	[Range(3, 100)]
	public IntParameter InScatteringPoints = new IntParameter { value = 1 };
	[Range(3, 100)]
	public IntParameter OpticalDepthPoints = new IntParameter { value = 1 };
	public FloatParameter ScatteringStrength = new FloatParameter { value = 1f };
	public FloatParameter Intensity = new FloatParameter { value = 1f };
	public FloatParameter DitherStrength = new FloatParameter { value = 1f };
	public FloatParameter DitherScale = new FloatParameter { value = 1f };
	public IntParameter OpticalDepthTextureSize = new IntParameter { value = 256 };
	public Vector3Parameter PlanetCenter = new Vector3Parameter { value = Vector3.zero };
	public Vector3Parameter WaveLengths = new Vector3Parameter { value = new Vector3(700, 530, 460) };

	public FloatParameter Test1 = new FloatParameter { value = 1f };
	public FloatParameter Test2 = new FloatParameter { value = 1f };
	public FloatParameter Test3 = new FloatParameter { value = 1f };
}

public sealed class AtmosphereEffectRenderer : PostProcessEffectRenderer<AtmosphereEffectSettings>
{

	private static readonly int atmosphereRadiusID = Shader.PropertyToID("atmosphereRadius");
	private static readonly int planetRadiusID = Shader.PropertyToID("planetRadius");
	private static readonly int planetCenterID = Shader.PropertyToID("planetCentre");
	private static readonly int densityFalloffID = Shader.PropertyToID("densityFalloff");
	private static readonly int camFrustumID = Shader.PropertyToID("_CamFrustum");
	private static readonly int dirToSunID = Shader.PropertyToID("dirToSun");
	private static readonly int inScatteringPointsID = Shader.PropertyToID("numInScatteringPoints");
	private static readonly int opticalDepthPointsID = Shader.PropertyToID("numOpticalDepthPoints");
	private static readonly int intensityID = Shader.PropertyToID("intensity");
	private static readonly int scatteringCoefficientsID = Shader.PropertyToID("scatteringCoefficients");
	private static readonly int ditherStrengthID = Shader.PropertyToID("ditherStrength");
	private static readonly int ditherScaleID = Shader.PropertyToID("ditherScale");

	private static readonly int _bakedOpticalDepthID = Shader.PropertyToID("_BakedOpticalDepth");
	private static readonly int _blueNoiseID = Shader.PropertyToID("_BlueNoise");


	private static readonly int test1ID = Shader.PropertyToID("test1");
	private static readonly int test2ID = Shader.PropertyToID("test2");
	private static readonly int test3ID = Shader.PropertyToID("test3");

	private static readonly Vector3[] frustumCorners = new Vector3[4];
	private static readonly Rect viewportRect = new Rect(0, 0, 1, 1);

	private const string MAIN_LIGHT_TAG = "MainLight";

	private Transform _directionalLight;
	public Texture2D _opticalDepthTexture;
	public Texture _blueNoise;


	public override void Init()
	{
		base.Init();

		// Find the main light GameObject instance
		GameObject light = GameObject.FindGameObjectWithTag(MAIN_LIGHT_TAG);
		if (light) _directionalLight = light.transform;

		_blueNoise = (Texture)Resources.Load("Textures/Planet/Atmosphere/blueNoise");
		// Create an instance for opticalDepthTexture
		//_opticalDepthTexture = new Texture2D(settings.OpticalDepthTextureSize, settings.OpticalDepthTextureSize,
		//	TextureFormat.RGBA32, false);

		PrecomputeOutScattering();
	}

	public override void Render(PostProcessRenderContext context)
	{
		Camera camera = context.camera;

		var sheet = context.propertySheets.Get(Shader.Find("Hidden/Custom/AtmosphereMedium"));

		// Compute values that will be sent to the shader
		var waveLengths = Helper.Vector3ToFloat3(settings.WaveLengths.value);
		float4 scatteringCoefficients = new float4(math.pow(400.0f / waveLengths, 4) * settings.ScatteringStrength, 1.0f);
		float atmosphereRadius = (1.0f + settings.AtmosphereScale) * settings.PlanetRadius;

		//PrecomputeOutScattering();

		sheet.properties.SetTexture(_bakedOpticalDepthID, _opticalDepthTexture);
		sheet.properties.SetTexture(_blueNoiseID, _blueNoise);

		sheet.properties.SetMatrix(camFrustumID, FrustumCorners(camera));

		sheet.properties.SetFloat(atmosphereRadiusID, atmosphereRadius);
		sheet.properties.SetFloat(planetRadiusID, settings.PlanetRadius);
		sheet.properties.SetFloat(densityFalloffID, settings.DensityFalloff);
		sheet.properties.SetFloat(intensityID, settings.Intensity);
		sheet.properties.SetFloat(ditherStrengthID, settings.DitherStrength);
		sheet.properties.SetFloat(ditherScaleID, settings.DitherScale);


		sheet.properties.SetFloat(test1ID, settings.Test1);
		sheet.properties.SetFloat(test2ID, settings.Test2);
		sheet.properties.SetFloat(test3ID, settings.Test3);


		sheet.properties.SetInt(inScatteringPointsID, settings.InScatteringPoints);
		sheet.properties.SetInt(opticalDepthPointsID, settings.OpticalDepthPoints);

		sheet.properties.SetVector(planetCenterID, settings.PlanetCenter);
		sheet.properties.SetVector(dirToSunID, -_directionalLight.forward);
		sheet.properties.SetVector(scatteringCoefficientsID, scatteringCoefficients);

		context.command.BlitFullscreenTriangle(context.source, context.destination, sheet, 0);
	}

	private Matrix4x4 FrustumCorners(Camera cam)
	{
		Transform camtr = cam.transform;

		System.Array.Clear(frustumCorners, 0, frustumCorners.Length);
		cam.CalculateFrustumCorners(viewportRect, cam.farClipPlane, cam.stereoActiveEye, frustumCorners);

		Vector3 bottomLeft = camtr.TransformVector(frustumCorners[1]);
		Vector3 topLeft = camtr.TransformVector(frustumCorners[0]);
		Vector3 bottomRight = camtr.TransformVector(frustumCorners[2]);

		Matrix4x4 frustumVectorsArray = Matrix4x4.identity;
		frustumVectorsArray.SetRow(0, bottomLeft);
		frustumVectorsArray.SetRow(1, bottomLeft + (bottomRight - bottomLeft) * 2);
		frustumVectorsArray.SetRow(2, bottomLeft + (topLeft - bottomLeft) * 2);

		return frustumVectorsArray;
	}

	private void PrecomputeOutScattering()
	{
		if (_opticalDepthTexture is null){
			_opticalDepthTexture = new Texture2D(settings.OpticalDepthTextureSize, settings.OpticalDepthTextureSize,
			TextureFormat.RGBA32, false);
			//Debug.Log("Created");
		}
		// Read the pixel data from the created texture to process it inside the job later
		var textureData = _opticalDepthTexture.GetPixelData<Color32>(0);
		int batchCount = 256;
		Debug.Log(textureData.Length);
		Debug.Log(new float2((textureData.Length - 1) % 256, (textureData.Length - 1) / 256) / (float)256);

		//var temp = new Color()

		//var temp = _opticalDepthTexture.GetPixels32();

		// Create a new job instance for the opticalDepthTexture generation
		var job = new OpticalDepthPrecomputingJob()
		{
			TextureData = textureData,
			atmosphereRadius = (1.0f + settings.AtmosphereScale),
			densityFalloff = settings.DensityFalloff,
			numOutScatteringSteps = settings.OpticalDepthPoints,
			textureSize = settings.OpticalDepthTextureSize,
		};
		job.Execute(0);
		// Schedule and complete the job
		var jobHandle = job.Schedule(textureData.Length, batchCount);
		jobHandle.Complete();

		_opticalDepthTexture.Apply(false);

		if (Application.isPlaying == true)
			ChunkGenerationManager.Instance.SomeTexture = _opticalDepthTexture;
	}
}
