Shader "Custom/GridShader"
{
    Properties
    {
        _GridColor ("Grid Color", Color) = (1,1,1,1)
        _BackgroundColor ("Background Color", Color) = (0.25,0.25,0.25,1)
        _GridSize ("Grid Size", Float) = 1
        _LineWidth ("Line Width", Float) = 0.04
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 worldXZ : TEXCOORD0;
            };

            float4 _GridColor;
            float4 _BackgroundColor;
            float _GridSize;
            float _LineWidth;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldXZ = v.vertex.xz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 grid = frac(i.worldXZ / _GridSize);
                float g =
                    step(grid.x, _LineWidth) +
                    step(grid.y, _LineWidth);

                g = saturate(g);
                return lerp(_BackgroundColor, _GridColor, g);
            }
            ENDCG
        }
    }
}
