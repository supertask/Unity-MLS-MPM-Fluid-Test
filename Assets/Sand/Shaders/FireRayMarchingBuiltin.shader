// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'


Shader "VolumetricFire3D/Builtin/FireRayMarching" 
{
	Properties
	{
		//_FireGradient("FireGradient", 2D) = "red" {}
		//_SmokeGradient("SmokeGradient", 2D) = "white" {}

		//_SmokeColor("SmokeColor", Color) = (1, 1, 1, 1)
		//_FireColor("FireColor", Color) = (1, 0.594, 0.282, 1)
		
		//_SmokeAbsorption("SmokeAbsorbtion", float) = 60.0
		//_FireAbsorption("FireAbsorbtion", float) = 40.0
	}
	SubShader 
	{
		Tags {
			"Queue" = "Transparent"
			"RenderType" = "Transparent"
		}

        // No culling or depth
		Cull Off ZWrite Off ZTest Always

		//col.xyz * col.w + backCol.xyz * (1 - col.w)
		Blend SrcAlpha OneMinusSrcAlpha
		
	
		GrabPass{ }
    	Pass 
    	{
    	
    		//Cull front
    		//Blend SrcAlpha OneMinusSrcAlpha

			CGPROGRAM

			#define UNIT_RP__BUILT_IN_RP
			#include "UnityCG.cginc"
			#include "./MpmRayMarchingCore.hlsl"

			#pragma target 5.0
			#pragma vertex vert
			#pragma fragment frag
			
			V2FObjectSpace vert(VertexInput v)
			{
				V2FObjectSpace OUT;

				#if defined(UNIT_RP__BUILT_IN_RP)
					OUT.positionWS = mul(unity_ObjectToWorld, v.positionOS).xyz; //world space position
					OUT.positionCS = UnityObjectToClipPos(v.positionOS); //clip space position
				#elif defined(UNIT_RP__URP)
					OUT.positionWS = TransformObjectToWorld(v.positionOS); //world space position
					OUT.positionCS = TransformWorldToHClip(OUT.positionWS); //clip space position
				#endif

				// Screen position
				//https://gamedev.stackexchange.com/questions/129139/how-do-i-calculate-uv-space-from-world-space-in-the-fragment-shader
				OUT.screenPos = OUT.positionCS.xyw;
				// Correct flip when rendering with a flipped projection matrix.
				// (I've observed this differing between the Unity scene & game views)
				OUT.screenPos.y *= _ProjectionParams.x; //For multi-platform like VR

				//
				// Get view vector
				//
				// Ref. Clouds
				// Camera space matches OpenGL convention where cam forward is -z. In unity forward is positive z.
				// (https://docs.unity3d.com/ScriptReference/Camera-cameraToWorldMatrix.html)
				float2 screenUV = (OUT.screenPos.xy / OUT.screenPos.z) * 0.5f + 0.5f;
				#if defined(UNIT_RP__BUILT_IN_RP) || defined(UNIT_RP__URP)
					float3 viewVector = mul(unity_CameraInvProjection, float4(screenUV * 2 - 1, 0, -1));
					OUT.viewVector = mul(unity_CameraToWorld, float4(viewVector,0));
				#elif defined(UNIT_RP__HDRP)
				#endif

				return OUT;
			}


			float4 frag(V2FObjectSpace IN) : COLOR
			{
				float2 screenUV = (IN.screenPos.xy / IN.screenPos.z) * 0.5f + 0.5f;

				//float viewLength = length(IN.viewVector);

				#if defined(UNIT_RP__BUILT_IN_RP)
					float3 mainLightPosition = _WorldSpaceLightPos0;
					float3 mainLightColor = _LightColor0;
					float3 mainCameraPos = _WorldSpaceCameraPos; 
				#elif defined(UNIT_RP__HDRP)
					DirectionalLightData light = _DirectionalLightDatas[0];
					float3 mainLightPosition = -light.forward.xyz;
					float3 mainLightColor = light.color;
					float3 mainCameraPos = _WorldSpaceCameraPos; 
				#elif defined(UNIT_RP__URP)
					float3 mainLightPosition = _MainLightPosition;
					float3 mainLightColor = _MainLightColor;
					float3 mainCameraPos = _WorldSpaceCameraPos; 
				#endif

				return volumetricRayMarching(
					IN.positionWS,
					screenUV,
					IN.viewVector,
					mainCameraPos,
					mainLightPosition,
					mainLightColor
				);
			}


			ENDCG

    	}
	}
}





















