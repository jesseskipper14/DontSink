Shader "Custom/Water_StencilOnly"
{
    SubShader
    {
        Tags { "Queue"="Geometry+1" "RenderType"="Transparent" }
        ZWrite Off
        ColorMask 0   // <-- only write to stencil
        Cull Off

        Stencil
        {
            Ref 2        // stencil value to write
            Comp Always  // always pass
            Pass Replace // replace stencil
        }

        // A pass is required even if we don’t draw anything
        Pass {}
    }
}
