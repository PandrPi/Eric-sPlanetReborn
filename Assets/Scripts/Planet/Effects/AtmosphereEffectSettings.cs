using Planet.Generation.Jobs;
using Unity.Mathematics;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using Planet.Managers;
using UnityEngine.Profiling;

[System.Serializable]
[PostProcess(typeof(AtmosphereEffectRenderer), PostProcessEvent.BeforeStack, "Custom/AtmosphereMedium")]
public sealed class AtmosphereEffectSettings : PostProcessEffectSettings
{
	public FloatParameter AtmosphereRadius = new FloatParameter { value = 100f };
	public FloatParameter PlanetRadius = new FloatParameter { value = 50f };
	[Range(0.01f, 10.0f)]
	public FloatParameter DensityScale = new FloatParameter { value = 4f };
	[Range(0.01f, 1.0f)]
	public FloatParameter AtmosphereIntensity = new FloatParameter { value = 1.0f };
	[Range(1, 100)]
	public IntParameter InScatteringPointsNum = new IntParameter { value = 8 };
	[Range(1, 100)]
	public IntParameter OutScatteringPointsNum = new IntParameter { value = 8 };
	public Vector3Parameter PlanetCenter = new Vector3Parameter { value = Vector3.zero };
	public Vector3Parameter ScatteringCoefficients = new Vector3Parameter { value = new Vector3(0.3f, 0.7f, 1.0f) };
}

public sealed class AtmosphereEffectRenderer : PostProcessEffectRenderer<AtmosphereEffectSettings>
{
	private Transform _directionalLight;
	private Shader _shader;
	private float opticScale;
	private Texture2D _opticalDepthTexture;

	private static readonly int bakedOpticalDepthID = Shader.PropertyToID("_BakedOpticalDepth");
	private static readonly int atmosphereRadiusID = Shader.PropertyToID("AtmosphereRadius");
	private static readonly int planetRadiusID = Shader.PropertyToID("PlanetRadius");
	private static readonly int planetCenterID = Shader.PropertyToID("PlanetCenter");
	private static readonly int densityScaleID = Shader.PropertyToID("DensityScale");
	private static readonly int opticScaleID = Shader.PropertyToID("OpticScale");
	private static readonly int frustumMatrixID = Shader.PropertyToID("FrustumMatrix");
	private static readonly int lightDirectionID = Shader.PropertyToID("LightDirection");
	private static readonly int inScatteringPointsNumID = Shader.PropertyToID("InScatteringPointsNum");
	private static readonly int outScatteringPointsNumID = Shader.PropertyToID("OutScatteringPointsNum");
	private static readonly int atmosphereIntensityID = Shader.PropertyToID("AtmosphereIntensity");
	private static readonly int scatteringCoefficientsID = Shader.PropertyToID("ScatteringCoefficients");

	private static readonly Vector3[] frustumCorners = new Vector3[4];
	private static readonly Rect viewportRect = new Rect(0, 0, 1, 1);

	//private const float densityScaleDividend = 3.8f * 1000.0f; //  = 5.0f;
	private const float opticScaleDividend = 1.0f;
	private const string MAIN_LIGHT_TAG = "MainLight";
	private const string ATMOSPHERE_SCATTERING_SHADER_NAME = "Hidden/Custom/AtmosphereMedium";

	public override void Init()
	{
		base.Init();

		// Find the main light GameObject instance
		GameObject light = GameObject.FindGameObjectWithTag(MAIN_LIGHT_TAG);
		if (light) _directionalLight = light.transform;

		// Find the atmosphere scattering shader
		_shader = Shader.Find(ATMOSPHERE_SCATTERING_SHADER_NAME);

		// Precompute the optical depth texture
		PrecomputeOutScattering();
	}

	public override void Release()
	{
		base.Release();

		// Destroy opticalDepth texture object
		Object.DestroyImmediate(_opticalDepthTexture);
		_opticalDepthTexture = null;
	}

	public override void Render(PostProcessRenderContext context)
	{
		Camera camera = context.camera;

		var sheet = context.propertySheets.Get(_shader);

		opticScale = opticScaleDividend / (settings.AtmosphereRadius - settings.PlanetRadius);
		//float opticScale = opticScaleDividend / (settings.AtmosphereRadius - settings.PlanetRadius);

		sheet.properties.SetMatrix(frustumMatrixID, FrustumCorners(camera));

		sheet.properties.SetTexture(bakedOpticalDepthID, _opticalDepthTexture);

		sheet.properties.SetFloat(atmosphereRadiusID, settings.AtmosphereRadius);
		sheet.properties.SetFloat(planetRadiusID, settings.PlanetRadius);
		sheet.properties.SetFloat(atmosphereIntensityID, settings.AtmosphereIntensity);

		sheet.properties.SetFloat(densityScaleID, settings.DensityScale);
		sheet.properties.SetFloat(opticScaleID, opticScale);

		sheet.properties.SetInt(inScatteringPointsNumID, settings.InScatteringPointsNum);
		sheet.properties.SetInt(outScatteringPointsNumID, settings.OutScatteringPointsNum);

		sheet.properties.SetVector(planetCenterID, settings.PlanetCenter);
		sheet.properties.SetVector(lightDirectionID, _directionalLight.forward);
		sheet.properties.SetVector(scatteringCoefficientsID, settings.ScatteringCoefficients);

		context.command.BlitFullscreenTriangle(context.source, context.destination, sheet, 0);

		OptimizationTestMethod(camera, settings);
	}

	private Matrix4x4 FrustumCorners(Camera cam)
	{
		Transform camTrs = cam.transform;

		System.Array.Clear(frustumCorners, 0, frustumCorners.Length);
		cam.CalculateFrustumCorners(viewportRect, cam.farClipPlane, cam.stereoActiveEye, frustumCorners);

		Vector3 bottomLeft = camTrs.TransformVector(frustumCorners[1]);
		Vector3 topLeft = camTrs.TransformVector(frustumCorners[0]);
		Vector3 bottomRight = camTrs.TransformVector(frustumCorners[2]);

		Matrix4x4 frustumVectorsArray = Matrix4x4.identity;
		frustumVectorsArray.SetRow(0, bottomLeft);
		frustumVectorsArray.SetRow(1, bottomLeft + (bottomRight - bottomLeft) * 2);
		frustumVectorsArray.SetRow(2, bottomLeft + (topLeft - bottomLeft) * 2);

		return frustumVectorsArray;
	}

	private void PrecomputeOutScattering()
	{
		const int textureSize = 256;

		if (_opticalDepthTexture is null)
			_opticalDepthTexture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);

		// Read the pixel data from the created texture to process it inside the job later
		var textureData = _opticalDepthTexture.GetPixelData<Color32>(0);
		// Create a new job instance for the opticalDepthTexture generation
		var job = new OpticalDepthPrecomputingJob()
		{
			TextureData = textureData,
			textureSize = textureSize,
			AtmosphereRadius = settings.AtmosphereRadius / settings.PlanetRadius,
			PlanetRadius = 1.0f,
			OutScatteringPointsNum = settings.OutScatteringPointsNum,
			DensityScale = settings.DensityScale,
		};

		// Schedule and complete the job
		int batchCount = 256;
		var jobHandle = job.Schedule(textureData.Length, batchCount);
		jobHandle.Complete();

		_opticalDepthTexture.Apply(false);

		if (ChunkGenerationManager.Instance is null == false)
			ChunkGenerationManager.Instance.opticalDepthTexture = _opticalDepthTexture;
	}

	const float K_R = 0.166f;
	const float K_M = 0.0025f;
	const float E = 12.0f;        // light intensity
	const float G_M = -0.92f;     // Mie g

	float2 GetRaySphereIntersection(float3 sphereCenter, float3 rayOrigin, float3 rayDir, float radius)
	{
		const float MAX = 1e9f;

		float3 p = rayOrigin - sphereCenter;
		float b = math.dot(p, rayDir);
		float c = math.dot(p, p) - radius * radius;

		float d = b * b - c;
		if (d < 0.0)
		{
			return new float2(MAX, -MAX);
		}

		d = math.sqrt(d);

		return new float2(math.max(0, -b - d), -b + d);
	}

	float CalculateMiePhase(float g, float c, float cc)
	{
		float gg = g * g;
		float a = (1.0f - gg) * (1.0f + cc);
		float b = 1.0f + gg - 2.0f * g * c;
		b *= math.sqrt(b);
		b *= 2.0f + gg;
		return 1.5f * a / b;
	}

	float CalculateDensity(float3 p, AtmosphereEffectSettings settings)
	{
		float heightAboveSurface = math.length(p - (float3)settings.PlanetCenter.value) - settings.PlanetRadius;
		float height01 = heightAboveSurface / (settings.AtmosphereRadius - settings.PlanetRadius);
		return math.exp(-height01 * settings.DensityScale) * (1 - height01);
		//return math.exp(-(math.length(p - (float3)settings.PlanetCenter.value) - settings.PlanetRadius) * densityScale);
	}

	float CalculateOpticalDepth(float3 p, float3 q, AtmosphereEffectSettings settings)
	{
		float3 step = (q - p) / (float)settings.OutScatteringPointsNum;
		float3 v = p + step * 0.5f;

		float sum = 0.0f;
		for (int i = 0; i < settings.OutScatteringPointsNum; i++)
		{
			sum += CalculateDensity(v, settings);
			v += step;
		}

		sum *= math.length(step) * opticScale;
		return sum;
	}

	float3 CalculateAtmosphereScattering(float3 rayOrigin, float3 rayDir, float2 intersectionInfo, float3 lightDir, AtmosphereEffectSettings settings)
	{
		float len = (intersectionInfo.y - intersectionInfo.x) / (float)settings.InScatteringPointsNum;
		float3 step = rayDir * len;
		float3 p = rayOrigin + rayDir * intersectionInfo.x;
		float3 v = p + rayDir * (len * 0.5f);

		float3 sum = new float3(0.0f, 0.0f, 0.0f);
		for (int i = 0; i < settings.InScatteringPointsNum; i++)
		{
			float2 f = GetRaySphereIntersection((float3)settings.PlanetCenter.value, v, lightDir, settings.AtmosphereRadius);
			float3 u = v + lightDir * f.y;

			//float n = (1 + 1) * (PI * 4.0);
			float n1 = CalculateOpticalDepth(p, v, settings);
			float n2 = CalculateOpticalDepth(v, u, settings);
			float n = (n1 + n2) * (math.PI * 4.0f);
			sum += CalculateDensity(v, settings) * math.exp(-n * (K_R * (float3)settings.ScatteringCoefficients.value + K_M));
			v += step;
		}

		sum *= len * opticScale;

		float c = math.dot(rayDir, -lightDir);
		float cc = c * c;
		float3 rayleighScattering = K_R * (float3)settings.ScatteringCoefficients.value * 0.75f * (1.0f + cc);
		float3 result = sum * (rayleighScattering + K_M * CalculateMiePhase(G_M, c, cc)) * E;
		return math.saturate(result);
	}

	private void OptimizationTestMethod(Camera camera, AtmosphereEffectSettings settings)
	{
		//Debug.Log("Start!");

		//float4 originalColor = new float4(0.0f);

		//float3 rayOrigin;
		//float3 rayDir;

		//rayOrigin = camera.transform.position;
		//rayDir = camera.transform.forward;

		//float2 intersectionInfo = GetRaySphereIntersection(settings.PlanetCenter.value, rayOrigin, rayDir, settings.AtmosphereRadius);
		//if (intersectionInfo.x > intersectionInfo.y)
		//{
		//	Debug.Log("intersectionInfo.x > intersectionInfo.y");
		//	return;
		//}

		//float3 scattering = CalculateAtmosphereScattering(rayOrigin, rayDir, intersectionInfo, -_directionalLight.forward, settings);
		//// Combine the original color with the atmosphere scattering color
		//float3 color = math.saturate(originalColor.xyz + scattering * settings.AtmosphereIntensity);
		//Debug.Log(color);

		//Debug.Log("End");
	}
}
