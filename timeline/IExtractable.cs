namespace Timeline
{
    public interface IExtractable<in TOriginal, out TReduced>
    {
        public TReduced Extract(TOriginal original);
    }
    public interface ISelfExtractable<out T>
    {
        public T Extract();
    }

    public interface IReconstructable<in TReduced, out TOriginal>
    {
        public TOriginal Reconstruct(TReduced reduced);
    }
    public interface ISelfReconstructable<in T>
    {
        public void Reconstruct(T reduced);
    }
    public interface ISelfReplicateable<out T>
    {
        public T Replicate();
    }
}