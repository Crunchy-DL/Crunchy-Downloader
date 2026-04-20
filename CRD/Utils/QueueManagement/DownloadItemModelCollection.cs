using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CRD.Utils.Structs;
using CRD.ViewModels;

namespace CRD.Utils.QueueManagement;

public sealed class DownloadItemModelCollection{
    private readonly ObservableCollection<DownloadItemModel> items = new();
    private readonly Dictionary<CrunchyEpMeta, DownloadItemModel> models = new();

    public ObservableCollection<DownloadItemModel> Items => items;

    public DownloadItemModel? Find(CrunchyEpMeta item){
        return models.TryGetValue(item, out var model)
            ? model
            : null;
    }

    public void Remove(CrunchyEpMeta item){
        if (models.Remove(item, out var model)){
            items.Remove(model);
        } else{
            Console.Error.WriteLine("Failed to remove episode from list");
        }
    }

    public void Clear(){
        models.Clear();
        items.Clear();
    }

    public void SyncFromQueue(IEnumerable<CrunchyEpMeta> queueItems){
        foreach (var queueItem in queueItems){
            if (models.TryGetValue(queueItem, out var existingModel)){
                existingModel.Refresh();
                continue;
            }

            var newModel = new DownloadItemModel(queueItem);
            models.Add(queueItem, newModel);
            items.Add(newModel);

            _ = newModel.LoadImage();
        }
    }
}