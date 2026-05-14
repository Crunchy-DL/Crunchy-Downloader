using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CRD.Downloader.Crunchyroll;
using CRD.Utils;
using CRD.Utils.CustomList;
using CRD.Utils.QueueManagement;
using CRD.Utils.Structs;
using CRD.ViewModels;

namespace CRD.Downloader;

public sealed partial class QueueManager : ObservableObject{
    public static QueueManager Instance{ get; } = new();
    
    #region Download Variables

    private readonly RefreshableObservableCollection<CrunchyEpMeta> queue = new();
    public ReadOnlyObservableCollection<CrunchyEpMeta> Queue{ get; }

    private readonly DownloadItemModelCollection downloadItems = new();

    public ObservableCollection<DownloadItemModel> DownloadItemModels => downloadItems.Items;
    
    private readonly UiMutationQueue uiMutationQueue;
    private readonly QueuePersistenceManager queuePersistenceManager;

    private readonly object downloadStartLock = new();
    private readonly HashSet<CrunchyEpMeta> activeOrStarting = new();

    private readonly ProcessingSlotManager processingSlots;

    private int pumpScheduled;
    private int pumpDirty;
    private DateTimeOffset? autoDownloadBlockedUntilUtc;
    private readonly object autoDownloadBlockLock = new();

    #endregion
    
    public int ActiveDownloads{
        get{
            lock (downloadStartLock){
                return activeOrStarting.Count;
            }
        }
    }

    public bool HasActiveDownloads => ActiveDownloads > 0;

    [ObservableProperty]
    private bool hasFailedItem;

    public event EventHandler? QueueStateChanged;

    private readonly CrunchyrollManager crunchyrollManager;

    public QueueManager(){
        crunchyrollManager = CrunchyrollManager.Instance;

        uiMutationQueue = new UiMutationQueue();
        queuePersistenceManager = new QueuePersistenceManager(this);
        Queue = new ReadOnlyObservableCollection<CrunchyEpMeta>(queue);

        processingSlots = new ProcessingSlotManager(
            crunchyrollManager.CrunOptions.SimultaneousProcessingJobs);

        queue.CollectionChanged += UpdateItemListOnRemove;
        queue.CollectionChanged += (_, _) => OnQueueStateChanged();
    }

    public void AddToQueue(CrunchyEpMeta item){
        uiMutationQueue.Enqueue(() => {
            if (!queue.Contains(item))
                queue.Add(item);
        });
    }

    public void RemoveFromQueue(CrunchyEpMeta item){
        uiMutationQueue.Enqueue(() => {
            int index = queue.IndexOf(item);
            if (index >= 0)
                queue.RemoveAt(index);
        });
    }

    public void ClearQueue(){
        uiMutationQueue.Enqueue(() => queue.Clear());
    }

    public void RefreshQueue(){
        uiMutationQueue.Enqueue(() => queue.Refresh());
    }


    public bool TryStartDownload(DownloadItemModel model){
        var item = model.epMeta;

        lock (downloadStartLock){
            if (activeOrStarting.Contains(item))
                return false;

            if (item.DownloadProgress.State is DownloadState.Downloading or DownloadState.Processing)
                return false;

            if (item.DownloadProgress.IsDone)
                return false;

            if (item.DownloadProgress.IsError)
                return false;

            if (item.DownloadProgress.IsPaused)
                return false;

            if (activeOrStarting.Count >= crunchyrollManager.CrunOptions.SimultaneousDownloads)
                return false;

            activeOrStarting.Add(item);
        }
        
        NotifyDownloadStateChanged();
        OnQueueStateChanged();
        _ = model.StartDownloadCore();
        return true;
    }

    public bool TryResumeDownload(CrunchyEpMeta item){
        lock (downloadStartLock){
            if (activeOrStarting.Contains(item))
                return false;

            if (!item.DownloadProgress.IsPaused)
                return false;

            if (activeOrStarting.Count >= crunchyrollManager.CrunOptions.SimultaneousDownloads)
                return false;

            activeOrStarting.Add(item);
        }

        NotifyDownloadStateChanged();
        OnQueueStateChanged();
        return true;
    }

    public void ReleaseDownloadSlot(CrunchyEpMeta item){
        bool removed;

        lock (downloadStartLock){
            removed = activeOrStarting.Remove(item);
        }

        if (removed){
            NotifyDownloadStateChanged();
            OnQueueStateChanged();

            if (crunchyrollManager.CrunOptions.AutoDownload){
                RequestPump();
            }
        }
    }

    public Task WaitForProcessingSlotAsync(CancellationToken cancellationToken = default){
        return processingSlots.WaitAsync(cancellationToken);
    }

    public void ReleaseProcessingSlot(){
        processingSlots.Release();
    }

    public void SetProcessingLimit(int newLimit){
        processingSlots.SetLimit(newLimit);
    }

    public void RestorePersistedQueue(){
        queuePersistenceManager.RestoreQueue();
    }

    public void SaveQueueSnapshot(){
        queuePersistenceManager.SaveNow();
    }

    internal List<CrunchyEpMeta> GetQueueSnapshot(){
        if (Dispatcher.UIThread.CheckAccess()){
            return queue.ToList();
        }

        return Dispatcher.UIThread
            .InvokeAsync(() => queue.ToList())
            .GetAwaiter()
            .GetResult();
    }

    public void ReplaceQueue(IEnumerable<CrunchyEpMeta> items){
        uiMutationQueue.Enqueue(() => {
            queue.Clear();
            foreach (var item in items){
                if (!queue.Contains(item))
                    queue.Add(item);
            }

            RestoreRetryStateFromQueue();
            UpdateDownloadListItems();
        });
    }

    private void UpdateItemListOnRemove(object? sender, NotifyCollectionChangedEventArgs e){
        if (e.Action == NotifyCollectionChangedAction.Remove){
            if (e.OldItems != null){
                foreach (var oldItem in e.OldItems.OfType<CrunchyEpMeta>()){
                    downloadItems.Remove(oldItem);
                }
            }
        } else if (e.Action == NotifyCollectionChangedAction.Reset && queue.Count == 0){
            downloadItems.Clear();
        }

        UpdateDownloadListItems();
    }

    public void MarkDownloadFinished(CrunchyEpMeta item, bool removeFromQueue){
        uiMutationQueue.Enqueue(() => {
            if (removeFromQueue){
                int index = queue.IndexOf(item);
                if (index >= 0)
                    queue.RemoveAt(index);
            } else{
                queue.Refresh();
            }

            OnQueueStateChanged();
        });
    }

    public void UpdateDownloadListItems(){
        downloadItems.SyncFromQueue(queue);

        HasFailedItem = queue.Any(item => item.DownloadProgress.IsError);

        if (crunchyrollManager.CrunOptions.AutoDownload){
            RequestPump();
        }
    }

    private void RequestPump(){
        Interlocked.Exchange(ref pumpDirty, 1);

        if (Interlocked.CompareExchange(ref pumpScheduled, 1, 0) != 0)
            return;

        Avalonia.Threading.Dispatcher.UIThread.Post(
            RunPump,
            Avalonia.Threading.DispatcherPriority.Background);
    }

    private void RunPump(){
        try{
            while (Interlocked.Exchange(ref pumpDirty, 0) == 1){
                PumpQueue();
            }
        } finally{
            Interlocked.Exchange(ref pumpScheduled, 0);

            if (Volatile.Read(ref pumpDirty) == 1 &&
                Interlocked.CompareExchange(ref pumpScheduled, 1, 0) == 0){
                Avalonia.Threading.Dispatcher.UIThread.Post(
                    RunPump,
                    Avalonia.Threading.DispatcherPriority.Background);
            }
        }
    }

    private void PumpQueue(){
        if (!crunchyrollManager.CrunOptions.AutoDownload)
            return;

        lock (autoDownloadBlockLock){
            if (autoDownloadBlockedUntilUtc.HasValue && !HasPendingRetryItems()){
                autoDownloadBlockedUntilUtc = null;
            }

            if (autoDownloadBlockedUntilUtc.HasValue && autoDownloadBlockedUntilUtc.Value > DateTimeOffset.UtcNow){
                return;
            }

            if (autoDownloadBlockedUntilUtc.HasValue){
                autoDownloadBlockedUntilUtc = null;
            }
        }
        
        List<CrunchyEpMeta> toStart = new();
        List<CrunchyEpMeta> toResume = new();
        bool changed = false;
        
        lock (downloadStartLock){
            int limit = crunchyrollManager.CrunOptions.SimultaneousDownloads;
            int freeSlots = Math.Max(0, limit - activeOrStarting.Count);

            if (freeSlots == 0)
                return;

            foreach (var item in queue.ToList()){
                if (freeSlots == 0)
                    break;

                if (item.DownloadProgress.IsError)
                    continue;

                if (item.DownloadProgress.IsWaitingForRetry)
                    continue;

                if (item.DownloadProgress.IsDone)
                    continue;

                if (item.DownloadProgress.State is DownloadState.Downloading or DownloadState.Processing)
                    continue;

                if (activeOrStarting.Contains(item))
                    continue;

                activeOrStarting.Add(item);
                freeSlots--;

                if (item.DownloadProgress.IsPaused){
                    toResume.Add(item);
                } else{
                    toStart.Add(item);
                }

                changed = true;
            }
        }
        
        if (changed){
            NotifyDownloadStateChanged();
        }

        foreach (var item in toResume){
            item.DownloadProgress.State = item.DownloadProgress.ResumeState;
            var model = downloadItems.Find(item);
            model?.Refresh();
        }

        foreach (var item in toStart){
            var model = downloadItems.Find(item);
            if (model != null){
                _ = model.StartDownloadCore();
            } else{
                ReleaseDownloadSlot(item);
            }
        }

        OnQueueStateChanged();
    }

    public void BlockAutoDownloadUntil(TimeSpan delay, CancellationToken cancellationToken = default){
        DateTimeOffset unblockAt = DateTimeOffset.UtcNow.Add(delay);

        lock (autoDownloadBlockLock){
            if (!autoDownloadBlockedUntilUtc.HasValue || unblockAt > autoDownloadBlockedUntilUtc.Value){
                autoDownloadBlockedUntilUtc = unblockAt;
            } else{
                unblockAt = autoDownloadBlockedUntilUtc.Value;
            }
        }

        _ = Task.Run(async () => {
            try{
                var remaining = unblockAt - DateTimeOffset.UtcNow;
                if (remaining > TimeSpan.Zero){
                    await Task.Delay(remaining, cancellationToken);
                }

                lock (autoDownloadBlockLock){
                    if (autoDownloadBlockedUntilUtc.HasValue && autoDownloadBlockedUntilUtc.Value <= DateTimeOffset.UtcNow){
                        autoDownloadBlockedUntilUtc = null;
                    }
                }

                RefreshQueue();
                UpdateDownloadListItems();
            } catch (OperationCanceledException){
                // ignored
            }
        }, cancellationToken);
    }

    public void ScheduleRetry(CrunchyEpMeta item, TimeSpan delay, string statusText, CancellationToken cancellationToken = default){
        item.DownloadProgress.ScheduleRetry(delay, statusText);
        RefreshQueue();
        OnQueueStateChanged();

        ScheduleRetryWake(item, item.DownloadProgress.RetryAtUtc, cancellationToken);
    }

    private void RestoreRetryStateFromQueue(){
        var retryItems = queue
            .Where(item => item.DownloadProgress.IsWaitingForRetry)
            .ToList();

        if (retryItems.Count == 0){
            lock (autoDownloadBlockLock){
                autoDownloadBlockedUntilUtc = null;
            }

            return;
        }

        var maxRetryAt = retryItems
            .Select(item => item.DownloadProgress.RetryAtUtc)
            .OfType<DateTimeOffset>()
            .Max();

        lock (autoDownloadBlockLock){
            autoDownloadBlockedUntilUtc = maxRetryAt;
        }

        foreach (var retryItem in retryItems){
            ScheduleRetryWake(retryItem, retryItem.DownloadProgress.RetryAtUtc);
        }
    }

    private bool HasPendingRetryItems(){
        return queue.Any(item => item.DownloadProgress.IsWaitingForRetry);
    }

    private void ScheduleRetryWake(CrunchyEpMeta item, DateTimeOffset? retryAtUtc, CancellationToken cancellationToken = default){
        if (!retryAtUtc.HasValue){
            return;
        }

        _ = Task.Run(async () => {
            try{
                var remaining = retryAtUtc.Value - DateTimeOffset.UtcNow;
                if (remaining > TimeSpan.Zero){
                    await Task.Delay(remaining, cancellationToken);
                }

                if (cancellationToken.IsCancellationRequested){
                    return;
                }

                item.DownloadProgress.RetryAtUtc = null;
                RefreshQueue();
                UpdateDownloadListItems();
            } catch (OperationCanceledException){
                // ignored
            }
        }, cancellationToken);
    }


    private void OnQueueStateChanged(){
        QueueStateChanged?.Invoke(this, EventArgs.Empty);
    }
    
    private void NotifyDownloadStateChanged(){
        OnPropertyChanged(nameof(ActiveDownloads));
        OnPropertyChanged(nameof(HasActiveDownloads));
    }
    
}
