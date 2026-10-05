using System.Collections.Generic;
using UnityEngine;

public sealed partial class NodeSettlementScene
{
    public NodeNaturePlan Nature { get; private set; }
    private float _natureLatitude = .45f;
    private readonly List<Sprite> _natureSprites = new();
    private Sprite _leaf, _triangle;
    private readonly List<Texture2D> _natureTextures = new();

    private void ResolveNatureLatitude(MapNodeRuntime node)
    {
        var generator = FindAnyObjectByType<WorldMapGraphGenerator>(FindObjectsInactive.Include);
        var graph = generator != null ? generator.graph : null;
        if (graph == null || node.NodeIndex < 0 || node.NodeIndex >= graph.nodes.Count)
        { Debug.LogWarning("[Settlement] Map coordinate unavailable; using temperate foliage.", this); return; }
        Rect bounds = graph.worldBounds;
        if (bounds.height <= 0) bounds = WorldTopologyService.Current.Bounds;
        if (bounds.height <= 0)
        { Debug.LogWarning("[Settlement] Latitude bounds unavailable; using temperate foliage.", this); return; }
        _natureLatitude = Mathf.InverseLerp(bounds.yMin, bounds.yMax, graph.nodes[node.NodeIndex].position.y) * 2 - 1;
    }

    private Sprite NatureSprite(Vector2[] vertices, ushort[] indices, bool landscape = false)
    {
        // Rasterized shape sprites also work in editor previews, where OverrideGeometry is restricted.
        int width = landscape ? 2048 : 128;
        int height = landscape ? 512 : 128;
        var texture = new Texture2D(width,height) { name = "Node nature shape", filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color[width * height];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color(1,1,1,0);
        float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
        if (landscape)
        {
            // Height-field coverage gives hill edges subpixel alpha instead of a binary staircase.
            int segment = 0;
            for (int x = 0; x < width; x++)
            {
                float at = (x + .5f) / width - .5f;
                while (segment < vertices.Length / 2 - 2 && at > vertices[(segment + 1) * 2].x) segment++;
                Vector2 a = vertices[segment * 2 + 1], b = vertices[(segment + 1) * 2 + 1];
                float ridge = Mathf.Lerp(a.y,b.y,Mathf.InverseLerp(a.x,b.x,at));
                float edge = (ridge + .5f) * height;
                for (int y = 0; y <= Mathf.Min(height - 1, Mathf.CeilToInt(edge)); y++)
                    pixels[y*width+x] = new Color(1,1,1,Mathf.Clamp01(edge-y));
            }
        }
        // Rasterize only each triangle's bounds; higher resolution needn't scan every triangle per pixel.
        else for (int i = 0; i < indices.Length; i += 3)
        {
            Vector2 a = vertices[indices[i]], b = vertices[indices[i+1]], c = vertices[indices[i+2]];
            float area = Cross(b-a,c-a);
            if (Mathf.Abs(area) < .000001f) continue;
            int left = Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(a.x, b.x, c.x) + .5f) * width), 0, width - 1);
            int right = Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(a.x, b.x, c.x) + .5f) * width), 0, width - 1);
            int bottom = Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(a.y, b.y, c.y) + .5f) * height), 0, height - 1);
            int top = Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(a.y, b.y, c.y) + .5f) * height), 0, height - 1);
            for (int y = bottom; y <= top; y++) for (int x = left; x <= right; x++)
            {
                Vector2 point = new((x + .5f) / width - .5f, (y + .5f) / height - .5f);
                float u = Cross(point-a,c-a) / area, v = Cross(b-a,point-a) / area;
                if (u >= 0 && v >= 0 && u + v <= 1) pixels[y*width+x] = Color.white;
            }
        }
        texture.SetPixels(pixels); texture.Apply(); _natureTextures.Add(texture);
        var sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(.5f, .5f), width,
            0, SpriteMeshType.FullRect);
        _natureSprites.Add(sprite);
        return sprite;
    }

    private void ReleaseNatureSprites()
    {
        foreach (var sprite in _natureSprites) Release(sprite);
        _natureSprites.Clear(); _leaf = null; _triangle = null;
        foreach (var texture in _natureTextures) Release(texture);
        _natureTextures.Clear();
    }

    private void BuildNature()
    {
        Nature = NodeNaturePlanner.Generate(Layout, _natureLatitude);
        BuildTownPaving();
        var root = Empty("NodeNature", _root, Vector2.zero);
        var landscape = Empty("IslandLandscape", root, Vector2.zero);
        var trees = Empty("BackgroundTrees", root, Vector2.zero);
        var plants = Empty("SmallFoliage", root, Vector2.zero);
        _triangle = NatureSprite(new[] { new Vector2(-.5f,-.5f), new Vector2(.5f,-.5f), new Vector2(0,.5f) }, new ushort[] { 0,1,2 });
        const int sides = 48;
        var circle = new Vector2[sides + 1]; var triangles = new ushort[sides * 3];
        for (int i = 0; i < sides; i++)
        {
            float angle = i * Mathf.PI * 2 / sides;
            circle[i + 1] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * .5f;
            triangles[i * 3] = 0; triangles[i * 3 + 1] = (ushort)(i + 1); triangles[i * 3 + 2] = (ushort)((i + 1) % sides + 1);
        }
        _leaf = NatureSprite(circle, triangles);
        for (int band = 0; band < Nature.hills.Count; band++)
        {
            var points = Nature.hills[band];
            // Normalize vertices to the sprite's rectangle; scale the renderer back to local units.
            float width = points[points.Length - 1].x - points[0].x;
            var vertices = new Vector2[points.Length * 2];
            var indices = new ushort[(points.Length - 1) * 6];
            for (int i = 0; i < points.Length; i++)
            {
                float x = (points[i].x - points[0].x) / width - .5f;
                vertices[2 * i] = new Vector2(x, -.5f);
                vertices[2 * i + 1] = new Vector2(x, points[i].y / 30 - .5f);
                if (i == points.Length - 1) continue;
                int k = i * 6; ushort a = (ushort)(i * 2);
                indices[k] = a; indices[k + 1] = (ushort)(a + 1); indices[k + 2] = (ushort)(a + 2);
                indices[k + 3] = (ushort)(a + 1); indices[k + 4] = (ushort)(a + 3); indices[k + 5] = (ushort)(a + 2);
            }
            NatureShape("Island ridge " + band, landscape, new Vector2(points[0].x + width / 2, 15),
                new Vector2(width,120), band == 0 ? new Color(.30f,.45f,.43f) : new Color(.27f,.39f,.29f),
                -20 + band, NatureSprite(vertices, indices, true));
        }
        foreach (var candidate in Nature.trees)
        {
            if (!candidate.populated) continue;
            var tree = Empty(candidate.id + "_" + Nature.treeKind, trees, candidate.position);
            tree.localScale = Vector3.one * candidate.scale;
            BuildTree(tree);
        }
        foreach (var candidate in Nature.plants)
        {
            if (!candidate.populated) continue;
            var plant = Empty(candidate.id + "_" + (NodePlantKind)candidate.variant, plants, candidate.position);
            plant.localScale = Vector3.one * candidate.scale;
            BuildPlant(plant, (NodePlantKind)candidate.variant, candidate.id);
        }
    }

    private GameObject NatureShape(string name, Transform parent, Vector2 at, Vector2 size, Color color,
        int order, Sprite art = null, bool small = false)
    {
        var go = Shape(name, parent, at, size, color, order, small ? "WorldBuildings" : "WorldBackdrop");
        if (art != null) go.GetComponent<SpriteRenderer>().sprite = art;
        return go;
    }

    private void BuildTree(Transform tree)
    {
        Color trunk = new(.35f,.26f,.16f), foliage = new(.20f,.38f,.23f);
        float height = Nature.treeKind == NodeTreeKind.Palm ? 10 : 8;
        NatureShape("Trunk", tree, new Vector2(0,height / 2), new Vector2(.48f,height), trunk, -6);
        if (Nature.treeKind == NodeTreeKind.Palm)
        {
            for (int i = 0; i < 7; i++)
            {
                float angle = -160 + i * 26;
                Vector2 direction = new(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                var frond = NatureShape("Palm frond", tree, new Vector2(0,height) + direction * 1.7f,
                    new Vector2(4.3f,.8f), foliage, -5, _leaf);
                frond.transform.localRotation = Quaternion.Euler(0,0,angle);
            }
            NatureShape("Palm crown", tree, new Vector2(0,height), new Vector2(1.1f,1), foliage, -4, _leaf);
        }
        else if (Nature.treeKind == NodeTreeKind.Pine)
        {
            for (int i = 0; i < 4; i++)
                NatureShape("Pine bough", tree, new Vector2(0,4 + i * 1.8f),
                    new Vector2(5.5f - i * 1.1f,4), foliage, -5, _triangle);
        }
        else
        {
            for (int i = 0; i < 3; i++)
                NatureShape("Leaf canopy", tree, new Vector2((i - 1) * 1.7f,height - (i == 1 ? 0 : 1)),
                    new Vector2(4.8f,4.4f), Color.Lerp(foliage, new Color(.40f,.50f,.25f), i * .18f), -5, _leaf);
        }
    }

    private void BuildPlant(Transform plant, NodePlantKind kind, string id)
    {
        Color green = new(.26f,.47f,.23f);
        if (kind == NodePlantKind.Bush)
        {
            for (int i = 0; i < 3; i++)
                NatureShape("Bush leaves", plant, new Vector2((i - 1) * .4f,.35f + (i == 1 ? .2f : 0)),
                    new Vector2(.85f,.8f), green, 6, _leaf, true);
            return;
        }
        if (kind == NodePlantKind.Fern) for (int i = 0; i < 5; i++)
        {
            float angle = -(i - 2) * 24;
            Vector2 direction = new(-Mathf.Sin(angle * Mathf.Deg2Rad), Mathf.Cos(angle * Mathf.Deg2Rad));
            var leaf = NatureShape("Fern leaf", plant, new Vector2(0,.04f) + direction * .425f,
                new Vector2(.17f,.85f), green, 6, _leaf, true);
            leaf.transform.localRotation = Quaternion.Euler(0,0,angle);
        }
        if (kind != NodePlantKind.Flowers) return;
        for (int i = 0; i < 3; i++)
        {
            float x = (i - 1) * .32f, height = i == 1 ? .9f : .65f;
            NatureShape("Flower stem", plant, new Vector2(x,height / 2), new Vector2(.055f,height), green, 6, null, true);
            var leaf = NatureShape("Flower leaf", plant, new Vector2(x + .10f,.25f), new Vector2(.28f,.12f), green, 6, _leaf, true);
            leaf.transform.localRotation = Quaternion.Euler(0,0,30);
            Color blossom = Color.Lerp(new Color(.93f,.73f,.27f), new Color(.83f,.36f,.60f), NodeNaturePlanner.Sample(Layout.seed,id,4));
            NatureShape("Blossom", plant, new Vector2(x,height), new Vector2(.30f,.30f), blossom, 7, _leaf, true);
            NatureShape("Flower center", plant, new Vector2(x,height), new Vector2(.10f,.10f), new Color(.95f,.87f,.55f), 8, _leaf, true);
        }
    }
}
