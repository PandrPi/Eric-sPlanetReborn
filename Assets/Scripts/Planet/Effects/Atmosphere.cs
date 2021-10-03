using UnityEngine;
using UnityEngine.Rendering.PostProcessing;


[System.Serializable]
[PostProcess(typeof(AtmosphereRenderer), PostProcessEvent.BeforeStack, "Custom/Atmosphere")]
public sealed class Atmosphere : PostProcessEffectSettings
{
	[Range(1f, 1000f)]
	public FloatParameter AtmosphereRadius = new FloatParameter { value = 100f };
	[Range(1f, 1000f)]
	public FloatParameter PlanetRadius = new FloatParameter { value = 50f };
	[Range(0.01f, 50f)]
	public FloatParameter DensityFalloff = new FloatParameter { value = 1f };
	[Range(3, 15)]
	public IntParameter InScatteringPoints = new IntParameter { value = 1 };
	[Range(3, 15)]
	public IntParameter OpticalDepthPoints = new IntParameter { value = 1 };
	public FloatParameter Intensity = new FloatParameter { value = 1f };
	public FloatParameter DitherStrength = new FloatParameter { value = 1f };
	public FloatParameter DitherScale = new FloatParameter { value = 1f };
	public Vector3Parameter PlanetCenter = new Vector3Parameter { value = Vector3.zero };
	public Vector3Parameter ScatteringCoefficients = new Vector3Parameter { value = Vector3.zero };


	public FloatParameter Test1 = new FloatParameter { value = 1f };
	public FloatParameter Test2 = new FloatParameter { value = 1f };
	public FloatParameter Test3 = new FloatParameter { value = 1f };
}

public sealed class AtmosphereRenderer : PostProcessEffectRenderer<Atmosphere>
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

	private const string MAIN_LIGHT_TAG = "MainLight";

	private Transform _directionalLight;
	private Texture _bakedOpticalDepth;
	private Texture _blueNoise;


	public override void Init()
	{
		base.Init();

		GameObject light = GameObject.FindGameObjectWithTag(MAIN_LIGHT_TAG);
		if (light) _directionalLight = light.transform;

		_bakedOpticalDepth = (Texture)Resources.Load("Planet/Atmosphere/bakedOpticalDepth");
		_blueNoise = (Texture)Resources.Load("Planet/Atmosphere/blueNoise");
	}

	public override void Render(PostProcessRenderContext context)
	{
		Camera camera = context.camera;

		var sheet = context.propertySheets.Get(Shader.Find("Hidden/Custom/Atmosphere"));

		sheet.properties.SetTexture(_bakedOpticalDepthID, _bakedOpticalDepth);
		sheet.properties.SetTexture(_blueNoiseID, _blueNoise);

		sheet.properties.SetMatrix(camFrustumID, FrustumCorners(camera));

		sheet.properties.SetFloat(atmosphereRadiusID, settings.AtmosphereRadius);
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
		sheet.properties.SetVector(scatteringCoefficientsID, settings.ScatteringCoefficients);

		context.command.BlitFullscreenTriangle(context.source, context.destination, sheet, 0);
	}

	private Matrix4x4 FrustumCorners(Camera cam)
	{
		Transform camtr = cam.transform;

		System.Array.Clear(frustumCorners, 0, frustumCorners.Length);
		cam.CalculateFrustumCorners(new Rect(0, 0, 1, 1), cam.farClipPlane, cam.stereoActiveEye, frustumCorners);

		Vector3 bottomLeft = camtr.TransformVector(frustumCorners[1]);
		Vector3 topLeft = camtr.TransformVector(frustumCorners[0]);
		Vector3 bottomRight = camtr.TransformVector(frustumCorners[2]);

		Matrix4x4 frustumVectorsArray = Matrix4x4.identity;
		frustumVectorsArray.SetRow(0, bottomLeft);
		frustumVectorsArray.SetRow(1, bottomLeft + (bottomRight - bottomLeft) * 2);
		frustumVectorsArray.SetRow(2, bottomLeft + (topLeft - bottomLeft) * 2);

		return frustumVectorsArray;
	}
}
