using System.Collections.Generic;
using Survival.Vitals;

namespace Survival.Afflictions
{
    public interface IAfflictionProducer
    {
        string ProducerId { get; }
        void Produce(List<AfflictionInstance> outList, float dt);
    }
}