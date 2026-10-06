Shader "Hidden/DontSink/PilotingLand"
{
    Properties { _MainTex ("Input",2D)="white"{} _Heights ("Geographic heights",2D)="black"{} _Overlay ("Local decorations",2D)="black"{} _DepthCurve ("Geographic depth",2D)="black"{} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _Heights, _Overlay, _DepthCurve;
            float4 _Heights_TexelSize, _Bounds, _Origin, _AxisX, _AxisY;
            float4 _WaterColor, _LandColor;
            float _Sea, _Visibility, _Circle, _FadeStart, _WaveMarks, _ViewTime, _HasOverlay;
            float _DepthEnabled, _DangerDepth, _CoastalBand;
            float4 _DockLines[30];
            int _DockLineCount;
            float4 OutputColor(float4 color)
            {
                // The previous CPU texture stored display-space colors. Preserve them
                // when this shader writes an sRGB render target in a linear project.
                #ifndef UNITY_COLORSPACE_GAMMA
                color.rgb = GammaToLinearSpace(color.rgb);
                #endif
                return color;
            }
            float4 frag(v2f_img input) : SV_Target
            {
                float2 uv = input.uv;
                float2 world = _Origin.xy + _AxisX.xy*uv.x + _AxisY.xy*uv.y;
                float2 mapUV = (world-_Bounds.xy)/_Bounds.zw;
                float validY = step(0,mapUV.y)*step(mapUV.y,1);
                // Match Sample01UV's grid endpoints, horizontal repeat and latitude clamp.
                float2 grid = float2(frac(mapUV.x),saturate(mapUV.y))*(_Heights_TexelSize.zw-1);
                float2 cell = floor(grid), next = min(cell+1,_Heights_TexelSize.zw-1);
                float2 weight = grid-cell;
                float a = tex2D(_Heights,(cell+.5)*_Heights_TexelSize.xy).r;
                float b = tex2D(_Heights,(float2(next.x,cell.y)+.5)*_Heights_TexelSize.xy).r;
                float c = tex2D(_Heights,(float2(cell.x,next.y)+.5)*_Heights_TexelSize.xy).r;
                float d = tex2D(_Heights,(next+.5)*_Heights_TexelSize.xy).r;
                float elevation = lerp(lerp(a,b,weight.x),lerp(c,d,weight.x),weight.y);
                float land = step(_Sea,elevation)*validY;
                if (_Circle < .5)
                {
                    float shore = (1-land)*step(_Sea-.003,elevation)*validY;
                    return OutputColor(land > .5 ? float4(_LandColor.rgb,_Visibility) : float4(.70,.66,.44,shore*_Visibility*.45));
                }
                float2 relative = uv*2-1;
                float distance = length(relative);
                float edge = 1-smoothstep(.96,1,distance);
                float visible = _Visibility > .0001 ? 1-smoothstep(_Visibility*clamp(_FadeStart,0,.99),_Visibility,distance) : 0;
                float landAlpha = land*visible;
                float4 color = lerp(_WaterColor,_LandColor,landAlpha);
                if (_Circle > 1.5)
                {
                    color = lerp(_WaterColor,float4(_LandColor.rgb,1),land*_Visibility);
                    float normalizedDepth = saturate((_Sea-elevation)/max(.0001,_Sea));
                    float curveIndex = normalizedDepth*1023;
                    float curveCell = floor(curveIndex);
                    float physicalDepth = lerp(tex2D(_DepthCurve,float2((curveCell+.5)/1024,.5)).r,
                        tex2D(_DepthCurve,float2((min(1023,curveCell+1)+.5)/1024,.5)).r,frac(curveIndex));
                    // Match BoatCoastalSurface.Target with the geographic base depth.
                    float blend = smoothstep(0,_CoastalBand,normalizedDepth);
                    physicalDepth *= blend*(2-blend);
                    float wet = (1-land)*validY*_DepthEnabled*_Visibility;
                    float shallow = wet*(1-smoothstep(_DangerDepth*1.5,_DangerDepth*5,physicalDepth));
                    float shoal = wet*(1-smoothstep(0,_DangerDepth*1.5,physicalDepth));
                    // Keep the depth tint translucent enough to reveal the
                    // existing wave crests and drifting foam beneath it.
                    color = lerp(color,float4(.22,.55,.59,.62),shallow);
                    color = lerp(color,float4(.66,.66,.46,.65),shoal);
                }
                float wave = sin(relative.y*41+sin(relative.x*17)+_ViewTime*.35);
                float sparkle = pow(max(0,wave),24)*_WaveMarks*(1-landAlpha);
                color.rgb += sparkle*float3(.07,.10,.10);
                float4 decoration = _HasOverlay > .5 ? tex2D(_Overlay,uv) : 0;
                #ifndef UNITY_COLORSPACE_GAMMA
                decoration.rgb = LinearToGammaSpace(decoration.rgb);
                #endif
                color.rgb = lerp(color.rgb,decoration.rgb,decoration.a);
                color.a = lerp(color.a,1,decoration.a);
                if (_Circle > 1.5)
                    for(int segment=0;segment<_DockLineCount;segment++)
                    {
                        float2 a=_DockLines[segment].xy, b=_DockLines[segment].zw, delta=b-a;
                        float along=saturate(dot(uv-a,delta)/max(.00000001,dot(delta,delta)));
                        float distanceToLine=length(uv-a-along*delta);
                        float width = segment%5 == 4 ? .0015 : .0025;
                        float opacity=(1-smoothstep(width,width+.001,distanceToLine))*_Visibility*(segment%5 == 4 ? .5 : 1);
                        color=lerp(color,float4(.3,.95,.85,1),opacity);
                    }
                color.a *= edge;
                return OutputColor(color);
            }
            ENDCG
        }
    }
}
