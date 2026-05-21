Shader "Custom/BottleLiquid_UI"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}

        _C1 ("Color 1", Color) = (1,0,0,1)
        _C2 ("Color 2", Color) = (0,1,0,1)
        _C3 ("Color 3", Color) = (0,0,1,1)
        _C4 ("Color 4", Color) = (1,1,0,1)

        _FillAmount ("Fill Amount", Range(0,1)) = 1

        _Color1Max ("Color 1 Height", Range(0,1)) = 0.25
        _Color2Max ("Color 2 Height", Range(0,1)) = 0.25
        _Color3Max ("Color 3 Height", Range(0,1)) = 0.25
        _Color4Max ("Color 4 Height", Range(0,1)) = 0.25

        _BottomOffset ("Bottom Offset", Range(0,1)) = 0.0
        _TopOffset    ("Top Offset",    Range(0,1)) = 1.0

        _StencilComp     ("Stencil Comparison",  Float) = 8
        _Stencil         ("Stencil ID",          Float) = 0
        _StencilOp       ("Stencil Operation",   Float) = 0
        _StencilWriteMask("Stencil Write Mask",  Float) = 255
        _StencilReadMask ("Stencil Read Mask",   Float) = 255
        _ColorMask       ("Color Mask",          Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
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
            float  _FillAmount;
            float  _Color1Max, _Color2Max, _Color3Max, _Color4Max;
            float  _BottomOffset, _TopOffset;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                float4 color  : COLOR;
            };

            struct v2f
            {
                float2 uv    : TEXCOORD0;
                float4 pos   : SV_POSITION;
                float4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos   = UnityObjectToClipPos(v.vertex);
                o.uv    = v.uv;
                o.color = v.color;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float4 spr = tex2D(_MainTex, i.uv);

                // Sprite alpha clips the bottle shape — outside glass = transparent
                if (spr.a < 0.01)
                    return float4(0, 0, 0, 0);

                // Remap UV to the adjustable bottle body range
                float uvY = (i.uv.y - _BottomOffset) / max(_TopOffset - _BottomOffset, 0.001);

                // Above fill level = transparent (empty space inside bottle)
                if (uvY > _FillAmount)
                    return float4(0, 0, 0, 0);

                // Below 0 = below bottle body
                if (uvY < 0.0)
                    return float4(0, 0, 0, 0);

                // Normalize within filled area
                float fillY = saturate(uvY / max(_FillAmount, 0.001));

                // Auto normalize layer heights
                float total = max(_Color1Max + _Color2Max + _Color3Max + _Color4Max, 0.001);
                float n1 = _Color1Max / total;
                float n2 = _Color2Max / total;
                float n3 = _Color3Max / total;

                float h1 = n1;
                float h2 = n1 + n2;
                float h3 = n1 + n2 + n3;

                float4 col;
                if      (fillY < h1) col = _C1;
                else if (fillY < h2) col = _C2;
                else if (fillY < h3) col = _C3;
                else                 col = _C4;

                col.a = spr.a * i.color.a;
                return col;
            }
            ENDCG
        }
    }
}