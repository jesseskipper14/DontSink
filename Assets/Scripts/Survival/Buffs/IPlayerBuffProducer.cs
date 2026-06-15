using System.Collections.Generic;

namespace Survival.Buffs
{
    public interface IPlayerBuffProducer
    {
        string ProducerId { get; }
        void Produce(List<PlayerBuffInstance> outList, float dt);
    }
}