using System.Collections.Generic;
using UnityEngine;

namespace Survival.Buffs
{
    [DisallowMultipleComponent]
    public sealed class PlayerEquipmentBuffProducer : MonoBehaviour, IPlayerBuffProducer
    {
        [SerializeField] private PlayerEquipment equipment;

        [SerializeField]
        private BottomBarSlotType[] slots =
        {
            BottomBarSlotType.Head,
            BottomBarSlotType.Body,
            BottomBarSlotType.Backpack,
            BottomBarSlotType.Toolbelt,
            BottomBarSlotType.Feet,
            BottomBarSlotType.Hands
        };

        public string ProducerId => "player.equipment";

        private void Reset()
        {
            if (equipment == null)
                equipment = GetComponentInParent<PlayerEquipment>();
        }

        private void Awake()
        {
            if (equipment == null)
                equipment =
                    GetComponent<PlayerEquipment>() ??
                    GetComponentInParent<PlayerEquipment>() ??
                    GetComponentInChildren<PlayerEquipment>(true);
        }

        public void Produce(List<PlayerBuffInstance> outList, float dt)
        {
            if (outList == null || equipment == null || slots == null)
                return;

            for (int i = 0; i < slots.Length; i++)
            {
                BottomBarSlotType slot = slots[i];

                ItemInstance item = equipment.Get(slot);
                if (item == null || item.Definition == null)
                    continue;

                IReadOnlyList<PlayerBuffDefinition> buffs = item.Definition.EquippedBuffs;
                if (buffs == null)
                    continue;

                for (int b = 0; b < buffs.Count; b++)
                {
                    PlayerBuffDefinition buff = buffs[b];
                    if (buff == null)
                        continue;

                    outList.Add(new PlayerBuffInstance
                    {
                        definition = buff,
                        sourceId = $"equipment:{slot}:{item.Definition.ItemId}",
                        severity01 = 1f
                    });
                }
            }
        }
    }
}