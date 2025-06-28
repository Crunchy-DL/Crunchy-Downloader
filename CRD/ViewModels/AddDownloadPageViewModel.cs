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
    private bool _addAllEpisodes;

    [ObservableProperty]
    private bool _buttonEnabled;

    [ObservableProperty]
    private bool _allButtonEnabled;

    [ObservableProperty]
    private bool _showLoading;

    [ObservableProperty]
    private bool _searchEnabled;

    [ObservableProperty]
    private bool _searchVisible = true;

    [ObservableProperty]
    private bool _slectSeasonVisible;

    [ObservableProperty]
    private bool _searchPopupVisible;

    public ObservableCollection<ItemModel> Items{ get; set; } = new();
    public ObservableCollection<CrBrowseSeries> SearchItems{ get; set; } = new();
    public ObservableCollection<ItemModel> SelectedItems{ get; set; } = new();

    [ObservableProperty]
    public CrBrowseSeries _selectedSearchItem;

    [ObservableProperty]
    public ComboBoxItem _currentSelectedSeason;

    public ObservableCollection<ComboBoxItem> SeasonList{ get; set; } = new();

    private Dictionary<string, List<ItemModel>> episodesBySeason = new();

    private List<ItemModel> selectedEpisodes = new();

    private CrunchySeriesList? currentSeriesList;

    private CrunchyMusicVideoList? currentMusicVideoList;

    private bool CurrentSeasonFullySelected;

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
                        var imageUrl = posterTall.Find(ele => ele.Height == 180)?.Source
                                       ?? (posterTall.Count >= 2 ? posterTall[1].Source : posterTall.FirstOrDefault()?.Source);
                        episode.LoadImage(imageUrl ?? string.Empty);
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
    public async Task OnButtonPress(){
        if (HasSelectedItemsOrEpisodes()){
            Console.WriteLine("Added to Queue");

            if (currentMusicVideoList != null){
                AddSelectedMusicVideosToQueue();
            }

            if (currentSeriesList != null){
                await AddSelectedEpisodesToQueue();
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
        AddItemsToSelectedEpisodes();

        if (selectedEpisodes.Count > 0){
            var musicClass = CrunchyrollManager.Instance.CrMusic;
            foreach (var selectedItem in selectedEpisodes){
                var music = currentMusicVideoList?.Data?.FirstOrDefault(ele => ele.Id == selectedItem.Id);

                if (music != null){
                    var meta = musicClass.EpisodeMeta(music);
                    QueueManager.Instance.CrAddMusicMetaToQueue(meta);
                }
            }
        } else if (AddAllEpisodes){
            var musicClass = CrunchyrollManager.Instance.CrMusic;
            foreach (var meta in currentMusicVideoList.Data.Select(crunchyMusicVideo => musicClass.EpisodeMeta(crunchyMusicVideo))){
                QueueManager.Instance.CrAddMusicMetaToQueue(meta);
            }
        }
    }

    private async Task AddSelectedEpisodesToQueue(){
        AddItemsToSelectedEpisodes();

        if (currentSeriesList != null){
            await QueueManager.Instance.CrAddSeriesToQueue(
                currentSeriesList,
                new CrunchyMultiDownload(
                    CrunchyrollManager.Instance.CrunOptions.DubLang,
                    AddAllEpisodes,
                    false,
                    selectedEpisodes.Select(selectedEpisode => selectedEpisode.AbsolutNum).ToList()));
        }
    }

    private void AddItemsToSelectedEpisodes(){
        foreach (var selectedItem in SelectedItems){
            if (!selectedEpisodes.Contains(selectedItem)){
                selectedEpisodes.Add(selectedItem);
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

        if (matchResult is ({ } locale, { } id)){
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
        var match = Regex.Match(UrlInput, @"^(?:https?:\/\/[^/]+)?(?:\/([a-z]{2}))?\/(?:[^/]+\/)?(artist|watch|series)(?:\/(musicvideo|concert))?\/([^/]+)(?:\/[^/]*)?$");

        return match.Success
            ? (match.Groups[1].Value ?? "", match.Groups[4].Value)
            : null;
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

        var list = await CrunchyrollManager.Instance.CrMusic.ParseArtistVideosByIdAsync(
            id, DetermineLocale(locale), true, true);

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

        if (CrunchyrollManager.Instance.CrunOptions.SearchFetchFeaturedMusic){
            var musicList = await CrunchyrollManager.Instance.CrMusic.ParseFeaturedMusicVideoByIdAsync(id, DetermineLocale(locale), true);

            if (musicList != null){
                currentMusicVideoList = musicList;
                PopulateItemsFromMusicVideoList();
            }
        }

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
        if (currentMusicVideoList?.Data is{ Count: > 0 }){
            foreach (var episode in currentMusicVideoList.Data){
                string seasonKey;
                switch (episode.EpisodeType){
                    case EpisodeType.MusicVideo:
                        seasonKey = "Music Videos ";
                        break;
                    case EpisodeType.Concert:
                        seasonKey = "Concerts ";
                        break;
                    case EpisodeType.Episode:
                    case EpisodeType.Unknown:
                    default:
                        seasonKey = "Unknown ";
                        break;
                }

                var imageUrl = episode.Images?.Thumbnail.FirstOrDefault()?.Source ?? "";
                var time = $"{(episode.DurationMs / 1000) / 60}:{(episode.DurationMs / 1000) % 60:D2}";

                var newItem = new ItemModel(episode.Id, imageUrl, episode.Description ?? "", time, episode.Title ?? "", seasonKey,
                    episode.SequenceNumber.ToString(), episode.Id, new List<string>(), episode.EpisodeType);

                if (!episodesBySeason.ContainsKey(seasonKey)){
                    episodesBySeason[seasonKey] = new List<ItemModel>{ newItem };
                    SeasonList.Add(new ComboBoxItem{ Content = seasonKey });
                } else{
                    episodesBySeason[seasonKey].Add(newItem);
                }
            }

            if (SeasonList.Count > 0){
                CurrentSelectedSeason = SeasonList.First();
            }
        }
    }

    private void PopulateEpisodesBySeason(){
        foreach (var episode in currentSeriesList?.List ?? Enumerable.Empty<Episode>()){
            var seasonKey = "S" + episode.Season;
            var itemModel = new ItemModel(
                episode.Id, episode.Img, episode.Description, episode.Time, episode.Name, seasonKey,
                episode.EpisodeNum.StartsWith("SP") ? episode.EpisodeNum : "E" + episode.EpisodeNum,
                episode.E, episode.Lang, episode.EpisodeType);

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
                selectedEpisodes.Remove(item);
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
            if (!selectedEpisodes.Contains(selectedItem)){
                selectedEpisodes.Add(selectedItem);
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


    #region SearchItemSelection

    async partial void OnSelectedSearchItemChanged(CrBrowseSeries? value){
        if (value is null || string.IsNullOrEmpty(value.Id)){
            return;
        }

        UpdateUiForSearchSelection();

        var list = await FetchSeriesListAsync(value.Id);

        if (list != null){
            currentSeriesList = list;
            await SearchPopulateEpisodesBySeason(value.Id);
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

    private async Task SearchPopulateEpisodesBySeason(string seriesId){
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

        foreach (var episode in currentSeriesList.List){
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
                episode.Lang, episode.EpisodeType);

            if (!episodesBySeason.ContainsKey(seasonKey)){
                episodesBySeason[seasonKey] = new List<ItemModel>{ episodeModel };
                SeasonList.Add(new ComboBoxItem{ Content = seasonKey });
            } else{
                episodesBySeason[seasonKey].Add(episodeModel);
            }
        }

        if (CrunchyrollManager.Instance.CrunOptions.SearchFetchFeaturedMusic){
            var locale = string.IsNullOrEmpty(CrunchyrollManager.Instance.CrunOptions.HistoryLang)
                ? CrunchyrollManager.Instance.DefaultLocale
                : CrunchyrollManager.Instance.CrunOptions.HistoryLang;
            var musicList = await CrunchyrollManager.Instance.CrMusic.ParseFeaturedMusicVideoByIdAsync(seriesId, DetermineLocale(locale), true);

            if (musicList != null){
                currentMusicVideoList = musicList;
                PopulateItemsFromMusicVideoList();
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
                    if (selectedEpisodes.Contains(episode)){
                        SelectedItems.Add(episode);
                    }
                } else{
                    Items.Add(episode);
                    if (selectedEpisodes.Contains(episode)){
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
        SearchItems.Clear();
        SelectedItems.Clear();
        SeasonList.Clear();
        episodesBySeason.Clear();
        selectedEpisodes.Clear();
    }
}

public class ItemModel(string id, string imageUrl, string description, string time, string title, string season, string episode, string absolutNum, List<string> availableAudios, EpisodeType epType)
    : INotifyPropertyChanged{
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
    public EpisodeType EpisodeType{ get; set; } = epType;

    public bool HasDubs{ get; set; } = availableAudios.Count != 0;

    public event PropertyChangedEventHandler? PropertyChanged;

    public async void LoadImage(string url){
        ImageBitmap = await Helpers.LoadImage(url, 208, 117);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ImageBitmap)));
    }
}