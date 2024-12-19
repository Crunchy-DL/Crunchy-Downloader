using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CRD.Downloader;
using CRD.Downloader.Crunchyroll;
using CRD.Utils;
using CRD.Utils.Structs;
using CRD.Utils.Structs.Crunchyroll.Music;

// ReSharper disable InconsistentNaming

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

    public ObservableCollection<ItemModel> Items{ get; set; } = new();
    public ObservableCollection<CrBrowseSeries> SearchItems{ get; set; } = new();
    public ObservableCollection<ItemModel> SelectedItems{ get; set; } = new();

    [ObservableProperty]
    public CrBrowseSeries _selectedSearchItem;

    [ObservableProperty]
    public ComboBoxItem _currentSelectedSeason;

    public ObservableCollection<ComboBoxItem> SeasonList{ get; set; } = new();

    private Dictionary<string, List<ItemModel>> episodesBySeason = new();

    private List<string> selectedEpisodes = new();

    private CrunchySeriesList? currentSeriesList;

    private CrunchyMusicVideoList? currentMusicVideoList;

    private bool CurrentSeasonFullySelected = false;

    public AddDownloadPageViewModel(){
        SelectedItems.CollectionChanged += OnSelectedItemsChanged;
    }

    private async Task UpdateSearch(string value){
        if (string.IsNullOrEmpty(value)){
            SearchPopupVisible = false;
            RaisePropertyChanged(nameof(SearchVisible));
            SearchItems.Clear();
            return;
        }

        var searchResults = await CrunchyrollManager.Instance.CrSeries.Search(value, CrunchyrollManager.Instance.CrunOptions.HistoryLang, true);

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

    #region UrlInput

    partial void OnUrlInputChanged(string value){
        if (SearchEnabled){
            _ = UpdateSearch(value);
            SetButtonProperties("Select Searched Series", false);
        } else if (UrlInput.Length > 9){
            EvaluateUrlInput();
        } else{
            SetButtonProperties("Enter Url", false);
            SetVisibility(true, false);
        }
    }

    private void EvaluateUrlInput(){
        var (buttonText, isButtonEnabled) = DetermineButtonTextAndState();

        SetButtonProperties(buttonText, isButtonEnabled);
        SetVisibility(false, false);
    }

    private (string, bool) DetermineButtonTextAndState(){
        return UrlInput switch{
            _ when UrlInput.Contains("/artist/") => ("List Episodes", true),
            _ when UrlInput.Contains("/watch/musicvideo/") => ("Add Music Video to Queue", true),
            _ when UrlInput.Contains("/watch/concert/") => ("Add Concert to Queue", true),
            _ when UrlInput.Contains("/watch/") => ("Add Episode to Queue", true),
            _ when UrlInput.Contains("/series/") => ("List Episodes", true),
            _ => ("Unknown", false),
        };
    }

    private void SetButtonProperties(string text, bool isEnabled){
        ButtonText = text;
        ButtonEnabled = isEnabled;
    }

    private void SetVisibility(bool isSearchVisible, bool isSelectSeasonVisible){
        SearchVisible = isSearchVisible;
        SlectSeasonVisible = isSelectSeasonVisible;
    }

    #endregion


    partial void OnSearchEnabledChanged(bool value){
        ButtonText = SearchEnabled ? "Select Searched Series" : "Enter Url";
        ButtonEnabled = false;
    }

    #region OnButtonPress

    [RelayCommand]
    public async void OnButtonPress(){
        if (HasSelectedItemsOrEpisodes()){
            Console.WriteLine("Added to Queue");

            if (currentMusicVideoList != null){
                AddSelectedMusicVideosToQueue();
            } else{
                AddSelectedEpisodesToQueue();
            }

            ResetState();
        } else if (UrlInput.Length > 9){
            await HandleUrlInputAsync();
        } else{
            Console.Error.WriteLine("Unknown input");
        }
    }

    private bool HasSelectedItemsOrEpisodes(){
        return selectedEpisodes.Count > 0 || SelectedItems.Count > 0 || AddAllEpisodes;
    }

    private void AddSelectedMusicVideosToQueue(){
        if (SelectedItems.Count > 0){
            var musicClass = CrunchyrollManager.Instance.CrMusic;
            foreach (var selectedItem in SelectedItems){
                var music = currentMusicVideoList.Value.Data?.FirstOrDefault(ele => ele.Id == selectedItem.Id);

                if (music != null){
                    var meta = musicClass.EpisodeMeta(music);
                    QueueManager.Instance.CrAddMusicMetaToQueue(meta);
                }
            }
        }
    }

    private async void AddSelectedEpisodesToQueue(){
        AddItemsToSelectedEpisodes();

        if (currentSeriesList != null){
            await QueueManager.Instance.CrAddSeriesToQueue(
                currentSeriesList.Value,
                new CrunchyMultiDownload(
                    CrunchyrollManager.Instance.CrunOptions.DubLang,
                    AddAllEpisodes,
                    false,
                    selectedEpisodes));
        }
    }

    private void AddItemsToSelectedEpisodes(){
        foreach (var selectedItem in SelectedItems){
            if (!selectedEpisodes.Contains(selectedItem.AbsolutNum)){
                selectedEpisodes.Add(selectedItem.AbsolutNum);
            }
        }
    }

    private void ResetState(){
        currentMusicVideoList = null;
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

        //TODO - find a better way to reduce ram usage
        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect();
    }

    private async Task HandleUrlInputAsync(){
        episodesBySeason.Clear();
        SeasonList.Clear();

        var matchResult = ExtractLocaleAndIdFromUrl();

        if (matchResult is (string locale, string id)){
            switch (GetUrlType()){
                case CrunchyUrlType.Artist:
                    await HandleArtistUrlAsync(locale, id);
                    break;
                case CrunchyUrlType.MusicVideo:
                    HandleMusicVideoUrl(id);
                    break;
                case CrunchyUrlType.Concert:
                    HandleConcertUrl(id);
                    break;
                case CrunchyUrlType.Episode:
                    HandleEpisodeUrl(locale, id);
                    break;
                case CrunchyUrlType.Series:
                    await HandleSeriesUrlAsync(locale, id);
                    break;
                default:
                    Console.Error.WriteLine("Unknown input");
                    break;
            }
        }
    }

    private (string locale, string id)? ExtractLocaleAndIdFromUrl(){
        var match = Regex.Match(UrlInput, "/([^/]+)/(?:artist|watch|series)(?:/(?:musicvideo|concert))?/([^/]+)/?");
        return match.Success ? (match.Groups[1].Value, match.Groups[2].Value) : null;
    }

    private CrunchyUrlType GetUrlType(){
        return UrlInput switch{
            _ when UrlInput.Contains("/artist/") => CrunchyUrlType.Artist,
            _ when UrlInput.Contains("/watch/musicvideo/") => CrunchyUrlType.MusicVideo,
            _ when UrlInput.Contains("/watch/concert/") => CrunchyUrlType.Concert,
            _ when UrlInput.Contains("/watch/") => CrunchyUrlType.Episode,
            _ when UrlInput.Contains("/series/") => CrunchyUrlType.Series,
            _ => CrunchyUrlType.Unknown,
        };
    }

    private async Task HandleArtistUrlAsync(string locale, string id){
        SetLoadingState(true);

        var list = await CrunchyrollManager.Instance.CrMusic.ParseArtistMusicVideosByIdAsync(
            id, DetermineLocale(locale), true);

        SetLoadingState(false);

        if (list != null){
            currentMusicVideoList = list;
            PopulateItemsFromMusicVideoList();
            UpdateUiForSelection();
        }
    }

    private void HandleMusicVideoUrl(string id){
        _ = QueueManager.Instance.CrAddMusicVideoToQueue(id);
        ResetState();
    }

    private void HandleConcertUrl(string id){
        _ = QueueManager.Instance.CrAddConcertToQueue(id);
        ResetState();
    }

    private void HandleEpisodeUrl(string locale, string id){
        _ = QueueManager.Instance.CrAddEpisodeToQueue(
            id, DetermineLocale(locale),
            CrunchyrollManager.Instance.CrunOptions.DubLang, true);
        ResetState();
    }

    private async Task HandleSeriesUrlAsync(string locale, string id){
        SetLoadingState(true);

        var list = await CrunchyrollManager.Instance.CrSeries.ListSeriesId(
            id, DetermineLocale(locale),
            new CrunchyMultiDownload(CrunchyrollManager.Instance.CrunOptions.DubLang, true), true);

        SetLoadingState(false);

        if (list != null){
            currentSeriesList = list;
            PopulateEpisodesBySeason();
            UpdateUiForSelection();
        } else{
            ButtonEnabled = true;
        }
    }

    private void PopulateItemsFromMusicVideoList(){
        if (currentMusicVideoList?.Data != null){
            foreach (var episode in currentMusicVideoList.Value.Data){
                var imageUrl = episode.Images?.Thumbnail?.FirstOrDefault().Source ?? "";
                var time = $"{(episode.DurationMs / 1000) / 60}:{(episode.DurationMs / 1000) % 60:D2}";

                var newItem = new ItemModel(episode.Id ?? "", imageUrl, episode.Description ?? "", time, episode.Title ?? "", "",
                    episode.SequenceNumber.ToString(), episode.SequenceNumber.ToString(), new List<string>());

                newItem.LoadImage(imageUrl);
                Items.Add(newItem);
            }
        }
    }

    private void PopulateEpisodesBySeason(){
        foreach (var episode in currentSeriesList?.List ?? Enumerable.Empty<Episode>()){
            var seasonKey = "S" + episode.Season;
            var itemModel = new ItemModel(
                episode.Id, episode.Img, episode.Description, episode.Time, episode.Name, seasonKey,
                episode.EpisodeNum.StartsWith("SP") ? episode.EpisodeNum : "E" + episode.EpisodeNum,
                episode.E, episode.Lang);

            if (!episodesBySeason.ContainsKey(seasonKey)){
                episodesBySeason[seasonKey] = new List<ItemModel>{ itemModel };
                SeasonList.Add(new ComboBoxItem{ Content = seasonKey });
            } else{
                episodesBySeason[seasonKey].Add(itemModel);
            }
        }

        CurrentSelectedSeason = SeasonList.First();
    }

    private string DetermineLocale(string locale){
        return string.IsNullOrEmpty(locale)
            ? (string.IsNullOrEmpty(CrunchyrollManager.Instance.CrunOptions.HistoryLang)
                ? CrunchyrollManager.Instance.DefaultLocale
                : CrunchyrollManager.Instance.CrunOptions.HistoryLang)
            : Languages.Locale2language(locale).CrLocale;
    }

    private void SetLoadingState(bool isLoading){
        ButtonEnabled = !isLoading;
        ShowLoading = isLoading;
    }

    private void UpdateUiForSelection(){
        ButtonEnabled = false;
        AllButtonEnabled = true;
        SlectSeasonVisible = false;
        ButtonText = "Select Episodes";
    }

    #endregion


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
        if (SelectedItems == null) return;
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
        if (Items == null) return;

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


    #region SearchItemSelection

    async partial void OnSelectedSearchItemChanged(CrBrowseSeries? value){
        if (value is null || string.IsNullOrEmpty(value.Id)){
            return;
        }

        UpdateUiForSearchSelection();

        var list = await FetchSeriesListAsync(value.Id);

        if (list != null){
            currentSeriesList = list;
            SearchPopulateEpisodesBySeason();
            UpdateUiForEpisodeSelection();
        } else{
            ButtonEnabled = true;
        }
    }

    private void UpdateUiForSearchSelection(){
        SearchPopupVisible = false;
        RaisePropertyChanged(nameof(SearchVisible));
        SearchItems.Clear();
        SearchVisible = false;
        SlectSeasonVisible = true;
        ButtonEnabled = false;
        ShowLoading = true;
    }

    private async Task<CrunchySeriesList?> FetchSeriesListAsync(string seriesId){
        var locale = string.IsNullOrEmpty(CrunchyrollManager.Instance.CrunOptions.HistoryLang)
            ? CrunchyrollManager.Instance.DefaultLocale
            : CrunchyrollManager.Instance.CrunOptions.HistoryLang;

        return await CrunchyrollManager.Instance.CrSeries.ListSeriesId(
            seriesId,
            locale,
            new CrunchyMultiDownload(CrunchyrollManager.Instance.CrunOptions.DubLang, true), true);
    }

    private void SearchPopulateEpisodesBySeason(){
        if (currentSeriesList?.List == null){
            return;
        }

        Items.Clear();
        SelectedItems.Clear();
        episodesBySeason.Clear();
        SeasonList.Clear();

        //TODO - find a better way to reduce ram usage
        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect();


        foreach (var episode in currentSeriesList.Value.List){
            var seasonKey = "S" + episode.Season;
            var episodeModel = new ItemModel(
                episode.Id,
                episode.Img,
                episode.Description,
                episode.Time,
                episode.Name,
                seasonKey,
                episode.EpisodeNum.StartsWith("SP") ? episode.EpisodeNum : "E" + episode.EpisodeNum,
                episode.E,
                episode.Lang);

            if (!episodesBySeason.ContainsKey(seasonKey)){
                episodesBySeason[seasonKey] = new List<ItemModel>{ episodeModel };
                SeasonList.Add(new ComboBoxItem{ Content = seasonKey });
            } else{
                episodesBySeason[seasonKey].Add(episodeModel);
            }
        }

        CurrentSelectedSeason = SeasonList.First();
    }

    private void UpdateUiForEpisodeSelection(){
        ShowLoading = false;
        ButtonEnabled = false;
        AllButtonEnabled = true;
        ButtonText = "Select Episodes";
    }

    #endregion


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

    public void Dispose(){
        foreach (var itemModel in Items){
            itemModel.ImageBitmap?.Dispose(); // Dispose the bitmap if it exists
            itemModel.ImageBitmap = null; // Nullify the reference to avoid lingering references
        }

        // Clear collections and other managed resources
        Items.Clear();
        Items = null;
        SearchItems.Clear();
        SearchItems = null;
        SelectedItems.Clear();
        SelectedItems = null;
        SeasonList.Clear();
        SeasonList = null;
        episodesBySeason.Clear();
        episodesBySeason = null;
        selectedEpisodes.Clear();
        selectedEpisodes = null;
    }
}

public class ItemModel(string id, string imageUrl, string description, string time, string title, string season, string episode, string absolutNum, List<string> availableAudios) : INotifyPropertyChanged{
    public string Id{ get; set; } = id;
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
        ImageBitmap = await Helpers.LoadImage(url, 208, 117);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ImageBitmap)));
    }
}