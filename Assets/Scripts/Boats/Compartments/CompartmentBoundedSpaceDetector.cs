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
        float joinEpsilon = 0.06f,
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

        CompartmentBoundaryAuthoring floor = FindBestFloorUnderClick(clickWorld, boundaries);
        if (floor == null)
        {
            result.Failure = FailureReason.NoFloorUnderClick;
            return false;
        }

        Bounds floorBounds = floor.WorldBounds;
        float floorTopY = floorBounds.max.y;
        float floorLeftX = floorBounds.min.x;
        float floorRightX = floorBounds.max.x;

        CompartmentBoundaryAuthoring leftWall = FindNearestJoinedWallFromClick(
            clickX: clickWorld.x,
            floorTopY: floorTopY,
            searchLeft: true,
            boundaries: boundaries,
            joinEpsilon: joinEpsilon);

        if (leftWall == null)
        {
            result.Floor = floor;
            result.Failure = FailureReason.NoLeftWall;
            return false;
        }

        CompartmentBoundaryAuthoring rightWall = FindNearestJoinedWallFromClick(
            clickX: clickWorld.x,
            floorTopY: floorTopY,
            searchLeft: false,
            boundaries: boundaries,
            joinEpsilon: joinEpsilon);

        if (rightWall == null)
        {
            result.Floor = floor;
            result.LeftWall = leftWall;
            result.Failure = FailureReason.NoRightWall;
            return false;
        }

        Bounds leftBounds = leftWall.WorldBounds;
        Bounds rightBounds = rightWall.WorldBounds;

        bool leftJoined =
            floorTopY >= leftBounds.min.y - joinEpsilon &&
            floorTopY <= leftBounds.max.y + joinEpsilon;

        if (!leftJoined)
        {
            result.Floor = floor;
            result.LeftWall = leftWall;
            result.Failure = FailureReason.LeftWallNotJoinedToFloor;
            return false;
        }

        bool rightJoined =
            floorTopY >= rightBounds.min.y - joinEpsilon &&
            floorTopY <= rightBounds.max.y + joinEpsilon;

        if (!rightJoined)
        {
            result.Floor = floor;
            result.LeftWall = leftWall;
            result.RightWall = rightWall;
            result.Failure = FailureReason.RightWallNotJoinedToFloor;
            return false;
        }

        float leftTopY = leftBounds.max.y;
        float rightTopY = rightBounds.max.y;

        Debug.Log(
            $"BoundedSpace pre-roof: leftWall={leftWall.name} leftTopY={leftTopY:F2}, " +
            $"rightWall={rightWall.name} rightTopY={rightTopY:F2}, " +
            $"leftInnerX={leftBounds.max.x:F2}, rightInnerX={rightBounds.min.x:F2}, clickY={clickWorld.y:F2}");

        CompartmentBoundaryAuthoring roof = FindNearestCeilingBetweenWalls(
            leftInnerX: leftBounds.max.x,
            rightInnerX: rightBounds.min.x,
            clickY: clickWorld.y,
            boundaries: boundaries,
            joinEpsilon: joinEpsilon);

        Debug.Log(
            $"BoundedSpace roof result: roof={(roof != null ? roof.name : "NULL")} " +
            $"openTop={(roof == null)}");

        bool openTop = roof == null;

        float minX = leftBounds.max.x;
        float maxX = rightBounds.min.x;
        float minY = floorTopY;
        float maxY = openTop
            ? Mathf.Min(leftTopY, rightTopY)
            : roof.WorldBounds.min.y;

        Debug.Log(
            $"BoundedSpace final bounds: minX={minX:F2} maxX={maxX:F2} minY={minY:F2} maxY={maxY:F2}");

        if (maxX - minX <= minWidth || maxY - minY <= minHeight)
        {
            result.Floor = floor;
            result.LeftWall = leftWall;
            result.RightWall = rightWall;
            result.Roof = roof;
            result.IsOpenTop = openTop;
            result.Failure = FailureReason.SpaceTooSmall;
            return false;
        }

        if (clickWorld.x < minX || clickWorld.x > maxX || clickWorld.y < minY || clickWorld.y > maxY)
        {
            result.Floor = floor;
            result.LeftWall = leftWall;
            result.RightWall = rightWall;
            result.Roof = roof;
            result.IsOpenTop = openTop;
            result.MinX = minX;
            result.MaxX = maxX;
            result.MinY = minY;
            result.MaxY = maxY;
            result.Failure = FailureReason.ClickOutsideDetectedBounds;
            return false;
        }

        List<CompartmentBoundaryAuthoring> openings = boundaries
            .Where(b => b.IsOpeningCarrier && OverlapsContainerBand(b.WorldBounds, minX, maxX, minY, maxY, joinEpsilon))
            .Distinct()
            .ToList();

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
            Roof = roof,
            Openings = openings
        };

        return true;
    }

    private static CompartmentBoundaryAuthoring FindBestFloorUnderClick(
        Vector2 click,
        List<CompartmentBoundaryAuthoring> boundaries)
    {
        CompartmentBoundaryAuthoring best = null;
        float bestDelta = float.PositiveInfinity;

        foreach (CompartmentBoundaryAuthoring b in boundaries)
        {
            if (!b.IsHorizontalLike)
                continue;

            Bounds bounds = b.WorldBounds;

            bool spansClickX = click.x >= bounds.min.x && click.x <= bounds.max.x;
            bool belowOrAtClick = bounds.max.y <= click.y + 0.001f;

            if (!spansClickX || !belowOrAtClick)
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

    private static CompartmentBoundaryAuthoring FindNearestJoinedWallFromClick(
    float clickX,
    float floorTopY,
    bool searchLeft,
    List<CompartmentBoundaryAuthoring> boundaries,
    float joinEpsilon)
    {
        CompartmentBoundaryAuthoring best = null;
        float bestDistance = float.PositiveInfinity;

        foreach (CompartmentBoundaryAuthoring b in boundaries)
        {
            if (!b.IsVerticalLike)
                continue;

            Bounds bounds = b.WorldBounds;

            // Wall must span the floor top Y, meaning it actually reaches the floor band.
            bool spansFloorTopY =
                floorTopY >= bounds.min.y - joinEpsilon &&
                floorTopY <= bounds.max.y + joinEpsilon;

            if (!spansFloorTopY)
                continue;

            float candidateBoundaryX = searchLeft ? bounds.max.x : bounds.min.x;

            // Must be on the correct side of the click.
            if (searchLeft)
            {
                if (candidateBoundaryX > clickX + joinEpsilon)
                    continue;
            }
            else
            {
                if (candidateBoundaryX < clickX - joinEpsilon)
                    continue;
            }

            float distance = Mathf.Abs(candidateBoundaryX - clickX);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = b;
            }
        }

        return best;
    }

    private static CompartmentBoundaryAuthoring FindNearestCeilingBetweenWalls(
    float leftInnerX,
    float rightInnerX,
    float clickY,
    List<CompartmentBoundaryAuthoring> boundaries,
    float joinEpsilon)
    {
        CompartmentBoundaryAuthoring best = null;
        float bestCeilingY = float.PositiveInfinity;

        foreach (CompartmentBoundaryAuthoring b in boundaries)
        {
            if (!b.HasRole(CompartmentBoundaryRole.Roof) &&
                !b.HasRole(CompartmentBoundaryRole.Floor) &&
                !b.HasRole(CompartmentBoundaryRole.Ledge))
            {
                continue;
            }

            Bounds bounds = b.WorldBounds;

            bool spansBetweenWalls =
                bounds.min.x <= leftInnerX + joinEpsilon &&
                bounds.max.x >= rightInnerX - joinEpsilon;

            if (!spansBetweenWalls)
                continue;

            // Ceiling must be above the click, otherwise it is not the ceiling of the clicked space.
            bool aboveClick =
                bounds.min.y >= clickY - joinEpsilon;

            if (!aboveClick)
                continue;

            float ceilingY = bounds.min.y;

            Debug.Log(
                $"Ceiling candidate {b.name}: roles={b.Roles} " +
                $"bounds=({bounds.min.x:F2},{bounds.min.y:F2})-({bounds.max.x:F2},{bounds.max.y:F2}) " +
                $"leftInnerX={leftInnerX:F2} rightInnerX={rightInnerX:F2} clickY={clickY:F2} " +
                $"spansBetweenWalls={spansBetweenWalls} aboveClick={aboveClick} ceilingY={ceilingY:F2}");

            if (ceilingY < bestCeilingY)
            {
                bestCeilingY = ceilingY;
                best = b;
            }
        }

        return best;
    }

    private static bool OverlapsContainerBand(
        Bounds bounds,
        float minX,
        float maxX,
        float minY,
        float maxY,
        float epsilon)
    {
        bool overlapsX = bounds.max.x >= minX - epsilon && bounds.min.x <= maxX + epsilon;
        bool overlapsY = bounds.max.y >= minY - epsilon && bounds.min.y <= maxY + epsilon;
        return overlapsX && overlapsY;
    }
}