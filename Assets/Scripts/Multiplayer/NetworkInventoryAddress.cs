using System;
using Unity.Collections;
using Unity.Netcode;

public enum NetworkInventoryKind : byte { Hotbar = 1, Equipment, Container, Storage }
public struct NetworkInventoryAddress : INetworkSerializable, IEquatable<NetworkInventoryAddress>
{
    public NetworkInventoryKind Kind;
    public int Slot;
    public FixedString512Bytes Parent;
    public uint Generation;
    public NetworkInventoryAddress(NetworkInventoryKind kind, int slot, string parent = "", uint generation = 0)
    { Kind = kind; Slot = slot; Parent = new FixedString512Bytes(parent ?? ""); Generation = generation; }
    public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
    { s.SerializeValue(ref Kind); s.SerializeValue(ref Slot); s.SerializeValue(ref Parent); s.SerializeValue(ref Generation); }
    public bool Equals(NetworkInventoryAddress other) => Kind == other.Kind && Slot == other.Slot && Parent.Equals(other.Parent) && Generation == other.Generation;
}
public interface INetworkInventorySlotBinding
{
    NetworkInventoryAddress NetworkAddress { get; }
}
