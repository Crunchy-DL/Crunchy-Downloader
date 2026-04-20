using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CRD.Downloader;
using CRD.Downloader.Crunchyroll;
using CRD.Utils;
using CRD.Utils.Files;
using CRD.Utils.Structs;

namespace CRD.Utils.QueueManagement;

public sealed class QueuePersistenceManager : IDisposable{
    private readonly object syncLock = new();
    private readonly QueueManager queueManager;
    private Timer? saveTimer;

    public QueuePersistenceManager(QueueManager queueManager){
        this.queueManager = queueManager ?? throw new ArgumentNullException(nameof(queueManager));
        this.queueManager.QueueStateChanged += OnQueueStateChanged;
    }

    public void RestoreQueue(){
        var options = CrunchyrollManager.Instance.CrunOptions;
        if (!options.PersistQueue){
            CfgManager.DeleteFileIfExists(CfgManager.PathCrQueue);
            return;
        }

        if (!CfgManager.CheckIfFileExists(CfgManager.PathCrQueue))
            return;

        var savedQueue = CfgManager.ReadJsonFromFile<List<CrunchyEpMeta>>(CfgManager.PathCrQueue);
        if (savedQueue == null || savedQueue.Count == 0){
            CfgManager.DeleteFileIfExists(CfgManager.PathCrQueue);
            return;
        }

        queueManager.ReplaceQueue(savedQueue.Select(PrepareRestoredItem));
    }

    public void SaveNow(){
        lock (syncLock){
            saveTimer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }

        PersistQueueSnapshot();
    }

    public void ScheduleSave(){
        lock (syncLock){
            if (saveTimer == null){
                saveTimer = new Timer(_ => PersistQueueSnapshot(), null, TimeSpan.FromMilliseconds(750), Timeout.InfiniteTimeSpan);
                return;
            }

            saveTimer.Change(TimeSpan.FromMilliseconds(750), Timeout.InfiniteTimeSpan);
        }
    }

    private void OnQueueStateChanged(object? sender, EventArgs e){
        ScheduleSave();
    }

    private void PersistQueueSnapshot(){
        var options = CrunchyrollManager.Instance.CrunOptions;
        if (!options.PersistQueue){
            CfgManager.DeleteFileIfExists(CfgManager.PathCrQueue);
            return;
        }

        var queue = queueManager.Queue;
        if (queue.Count == 0){
            CfgManager.DeleteFileIfExists(CfgManager.PathCrQueue);
            return;
        }

        var snapshot = queue
            .Select(CloneForPersistence)
            .Where(item => item != null)
            .ToList();

        if (snapshot.Count == 0){
            CfgManager.DeleteFileIfExists(CfgManager.PathCrQueue);
            return;
        }

        CfgManager.WriteJsonToFile(CfgManager.PathCrQueue, snapshot);
    }

    private static CrunchyEpMeta PrepareRestoredItem(CrunchyEpMeta item){
        item.Data ??= [];
        item.DownloadSubs ??= [];
        item.downloadedFiles ??= [];
        item.DownloadProgress ??= new DownloadProgress();

        if (!item.DownloadProgress.IsFinished){
            item.DownloadProgress.ResetForRetry();
        }

        item.RenewCancellationToken();
        return item;
    }

    private static CrunchyEpMeta? CloneForPersistence(CrunchyEpMeta item){
        return Helpers.DeepCopy(item);
    }

    public void Dispose(){
        lock (syncLock){
            saveTimer?.Dispose();
            saveTimer = null;
        }

        queueManager.QueueStateChanged -= OnQueueStateChanged;
    }
}
