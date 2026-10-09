Shader "Custom/CelestialStarBatch2D"
{
    Properties
    {
        _MainTex ("Star Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "DisableBatching"="True" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha One
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #define CELESTIAL_ADDITIVE
            #include "CelestialBatch.cginc"
            ENDCG
        }
    }
}
