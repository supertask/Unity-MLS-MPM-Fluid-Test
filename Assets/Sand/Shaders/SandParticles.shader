Shader "Unlit/Sand"
{
	Properties
    {
        _MainTex("Main Texture", 2D) = "white" {}
        _SandTex("Light mask A1 Texture", 2D) = "white" {}
	}

	CGINCLUDE

	#define NUM_OF_RECT_VERTEX 4
	#define NUM_OF_UP_RES 10
	#include "UnityCG.cginc"
	#include "Assets/Common/Shaders/ConstantUtil.hlsl"
	#include "Assets/Common/Shaders/Quaternion.hlsl"
	#include "Assets/Common/Shaders/RandomUtil.hlsl"
	#include "Assets/MLS-MPM-Core/Shaders/MpmStruct.hlsl"

	struct v2g
	{
		float3 position : TEXCOORD0;
		float4 color    : COLOR;
		float  size : TEXCOORD1;
		float3 viewVector : TEXCOORD2;
	};
	struct g2f
	{
		float4 position : POSITION;
		float2 uv : TEXCOORD0;
		uint index : TEXCOORD1;
		float4 color    : COLOR;
	};

	StructuredBuffer<MpmParticle> _ParticlesBuffer;
	sampler2D _MainTex;
	float4    _MainTex_ST;
	sampler2D _SandTex;
	float4    _SandTex_ST;


	float4     _StartColor;
	float4     _EndColor;
	float     _ParticleSize;
	float     _UpresExtendSize;
	float4x4  _InvViewMatrix;
	static const float3 neighbourPositions[4] =
	{
		float3(-1, 1, 0),
		float3(1, 1, 0),
		float3(-1,-1, 0),
		float3(1,-1, 0),
	};
	static const float2 neighbourTexcoords[4] =
	{
		float2(0, 0),
		float2(1, 0),
		float2(0, 1),
		float2(1, 1),
	};



	// --------------------------------------------------------------------
	// Vertex Shader
	// --------------------------------------------------------------------
	v2g vert(uint id : SV_VertexID) // SV_VertexID:
	{
		v2g OUT = (v2g)0;
		MpmParticle particle = _ParticlesBuffer[id];
		OUT.position = particle.position;
		//OUT.position = random(float2(id, 0));
		//OUT.color = random(float2(id, 0));
		OUT.color = 1;
		//OUT.size = particle.type > 0 ? _ParticleSize : 0;
		OUT.size = _ParticleSize;

		OUT.viewVector = normalize(ObjSpaceViewDir(float4(particle.position, 0)));


		return OUT;
	}

	[maxvertexcount(NUM_OF_RECT_VERTEX * NUM_OF_UP_RES)]
	void geom(point v2g In[1], uint pid : SV_PrimitiveID, inout TriangleStream<g2f> SpriteStream)
	{
		g2f OUT = (g2f)0;
		float3 centerPosition = In[0].position;

		[unroll]
		for (int i = 0; i < NUM_OF_RECT_VERTEX * NUM_OF_UP_RES; i++)
		{
			int vi = (int) (i % NUM_OF_RECT_VERTEX); //vertex index, 0 ~ 4
			int pi = (int) (i / NUM_OF_RECT_VERTEX); //particle index, 0 ~ 10
			OUT.index = pid * pi;

			if ( (vi % NUM_OF_RECT_VERTEX == 0)) {
				SpriteStream.RestartStrip();
			}

			float3 addtionalParticlePos = centerPosition +
				_UpresExtendSize * randomPosOnSphere(OUT.index, 0); //random3D(float2(pi,0));

			float3 positionWS = neighbourPositions[vi] * In[0].size;
			positionWS = mul(_InvViewMatrix, positionWS) + addtionalParticlePos;

			//TODO(Tasuku): テクスチャを回転させる
			//float3 centerToVertexPositionWS = positionWS - centerPosition;
			//centerToVertexPositionWS = rotateAngleAxis(
			//	centerToVertexPositionWS, In[0].viewVector, PI * 0.25);
			//positionWS = centerPosition + centerToVertexPositionWS;

			OUT.position = UnityObjectToClipPos(float4(positionWS, 1.0));

			OUT.color = In[0].color;
			OUT.uv = neighbourTexcoords[vi];

			SpriteStream.Append(OUT);

		}

		SpriteStream.RestartStrip();
	}

	// --------------------------------------------------------------------
	// Fragment Shader
	// --------------------------------------------------------------------
	fixed4 frag(g2f i) : SV_Target
	{
		float texIndex = floor(random(float2(i.index, 0)) * 16.0);
		float2 texIndex2D = float2(texIndex % 4, floor(texIndex / 4.0)); // texIndex / 4);
		float4 col = tex2D(_MainTex, i.uv.xy * 0.25 + 0.25 * texIndex2D);
		float rand = random( float2(random(float2(i.index, 0)), 0) );
		col *= lerp(_StartColor, _EndColor, rand);	
		return col;
		//return tex2D(_SandTex, i.uv.xy * 0.25) * _EndColor; // * i.color;
		//return float4(0,1,0,1);
		//return i.color;
	}
	ENDCG

	SubShader
	{
		Tags{
			"Queue"="Transparent"
			"RenderType" = "Transparent"
			//"Queue"="Geometry"
			//"RenderType" = "Opaque"
			"IgnoreProjector" = "True"
		}
		LOD 100

		//Blend One One
		ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        //Cull front 

		Pass
		{
			CGPROGRAM
			#pragma target   5.0
			#pragma vertex   vert
			#pragma geometry geom
			#pragma fragment frag
			ENDCG
		}
	}
}
