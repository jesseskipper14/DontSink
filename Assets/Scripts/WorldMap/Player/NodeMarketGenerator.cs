using UnityEngine;

public static class NodeMarketGenerator
{
    public static void Generate(NodeMarketState market, MapNodeRuntime node, int daySeed)
    {
        market.stock.stacks.Clear();

        // Prosperity influences quantity/quality later. Start with quantity.
        int prosperity =
            Mathf.FloorToInt(node.State.GetStat(NodeStatId.Prosperity).value);

        int baseFood = 1 + prosperity;
        int variance = Mathf.Clamp(prosperity, 0, 4);

        var rng = new System.Random(daySeed);
        int roll = rng.Next(-variance, variance + 1);
        int foodCrates = Mathf.Max(0, baseFood + roll);

        market.stock.Add("item.food_crate", foodCrates);
    }
}
