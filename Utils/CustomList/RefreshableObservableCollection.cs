using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace CRD.Utils.CustomList;

public class RefreshableObservableCollection<T> : ObservableCollection<T>{
    public void Refresh(){
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}