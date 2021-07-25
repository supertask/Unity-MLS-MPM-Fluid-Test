Shader "Unlit/MpmParticles"
{
	CGINCLUDE
	#include "UnityCG.cginc"
	#include "MpmStruct.hlsl"

	struct v2g
	{
		float3 position : TEXCOORD0;
		float4 color    : COLOR;
		float  size : TEXCOORD1;
	};
	struct g2f
	{
		float4 position : POSITION;
		float2 uv : TEXCOORD0;
		float4 color    : COLOR;
	};

	StructuredBuffer<MpmParticle> _ParticlesBuffer;
	sampler2D _MainTex;
	float4    _MainTex_ST;
	float     _ParticleSize;
	float4x4  _InvViewMatrix;
	static const float3 g_positions[4] =
	{
		float3(-1, 1, 0),
		float3(1, 1, 0),
		float3(-1,-1, 0),
		float3(1,-1, 0),
	};
	static const float2 g_texcoords[4] =
	{
		float2(0, 0),
		float2(1, 0),
		float2(0, 1),
		float2(1, 1),
	};

	inline float random(float2 _st) {
		return frac(sin(dot(_st.xy,
			float2(12.9898, 78.233))) *
			43758.5453123);
	}

	// --------------------------------------------------------------------
	// Vertex Shader
	// --------------------------------------------------------------------
	v2g vert(uint id : SV_VertexID) // SV_VertexID:
	{
		v2g o = (v2g)0;
		MpmParticle particle = _ParticlesBuffer[id];
		o.position = particle.position;
		//o.position = random(float2(id, 0));
		//o.color = random(float2(id, 0));
		o.color = 1;
		//o.size = particle.type > 0 ? _ParticleSize : 0;
		o.size = _ParticleSize;
		return o;
	}

	[maxvertexcount(4)]
	void geom(point v2g In[1], inout TriangleStream<g2f> SpriteStream)
	{
		g2f o = (g2f)0;
		[unroll]
		for (int i = 0; i < 4; i++)
		{
			float3 position = g_positions[i] * In[0].size;
			position = mul(_InvViewMatrix, position) + In[0].position;
			o.position = UnityObjectToClipPos(float4(position, 1.0));

			o.color = In[0].color;
			o.uv = g_texcoords[i];

			SpriteStream.Append(o);
		}

		SpriteStream.RestartStrip();
	}

	// --------------------------------------------------------------------
	// Fragment Shader
	// --------------------------------------------------------------------
	fixed4 frag(g2f i) : SV_Target
	{
		float4 col = tex2D(_MainTex, i.uv.xy) * i.color;
		return col;
		//return i.color;
	}
	ENDCG

	SubShader
	{
		Tags{
			"Queue" = "Transparent"
			"RenderType" = "Transparent"
			"IgnoreProjector" = "True"
		}
		LOD 100

		ZWrite Off
		Blend One One

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
