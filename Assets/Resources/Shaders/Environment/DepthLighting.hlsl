#ifndef DONT_SINK_DEPTH_LIGHTING_INCLUDED
#define DONT_SINK_DEPTH_LIGHTING_INCLUDED
float _DontSinkDepthLightingEnabled;
float4 _DontSinkDepthLighting; // waterline, fade start, black depth, daylight
int _DontSinkDepthLampCount;
float4 _DontSinkDepthLampPositions[16];
float4 _DontSinkDepthLampColors[16];
float4 _DontSinkDepthLampCones[16];
float3 DontSinkDepthLight(float2 worldPosition)
{
    if (_DontSinkDepthLightingEnabled < .5) return float3(1, 1, 1);
    float ambient = 1 - smoothstep(_DontSinkDepthLighting.y, _DontSinkDepthLighting.z, _DontSinkDepthLighting.x - worldPosition.y);
    float3 light = ambient * _DontSinkDepthLighting.w;
    for (int i = 0; i < _DontSinkDepthLampCount; i++)
    {
        float4 p = _DontSinkDepthLampPositions[i], cone = _DontSinkDepthLampCones[i];
        float2 offset = worldPosition - p.xy;
        float distance = length(offset);
        float radial = 1 - smoothstep(min(p.w, p.z - .0001), max(p.z, .0001), distance);
        float cosine = dot(offset / max(distance, .0001), cone.xy);
        float angular = cone.z <= -.999 || distance < .0001 ? 1 : smoothstep(cone.w, max(cone.w + .0001, cone.z), cosine);
        light += _DontSinkDepthLampColors[i].rgb * radial * angular;
    }
    return saturate(light);
}
#endif
