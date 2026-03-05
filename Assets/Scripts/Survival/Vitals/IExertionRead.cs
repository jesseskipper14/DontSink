namespace Survival.Vitals
{
    public interface IExertionRead
    {
        /// <summary>0..1 exertion intensity (rest=0, sprint=1).</summary>
        float Exertion01 { get; }
    }
}