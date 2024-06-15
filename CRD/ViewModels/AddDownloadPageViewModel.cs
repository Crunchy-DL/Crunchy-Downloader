using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CRD.Downloader;
using CRD.Utils;
using CRD.Utils.Structs;
using CRD.Views;
using ReactiveUI;


namespace CRD.ViewModels;

public partial class AddDownloadPageViewModel : ViewModelBase{
    [ObservableProperty] public string _urlInput = "";
    [ObservableProperty] public string _buttonText = "Enter Url";
    [ObservableProperty] public bool _addAllEpisodes = false;

    [ObservableProperty] public bool _buttonEnabled = false;
    [ObservableProperty] public bool _allButtonEnabled = false;
    [ObservableProperty] public bool _showLoading = false;
    public ObservableCollection<ItemModel> Items{ get; } = new();
    public ObservableCollection<ItemModel> SelectedItems{ get; } = new();

    [ObservableProperty] public ComboBoxItem _currentSelectedSeason;
    public ObservableCollection<ComboBoxItem> SeasonList{ get; } = new();

    private Dictionary<string, List<ItemModel>> episodesBySeason = new();

    private List<string> selectedEpisodes = new();

    private CrunchySeriesList? currentSeriesList;

    public AddDownloadPageViewModel(){
        SelectedItems.CollectionChanged += OnSelectedItemsChanged;
    }


    partial void OnUrlInputChanged(string value){
        if (UrlInput.Length > 9){
            if (UrlInput.Contains("/watch/concert/") || UrlInput.Contains("/artist/")){
                MessageBus.Current.SendMessage(new ToastMessage("Concerts / Artists not implemented yet", ToastType.Error, 1));
            } else if (UrlInput.Contains("/watch/")){
                //Episode
                ButtonText = "Add Episode to Queue";
                ButtonEnabled = true;
            } else if (UrlInput.Contains("/series/")){
                //Series
                ButtonText = "List Episodes";
                ButtonEnabled = true;
            } else{
                ButtonEnabled = false;
            }
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
                Crunchyroll.Instance.AddSeriesToQueue(currentSeriesList.Value, new CrunchyMultiDownload(Crunchyroll.Instance.CrunOptions.DubLang, AddAllEpisodes, false, selectedEpisodes));
                
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
                    var id = match.Groups[2].Value;     // Capture the ID part
                    Crunchyroll.Instance.AddEpisodeToQue(id, locale, Crunchyroll.Instance.CrunOptions.DubLang);
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
                    var id = match.Groups[2].Value;     // Capture the ID part

                    if (id.Length != 9){
                        return;
                    }

                    ButtonEnabled = false;
                    ShowLoading = true;
                    var list = await Crunchyroll.Instance.CrSeries.ListSeriesId(id,"", new CrunchyMultiDownload(Crunchyroll.Instance.CrunOptions.DubLang, true));
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
                        ButtonText = "Select Episodes";
                    } else{
                        ButtonEnabled = true;
                    }
                }
            }
        } else{
            Console.WriteLine("Unnkown input");
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

    async partial void OnCurrentSelectedSeasonChanged(ComboBoxItem? value){
        if (value == null){
            return;
        }

        string key = value.Content + "";
        Items.Clear();
        if (episodesBySeason.TryGetValue(key, out var season)){
            foreach (var episode in season){
                if (episode.ImageBitmap == null){
                    await episode.LoadImage();
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
    }
}

public class ItemModel(string imageUrl, string description, string time, string title, string season, string episode, string absolutNum, List<string> availableAudios){
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

    public async Task LoadImage(){
        try{
            using (var client = new HttpClient()){
                var response = await client.GetAsync(ImageUrl);
                response.EnsureSuccessStatusCode();
                using (var stream = await response.Content.ReadAsStreamAsync()){
                    ImageBitmap = new Bitmap(stream);
                }
            }
        } catch (Exception ex){
            // Handle exceptions
            Console.WriteLine("Failed to load image: " + ex.Message);
        }
    }
}