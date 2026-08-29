public interface IWorldItemMassModifier
{
    /// <summary>
    /// Receives the canonical mass derived from ItemInstance.TotalMass and may
    /// return a modified FINAL world mass.
    ///
    /// Ordinary items need no modifier. This exists for genuinely dynamic
    /// payloads such as money whose mass is not represented by nested ItemInstances.
    /// </summary>
    float ModifyWorldItemMass(
        ItemInstance instance,
        float canonicalMass);
}
