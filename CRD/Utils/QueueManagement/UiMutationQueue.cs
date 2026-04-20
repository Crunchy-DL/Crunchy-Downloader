using System;
using System.Collections.Generic;
using System.Threading;
using Avalonia.Threading;

namespace CRD.Utils.QueueManagement;

public sealed class UiMutationQueue{
    private readonly object syncLock = new();
    private readonly Queue<Action> pendingMutations = new();

    private readonly Dispatcher dispatcher;
    private readonly DispatcherPriority priority;

    private bool isProcessing;
    private int pumpScheduled;

    public UiMutationQueue()
        : this(null, DispatcherPriority.Background){
    }

    public UiMutationQueue(
        Dispatcher? dispatcher,
        DispatcherPriority priority){
        this.dispatcher = dispatcher ?? Dispatcher.UIThread;
        this.priority = priority;
    }

    public void Enqueue(Action mutation){
        if (mutation == null)
            throw new ArgumentNullException(nameof(mutation));

        lock (syncLock){
            pendingMutations.Enqueue(mutation);
        }

        if (Interlocked.CompareExchange(ref pumpScheduled, 1, 0) != 0)
            return;

        dispatcher.Post(ProcessPendingMutations, priority);
    }

    private void ProcessPendingMutations(){
        if (isProcessing)
            return;

        try{
            isProcessing = true;

            while (true){
                Action? mutation;

                lock (syncLock){
                    mutation = pendingMutations.Count > 0
                        ? pendingMutations.Dequeue()
                        : null;
                }

                if (mutation is null)
                    break;

                mutation();
            }
        } finally{
            isProcessing = false;
            Interlocked.Exchange(ref pumpScheduled, 0);

            bool hasPending;
            lock (syncLock){
                hasPending = pendingMutations.Count > 0;
            }

            if (hasPending &&
                Interlocked.CompareExchange(ref pumpScheduled, 1, 0) == 0){
                dispatcher.Post(ProcessPendingMutations, priority);
            }
        }
    }
}