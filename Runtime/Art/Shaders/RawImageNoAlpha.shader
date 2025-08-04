Shader "LIV/LCK/RawImageNoAlpha"
{
    
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Cull off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;     
                
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {                                
                float2 uv : TEXCOORD0;     
                float4 vertex : SV_POSITION;
                
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;

            v2f vert (appdata_t v)
            {
                v2f o = (v2f)0;

                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                fixed4 texColor = tex2D(_MainTex, i.uv);
                texColor.a = 1.0; // Force alpha to 1
                return texColor;
            }
            ENDCG
        }
        Pass
        {
            Name "DepthOnly"
            Tags{"LightMode" = "DepthOnly"}
        }
    }
}

