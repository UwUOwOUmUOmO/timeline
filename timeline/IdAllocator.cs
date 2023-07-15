using System.Threading;

namespace Timeline
{
    public abstract class IdAllocator<T>
    {
        public T NextId() => GetNextId();
        public T CurrentId() => GetCurrentId();
        public static implicit operator T(IdAllocator<T> allocator) => allocator.NextId();

        public abstract T Add(T amount);
        public abstract void Set(T value);
        public abstract void Reset();
        protected abstract T GetNextId();
        protected abstract T GetCurrentId();
    }

    public class U32IdAllocator : IdAllocator<uint>
    {
        private uint _currentId;
        protected override uint GetNextId() => Add(1);

        protected override uint GetCurrentId() => _currentId;

        public override uint Add(uint amount)
        {
            lock (this)
            {
                _currentId += amount;
                return _currentId;
            }
        }

        public override void Set(uint value)
        {
            _currentId = value;
        }

        public override void Reset() => _currentId = 0;
    }
    public class I32IdAllocator : IdAllocator<int>
    {
        private int _currentId;
        protected override int GetNextId() => Interlocked.Increment(ref _currentId);
        protected override int GetCurrentId() => _currentId;
        public override int Add(int amount) => Interlocked.Add(ref _currentId, amount);

        public override void Set(int value) => _currentId = value;
        public override void Reset() => _currentId = 0;
    }
    public class U64IdAllocator : IdAllocator<ulong>
    {
        private ulong _currentId;
        protected override ulong GetNextId() => Add(1);
        public override ulong Add(ulong amount)
        {
            lock (this)
            {
                _currentId += amount;
                return _currentId;
            }
        }

        public override void Set(ulong value)
        {
            _currentId = value;
        }
        protected override ulong GetCurrentId() => _currentId;
        public override void Reset() => _currentId = 0;
    }
}