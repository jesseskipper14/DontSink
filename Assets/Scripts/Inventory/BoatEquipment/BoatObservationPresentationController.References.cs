using UnityEngine;

public sealed partial class BoatObservationPresentationController
{
    [Header("Carried star reference")]
    [SerializeField] private float referenceMinimumScale = 0.7f;
    [SerializeField] private float referenceMaximumScale = 1.35f;
    [SerializeField] private float referenceSlideDuration = 0.75f;
    [SerializeField] private float referenceRotationPerScroll = 6f;
    private ItemInstance referenceItem;
    private CelestialChartFragmentVisual referenceVisual;
    private bool referenceChooser, referenceDragging;
    private Vector2 referenceCenter, referenceChooserScroll;
    private float referenceRotation, referenceScale = 1f, referenceRaisedAt;
    private string referenceTitle;

    private bool ReferenceConsumesScroll => referenceVisual != null &&
        (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ||
         Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl));

    private void TickReferenceComparison()
    {
        if (referenceItem != null && (!StarReferenceItems.IsEligible(referenceItem) ||
            !StarReferenceItems.IsOwned(gameObject, referenceItem))) CloseReferenceComparison();
        if (ReferenceConsumesScroll)
            ApplyReferenceScroll(Input.mouseScrollDelta,
                Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl));
        if (!Input.GetKeyDown(KeyCode.R)) return;
        if (referenceVisual != null || referenceChooser) { CloseReferenceComparison(); return; }
        // Numeric hotbar selection stays available; wheel belongs to telescope zoom.
        var inventory = CelestialChartPaperConsumption.ResolveInventory(gameObject);
        ItemInstance selected = null;
        if (inventory != null)
        {
            var slot = inventory.SelectedSlot;
            selected = slot >= BottomBarSlotType.Hotbar0 && slot <= BottomBarSlotType.Hotbar7
                ? inventory.GetSlot(PlayerInventory.SlotTypeToHotbarIndex(slot))?.Instance
                : inventory.Equipment?.Get(slot);
        }
        if (!TryRaiseReference(selected)) referenceChooser = true;
    }

    private void ApplyReferenceScroll(Vector2 delta, bool scale)
    {
        // Shift-wheel can arrive on the horizontal axis. Read once per frame,
        // independent of IMGUI event order or another panel consuming that event.
        float steps = Mathf.Abs(delta.y) >= Mathf.Abs(delta.x) ? delta.y : delta.x;
        if (scale) referenceScale = Mathf.Clamp(referenceScale + steps * 0.12f,
            Mathf.Max(0.1f, referenceMinimumScale), Mathf.Max(referenceMinimumScale, referenceMaximumScale));
        else referenceRotation = Mathf.Repeat(referenceRotation + steps * referenceRotationPerScroll * 3f, 360f);
    }

    private bool TryRaiseReference(ItemInstance item)
    {
        if (!IsEscapeOpen || !HasLocalAuthority(out _) || !StarReferenceItems.IsEligible(item) ||
            !StarReferenceItems.IsOwned(gameObject, item)) return false;
        if (ReferenceEquals(item, referenceItem)) { referenceChooser = false; return true; }
        var chart = item.CartographicChart; // Detached evidence; builder cannot mutate the carried item.
        var observation = Object.FindFirstObjectByType<CelestialObservationOverlayRunner>();
        var visual = CelestialChartFragmentVisualBuilder.Build(chart.starReference,
            observation != null ? observation.FragmentVisualSettings : null);
        if (visual == null) return false;
        CloseReferenceComparison();
        referenceItem = item;
        referenceVisual = visual;
        referenceTitle = chart.title;
        referenceCenter = new Vector2(sessionCamera.pixelWidth * 0.68f, sessionCamera.pixelHeight * 0.62f);
        referenceRaisedAt = Time.unscaledTime;
        return true;
    }

    private void CloseReferenceComparison()
    {
        referenceVisual?.Dispose();
        referenceVisual = null;
        referenceItem = null;
        referenceChooser = referenceDragging = false;
        referenceCenter = referenceChooserScroll = Vector2.zero;
        referenceRotation = referenceRaisedAt = 0f;
        referenceScale = 1f;
        referenceTitle = null;
    }

    private void DrawReferenceComparisonGUI()
    {
        Rect cameraRect = sessionCamera.pixelRect;
        var viewport = new Rect(cameraRect.x, Screen.height - cameraRect.yMax, cameraRect.width, cameraRect.height);
        GUI.BeginGroup(viewport);
        var ev = Event.current;
        if (GUI.Button(new Rect(viewport.width - 218f, 12f, 206f, 28f),
            referenceVisual == null ? "Raise reference [R]" : "Lower reference [R]"))
        {
            if (referenceVisual != null) CloseReferenceComparison();
            else referenceChooser = !referenceChooser;
        }
        if (referenceVisual != null && GUI.Button(new Rect(viewport.width - 218f, 44f, 206f, 26f), "Choose another reference"))
            referenceChooser = !referenceChooser;

        if (referenceVisual != null)
        {
            float aspect = referenceVisual.PixelSize.x / (float)Mathf.Max(1, referenceVisual.PixelSize.y);
            float height = Mathf.Min(360f, viewport.height * 0.48f, viewport.width * 0.44f / aspect) * referenceScale;
            Vector2 size = new Vector2(height * aspect, height);
            float slide = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Time.unscaledTime - referenceRaisedAt) / Mathf.Max(0.01f, referenceSlideDuration)));
            Vector2 center = new Vector2(referenceCenter.x, Mathf.Lerp(viewport.height + size.magnitude, referenceCenter.y, slide));
            var paperRect = new Rect(center - size * 0.5f, size);
            Vector2 localPointer = (Vector2)(Quaternion.Euler(0, 0, -referenceRotation) * (ev.mousePosition - center)) + center;
            bool onPaper = paperRect.Contains(localPointer);
            if (ev.type == EventType.ScrollWheel && (ev.shift || ev.control))
            {
                ev.Use();
            }
            if (!referenceChooser && ev.type == EventType.MouseDown && ev.button == 0 && onPaper)
            { referenceDragging = true; ev.Use(); }
            if (referenceDragging && ev.type == EventType.MouseDrag && ev.button == 0)
            {
                referenceCenter += ev.delta;
                referenceCenter.x = Mathf.Clamp(referenceCenter.x, 0f, viewport.width);
                referenceCenter.y = Mathf.Clamp(referenceCenter.y, 0f, viewport.height);
                ev.Use();
            }
            if (referenceDragging && ev.type == EventType.MouseUp && ev.button == 0)
            { referenceDragging = false; ev.Use(); }
            var matrix = GUI.matrix;
            var color = GUI.color;
            GUI.color = Color.white;
            GUIUtility.RotateAroundPivot(referenceRotation, center);
            GUI.DrawTexture(paperRect, referenceVisual.PaperTexture, ScaleMode.StretchToFill, true);
            GUI.DrawTexture(paperRect, referenceVisual.InkTexture, ScaleMode.StretchToFill, true);
            GUI.matrix = matrix;
            GUI.color = color;
            GUI.Label(new Rect(viewport.width - 410f, viewport.height - 50f, 398f, 46f),
                referenceTitle + "\nDrag paper · Shift + wheel rotate · Ctrl + wheel scale");
            // Prevent survey targeting through the opaque paper. RMB remains camera look.
            if (onPaper && ev.type == EventType.MouseDown && ev.button == 0) ev.Use();
        }

        if (referenceChooser)
        {
            var panel = new Rect(Mathf.Max(0f, viewport.width - 332f), 78f, 320f, Mathf.Min(300f, viewport.height - 90f));
            GUILayout.BeginArea(panel, GUI.skin.box);
            GUILayout.Label("Carried star references");
            referenceChooserScroll = GUILayout.BeginScrollView(referenceChooserScroll);
            int count = 0;
            foreach (var carried in CartographicChartIntegration.Collect(gameObject))
            {
                if (!carried.StillOwned || !StarReferenceItems.IsEligible(carried.Item)) continue;
                count++;
                if (GUILayout.Button(carried.Item.CartographicChart.title)) TryRaiseReference(carried.Item);
            }
            if (count == 0) GUILayout.Label("No carried star references. Make a reference copy at the Star Chart table.");
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            if (panel.Contains(ev.mousePosition) && ev.type == EventType.MouseDown && ev.button == 0) ev.Use();
        }
        GUI.EndGroup();
    }
}

