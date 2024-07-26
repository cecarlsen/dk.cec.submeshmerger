/*
	Copyright © Carl Emil Carlsen 2024
	http://cec.dk
*/

Shader "Hidden/HiddenSubmeshMerger"
{
	CGINCLUDE
	
	#include "UnityCG.cginc"

	sampler2D _MainTex;

	float4 _UVTransform;
	float4 _QuadTransform;


	struct ToVertBlit {
		float4 vertex : POSITION;
	};


	struct ToFragBlit {
		float4 vertex : SV_POSITION;
		float2 uv : TEXCOORD0;
	};


	ToFragBlit VertBlitQuad( ToVertBlit i ) // Expects unsiged i.vertex.xy.
	{
		ToFragBlit o;
		o.vertex = float4( ( i.vertex.xy * _QuadTransform.zw + _QuadTransform.xy ) * 2 - 1 , 0.0, 1.0 );
		o.vertex.y *= -1;
		o.uv = i.vertex.xy;
		#if !UNITY_UV_STARTS_AT_TOP
			o.uv.y = 1 - o.uv.y;
		#endif
		o.uv = o.uv * _UVTransform.zw + _UVTransform.xy;
		
		return o;
	}


	float4 Frag( ToFragBlit i ) : SV_Target
	{
		return tex2D( _MainTex, i.uv );
	}
	
	ENDCG
	

	SubShader
	{
		// No culling or depth
		Cull Off ZWrite Off ZTest Always

		Pass
		{
			Name "COPY_QUAD"
			CGPROGRAM
			#pragma target 3.0
			#pragma vertex VertBlitQuad
			#pragma fragment Frag
			ENDCG
		}
	}
}
