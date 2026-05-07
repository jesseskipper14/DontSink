using UnityEngine;

public static class WallSplitUtility
{
    public readonly struct SplitResult
    {
        public readonly bool IsValid;
        public readonly WallSegmentAuthoring Wall;

        public readonly float SegmentBottomY;
        public readonly float SegmentTopY;

        public readonly float OpeningBottomY;
        public readonly float OpeningTopY;

        public readonly float BottomHeight;
        public readonly float TopHeight;

        public readonly float BottomCenterY;
        public readonly float TopCenterY;

        public readonly bool HasBottomPiece;
        public readonly bool HasTopPiece;

        public SplitResult(
            bool isValid,
            WallSegmentAuthoring wall,
            float segmentBottomY,
            float segmentTopY,
            float openingBottomY,
            float openingTopY,
            float bottomHeight,
            float topHeight,
            float bottomCenterY,
            float topCenterY,
            bool hasBottomPiece,
            bool hasTopPiece)
        {
            IsValid = isValid;
            Wall = wall;
            SegmentBottomY = segmentBottomY;
            SegmentTopY = segmentTopY;
            OpeningBottomY = openingBottomY;
            OpeningTopY = openingTopY;
            BottomHeight = bottomHeight;
            TopHeight = topHeight;
            BottomCenterY = bottomCenterY;
            TopCenterY = topCenterY;
            HasBottomPiece = hasBottomPiece;
            HasTopPiece = hasTopPiece;
        }
    }

    public static bool TryComputeSplit(
        WallSegmentAuthoring wall,
        float openingBottomWorldY,
        float openingHeight,
        float minPieceHeight,
        out SplitResult result)
    {
        result = default;

        if (wall == null || openingHeight <= 0f)
            return false;

        float segmentBottomY = wall.WorldBottomY;
        float segmentTopY = wall.WorldTopY;

        float openingBottomY = openingBottomWorldY;
        float openingTopY = openingBottomY + openingHeight;

        const float epsilon = 0.0001f;

        if (openingBottomY < segmentBottomY - epsilon)
            return false;

        if (openingTopY > segmentTopY + epsilon)
            return false;

        float bottomHeight = Mathf.Max(0f, openingBottomY - segmentBottomY);
        float topHeight = Mathf.Max(0f, segmentTopY - openingTopY);

        bool hasBottom = bottomHeight >= minPieceHeight;
        bool hasTop = topHeight >= minPieceHeight;

        bool bottomTooSmall = bottomHeight > epsilon && bottomHeight < minPieceHeight;
        bool topTooSmall = topHeight > epsilon && topHeight < minPieceHeight;

        if (bottomTooSmall || topTooSmall)
            return false;

        if (!hasBottom && !hasTop)
            return false;

        float bottomCenterY = segmentBottomY + bottomHeight * 0.5f;
        float topCenterY = openingTopY + topHeight * 0.5f;

        result = new SplitResult(
            true,
            wall,
            segmentBottomY,
            segmentTopY,
            openingBottomY,
            openingTopY,
            bottomHeight,
            topHeight,
            bottomCenterY,
            topCenterY,
            hasBottom,
            hasTop);

        return true;
    }
}