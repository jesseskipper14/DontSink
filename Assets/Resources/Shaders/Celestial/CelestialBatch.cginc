#include "UnityCG.cginc"

sampler2D _MainTex;
float4 _Color;
float4 _Observer;
// Metric span, viewport aspect, sky center Y, periodic world width.
float4 _Projection;
float4 _Envelope;
float3 _SkyOrigin, _SkyRight, _SkyUp;
float2 _PixelWorld;
float _Visibility;

struct appdata
{
    float4 vertex : POSITION;
    float2 uv : TEXCOORD0;
    float2 pixelOffset : TEXCOORD1;
    float4 twinkle : TEXCOORD2;
    float4 color : COLOR;
};
struct v2f
{
    float4 vertex : SV_POSITION;
    float2 uv : TEXCOORD0;
    float4 twinkle : TEXCOORD1;
    float4 color : COLOR;
};

v2f vert(appdata v)
{
    v2f o;
    float2 delta = v.vertex.xy - _Observer.xy;
    // Match WorldTopology: shortest periodic X delta, finite Y. Keep the
    // signed direction at exactly half a circumference as the CPU does.
    delta.x = fmod(delta.x, _Projection.w);
    if (delta.x > _Projection.w * 0.5) delta.x -= _Projection.w;
    if (delta.x < -_Projection.w * 0.5) delta.x += _Projection.w;
    float2 viewport = float2(0.5 + delta.x / _Projection.x,
        _Projection.z + delta.y / _Projection.x * _Projection.y);
    float visible = step(_Envelope.x, viewport.x) * step(viewport.x, _Envelope.z) *
        step(_Envelope.y, viewport.y) * step(viewport.y, _Envelope.w);
    float2 offset = v.pixelOffset * _PixelWorld;
    float sine, cosine;
    sincos(v.vertex.z, sine, cosine);
    offset = float2(offset.x * cosine - offset.y * sine, offset.x * sine + offset.y * cosine);
    float3 world = _SkyOrigin + _SkyRight * viewport.x + _SkyUp * viewport.y;
    world.xy += offset;
    o.vertex = mul(UNITY_MATRIX_VP, float4(world, 1));
    o.uv = v.uv;
    o.twinkle = v.twinkle;
    o.color = v.color * _Color;
    o.color.a *= _Visibility * visible;
    return o;
}

float4 frag(v2f i) : SV_Target
{
    float4 tex = tex2D(_MainTex, i.uv);
    float glow = 1;
    #ifdef CELESTIAL_ADDITIVE
    glow = i.twinkle.w * max(0.05, 1 + sin(_Time.y * i.twinkle.y + i.twinkle.x) * i.twinkle.z);
    #endif
    return float4(tex.rgb * i.color.rgb * glow, tex.a * i.color.a);
}
