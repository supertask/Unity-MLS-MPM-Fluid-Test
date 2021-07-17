Shader "Unlit/MpmGrid"
{
	CGINCLUDE
	#include "UnityCG.cginc"
	#include "Constant.hlsl"
	#include "MpmStruct.hlsl"
	#include "Grid.hlsl"

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

	sampler2D _MainTex;
	float4    _MainTex_ST;
	float     _DebugObjectSize;


	//#if defined(LScattering) || defined(LFScattering)
	//	StructuredBuffer<LockMpmCell> _LockGridBuffer;
	//#else
	//	StructuredBuffer<MpmCell> _GridBuffer;
	//#endif
	
	StructuredBuffer<MpmCell> _GridBuffer;
	StructuredBuffer<LockMpmCell> _LockGridBuffer;

	/*
	int     _GridResolutionWidth;
	int     _GridResolutionHeight;
	int     _GridResolutionDepth;
	float     _CellSpacingSize;
	float3     _CellStartPos;
	*/

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
	v2g vert(uint cellIndex : SV_VertexID) // SV_VertexID:
	{
		v2g o = (v2g)0;
		int3 cellIndex3D = CellIndex1DTo3D(cellIndex);
		float3 cellPositionWS = CellIndex3DToPositionWS(cellIndex3D);
		//MpmCell cell = _GridBuffer[cellIndex];
		//float mass = cell.mass;
		//LockMpmCell cell = _GridBuffer[cellIndex];

		#if defined(LScattering) || defined(LFScattering)
			LockMpmCell cell = _LockGridBuffer[cellIndex];
			float mass = ((float)cell.mass) * INT_TO_FLOAT_DIGIT_1;
		#else
			MpmCell cell = _GridBuffer[cellIndex];
			float mass = cell.mass;
		#endif

		//LockMpmCell cell = _GridBuffer[cellIndex];
		//float mass = ((float)cell.mass) * INT_TO_FLOAT_DIGIT_1;


		o.position = cellPositionWS;
		//o.color = random(float2(id, 0));
		o.color = mass > 0 ? float4(1,0,0,1) : float4(1,1,1,1);
		//o.size = mass > 0 ? mass * _DebugObjectSize : _DebugObjectSize;
		o.size = mass * _DebugObjectSize;
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
		return tex2D(_MainTex, i.uv.xy) * i.color;
		//return i.color;
	}
	ENDCG

	SubShader
	{
		Tags{ "RenderType" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" }
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
