using System.Collections.Generic;
using UnityEngine;

public sealed partial class NodeSettlementScene
{
    private void BuildTerraceStructure(SettlementTerrace terrace, Transform parent)
    {
        var frame = Empty("Terrace timber frame", parent, Vector2.zero);
        var blocked = new List<Vector2>();
        foreach (var plot in Layout.plots)
            if (!plot.IsOpen && Mathf.Abs(plot.position.y - terrace.y) < .1f)
                blocked.Add(new Vector2(plot.position.x - plot.width / 2 - .3f,plot.position.x + plot.width / 2 + .3f));
        foreach (var link in Layout.connections)
        {
            float low = link.fromTerrace < 0 ? 0 : Layout.terraces[link.fromTerrace].y;
            float high = Layout.terraces[link.toTerrace].y;
            if (terrace.y >= low && terrace.y <= high)
                blocked.Add(new Vector2(link.x - 1.25f,link.x + 1.25f));
        }
        foreach (var plot in Layout.plots)
            if (Layout.plots.Exists(p => p.parentPlotId == plot.id) &&
                terrace.y >= plot.position.y && terrace.y <= plot.position.y + plot.height + .1f)
            {
                float x = plot.position.x + plot.width / 2 + .55f;
                blocked.Add(new Vector2(x - 1.25f,x + 1.25f));
            }
        blocked.Sort((a,b) => a.x.CompareTo(b.x));
        float cursor = terrace.left;
        foreach (var gap in blocked)
        {
            if (gap.y <= cursor || gap.x >= terrace.right) continue;
            BuildRailing(frame,cursor,Mathf.Min(gap.x,terrace.right),terrace.y);
            cursor = Mathf.Max(cursor,gap.y);
        }
        BuildRailing(frame,cursor,terrace.right,terrace.y);

        int bays = Mathf.Max(1,Mathf.CeilToInt((terrace.right - terrace.left) / 8));
        for (int i = 0; i <= bays; i++)
        {
            float x = Mathf.Lerp(terrace.left + .5f,terrace.right - .5f,i / (float)bays);
            bool ladder = false;
            foreach (var link in Layout.connections) if (Mathf.Abs(x - link.x) < 1.25f) ladder = true;
            if (ladder) continue;
            float bottom = 0;
            foreach (var lower in Layout.terraces)
                if (lower.y < terrace.y && x >= lower.left && x <= lower.right) bottom = Mathf.Max(bottom,lower.y);
            float top = terrace.y - .4f;
            if (top <= bottom) continue;
            Shape("Timber support post",frame,new Vector2(x,(top+bottom)/2),new Vector2(.32f,top-bottom),
                new Color(.32f,.25f,.18f),-3);
            float braceDepth = Mathf.Min(2.4f,(top-bottom)*.7f);
            float span = Mathf.Min(2.4f,braceDepth);
            for (int side = -1; side <= 1; side += 2)
            {
                float endX = Mathf.Clamp(x + side*span,terrace.left,terrace.right);
                if (Mathf.Abs(endX-x) < .4f) continue;
                TimberBrace(frame,new Vector2(x,top-braceDepth),new Vector2(endX,top));
            }
        }
    }

    private void BuildRailing(Transform frame,float left,float right,float y)
    {
        if (right-left < .55f) return;
        Color timber = new(.46f,.35f,.24f);
        Shape("Railing top rail",frame,new Vector2((left+right)/2,y+1.05f),new Vector2(right-left,.13f),timber,-1);
        Shape("Railing middle rail",frame,new Vector2((left+right)/2,y+.5f),new Vector2(right-left,.09f),timber,-1);
        int bays = Mathf.Max(1,Mathf.CeilToInt((right-left)/2.5f));
        for (int i = 0; i <= bays; i++)
            Shape("Railing post",frame,new Vector2(Mathf.Lerp(left,right,i/(float)bays),y+.55f),new Vector2(.13f,1.1f),timber,-1);
    }

    private void TimberBrace(Transform frame,Vector2 from,Vector2 to)
    {
        Vector2 delta = to-from;
        var brace = Shape("Diagonal timber brace",frame,(from+to)/2,new Vector2(delta.magnitude,.22f),
            new Color(.38f,.29f,.20f),-2);
        brace.transform.localRotation = Quaternion.Euler(0,0,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg);
    }
}
