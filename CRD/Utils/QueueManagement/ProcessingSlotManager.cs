using System;
using System.Threading;
using System.Threading.Tasks;

namespace CRD.Utils.QueueManagement;

public sealed class ProcessingSlotManager{
    private readonly SemaphoreSlim semaphore;
    private readonly object syncLock = new();

    private int limit;
    private int borrowedPermits;

    public int Limit{
        get{
            lock (syncLock){
                return limit;
            }
        }
    }

    public ProcessingSlotManager(int initialLimit){
        if (initialLimit < 0)
            throw new ArgumentOutOfRangeException(nameof(initialLimit));

        limit = initialLimit;

        semaphore = new SemaphoreSlim(
            initialCount: initialLimit,
            maxCount: int.MaxValue);
    }

    public Task WaitAsync(CancellationToken cancellationToken = default){
        return semaphore.WaitAsync(cancellationToken);
    }

    public void Release(){
        lock (syncLock){
            if (borrowedPermits > 0){
                borrowedPermits--;
                return;
            }

            semaphore.Release();
        }
    }

    public void SetLimit(int newLimit){
        if (newLimit < 0)
            throw new ArgumentOutOfRangeException(nameof(newLimit));

        lock (syncLock){
            if (newLimit == limit)
                return;

            int delta = newLimit - limit;

            if (delta > 0){
                int giveBackBorrowed = Math.Min(borrowedPermits, delta);
                borrowedPermits -= giveBackBorrowed;

                int permitsToRelease = delta - giveBackBorrowed;
                if (permitsToRelease > 0)
                    semaphore.Release(permitsToRelease);
            } else{
                int permitsToRemove = -delta;

                while (permitsToRemove > 0 && semaphore.Wait(0)){
                    permitsToRemove--;
                }

                borrowedPermits += permitsToRemove;
            }

            limit = newLimit;
        }
    }
}