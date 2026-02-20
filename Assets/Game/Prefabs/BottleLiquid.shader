// Upgrade NOTE: commented out 'float4x4 _Object2World', a built-in variable
// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

Shader "Custom/BottleLiquid"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}

        _C1 ("Color 1", Color) = (1,0,0,1)
        _C2 ("Color 2", Color) = (0,1,0,1)
        _C3 ("Color 3", Color) = (0,0,1,1)
        _C4 ("Color 4", Color) = (1,1,0,1)

        _FillAmount ("Fill Amount", Range(0,1)) = 1
        _CurveStrength ("Curve Strength", Float) = 0.15
        _SARM ("ScaleAndRotationMultiplier", Float) = 1.0

        _Color1Max ("Color 1 Height", Range(0,1)) = 0.25
        _Color2Max ("Color 2 Height", Range(0,1)) = 0.25
        _Color3Max ("Color 3 Height", Range(0,1)) = 0.25
        _Color4Max ("Color 4 Height", Range(0,1)) = 0.25

        _MaxFillHeight ("Max Fill Height", Range(0,1)) = 0.75
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;

            float4 _C1, _C2, _C3, _C4;
            float _FillAmount, _CurveStrength, _SARM;
            float _Color1Max, _Color2Max, _Color3Max, _Color4Max;
            float _MaxFillHeight;

            // float4x4 _Object2World;

            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f {
                float2 uv : TEXCOORD0;
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float4 spr = tex2D(_MainTex, i.uv);

                if (spr.a < 0.001)
                    return float4(0,0,0,0);

                //----------------------------------------------------------------
                // ROTATION FIX → Make liquid level rotate with bottle
                //----------------------------------------------------------------

                // Bottle up direction in world
                float3 upDir = normalize(unity_ObjectToWorld._m01_m11_m21);

                // World position of current pixel
                float3 wp = i.worldPos;

                // Bottle origin (pivot)
                float3 origin = unity_ObjectToWorld._m03_m13_m23;

                // Local Y relative to rotated bottle
                float localY = dot((wp - origin), upDir);

                // Convert world-localY back into 0–1 UV space
                float curvedY = localY / _SARM;

                //----------------------------------------------------------------
                // APPLY CURVE AT TOP BASED ON UV.X
                //----------------------------------------------------------------

                float cx = i.uv.x - 0.5;
                float curve = -(cx * cx) * _CurveStrength * _SARM;

                curvedY += curve;

                //----------------------------------------------------------------
                // FILL LIMIT
                //----------------------------------------------------------------

                float maxH = _FillAmount * _MaxFillHeight;

                if (curvedY > maxH)
                    return float4(0,0,0,0);

                float fillY = curvedY / maxH;

                //----------------------------------------------------------------
                // AUTO NORMALIZATION OF LAYER HEIGHTS
                //----------------------------------------------------------------

                float total = max(_Color1Max + _Color2Max + _Color3Max + _Color4Max, 0.001);

                float n1 = _Color1Max / total;
                float n2 = _Color2Max / total;
                float n3 = _Color3Max / total;
                float n4 = _Color4Max / total;

                float h1 = n1;
                float h2 = n1 + n2;
                float h3 = n1 + n2 + n3;

                float4 col;

                if (fillY < h1) col = _C1;
                else if (fillY < h2) col = _C2;
                else if (fillY < h3) col = _C3;
                else col = _C4;

                col.a = spr.a;
                return col;
            }
            ENDCG
        }
    }
}
