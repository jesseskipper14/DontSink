using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Netcode;
using UnityEngine;

// Low-frequency host snapshots, independent of the pose stream. No client RPC
// accepts this data. Reuse the persistence DTOs without Unity asset references.
public struct NetworkItemData : INetworkSerializable, IEquatable<NetworkItemData>
{
    public const int MaxBytes = 256 * 1024;
    public byte[] Bytes;
    public bool IsEmpty => Bytes == null || Bytes.Length == 0;
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        int length = Bytes?.Length ?? 0;
        serializer.SerializeValue(ref length);
        if (length < 0 || length > MaxBytes) throw new InvalidDataException("Shared item snapshot exceeds its byte budget.");
        if (serializer.IsReader)
        {
            Bytes = new byte[length];
            if (length > 0) serializer.GetFastBufferReader().ReadBytesSafe(ref Bytes, length);
        }
        else if (length > 0) serializer.GetFastBufferWriter().WriteBytesSafe(Bytes);
    }
    public bool Equals(NetworkItemData other)
    {
        if (ReferenceEquals(Bytes, other.Bytes)) return true;
        int count = Bytes?.Length ?? 0;
        if (count != (other.Bytes?.Length ?? 0)) return false;
        for (int i = 0; i < count; i++) if (Bytes[i] != other.Bytes[i]) return false;
        return true;
    }
}

public static class NetworkItemCodec
{
    private const int Version = 1, MaxDepth = 8, MaxItems = 512, MaxSlots = 64;
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public static NetworkItemData Capture(ItemInstance item) => CaptureItemSnapshot(item?.ToSnapshot());
    public static NetworkItemData CaptureItemSnapshot(ItemInstanceSnapshot snapshot) => Write(1, writer =>
    { int count = 0; WriteItem(writer, snapshot, 0, ref count); });

    public static NetworkItemData Capture(PlayerInventory inventory, PlayerEquipment equipment) => Write(2, writer =>
        WriteLoadout(writer, new PlayerLoadoutSnapshot { inventory = inventory.CaptureSnapshot(), equipment = equipment.CaptureSnapshot() }));
    public static NetworkItemData Capture(PlayerLoadoutSnapshot snapshot) => Write(2, writer => WriteLoadout(writer, snapshot));
    private static void WriteLoadout(BinaryWriter writer, PlayerLoadoutSnapshot snapshot)
    {
        var slots = snapshot.inventory;
        writer.Write(slots.hotbarSlotCount); writer.Write((int)slots.selectedSlot);
        int count = 0;
        foreach (var slot in slots.hotbarSlots) WriteItem(writer, slot, 0, ref count);
        var equipped = snapshot.equipment;
        foreach (var slot in new[] { equipped.hands, equipped.head, equipped.feet,
            equipped.toolbelt, equipped.backpack, equipped.body }) WriteItem(writer, slot, 0, ref count);
    }

    public static ItemInstanceSnapshot ReadItem(NetworkItemData data, IItemDefinitionResolver resolver)
    {
        using var reader = Reader(data, 1);
        int count = 0; var result = ReadItem(reader, resolver, 0, ref count);
        End(reader); return result;
    }
    public static PlayerLoadoutSnapshot ReadLoadout(NetworkItemData data, IItemDefinitionResolver resolver)
    {
        using var reader = Reader(data, 2);
        int slots = reader.ReadInt32(), selection = reader.ReadInt32();
        if (slots < 1 || slots > 8 || !Enum.IsDefined(typeof(BottomBarSlotType), selection))
            throw new InvalidDataException("Invalid shared inventory layout.");
        var inventory = new InventorySnapshot { hotbarSlotCount = slots, selectedSlot = (BottomBarSlotType)selection };
        int count = 0;
        for (int i = 0; i < slots; i++) inventory.hotbarSlots.Add(ReadItem(reader, resolver, 0, ref count));
        var equipment = new EquipmentSnapshot {
            hands = ReadItem(reader, resolver, 0, ref count), head = ReadItem(reader, resolver, 0, ref count),
            feet = ReadItem(reader, resolver, 0, ref count), toolbelt = ReadItem(reader, resolver, 0, ref count),
            backpack = ReadItem(reader, resolver, 0, ref count), body = ReadItem(reader, resolver, 0, ref count) };
        End(reader); return new PlayerLoadoutSnapshot { inventory = inventory, equipment = equipment };
    }

    public static NetworkItemData CaptureContainer(ItemContainerSnapshot snapshot) => Write(3, writer =>
    {
        if (snapshot == null || snapshot.slotCount < 1 || snapshot.slotCount > MaxSlots || snapshot.slots.Count != snapshot.slotCount)
            throw new InvalidDataException("Invalid storage layout.");
        writer.Write(snapshot.slotCount); writer.Write(snapshot.columnCount); int count = 0;
        foreach (var item in snapshot.slots) WriteItem(writer, item, 0, ref count);
    });
    public static ItemContainerSnapshot ReadContainer(NetworkItemData data, int slots, int columns, IItemDefinitionResolver resolver)
    {
        using var reader = Reader(data, 3);
        if (slots < 1 || slots > MaxSlots || reader.ReadInt32() != slots || reader.ReadInt32() != columns)
            throw new InvalidDataException("Incompatible storage layout.");
        var snapshot = new ItemContainerSnapshot { slotCount = slots, columnCount = columns }; int count = 0;
        for (int i = 0; i < slots; i++) snapshot.slots.Add(ReadItem(reader, resolver, 0, ref count));
        End(reader); return snapshot;
    }

    private static NetworkItemData Write(byte kind, Action<BinaryWriter> write)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Utf8, true);
        writer.Write(Version); writer.Write(kind); write(writer); writer.Flush();
        if (stream.Length > NetworkItemData.MaxBytes) throw new InvalidDataException("Shared item data exceeds 256 KiB; extend the protocol before using it.");
        return new NetworkItemData { Bytes = stream.ToArray() };
    }
    private static BinaryReader Reader(NetworkItemData data, byte kind)
    {
        if (data.IsEmpty || data.Bytes.Length > NetworkItemData.MaxBytes) throw new InvalidDataException("Missing shared item data.");
        var reader = new BinaryReader(new MemoryStream(data.Bytes, false), Utf8);
        if (reader.ReadInt32() == Version && reader.ReadByte() == kind) return reader;
        reader.Dispose(); throw new InvalidDataException("Incompatible shared item schema.");
    }
    private static void End(BinaryReader reader)
    {
        if (reader.BaseStream.Position != reader.BaseStream.Length) throw new InvalidDataException("Trailing shared item data.");
    }
    private static void Text(BinaryWriter writer, string value)
    {
        byte[] bytes = Utf8.GetBytes(value ?? "");
        if (bytes.Length > NetworkItemData.MaxBytes) throw new InvalidDataException("Shared item text exceeds its budget.");
        writer.Write(bytes.Length); writer.Write(bytes);
    }
    private static string Text(BinaryReader reader)
    {
        int length = reader.ReadInt32();
        if (length < 0 || length > NetworkItemData.MaxBytes || length > reader.BaseStream.Length - reader.BaseStream.Position)
            throw new InvalidDataException("Invalid shared item text length.");
        return Utf8.GetString(reader.ReadBytes(length));
    }
    private static void Budget(int depth, ref int count)
    {
        if (depth > MaxDepth || ++count > MaxItems) throw new InvalidDataException("Shared item nesting exceeds its budget.");
    }
    private static void WriteItem(BinaryWriter writer, ItemInstanceSnapshot item, int depth, ref int count)
    {
        writer.Write(item != null); if (item == null) return;
        Budget(depth, ref count);
        writer.Write(item.version); Text(writer, item.instanceId); Text(writer, item.itemId);
        writer.Write(item.quantity); writer.Write(item.currentCharges); writer.Write(item.retainedWaterVolume);
        // Optional chart DTOs already define presence flags and detached copies.
        // Keep that established format so masks, stars and future evidence survive.
        Text(writer, item.chartingInstrument == null ? null : JsonUtility.ToJson(item.chartingInstrument));
        Text(writer, item.cartographicChart == null ? null : JsonUtility.ToJson(item.cartographicChart));
        writer.Write(item.container != null);
        if (item.container == null) return;
        int slots = item.container.slotCount;
        if (slots < 0 || slots > MaxSlots || item.container.slots?.Count != slots)
            throw new InvalidDataException("Invalid shared container layout.");
        writer.Write(item.container.version); writer.Write(slots); writer.Write(item.container.columnCount);
        foreach (var slot in item.container.slots) WriteItem(writer, slot, depth + 1, ref count);
    }
    private static ItemInstanceSnapshot ReadItem(BinaryReader reader, IItemDefinitionResolver resolver, int depth, ref int count)
    {
        if (!reader.ReadBoolean()) return null;
        Budget(depth, ref count);
        var item = new ItemInstanceSnapshot { version = reader.ReadInt32(), instanceId = Text(reader), itemId = Text(reader),
            quantity = reader.ReadInt32(), currentCharges = reader.ReadInt32(), retainedWaterVolume = reader.ReadSingle() };
        var definition = resolver?.Resolve(item.itemId);
        if (item.version != 1 || definition == null || string.IsNullOrWhiteSpace(item.instanceId) ||
            item.instanceId.Length > 128 || item.quantity < 1 || item.quantity > definition.MaxStack ||
            !float.IsFinite(item.retainedWaterVolume) || item.retainedWaterVolume < 0 ||
            item.currentCharges < 0 || item.currentCharges > definition.MaxCharges)
            throw new InvalidDataException("Invalid item identity, definition or values.");
        string instrument = Text(reader), chart = Text(reader);
        item.chartingInstrument = instrument.Length == 0 ? null : JsonUtility.FromJson<ChartingInstrumentState>(instrument);
        item.cartographicChart = chart.Length == 0 ? null : JsonUtility.FromJson<CartographicChartState>(chart);
        if (reader.ReadBoolean())
        {
            int version = reader.ReadInt32(), slots = reader.ReadInt32(), columns = reader.ReadInt32();
            if (version != 1 || !definition.IsContainer || slots != definition.ContainerSlotCount ||
                slots < 0 || slots > MaxSlots || columns != definition.ContainerColumnCount)
                throw new InvalidDataException("Incompatible container definition/layout.");
            item.container = new ItemContainerSnapshot { slotCount = slots, columnCount = columns };
            for (int i = 0; i < slots; i++) item.container.slots.Add(ReadItem(reader, resolver, depth + 1, ref count));
        }
        if (item.cartographicChart?.HasState == true && (item.quantity != 1 || definition.IsContainer))
            throw new InvalidDataException("Invalid chart carrier.");
        return item;
    }
}
