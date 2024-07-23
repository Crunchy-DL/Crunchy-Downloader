using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CRD.Downloader;
using CRD.Downloader.Crunchyroll;
using CRD.Utils;
using CRD.Utils.Structs;
using CRD.Views;
using DynamicData;
using FluentAvalonia.Core;
using ReactiveUI;


namespace CRD.ViewModels;

public partial class AddDownloadPageViewModel : ViewModelBase{
    [ObservableProperty]
    private string _urlInput = "";

    [ObservableProperty]
    private string _buttonText = "Enter Url";

    [ObservableProperty]
    private string _buttonTextSelectSeason = "Select Season";

    [ObservableProperty]
    private bool _addAllEpisodes = false;

    [ObservableProperty]
    private bool _buttonEnabled = false;

    [ObservableProperty]
    private bool _allButtonEnabled = false;

    [ObservableProperty]
    private bool _showLoading = false;

    [ObservableProperty]
    private bool _searchEnabled = false;

    [ObservableProperty]
    private bool _searchVisible = true;
    
    [ObservableProperty]
    private bool _slectSeasonVisible = false;

    [ObservableProperty]
    private bool _searchPopupVisible = false;

    public ObservableCollection<ItemModel> Items{ get; } = new();
    public ObservableCollection<CrBrowseSeries> SearchItems{ get; set; } = new();
    public ObservableCollection<ItemModel> SelectedItems{ get; } = new();

    [ObservableProperty]
    public CrBrowseSeries _selectedSearchItem;

    [ObservableProperty]
    public ComboBoxItem _currentSelectedSeason;

    public ObservableCollection<ComboBoxItem> SeasonList{ get; } = new();

    private Dictionary<string, List<ItemModel>> episodesBySeason = new();

    private List<string> selectedEpisodes = new();

    private CrunchySeriesList? currentSeriesList;

    private bool CurrentSeasonFullySelected = false;

    private readonly SemaphoreSlim _updateSearchSemaphore = new SemaphoreSlim(1, 1);

    public AddDownloadPageViewModel(){
        SelectedItems.CollectionChanged += OnSelectedItemsChanged;
    }

    private async Task UpdateSearch(string value){
        var searchResults = await CrunchyrollManager.Instance.CrSeries.Search(value, CrunchyrollManager.Instance.CrunOptions.HistoryLang);

        var searchItems = searchResults?.Data?.First().Items;
        SearchItems.Clear();
        if (searchItems is{ Count: > 0 }){
            foreach (var episode in searchItems){
                if (episode.ImageBitmap == null){
                    if (episode.Images.PosterTall != null){
                        var posterTall = episode.Images.PosterTall.First();
                        var imageUrl = posterTall.Find(ele => ele.Height == 180).Source
                                       ?? (posterTall.Count >= 2 ? posterTall[1].Source : posterTall.FirstOrDefault().Source);
                        episode.LoadImage(imageUrl);
                    }
                }

                SearchItems.Add(episode);
            }

            SearchPopupVisible = true;
            RaisePropertyChanged(nameof(SearchItems));
            RaisePropertyChanged(nameof(SearchVisible));
            return;
        }

        SearchPopupVisible = false;
        RaisePropertyChanged(nameof(SearchVisible));
        SearchItems.Clear();
    }

    partial void OnUrlInputChanged(string value){
        if (SearchEnabled){
            UpdateSearch(value);
            ButtonText = "Select Searched Series";
            ButtonEnabled = false;
        } else if (UrlInput.Length > 9){
            if (UrlInput.Contains("/watch/concert/") || UrlInput.Contains("/artist/")){
                MessageBus.Current.SendMessage(new ToastMessage("Concerts / Artists not implemented yet", ToastType.Error, 1));
            } else if (UrlInput.Contains("/watch/")){
                //Episode
                ButtonText = "Add Episode to Queue";
                ButtonEnabled = true;
                SearchVisible = false;
                SlectSeasonVisible = false;
            } else if (UrlInput.Contains("/series/")){
                //Series
                ButtonText = "List Episodes";
                ButtonEnabled = true;
                SearchVisible = false;
                SlectSeasonVisible = false;
            } else{
                ButtonEnabled = false;
                SearchVisible = true;
                SlectSeasonVisible = false;
            }
        } else{
            ButtonText = "Enter Url";
            ButtonEnabled = false;
            SearchVisible = true;
            SlectSeasonVisible = false;
        }
    }

    partial void OnSearchEnabledChanged(bool value){
        if (SearchEnabled){
            ButtonText = "Select Searched Series";
            ButtonEnabled = false;
        } else{
            ButtonText = "Enter Url";
            ButtonEnabled = false;
        }
    }

    [RelayCommand]
    public async void OnButtonPress(){
        if ((selectedEpisodes.Count > 0 || SelectedItems.Count > 0 || AddAllEpisodes)){
            Console.WriteLine("Added to Queue");

            if (SelectedItems.Count > 0){
                foreach (var selectedItem in SelectedItems){
                    if (!selectedEpisodes.Contains(selectedItem.AbsolutNum)){
                        selectedEpisodes.Add(selectedItem.AbsolutNum);
                    }
                }
            }

            if (currentSeriesList != null){
                await QueueManager.Instance.CRAddSeriesToQueue(currentSeriesList.Value, new CrunchyMultiDownload(CrunchyrollManager.Instance.CrunOptions.DubLang, AddAllEpisodes, false, selectedEpisodes));
            }


            UrlInput = "";
            selectedEpisodes.Clear();
            SelectedItems.Clear();
            Items.Clear();
            currentSeriesList = null;
            SeasonList.Clear();
            episodesBySeason.Clear();
            AllButtonEnabled = false;
            AddAllEpisodes = false;
            ButtonText = "Enter Url";
            ButtonEnabled = false;
            SearchVisible = true;
            SlectSeasonVisible = false;
        } else if (UrlInput.Length > 9){
            episodesBySeason.Clear();
            SeasonList.Clear();
            if (UrlInput.Contains("/watch/concert/") || UrlInput.Contains("/artist/")){
                MessageBus.Current.SendMessage(new ToastMessage("Concerts / Artists not implemented yet", ToastType.Error, 1));
            } else if (UrlInput.Contains("/watch/")){
                //Episode

                var match = Regex.Match(UrlInput, "/([^/]+)/watch/([^/]+)");

                if (match.Success){
                    var locale = match.Groups[1].Value; // Capture the locale part
                    var id = match.Groups[2].Value; // Capture the ID part
                    QueueManager.Instance.CRAddEpisodeToQue(id,
                        string.IsNullOrEmpty(locale)
                            ? string.IsNullOrEmpty(CrunchyrollManager.Instance.CrunOptions.HistoryLang) ? CrunchyrollManager.Instance.DefaultLocale : CrunchyrollManager.Instance.CrunOptions.HistoryLang
                            : Languages.Locale2language(locale).CrLocale, CrunchyrollManager.Instance.CrunOptions.DubLang, true);
                    UrlInput = "";
                    selectedEpisodes.Clear();
                    SelectedItems.Clear();
                    Items.Clear();
                    currentSeriesList = null;
                    SeasonList.Clear();
                    episodesBySeason.Clear();
                }
            } else if (UrlInput.Contains("/series/")){
                //Series
                var match = Regex.Match(UrlInput, "/([^/]+)/series/([^/]+)");

                if (match.Success){
                    var locale = match.Groups[1].Value; // Capture the locale part
                    var id = match.Groups[2].Value; // Capture the ID part

                    if (id.Length != 9){
                        return;
                    }

                    ButtonEnabled = false;
                    ShowLoading = true;
                    var list = await CrunchyrollManager.Instance.CrSeries.ListSeriesId(id, string.IsNullOrEmpty(locale)
                        ? string.IsNullOrEmpty(CrunchyrollManager.Instance.CrunOptions.HistoryLang) ? CrunchyrollManager.Instance.DefaultLocale : CrunchyrollManager.Instance.CrunOptions.HistoryLang
                        : Languages.Locale2language(locale).CrLocale, new CrunchyMultiDownload(CrunchyrollManager.Instance.CrunOptions.DubLang, true));
                    ShowLoading = false;
                    if (list != null){
                        currentSeriesList = list;
                        foreach (var episode in currentSeriesList.Value.List){
                            if (episodesBySeason.ContainsKey("S" + episode.Season)){
                                episodesBySeason["S" + episode.Season].Add(new ItemModel(episode.Img, episode.Description, episode.Time, episode.Name, "S" + episode.Season, "E" + episode.EpisodeNum, episode.E,
                                    episode.Lang));
                            } else{
                                episodesBySeason.Add("S" + episode.Season, new List<ItemModel>{
                                    new ItemModel(episode.Img, episode.Description, episode.Time, episode.Name, "S" + episode.Season, "E" + episode.EpisodeNum, episode.E, episode.Lang)
                                });
                                SeasonList.Add(new ComboBoxItem{ Content = "S" + episode.Season });
                            }
                        }

                        CurrentSelectedSeason = SeasonList[0];
                        ButtonEnabled = false;
                        AllButtonEnabled = true;
                        SlectSeasonVisible = true;
                        ButtonText = "Select Episodes";
                    } else{
                        ButtonEnabled = true;
                    }
                }
            }
        } else{
            Console.Error.WriteLine("Unnkown input");
        }
    }

    [RelayCommand]
    public void OnSelectSeasonPressed(){
        if (CurrentSeasonFullySelected){
            foreach (var item in Items){
                selectedEpisodes.Remove(item.AbsolutNum);
                SelectedItems.Remove(item);
            }

            ButtonTextSelectSeason = "Select Season";
        } else{
            var selectedItemsSet = new HashSet<ItemModel>(SelectedItems);

            foreach (var item in Items){
                if (selectedItemsSet.Add(item)){
                    SelectedItems.Add(item);
                }
            }

            ButtonTextSelectSeason = "Deselect Season";
        }
    }

    partial void OnCurrentSelectedSeasonChanging(ComboBoxItem? oldValue, ComboBoxItem newValue){
        foreach (var selectedItem in SelectedItems){
            if (!selectedEpisodes.Contains(selectedItem.AbsolutNum)){
                selectedEpisodes.Add(selectedItem.AbsolutNum);
            }
        }

        if (selectedEpisodes.Count > 0 || SelectedItems.Count > 0 || AddAllEpisodes){
            ButtonText = "Add Episodes to Queue";
            ButtonEnabled = true;
        } else{
            ButtonEnabled = false;
            ButtonText = "Select Episodes";
        }
    }

    private void OnSelectedItemsChanged(object? sender, NotifyCollectionChangedEventArgs e){
        CurrentSeasonFullySelected = Items.All(item => SelectedItems.Contains(item));

        if (CurrentSeasonFullySelected){
            ButtonTextSelectSeason = "Deselect Season";
        } else{
            ButtonTextSelectSeason = "Select Season";
        }

        if (selectedEpisodes.Count > 0 || SelectedItems.Count > 0 || AddAllEpisodes){
            ButtonText = "Add Episodes to Queue";
            ButtonEnabled = true;
        } else{
            ButtonEnabled = false;
            ButtonText = "Select Episodes";
        }
    }

    partial void OnAddAllEpisodesChanged(bool value){
        if ((selectedEpisodes.Count > 0 || SelectedItems.Count > 0 || AddAllEpisodes)){
            ButtonText = "Add Episodes to Queue";
            ButtonEnabled = true;
        } else{
            ButtonEnabled = false;
            ButtonText = "Select Episodes";
        }
    }

    async partial void OnSelectedSearchItemChanged(CrBrowseSeries? value){
        if (value == null || string.IsNullOrEmpty(value.Id)){
            return;
        }

        SearchPopupVisible = false;
        RaisePropertyChanged(nameof(SearchVisible));
        SearchItems.Clear();
        SearchVisible = false;
        SlectSeasonVisible = true;
        ButtonEnabled = false;
        ShowLoading = true;
        var list = await CrunchyrollManager.Instance.CrSeries.ListSeriesId(value.Id,
            string.IsNullOrEmpty(CrunchyrollManager.Instance.CrunOptions.HistoryLang) ? CrunchyrollManager.Instance.DefaultLocale : CrunchyrollManager.Instance.CrunOptions.HistoryLang,
            new CrunchyMultiDownload(CrunchyrollManager.Instance.CrunOptions.DubLang, true));
        ShowLoading = false;
        if (list != null){
            currentSeriesList = list;
            foreach (var episode in currentSeriesList.Value.List){
                if (episodesBySeason.ContainsKey("S" + episode.Season)){
                    episodesBySeason["S" + episode.Season].Add(new ItemModel(episode.Img, episode.Description, episode.Time, episode.Name, "S" + episode.Season, "E" + episode.EpisodeNum, episode.E,
                        episode.Lang));
                } else{
                    episodesBySeason.Add("S" + episode.Season, new List<ItemModel>{
                        new(episode.Img, episode.Description, episode.Time, episode.Name, "S" + episode.Season, "E" + episode.EpisodeNum, episode.E, episode.Lang)
                    });
                    SeasonList.Add(new ComboBoxItem{ Content = "S" + episode.Season });
                }
            }

            CurrentSelectedSeason = SeasonList[0];
            ButtonEnabled = false;
            AllButtonEnabled = true;
            ButtonText = "Select Episodes";
        } else{
            ButtonEnabled = true;
        }
    }

    partial void OnCurrentSelectedSeasonChanged(ComboBoxItem? value){
        if (value == null){
            return;
        }

        string key = value.Content + "";
        Items.Clear();
        if (episodesBySeason.TryGetValue(key, out var season)){
            foreach (var episode in season){
                if (episode.ImageBitmap == null){
                    episode.LoadImage(episode.ImageUrl);
                    Items.Add(episode);
                    if (selectedEpisodes.Contains(episode.AbsolutNum)){
                        SelectedItems.Add(episode);
                    }
                } else{
                    Items.Add(episode);
                    if (selectedEpisodes.Contains(episode.AbsolutNum)){
                        SelectedItems.Add(episode);
                    }
                }
            }
        }

        CurrentSeasonFullySelected = Items.All(item => SelectedItems.Contains(item));

        if (CurrentSeasonFullySelected){
            ButtonTextSelectSeason = "Deselect Season";
        } else{
            ButtonTextSelectSeason = "Select Season";
        }
    }
}

public class ItemModel(string imageUrl, string description, string time, string title, string season, string episode, string absolutNum, List<string> availableAudios) : INotifyPropertyChanged{
    public string ImageUrl{ get; set; } = imageUrl;
    public Bitmap? ImageBitmap{ get; set; }
    public string Title{ get; set; } = title;
    public string Description{ get; set; } = description;
    public string Time{ get; set; } = time;
    public string Season{ get; set; } = season;
    public string Episode{ get; set; } = episode;

    public string AbsolutNum{ get; set; } = absolutNum;

    public string TitleFull{ get; set; } = season + episode + " - " + title;

    public List<string> AvailableAudios{ get; set; } = availableAudios;

    public event PropertyChangedEventHandler? PropertyChanged;

    public async void LoadImage(string url){
        ImageBitmap = await Helpers.LoadImage(url);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ImageBitmap)));
    }
}