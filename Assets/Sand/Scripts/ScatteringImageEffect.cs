using System;
using UnityEngine;

using ImageEffectUtil;

namespace MlsMpm.Sand
{
	//[ExecuteInEditMode, ImageEffectAllowedInSceneView]
	[ImageEffectAllowedInSceneView]
    public class ScatteringImageEffect : ImageEffectBase
    {
        public const string HEADER_DECORATION = " --- ";

		[Header (HEADER_DECORATION + "System" + HEADER_DECORATION)]
        public GameObject mediator;
        public GpuMpmParticleSystem mpmPS;


        [Header (HEADER_DECORATION + "Marching settings" + HEADER_DECORATION)]
		public float rayOffsetStrength = 1.0f;

		[Header (HEADER_DECORATION + "Base Shape" + HEADER_DECORATION)]
		public float densityOffset = 150;
		public float smokeAbsorption = 60.0f;

		[Header (HEADER_DECORATION + "Lighting" + HEADER_DECORATION)]
		public float lightAbsorptionTowardSun = 1.21f;
		public float lightAbsorptionThroughCloud = 0.75f;
		[Range(0, 1)] public float darknessThreshold = 0.15f;

		[Range (0, 1)] public float forwardScattering = 0.811f;
		[Range (0, 1)] public float backScattering = 0.33f;
		[Range (0, 10)] public float baseBrightness = 1.0f; //should be 1, maybe
		[Range (0, 1)] public float phaseFactor = 0.488f;

		public float fireIntensity = 1.0f;


        protected override void Start() {
            base.Start();
        }

		//[ImageEffectOpaque]
        protected override void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            Bounds bounds  = this.mpmPS.GetGridBounds();

			this.imageEffectMat.SetFloat ("_FireIntensity", fireIntensity);
			this.imageEffectMat.SetFloat ("_DensityOffset", densityOffset);
            
			this.imageEffectMat.SetVector ("_PhaseParams", new Vector4 (forwardScattering, backScattering, baseBrightness, phaseFactor));
			this.imageEffectMat.SetFloat ("_RayOffsetStrength", rayOffsetStrength);

			this.imageEffectMat.SetFloat("_SmokeAbsorption", smokeAbsorption);
			this.imageEffectMat.SetFloat("_LightAbsorptionTowardSun", lightAbsorptionTowardSun);
			this.imageEffectMat.SetFloat("_LightAbsorptionThroughCloud", lightAbsorptionThroughCloud);
			this.imageEffectMat.SetFloat("_DarknessThreshold", darknessThreshold);
			//Debug.Log("_LightAbsorptionThroughCloud: " + lightAbsorptionThroughCloud);
			
			this.imageEffectMat.SetVector("_BoundingPosition", this.mediator.transform.position);
			this.imageEffectMat.SetVector("_BoundingScale", bounds.size);
            //this.imageEffectMat.SetVector ("_BoundsMin", bounds.min);
			//this.imageEffectMat.SetVector ("_BoundsMax", bounds.max);

			this.imageEffectMat.SetBuffer("_GridBuffer", this.mpmPS.LockGridBuffer);
			this.imageEffectMat.SetVector("_GridDimension", this.mpmPS.GetGridDimension());
			//Debug.Log("grid dimension: " + this.mpmPS.GetGridDimension() );


            Graphics.Blit(src, dst, this.imageEffectMat);
        }

    }
}