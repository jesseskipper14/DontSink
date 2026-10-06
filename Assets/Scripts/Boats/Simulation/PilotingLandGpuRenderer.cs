using System.Collections.Generic;
using UnityEngine;
using Unity.Profiling;

/// <summary>Shared immutable height data; moving views rasterize on the GPU, not the CPU.</summary>
public sealed class PilotingLandGpuRenderer
{
    private sealed class HeightData { public Texture2D Texture; public int Users; }
    private static readonly Dictionary<WorldMapTopographyField,HeightData> Heights = new();
    private static readonly ProfilerMarker RasterMarker = new("Piloting.Land.GpuRaster");
    private static readonly ProfilerMarker UploadMarker = new("Piloting.Land.HeightUploadOnce");
    private WorldMapTopographyField _field;
    private HeightData _height;
    private Material _material;
    private RenderTexture _target;
    private Texture2D _depthCurve;
    private BoatTerrainProfile _depthProfile;
    private float _nextDepthRefresh;
    private readonly Vector4[] _dockLines = new Vector4[30];
    private int _dockLineCount;
    public void SetDockLines(IReadOnlyList<Vector4> lines)
    {
        _dockLineCount=Mathf.Min(lines.Count,_dockLines.Length);
        for(int i=0;i<_dockLineCount;i++) _dockLines[i]=lines[i];
    }
    public Texture Texture => _target;

    public bool Render(WorldMapTopographyField field, Vector2 origin, Vector2 axisX, Vector2 axisY,
        float sea, float visibility, int width, int height, PilotingViewscapeSettings circle = null,
        Texture overlay = null, bool circularMain = false, BoatTerrainProfile depthProfile = null,
        float dangerDepth = 0, float coastalBand = .12f)
    {
        if (field == null || !field.IsValid) return false;
        if (_material == null)
        {
            var shader = Resources.Load<Shader>("Shaders/Piloting/PilotingLand");
            if (shader == null || !shader.isSupported) return false;
            _material = new Material(shader) { name = "Piloting land GPU", hideFlags = HideFlags.HideAndDontSave };
        }
        if (!ReferenceEquals(_field,field))
        {
            ReleaseHeight(); _field = field;
            if (!Heights.TryGetValue(field,out _height))
            {
                using var upload = UploadMarker.Auto();
                var data = field.CopyHeight01();
                // Get01 wraps the duplicated last X column for current geography.
                if (field.GenerationVersion >= 3)
                    for (int y=0;y<field.Height;y++) data[y*field.Width+field.Width-1] = data[y*field.Width];
                var texture = new Texture2D(field.Width,field.Height,TextureFormat.RFloat,false,true)
                    { name = "Piloting shared geography heights", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                texture.SetPixelData(data,0); texture.Apply(false,true);
                _height = new HeightData { Texture = texture }; Heights.Add(field,_height);
            }
            _height.Users++;
        }
        if (_target == null || _target.width != width || _target.height != height)
        {
            ReleaseTarget(); _target = new RenderTexture(width,height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB)
                { name = "Piloting GPU land view", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            _target.Create();
        }
        Rect bounds = field.WorldBounds;
        _material.SetTexture("_Heights",_height.Texture);
        _material.SetTexture("_Overlay",overlay != null ? overlay : Texture2D.blackTexture);
        _material.SetFloat("_HasOverlay",overlay != null ? 1 : 0);
        _material.SetVector("_Bounds",new Vector4(bounds.xMin,bounds.yMin,bounds.width,bounds.height));
        _material.SetVector("_Origin",origin); _material.SetVector("_AxisX",axisX); _material.SetVector("_AxisY",axisY);
        _material.SetFloat("_Sea",sea); _material.SetFloat("_Visibility",visibility);
        _material.SetFloat("_Circle",circularMain ? 2 : circle != null ? 1 : 0);
        _material.SetInt("_DockLineCount",circularMain ? _dockLineCount : 0);
        _material.SetVectorArray("_DockLines",_dockLines);
        _material.SetFloat("_DangerDepth",dangerDepth);
        _material.SetFloat("_CoastalBand",Mathf.Max(.001f,coastalBand));
        _material.SetFloat("_DepthEnabled",depthProfile != null && depthProfile.geographicDepth != null ? 1 : 0);
        if (depthProfile != null && depthProfile.geographicDepth != null)
        {
            if (_depthCurve == null)
                _depthCurve = new Texture2D(1024,1,TextureFormat.RFloat,false,true)
                    { name="Piloting geographic depth curve",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp };
            if (_depthProfile != depthProfile || Time.unscaledTime >= _nextDepthRefresh)
            {
                var depths=new float[1024];
                for(int i=0;i<depths.Length;i++) depths[i]=Mathf.Max(0,depthProfile.geographicDepth.Evaluate(i/1023f));
                _depthCurve.SetPixelData(depths,0);_depthCurve.Apply(false,false);
                _depthProfile=depthProfile;_nextDepthRefresh=Time.unscaledTime+10;
            }
            _material.SetTexture("_DepthCurve",_depthCurve);
        }
        _material.SetFloat("_FadeStart",circle != null ? circle.fadeStartFraction : .7f);
        _material.SetFloat("_WaveMarks",circle != null ? circle.waveMarks : 0);
        _material.SetFloat("_ViewTime",Time.time);
        _material.SetColor("_WaterColor",circle != null ? circle.waterColor : circularMain ? new Color(.04f,.13f,.19f,.78f) : Color.clear);
        _material.SetColor("_LandColor",circle != null ? circle.landColor : new Color(.43f,.49f,.32f,1));
        using (RasterMarker.Auto())
        {
            var previous = RenderTexture.active;
            bool previousSrgb = GL.sRGBWrite;
            try { GL.sRGBWrite = QualitySettings.activeColorSpace == ColorSpace.Linear; Graphics.Blit(Texture2D.whiteTexture,_target,_material); }
            finally { GL.sRGBWrite = previousSrgb; RenderTexture.active = previous; }
        }
        return true;
    }
    private static void DestroyOwned(Object value)
    { if(value != null) { if(Application.isPlaying) Object.Destroy(value); else Object.DestroyImmediate(value); } }
    private void ReleaseHeight()
    {
        if (_height != null && --_height.Users == 0) { Heights.Remove(_field); DestroyOwned(_height.Texture); }
        _height = null; _field = null;
    }
    private void ReleaseTarget()
    { if(_target != null) { _target.Release(); DestroyOwned(_target); } _target = null; }
    public void Reset()
    {
        _dockLineCount = 0;
        ReleaseHeight(); ReleaseTarget(); DestroyOwned(_material); _material = null;
        DestroyOwned(_depthCurve);_depthCurve=null;_depthProfile=null;_nextDepthRefresh=0;
    }
}
