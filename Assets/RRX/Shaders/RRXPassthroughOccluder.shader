Shader "RRX/PassthroughOccluder"
{
    // Depth-only occluder for MR: writes Z so virtual geometry outside the tube fails the depth test, but does
    // not write color — passthrough / alpha-0 clear stays visible (no black ring). Queue before default Geometry
    // so mall/walls draw after and respect this depth.
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry-400" }
        Pass
        {
            ColorMask 0
            Cull Off
            ZWrite On
            ZTest LEqual

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
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return fixed4(0.0, 0.0, 0.0, 1.0);
            }
            ENDCG
        }
    }
    FallBack Off
}
