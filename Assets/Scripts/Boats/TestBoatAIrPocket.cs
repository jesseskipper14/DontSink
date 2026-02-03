//using System.Collections.Generic;
//using UnityEngine;

//public class TestBoatAirPocket : Boat
//{
//    // ========================
//    // Settings
//    // ========================

//    [Header("Structure")]
//    public float wallThickness = 0.3f;

//    // ========================
//    // State
//    // ========================

//    private List<Compartment> compartments = new List<Compartment>();
//    private PhysicsGlobals physicsGlobals; // reference to ScriptableObject
//    public Boat(PhysicsGlobals physicsGlobals)
//    {
//        this.physicsGlobals = physicsGlobals;
//    }

//    // ========================
//    // Unity Lifecycle
//    // ========================

//    protected override void Awake()
//    {
//        base.Awake();
//        CreateCompartments();
//    }

//    protected override void OnEnable()
//    {
//        base.OnEnable();

//        // Remove default Boat sprite (debug-only visual)
//        var existing = GetComponent<SpriteRenderer>();
//        if (existing)
//            Destroy(existing);

//        // Ensure Rigidbody2D exists
//        Rigidbody2D rb = GetComponent<Rigidbody2D>();
//        if (rb == null)
//            rb = gameObject.AddComponent<Rigidbody2D>();

//        rb.bodyType = RigidbodyType2D.Kinematic;
//        rb.gravityScale = 0f;
//        rb.mass = 10f;

//        // --- Hull Walls ---
//        CreateWall(
//            "Bottom",
//            hullWidth,
//            wallThickness,
//            new Vector2(0f, -hullHeight * 0.5f + wallThickness * 0.5f)
//        );

//        CreateWall(
//            "LeftWall",
//            wallThickness,
//            hullHeight,
//            new Vector2(-hullWidth * 0.5f + wallThickness * 0.5f, 0f)
//        );

//        CreateWall(
//            "RightWall",
//            wallThickness,
//            hullHeight,
//            new Vector2(hullWidth * 0.5f - wallThickness * 0.5f, 0f)
//        );

//        // --- Cargo Trigger ---
//        CreateCargoTrigger();

//        // --- Interior Mask ---
//        CreateInteriorMask();
//    }

//    private void Update()
//    {
//        // Debug flooding controls
//        if (Input.GetKey(KeyCode.F))
//            compartments[2].AddWater(5f * Time.deltaTime);

//        if (Input.GetKey(KeyCode.G))
//            compartments[1].AddWater(5f * Time.deltaTime);

//        if (Input.GetKey(KeyCode.H))
//            compartments[0].AddWater(5f * Time.deltaTime);
//    }

//    // ========================
//    // Construction Helpers
//    // ========================

//    private void CreateWall(string name, float width, float height, Vector2 localPos)
//    {
//        GameObject part = new GameObject(name);
//        part.transform.SetParent(transform);
//        part.transform.localPosition = localPos;

//        // Visual
//        var sr = part.AddComponent<SpriteRenderer>();
//        sr.sprite = Sprite.Create(
//            Texture2D.whiteTexture,
//            new Rect(0, 0, 1, 1),
//            new Vector2(0.5f, 0.5f)
//        );
//        sr.drawMode = SpriteDrawMode.Sliced;
//        sr.size = new Vector2(width, height);
//        sr.color = Color.red;
//        sr.sortingOrder = 10;

//        // Physics
//        BoxCollider2D bc = part.AddComponent<BoxCollider2D>();
//        bc.size = new Vector2(width, height);
//    }

//    private void CreateCargoTrigger()
//    {
//        GameObject cargoTrigger = new GameObject("CargoTrigger");
//        cargoTrigger.transform.SetParent(transform);

//        cargoTrigger.transform.localPosition =
//            new Vector3(0f, -hullHeight * 0.5f + wallThickness + 0.1f, 0f);

//        BoxCollider2D triggerCol = cargoTrigger.AddComponent<BoxCollider2D>();
//        triggerCol.size = new Vector2(hullWidth - wallThickness * 2f, 0.2f);
//        triggerCol.isTrigger = true;
//    }

//    private void CreateCompartments()
//    {
//        compartments.Clear();

//        // --- Bow ---
//        Compartment bow = CreateCompartment(
//            "Bow",
//            new Vector3(hullWidth * 0.35f, 0f, 0f),
//            new Vector2(2.5f, 2f),
//            exposedToOcean: false
//        );

//        // --- Mid ---
//        Compartment mid = CreateCompartment(
//            "Mid",
//            Vector3.zero,
//            new Vector2(2.5f, 2f),
//            exposedToOcean: false
//        );

//        // --- Stern ---
//        Compartment stern = CreateCompartment(
//            "Stern",
//            new Vector3(-hullWidth * 0.35f, 0f, 0f),
//            new Vector2(2.5f, 2f),
//            exposedToOcean: true
//        );

//        // --- Connections ---
//        ConnectCompartments(bow, mid);
//        ConnectCompartments(mid, stern);
//    }

//    private Compartment CreateCompartment(
//        string name,
//        Vector3 localPos,
//        Vector2 size,
//        bool exposedToOcean
//    )
//    {
//        GameObject go = new GameObject($"Compartment_{name}");
//        go.transform.SetParent(transform);
//        go.transform.localPosition = localPos;

//        Compartment c = go.AddComponent<Compartment>();
//        c.compartmentName = name;
//        c.size = size;
//        c.maxVolume = size.x * size.y;
//        c.isExposedToOcean = exposedToOcean;
//        c.oceanExposureLocalY = size.y * 0.5f;

//        compartments.Add(c);
//        return c;
//    }

//    private void ConnectCompartments(Compartment a, Compartment b)
//    {
//        CompartmentConnection connection = new CompartmentConnection
//        {
//            A = a,
//            B = b,
//            isOpen = true
//        };

//        a.connections.Add(connection);
//        b.connections.Add(connection);
//    }

//    private void CreateInteriorMask()
//    {
//        GameObject maskObj = new GameObject("InteriorMask");
//        maskObj.transform.SetParent(transform);
//        maskObj.transform.localPosition = Vector3.zero;
//        maskObj.transform.localRotation = Quaternion.identity;
//        maskObj.transform.localScale = Vector3.one;

//        var mask = maskObj.AddComponent<SpriteMask>();

//        int pixelsPerUnit = 100;
//        int texWidth = Mathf.RoundToInt(
//            (hullWidth - wallThickness * 2f) * pixelsPerUnit
//        );
//        int texHeight = Mathf.RoundToInt(
//            (hullHeight - wallThickness) * pixelsPerUnit
//        );

//        Texture2D tex = new Texture2D(texWidth, texHeight);
//        Color[] pixels = new Color[texWidth * texHeight];

//        for (int i = 0; i < pixels.Length; i++)
//            pixels[i] = Color.white;

//        tex.SetPixels(pixels);
//        tex.Apply();

//        mask.sprite = Sprite.Create(
//            tex,
//            new Rect(0, 0, texWidth, texHeight),
//            new Vector2(0.5f, 0.5f),
//            pixelsPerUnit
//        );

//        mask.isCustomRangeActive = true;
//        mask.frontSortingOrder = 10;
//        mask.backSortingOrder = -10;
//    }
//}
