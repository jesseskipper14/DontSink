using UnityEngine;

public static class FloorSplitUtility
{
    public readonly struct SplitResult
    {
        public readonly bool IsValid;
        public readonly FloorSegmentAuthoring Floor;
        public readonly float SegmentLeftX;
        public readonly float SegmentRightX;
        public readonly float OpeningLeftX;
        public readonly float OpeningRightX;
        public readonly float LeftWidth;
        public readonly float RightWidth;
        public readonly float LeftCenterX;
        public readonly float RightCenterX;

        public SplitResult(
            bool isValid,
            FloorSegmentAuthoring floor,
            float segmentLeftX,
            float segmentRightX,
            float openingLeftX,
            float openingRightX,
            float leftWidth,
            float rightWidth,
            float leftCenterX,
            float rightCenterX)
        {
            IsValid = isValid;
            Floor = floor;
            SegmentLeftX = segmentLeftX;
            SegmentRightX = segmentRightX;
            OpeningLeftX = openingLeftX;
            OpeningRightX = openingRightX;
            LeftWidth = leftWidth;
            RightWidth = rightWidth;
            LeftCenterX = leftCenterX;
            RightCenterX = rightCenterX;
        }
    }

    public static bool TryComputeSplit(
        FloorSegmentAuthoring floor,
        float hatchCenterWorldX,
        float openingWidth,
        float minPieceWidth,
        out SplitResult result)
    {
        result = default;

        if (floor == null || openingWidth <= 0f)
            return false;

        float segmentLeftX = floor.WorldLeftX;
        float segmentRightX = floor.WorldRightX;

        float openingLeftX = hatchCenterWorldX - openingWidth * 0.5f;
        float openingRightX = hatchCenterWorldX + openingWidth * 0.5f;

        const float epsilon = 0.0001f;

        if (openingLeftX < segmentLeftX + epsilon || openingRightX > segmentRightX - epsilon)
            return false;

        float leftWidth = openingLeftX - segmentLeftX;
        float rightWidth = segmentRightX - openingRightX;

        if (leftWidth < minPieceWidth || rightWidth < minPieceWidth)
            return false;

        float leftCenterX = segmentLeftX + leftWidth * 0.5f;
        float rightCenterX = openingRightX + rightWidth * 0.5f;

        result = new SplitResult(
            true,
            floor,
            segmentLeftX,
            segmentRightX,
            openingLeftX,
            openingRightX,
            leftWidth,
            rightWidth,
            leftCenterX,
            rightCenterX);

        return true;
    }
}