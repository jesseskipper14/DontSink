using System.Collections.Generic;
using UnityEngine;

public sealed partial class NodeSettlementScene
{
    private void BuildTownProps()
    {
        var root = Empty("TownProps",_root,Vector2.zero);
        var placed = new List<Vector2>();
        foreach (var terrace in Layout.terraces)
        {
            // Rank a pool of street sites, then populate safe sites. A failed random roll
            // mustn't eliminate a town's only available place for decoration.
            var sites = new List<(string id, Vector2 at, int kind, float rank)>();
            for (int index = 0; terrace.left + 1.4f + index * 1.5f < terrace.right - 1.4f; index++)
            {
                string id = "prop/" + terrace.id + "/" + index;
                Vector2 at = new(terrace.left + 1.4f + index * 1.5f,terrace.y);
                sites.Add((id,at,(int)(NodeNaturePlanner.Sample(Layout.seed,id,1)*4),NodeNaturePlanner.Sample(Layout.seed,id,0)));
            }
            sites.Sort((a,b) => { int rank = a.rank.CompareTo(b.rank); return rank != 0 ? rank : string.CompareOrdinal(a.id,b.id); });
            float width = terrace.right-terrace.left;
            int cargoCapacity = NodeTownPresentationRules.CargoCapacity(width);
            int cargoTarget = NodeTownPresentationRules.CargoCount(width,Visit?.prosperity ?? 0);
            int cargoSlots = 0, amenitySlots = 0;
            foreach (var site in sites)
            {
                bool cargo = site.kind == 1 || site.kind == 2;
                if (cargo ? cargoSlots >= cargoCapacity : amenitySlots >= 1) continue;
                float radius = site.kind == 0 ? 1.05f : .65f;
                if (!TownPropSiteClear(site.at,radius,placed)) continue;
                // Reserve even empty cargo slots, so prosperity never moves other props.
                placed.Add(site.at);
                if (cargo) { if (++cargoSlots > cargoTarget) continue; }
                else amenitySlots++;
                var prop = Empty(site.id + "_" + new[]{"Bench","Crates","Barrel","Planter"}[site.kind],root,site.at);
                BuildProp(prop,site.kind,NodeNaturePlanner.Sample(Layout.seed,site.id,2));
            }
        }
        Debug.Log($"[Settlement] Town props placed: {root.childCount} | prosperity={Visit?.prosperity ?? 0:F2} | node={Layout.nodeStableId}",this);
        if (root.childCount == 0) Debug.LogWarning("[Settlement] No populated street sites for town props.",this);
    }

    private bool TownPropSiteClear(Vector2 at,float radius,List<Vector2> placed)
    {
        if (!NodeNaturePlanner.PlantSiteClear(Layout,at,radius)) return false;
        foreach (var other in placed) if (Vector2.Distance(at,other)<3) return false;
        foreach (var plant in Nature.plants)
            if (plant.populated && Vector2.Distance(at,plant.position)<radius+.35f) return false;
        foreach (var owner in Layout.plots) foreach (var socket in owner.sockets)
            if (Vector2.Distance(at,owner.position+socket.offset)<radius+.5f) return false;
        return true;
    }

    private void BuildProp(Transform prop,int kind,float shade)
    {
        Color wood = Color.Lerp(new Color(.42f,.29f,.17f),new Color(.61f,.45f,.27f),shade);
        Color dark = new(.27f,.23f,.18f), metal = new(.34f,.38f,.39f);
        GameObject Part(string name,Vector2 at,Vector2 size,Color color,int order=8,Sprite art=null) =>
            NatureShape(name,prop,at,size,color,order,art,true);
        if (kind == 0)
        {
            Part("Bench seat",new Vector2(0,.5f),new Vector2(1.9f,.16f),wood);
            Part("Bench back",new Vector2(0,.87f),new Vector2(1.8f,.28f),wood);
            for (int side=-1;side<=1;side+=2)
            {
                Part("Bench leg",new Vector2(side*.67f,.23f),new Vector2(.13f,.46f),dark);
                Part("Back support",new Vector2(side*.67f,.67f),new Vector2(.09f,.64f),dark,7);
            }
        }
        else if (kind == 1)
        {
            for (int i=0;i<2;i++)
            {
                Vector2 at = new(i==0 ? -.25f : -.1f,i==0 ? .36f : .98f);
                float size = i==0 ? .72f : .52f;
                Part("Crate body",at,new Vector2(size,size),wood);
                foreach (float y in new[]{-.4f,.4f})
                    Part("Crate rim",at+new Vector2(0,y*size),new Vector2(size,.075f),dark,9);
                var brace=Part("Crate diagonal",at,new Vector2(size*1.05f,.07f),dark,9);
                brace.transform.localRotation=Quaternion.Euler(0,0,45);
            }
        }
        else if (kind == 2)
        {
            Part("Barrel body",new Vector2(0,.52f),new Vector2(.75f,1.04f),wood,8,_leaf);
            Part("Barrel staves",new Vector2(0,.5f),new Vector2(.68f,.76f),wood);
            foreach(float y in new[]{.2f,.8f}) Part("Barrel hoop",new Vector2(0,y),new Vector2(.72f,.085f),metal,9);
            Part("Barrel lid",new Vector2(0,.97f),new Vector2(.59f,.14f),dark,9,_leaf);
        }
        else
        {
            Part("Planter box",new Vector2(0,.2f),new Vector2(1.05f,.4f),wood);
            Part("Planter rim",new Vector2(0,.4f),new Vector2(1.15f,.09f),dark,9);
            for(int i=0;i<3;i++) Part("Planter leaves",new Vector2((i-1)*.28f,.64f+(i==1?.15f:0)),
                new Vector2(.5f,.6f),new Color(.29f,.49f,.25f),10,_leaf);
        }
    }
}
