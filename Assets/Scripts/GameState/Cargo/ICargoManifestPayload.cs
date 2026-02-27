public interface ICargoManifestPayload
{
    /// Return a small JSON blob representing item state (quantity, itemId, etc).
    string CapturePayloadJson();

    /// Restore previously captured payload (may be null/empty).
    void RestorePayloadJson(string payloadJson);
}
