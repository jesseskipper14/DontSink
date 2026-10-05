using UnityEngine;

public sealed partial class NodeSettlementScene
{
    private void BuildTownPaving()
    {
        float left = Layout.harborArrival.x;
        foreach (var terrace in Layout.terraces) left = Mathf.Min(left, terrace.left);
        left -= 6;
        float right = Layout.harborArrival.x - 1;
        var road = Empty("Stone paved road", _root, new Vector2(0,.01f));
        // Thin surface veneer; the existing sandy terrain still owns collision.
        var bed = Shape("Mortar bed", road, new Vector2((left + right) / 2,-.13f),
            new Vector2(right - left,.24f), new Color(.32f,.32f,.29f), 1, "GroundFront");
        // Keep mortar behind the stones while sharing their requested sorting order.
        bed.transform.localPosition += new Vector3(0,0,.001f);
        for (int row = 0; row < 2; row++)
        {
            float x = left;
            for (int index = 0; x < right; index++)
            {
                string id = "paving/" + row + "/" + index;
                float width = Mathf.Min(right - x, .65f + NodeNaturePlanner.Sample(Layout.seed,id,0) * .4f);
                float shade = NodeNaturePlanner.Sample(Layout.seed,id,1);
                Color stone = Color.Lerp(new Color(.43f,.45f,.43f),new Color(.65f,.64f,.57f),shade);
                float height = .085f + NodeNaturePlanner.Sample(Layout.seed,id,2) * .012f;
                Shape("Paver " + row + "_" + index, road, new Vector2(x + width / 2,-.075f - row * .105f),
                    new Vector2(Mathf.Max(.01f,width - .035f),height),stone,1,"GroundFront");
                x += width;
            }
        }
    }
}

