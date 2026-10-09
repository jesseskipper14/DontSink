using UnityEngine;

public sealed partial class DeckBoardZone
{
    private Vector2 ResolveDeckDestination(PlayerBoardingState actor)
    {
        Transform root = ResolveBoatRoot();
        if (boardPoint == null) return actor.transform.position;
        if (!BoatRailTraversalBlocker.HasRails(root)) return boardPoint.position;
        Rigidbody2D body = actor.GetComponent<Rigidbody2D>();
        Vector3 position = PhysicsFrame2D.InverseTransformPoint(root,
            body != null ? body.position : (Vector2)actor.transform.position);
        Vector3 deck = root.InverseTransformPoint(boardPoint.position);
        if (_trigger is BoxCollider2D box)
        {
            Vector3 a = root.InverseTransformPoint(box.transform.TransformPoint(box.offset - new Vector2(box.size.x * .5f, 0)));
            Vector3 b = root.InverseTransformPoint(box.transform.TransformPoint(box.offset + new Vector2(box.size.x * .5f, 0)));
            position.x = Mathf.Clamp(position.x, Mathf.Min(a.x, b.x), Mathf.Max(a.x, b.x));
        }
        deck.x = position.x;
        return PhysicsFrame2D.TransformPoint(root, deck);
    }

    private bool IsRailCrossingBlocked(PlayerBoardingState actor, bool unboarding)
    {
        Transform root = ResolveBoatRoot();
        if (!BoatRailTraversalBlocker.HasRails(root)) return false;
        Rigidbody2D body = actor.GetComponent<Rigidbody2D>();
        Vector2 start = body != null ? body.position : (Vector2)actor.transform.position;
        Vector2 halfSize = BoatRailTraversalBlocker.ActorHalfSize(actor, root);
        Vector2 end = ResolveDeckDestination(actor);
        if (unboarding)
        {
            // Leave context is a crossing down through the local deck edge, even
            // when the vessel is rotated/capsized. It doesn't teleport the actor.
            Vector3 local = PhysicsFrame2D.InverseTransformPoint(root, start);
            local.y -= halfSize.y * 2f + .1f;
            end = PhysicsFrame2D.TransformPoint(root, local);
        }
        return BoatRailTraversalBlocker.IsCrossingBlocked(root, start, end, halfSize);
    }

}
