using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class CompartmentBoundedSpaceDetector
{
    public enum FailureReason
    {
        None = 0,
        NoFloorUnderClick = 1,
        NoLeftWall = 2,
        NoRightWall = 3,
        LeftWallNotJoinedToFloor = 4,
        RightWallNotJoinedToFloor = 5,
        ClickOutsideDetectedBounds = 6,
        SpaceTooSmall = 7
    }

    public struct Result
    {
        public bool IsValid;
        public bool IsOpenTop;
        public FailureReason Failure;

        public float MinX;
        public float MaxX;
        public float MinY;
        public float MaxY;

        public CompartmentBoundaryAuthoring Floor;
        public CompartmentBoundaryAuthoring LeftWall;
        public CompartmentBoundaryAuthoring RightWall;
        public CompartmentBoundaryAuthoring Roof;

        public List<CompartmentBoundaryAuthoring> Openings;

        public override string ToString()
        {
            if (!IsValid)
                return $"Invalid: {Failure}";

            return $"Valid bounds x=[{MinX:F2},{MaxX:F2}] y=[{MinY:F2},{MaxY:F2}] openTop={IsOpenTop}";
        }
    }

    public static bool TryDetectBoundedSpaceAtPoint(
        Vector2 clickWorld,
        IEnumerable<CompartmentBoundaryAuthoring> allBoundaries,
        out Result result,
        float joinEpsilon = 0.12f,
        float minWidth = 0.1f,
        float minHeight = 0.1f)
    {
        result = default;

        List<CompartmentBoundaryAuthoring> boundaries = allBoundaries?
            .Where(b => b != null && b.CountsAsBoundary && b.Collider != null)
            .ToList();

        if (boundaries == null || boundaries.Count == 0)
        {
            result.Failure = FailureReason.NoFloorUnderClick;
            return false;
        }

        CompartmentBoundaryAuthoring floor = FindNearestHorizontalBlockerBelow(clickWorld, boundaries, joinEpsilon);
        if (floor == null)
        {
            result.Failure = FailureReason.NoFloorUnderClick;
            return false;
        }

        CompartmentBoundaryAuthoring ceiling = FindNearestHorizontalBlockerAbove(clickWorld, boundaries, joinEpsilon);

        if (ceiling == null)
        {
            // Open-top fallback: use lower side wall top later once walls are found.
            // Leave null for now.
        }

        float floorTopY = floor.WorldBounds.max.y;
        float provisionalCeilingY = ceiling != null ? ceiling.WorldBounds.min.y : float.PositiveInfinity;

        CompartmentBoundaryAuthoring leftWall = FindNearestVerticalBlocker(
            clickWorld,
            boundaries,
            searchLeft: true,
            floorTopY: floorTopY,
            ceilingY: provisionalCeilingY,
            epsilon: joinEpsilon);

        if (leftWall == null)
        {
            result.Floor = floor;
            result.Roof = ceiling;
            result.Failure = FailureReason.NoLeftWall;
            return false;
        }

        CompartmentBoundaryAuthoring rightWall = FindNearestVerticalBlocker(
            clickWorld,
            boundaries,
            searchLeft: false,
            floorTopY: floorTopY,
            ceilingY: provisionalCeilingY,
            epsilon: joinEpsilon);

        if (rightWall == null)
        {
            result.Floor = floor;
            result.LeftWall = leftWall;
            result.Roof = ceiling;
            result.Failure = FailureReason.NoRightWall;
            return false;
        }

        Bounds leftBounds = leftWall.WorldBounds;
        Bounds rightBounds = rightWall.WorldBounds;

        float leftInnerX = leftBounds.max.x;
        float rightInnerX = rightBounds.min.x;

        float leftTopY = leftBounds.max.y;
        float rightTopY = rightBounds.max.y;

        bool openTop = ceiling == null;
        float ceilingY = openTop
            ? Mathf.Min(leftTopY, rightTopY)
            : ceiling.WorldBounds.min.y;

        float minX = leftInnerX;
        float maxX = rightInnerX;
        float minY = floorTopY;
        float maxY = ceilingY;

        if (maxX - minX <= minWidth || maxY - minY <= minHeight)
        {
            result.Floor = floor;
            result.LeftWall = leftWall;
            result.RightWall = rightWall;
            result.Roof = ceiling;
            result.IsOpenTop = openTop;
            result.Failure = FailureReason.SpaceTooSmall;
            return false;
        }

        if (clickWorld.x < minX || clickWorld.x > maxX || clickWorld.y < minY || clickWorld.y > maxY)
        {
            result.Floor = floor;
            result.LeftWall = leftWall;
            result.RightWall = rightWall;
            result.Roof = ceiling;
            result.IsOpenTop = openTop;
            result.MinX = minX;
            result.MaxX = maxX;
            result.MinY = minY;
            result.MaxY = maxY;
            result.Failure = FailureReason.ClickOutsideDetectedBounds;
            return false;
        }

        result = new Result
        {
            IsValid = true,
            IsOpenTop = openTop,
            Failure = FailureReason.None,
            MinX = minX,
            MaxX = maxX,
            MinY = minY,
            MaxY = maxY,
            Floor = floor,
            LeftWall = leftWall,
            RightWall = rightWall,
            Roof = ceiling,
            Openings = new List<CompartmentBoundaryAuthoring>()
        };

        return true;
    }

    private static CompartmentBoundaryAuthoring FindNearestHorizontalBlockerBelow(
    Vector2 click,
    List<CompartmentBoundaryAuthoring> boundaries,
    float epsilon)
    {
        CompartmentBoundaryAuthoring best = null;
        float bestDelta = float.PositiveInfinity;

        foreach (CompartmentBoundaryAuthoring b in boundaries)
        {
            if (!b.IsHorizontalBlocker)
                continue;

            Bounds bounds = b.WorldBounds;

            bool spansClickX = click.x >= bounds.min.x - epsilon && click.x <= bounds.max.x + epsilon;
            bool isBelow = bounds.max.y <= click.y + epsilon;

            if (!spansClickX || !isBelow)
                continue;

            float delta = click.y - bounds.max.y;
            if (delta < bestDelta)
            {
                bestDelta = delta;
                best = b;
            }
        }

        return best;
    }

    private static CompartmentBoundaryAuthoring FindNearestHorizontalBlockerAbove(
        Vector2 click,
        List<CompartmentBoundaryAuthoring> boundaries,
        float epsilon)
    {
        CompartmentBoundaryAuthoring best = null;
        float bestDelta = float.PositiveInfinity;

        foreach (CompartmentBoundaryAuthoring b in boundaries)
        {
            if (!b.IsHorizontalBlocker)
                continue;

            Bounds bounds = b.WorldBounds;

            bool spansClickX = click.x >= bounds.min.x - epsilon && click.x <= bounds.max.x + epsilon;
            bool isAbove = bounds.min.y >= click.y - epsilon;

            if (!spansClickX || !isAbove)
                continue;

            float delta = bounds.min.y - click.y;
            if (delta < bestDelta)
            {
                bestDelta = delta;
                best = b;
            }
        }

        return best;
    }

    private static CompartmentBoundaryAuthoring FindNearestVerticalBlocker(
        Vector2 click,
        List<CompartmentBoundaryAuthoring> boundaries,
        bool searchLeft,
        float floorTopY,
        float ceilingY,
        float epsilon)
    {
        CompartmentBoundaryAuthoring best = null;
        float bestDistance = float.PositiveInfinity;

        foreach (CompartmentBoundaryAuthoring b in boundaries)
        {
            if (!b.IsVerticalBlocker)
                continue;

            Bounds bounds = b.WorldBounds;

            float blockerX = searchLeft ? bounds.max.x : bounds.min.x;

            if (searchLeft)
            {
                if (blockerX > click.x + epsilon)
                    continue;
            }
            else
            {
                if (blockerX < click.x - epsilon)
                    continue;
            }

            // Must overlap the clicked vertical band at least around the click itself.
            bool spansClickY = click.y >= bounds.min.y - epsilon && click.y <= bounds.max.y + epsilon;
            bool reachesFloorBand = floorTopY >= bounds.min.y - epsilon && floorTopY <= bounds.max.y + epsilon;

            bool reachesCeilingBand = true;
            if (!float.IsPositiveInfinity(ceilingY))
                reachesCeilingBand = ceilingY >= bounds.min.y - epsilon && ceilingY <= bounds.max.y + epsilon;

            if (!spansClickY || !reachesFloorBand || !reachesCeilingBand)
                continue;

            float dist = Mathf.Abs(blockerX - click.x);
            if (dist < bestDistance)
            {
                bestDistance = dist;
                best = b;
            }
        }

        return best;
    }
}