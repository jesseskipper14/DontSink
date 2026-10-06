using System.Collections.Generic;
using UnityEngine;
using Unity.Profiling;

/// <summary>Close physical viewport only. No chart reveal or navigation writes.</summary>
public sealed class PilotingNearFieldRenderer
{
    private static readonly ProfilerMarker TotalMarker = new("Piloting.NearField.Total");
    private static readonly ProfilerMarker SearchMarker = new("Piloting.NearField.HarborSearch");
    private static readonly ProfilerMarker BerthMarker = new("Piloting.NearField.BerthSolve");
    private readonly PilotingLandGpuRenderer _land = new();
    private BoatSceneWorldPositionBridge _bridge;
    private BoatHarborPresentation _harbor;
    private float _nextSearch, _nextInvalidate, _visibility = 1, _cachedScale, _cachedLength, _cachedDraft, _cachedSea;
    private Boat _cachedBoat;
    private WorldMapTopographyField _cachedField;
    private HarborTravelSettings _cachedSettings;
    private readonly Dictionary<MapNode,HarborBerth?> _solutions = new();
    private readonly List<HarborVisualObservation> _observations = new();
    private readonly List<HarborBerth> _berths = new();
    private readonly Vector2[] _corners = new Vector2[4];
    private readonly List<Vector4> _dockLines = new();

    public void Configure(BoatSceneWorldPositionBridge bridge,BoatHarborPresentation harbor,float visibility)
    { _bridge = bridge; _harbor = harbor; _visibility = Mathf.Clamp01(visibility); }

    public static Vector2 GeographicToNavigation(Vector2 geographic,Vector2 observer,Vector2 navigation,
        Vector2 right,Vector2 forward,float scale) => navigation + new Vector2(
            Vector2.Dot(WorldTopologyService.Delta(observer,geographic),right),
            Vector2.Dot(WorldTopologyService.Delta(observer,geographic),forward)) / Mathf.Max(.000001f,scale);

    public void Draw(PilotingViewProjection view,BoatPilotingState state)
    {
        using var totalSample = TotalMarker.Auto();
        if (Event.current.type != EventType.Repaint) return;
        var cache = WorldMapRuntimeCache.I;
        if (_bridge == null || state == null || cache == null || !cache.HasTopography || !_bridge.ProjectionReady ||
            _bridge.PilotingState != state || view.PlayArea.height <= 0 || view.PlayArea.width <= 0) return;
        float scale = _bridge.WorldUnitsPerLocalUnit;
        if (scale <= 0) return;
        Vector2 observer = _bridge.ProjectNavigationPosition(state.NavigationPosition);
        Vector2 right = _bridge.ProjectNavigationVector(Vector2.right)/scale;
        Vector2 forward = _bridge.ProjectNavigationVector(Vector2.up)/scale;
        var settings = SceneTransitionController.I != null ? SceneTransitionController.I.HarborSettings : null;
        var boat = _harbor?.Controller != null ? _harbor.Controller.HarborBoat : null;
        if (Time.unscaledTime >= _nextSearch)
        {
            _nextSearch = Time.unscaledTime+.5f;
            using (SearchMarker.Auto())
                HarborVisualObservation.Collect(observer,new Vector2(view.VisibleWorldWidth,view.VisibleWorldHeight).magnitude*scale*.5f,_observations);
            if (boat != null && settings?.terrainProfile != null)
            {
                HarborTravelService.BoatSize(boat,settings.terrainProfile.waterLevelY,out float length,out float draft);
                if (_cachedField != cache.Field || _cachedSea != cache.EffectiveSeaLevel01 || _cachedBoat != boat ||
                    _cachedSettings != settings || _cachedScale != scale || Time.unscaledTime >= _nextInvalidate ||
                    Mathf.Abs(length-_cachedLength)>.25f || Mathf.Abs(draft-_cachedDraft)>.25f)
                {
                    _solutions.Clear(); _nextInvalidate = Time.unscaledTime+10; _cachedField = cache.Field;
                    _cachedSea = cache.EffectiveSeaLevel01; _cachedBoat = boat; _cachedSettings = settings;
                    _cachedScale = scale; _cachedLength = length; _cachedDraft = draft;
                }
            }
            _berths.Clear();
            using (BerthMarker.Auto())
                if (boat != null && settings != null)
                    foreach(var observation in _observations)
                    {
                        if (!_solutions.TryGetValue(observation.Node,out var saved))
                        {
                            saved = HarborTravelService.TryGeometry(observation.Node,boat,scale,settings,out var solved,out _) ? solved : (HarborBerth?)null;
                            _solutions[observation.Node] = saved;
                        }
                        if (saved.HasValue) _berths.Add(saved.Value);
                    }
        }
        _dockLines.Clear();
        Vector2 Project(Vector2 point)
        {
            Vector2 nav = GeographicToNavigation(point,observer,state.NavigationPosition,right,forward,scale);
            return new Vector2((nav.x-view.Left)/view.VisibleWorldWidth,(nav.y-view.Bottom)/view.VisibleWorldHeight);
        }
        void AddBerth(HarborBerth berth)
        {
            if(_dockLines.Count>=30) return;
            HarborPresentationMath.BerthCorners(berth,_corners);
            Vector2 min=Project(_corners[0]),max=min;
            for(int i=1;i<4;i++){var p=Project(_corners[i]);min=Vector2.Min(min,p);max=Vector2.Max(max,p);}
            if(!new Rect(min,max-min).Overlaps(new Rect(0,0,1,1)))return;
            void Segment(Vector2 a,Vector2 b)=>_dockLines.Add(new Vector4(a.x,a.y,b.x,b.y));
            for(int i=0;i<4;i++)Segment(Project(_corners[i]),Project(_corners[(i+1)%4]));
            Segment(Project(berth.Center),Project(berth.Departure));
        }
        bool activeAlreadyDrawn=false;
        foreach(var berth in _berths)
        {
            AddBerth(berth);
            if(_harbor?.Controller!=null && berth.Center==_harbor.Controller.CurrentHarborBerth.Center)activeAlreadyDrawn=true;
        }
        if(!activeAlreadyDrawn && _harbor?.Controller!=null && !string.IsNullOrEmpty(_harbor.Controller.CurrentHarborNodeId))
            AddBerth(_harbor.Controller.CurrentHarborBerth);
        _land.SetDockLines(_dockLines);
        float dangerDepth = Mathf.Max(3,_cachedDraft+(settings?.depthClearance ?? 2)+
            (settings?.terrainProfile != null ? settings.terrainProfile.rollingAmplitude : 0));
        if (_land.Render(cache.Field,_bridge.ProjectNavigationPosition(new Vector2(view.Left,view.Bottom)),
            _bridge.ProjectNavigationVector(new Vector2(view.VisibleWorldWidth,0)),
            _bridge.ProjectNavigationVector(new Vector2(0,view.VisibleWorldHeight)),
            cache.EffectiveSeaLevel01,_visibility,512,512,circularMain:true,
            depthProfile:settings?.terrainProfile,dangerDepth:dangerDepth,coastalBand:settings?.coastalDepthBand ?? .12f))
            GUI.DrawTexture(view.PlayArea,_land.Texture);
    }
    public void Reset()
    {
        _land.Reset(); _bridge=null; _harbor=null; _observations.Clear(); _berths.Clear(); _solutions.Clear();
        _cachedField=null; _cachedBoat=null; _cachedSettings=null; _nextSearch=0; _nextInvalidate=0;
    }
}

