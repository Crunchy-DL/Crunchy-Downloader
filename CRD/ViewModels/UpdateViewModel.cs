using System;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CRD.Downloader;
using CRD.Utils.Updater;
using Markdig;

namespace CRD.ViewModels;

public partial class UpdateViewModel : ViewModelBase{

    [ObservableProperty]
    private bool _updateAvailable;

    [ObservableProperty]
    private bool _updating;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private bool _failed;
    
    private AccountPageViewModel accountPageViewModel;

    [ObservableProperty]
    private string _changelogText = "<p><strong>No changelog found.</strong></p>";
    
    [ObservableProperty]
    private string _currentVersion;

    public UpdateViewModel(){
        
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        _currentVersion = $"{version?.Major}.{version?.Minor}.{version?.Build}";
        
        LoadChangelog();
      
        UpdateAvailable = ProgramManager.Instance.UpdateAvailable;
        
        Updater.Instance.PropertyChanged += Progress_PropertyChanged;
    }

    [RelayCommand]
    public void StartUpdate(){
        Updating = true;
        ProgramManager.Instance.NavigationLock = true;
        // Title = "Updating";
        _ = Updater.Instance.DownloadAndUpdateAsync();
    }

    private void Progress_PropertyChanged(object? sender, PropertyChangedEventArgs e){
        if (e.PropertyName == nameof(Updater.Instance.progress)){
            Progress = Updater.Instance.progress;
        } else if (e.PropertyName == nameof(Updater.Instance.failed)){
            Failed = Updater.Instance.failed;
            ProgramManager.Instance.NavigationLock = !Failed;
        }
    }

    private void LoadChangelog(){
        string changelogPath = "CHANGELOG.md";

        if (!File.Exists(changelogPath)){
            ChangelogText = "<p><strong>No changelog found.</strong></p>";
            return;
        }

        string markdownText = File.ReadAllText(changelogPath);

        markdownText = PreprocessMarkdown(markdownText);

        var pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UseSoftlineBreakAsHardlineBreak()
            .Build();

        string htmlContent = Markdown.ToHtml(markdownText, pipeline);

        htmlContent = MakeIssueLinksClickable(htmlContent);
        htmlContent = ModifyImages(htmlContent);
        
        Color themeTextColor = Application.Current?.RequestedThemeVariant == ThemeVariant.Dark ? Colors.White : Color.Parse("#E4000000");
        string cssColor = $"#{themeTextColor.R:X2}{themeTextColor.G:X2}{themeTextColor.B:X2}";

        string styledHtml = $@"
    <html>
    <head>
    <style type=""text/css"">
        body {{
            color: {cssColor};
            background: transparent;
            font-family: Arial, sans-serif;
        }}
        img {{
            max-width: 100%;
            height: auto;
            display: block;
            margin: 10px auto;
            max-height: 300px;
            object-fit: contain;
        }}
        li {{
            margin-bottom: 10px; 
            line-height: 1.6;
        }}
        code {{
            background: #f0f0f0;
            font-family: Consolas, Monaco, 'Courier New', monospace;
            white-space: nowrap;
            vertical-align: middle; 
            display: inline-block;
        }}
        pre code {{
            background: #f5f5f5;
            display: block;
            padding: 10px;
            border-radius: 5px;
            white-space: pre-wrap;
            word-wrap: break-word;
            font-family: Consolas, Monaco, 'Courier New', monospace;
        }}
    </style>
    </head>
    <body>
        {htmlContent}
    </body>
    </html>";

        ChangelogText = styledHtml;
    }

    private string MakeIssueLinksClickable(string htmlContent){
        // Match GitHub issue links
        string issuePattern = @"<a href=['""](https:\/\/github\.com\/Crunchy-DL\/Crunchy-Downloader\/issues\/(\d+))['""][^>]*>[^<]+<\/a>";

        // Match GitHub discussion links
        string discussionPattern = @"<a href=['""](https:\/\/github\.com\/Crunchy-DL\/Crunchy-Downloader\/discussions\/(\d+))['""][^>]*>[^<]+<\/a>";

        htmlContent = Regex.Replace(htmlContent, issuePattern, match => {
            string fullUrl = match.Groups[1].Value;
            string issueNumber = match.Groups[2].Value;
            return $"<a href='{fullUrl}' target='_blank'>#{issueNumber}</a>";
        });

        htmlContent = Regex.Replace(htmlContent, discussionPattern, match => {
            string fullUrl = match.Groups[1].Value;
            string discussionNumber = match.Groups[2].Value;
            return $"<a href='{fullUrl}' target='_blank'>#{discussionNumber}</a>";
        });

        return htmlContent;
    }


    private string ModifyImages(string htmlContent){
        // Regex to match <img> tags
        string imgPattern = @"<img\s+src=['""]([^'""]+)['""]( alt=['""]([^'""]+)['""])?\s*\/?>";

        return Regex.Replace(htmlContent, imgPattern, match => {
            string imgUrl = match.Groups[1].Value;
            string altText = "View Image"; // match.Groups[3].Success ? match.Groups[3].Value : "View Image";

            return $"<a href='{imgUrl}' target='_blank'>{altText}</a>";
        });
    }


    private string PreprocessMarkdown(string markdownText){
        // Regex to match <details> blocks containing an image
        string detailsPattern = @"<details>\s*<summary>.*?<\/summary>\s*<img\s+src=['""]([^'""]+)['""]\s+alt=['""]([^'""]+)['""]\s*\/?>\s*<\/details>";

        return Regex.Replace(markdownText, detailsPattern, match => {
            string imageUrl = match.Groups[1].Value;
            string altText = match.Groups[2].Value;

            return $"![{altText}]({imageUrl})";
        });
    }
    
}