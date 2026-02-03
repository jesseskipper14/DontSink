using System.Collections.Generic;
using UnityEngine;

public static class AirEscapeSolver
{
    /// <summary>
    /// Computes which compartments can release air this frame.
    /// Must run before air integrity decay.
    /// </summary>
    public static void UpdateAirEscape(
        IEnumerable<Compartment> compartments,
        System.Func<Compartment, bool> ventsToOutside
    )
    {
        // Reset
        foreach (var c in compartments)
            c._canReleaseAir = false; // internal flag or property

        Queue<Compartment> queue = new Queue<Compartment>();

        // Seed from outside-vented compartments
        foreach (var c in compartments)
        {
            if (!ventsToOutside(c))
                continue;

            // Air only escapes if opening is above water
            if (c.SolveSurfaceOffsetFromArea(c.WaterArea) < c.CeilingY)
            {
                c._canReleaseAir = true;
                queue.Enqueue(c);
            }
        }

        // Propagate upward
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            foreach (var conn in current.connections)
            {
                if (!conn.isOpen)
                    continue;

                Compartment neighbor = conn.Other(current);
                if (neighbor == null || neighbor._canReleaseAir)
                    continue;

                // Only propagate upward
                if (neighbor.CeilingY <= current.CeilingY)
                    continue;

                // Opening must be above water on BOTH sides
                if (conn.TopYWorld <= current.SolveSurfaceOffsetFromArea(current.WaterArea))
                    continue;

                if (conn.TopYWorld <= neighbor.SolveSurfaceOffsetFromArea(neighbor.WaterArea))
                    continue;

                neighbor._canReleaseAir = true;
                queue.Enqueue(neighbor);
            }
        }
    }
}
