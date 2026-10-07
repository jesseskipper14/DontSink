using UnityEngine;

public sealed partial class WorldMapKnowledgeState
{
    // Runs once inside an accepted surface commit, never during movement, drawing, or save restoration.
    // Four-neighbor components wrap horizontally; the finite north/south edges are not adjacent.
    private void CleanupTinySurfaceGaps(int maximumCells)
    {
        maximumCells = Mathf.Clamp(maximumCells, 0, 64);
        if (maximumCells == 0) return;
        int count = surfaceRevealed.Length;
        var visited = new bool[count];
        var queue = new int[count];
        int tail = 0;
        void Enqueue(int index)
        {
            if (visited[index] || surfaceRevealed[index]) return;
            visited[index] = true;
            queue[tail++] = index;
        }
        for (int start = 0; start < count; start++)
        {
            if (visited[start] || surfaceRevealed[start]) continue;
            tail = 0;
            Enqueue(start);
            for (int head = 0; head < tail; head++)
            {
                int index = queue[head], x = index % width, y = index / width;
                Enqueue(y * width + (x == 0 ? width - 1 : x - 1));
                Enqueue(y * width + (x + 1 == width ? 0 : x + 1));
                if (y > 0) Enqueue(index - width);
                if (y + 1 < height) Enqueue(index + width);
            }
            if (tail > maximumCells) continue;
            for (int i = 0; i < tail; i++) surfaceRevealed[queue[i]] = true;
        }
    }
}
