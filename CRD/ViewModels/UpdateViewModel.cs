using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CRD.Downloader;
using CRD.Downloader.Crunchyroll;
using CRD.Utils.Updater;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Inline = Markdig.Syntax.Inlines.Inline;

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
    private string _currentVersion;

    public ObservableCollection<Control> ChangelogBlocks{ get; } = new();

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

    #region Changelog Builder

    private int textSize = 16;
    
    public void LoadChangelog(){
        string changelogPath = "CHANGELOG.md";

        if (!File.Exists(changelogPath)){
            ChangelogBlocks.Clear();
            ChangelogBlocks.Add(new TextBlock{
                Text = "No changelog found",
                FontSize = 16,
                TextWrapping = TextWrapping.Wrap
            });
            return;
        }

        string markdownText = File.ReadAllText(changelogPath);

        markdownText = PreprocessMarkdown(markdownText);

        var pipeline = new MarkdownPipelineBuilder().Build();
        var document = Markdown.Parse(markdownText, pipeline);

        ChangelogBlocks.Clear();

        try{
            foreach (var block in document){
                switch (block){
                    case HeadingBlock heading:
                        string headingText = string.Concat(heading.Inline.Select(i => i.ToString()));
                        ChangelogBlocks.Add(new TextBlock{
                            Text = headingText,
                            FontSize = heading.Level switch{ 1 => textSize + 10, 2 => textSize + 6, _ => textSize + 4 },
                            FontWeight = FontWeight.Bold,
                            Margin = new Thickness(0, 20, 0, 5),
                            TextWrapping = TextWrapping.Wrap
                        });
                        break;

                    case ParagraphBlock paragraph:
                        var inlineControls = BuildInlineControls(paragraph.Inline?.FirstChild);
                        var container = new WrapPanel{
                            Margin = new Thickness(0, 5, 0, 5)
                        };
                        foreach (var ctrl in inlineControls)
                            container.Children.Add(ctrl);

                        ChangelogBlocks.Add(container);
                        break;

                    case ListBlock list:
                        foreach (ListItemBlock item in list){
                            foreach (var blocki in item){
                                if (blocki is ParagraphBlock para){
                                    var container1 = new WrapPanel{ Margin = new Thickness(10, 2, 0, 2) };
                                    container1.Children.Add(new TextBlock{ Text = "• ", FontWeight = FontWeight.Bold, FontSize = textSize});

                                    foreach (var ctrl in BuildInlineControls(para.Inline?.FirstChild))
                                        container1.Children.Add(ctrl);

                                    ChangelogBlocks.Add(container1);
                                }
                            }
                        }

                        break;
                }
            }
        } catch (Exception e){
            Console.Error.WriteLine(e);
        }
    }

    IEnumerable<Control> BuildInlineControls(Inline? inline){
        var controls = new List<Control>();
        var urlRegex = new Regex(@"https?://[^\s]+", RegexOptions.Compiled);
        var githubRefRegex = new Regex(
            @"https://github\.com/[^/]+/[^/]+/(issues|discussions)/(\d+)",
            RegexOptions.Compiled);

        while (inline != null){
            switch (inline){
                case LiteralInline lit:
                    var text = lit.Content.Text.Substring(lit.Content.Start, lit.Content.Length);

                    var lastIndex = 0;
                    foreach (Match match in urlRegex.Matches(text)){
                        if (match.Index > lastIndex){
                            controls.Add(new TextBlock{
                                Text = text.Substring(lastIndex, match.Index - lastIndex),
                                TextWrapping = TextWrapping.Wrap,
                                FontSize = textSize
                            });
                        }

                        string url = match.Value;
                        string buttonText = url;

                        var ghMatch = githubRefRegex.Match(url);
                        if (ghMatch.Success && ghMatch.Groups.Count > 2){
                            buttonText = $"#{ghMatch.Groups[2].Value}";
                        }

                        controls.Add(CreateLinkButton(buttonText, url));
                        lastIndex = match.Index + match.Length;
                    }

                    if (lastIndex < text.Length){
                        controls.Add(new TextBlock{
                            Text = text.Substring(lastIndex),
                            TextWrapping = TextWrapping.Wrap,
                            FontSize = textSize
                        });
                    }

                    break;

                case EmphasisInline emph:
                    var emphControls = BuildInlineControls(emph.FirstChild);
                    foreach (var ec in emphControls){
                        if (ec is TextBlock tb){
                            tb.FontWeight = emph.DelimiterChar == '*' ? FontWeight.Bold : FontWeight.Normal;
                            tb.FontStyle = emph.DelimiterChar == '_' ? FontStyle.Italic : FontStyle.Normal;
                        }

                        controls.Add(ec);
                    }

                    break;

                case LinkInline link:
                    var linkText = ConvertInlinesToText(link.FirstChild);
                    controls.Add(CreateLinkButton(linkText, link.Url));
                    break;
            }

            inline = inline.NextSibling;
        }

        return controls;
    }


    string ConvertInlinesToText(Inline? inline){
        var result = new StringBuilder();

        while (inline != null){
            switch (inline){
                case LiteralInline lit:
                    result.Append(lit.Content.Text.Substring(lit.Content.Start, lit.Content.Length));
                    break;

                case EmphasisInline emph:
                    result.Append(ConvertInlinesToText(emph.FirstChild));
                    break;

                case LinkInline link:
                    var linkText = ConvertInlinesToText(link.FirstChild);
                    result.Append($"{linkText} ({link.Url})");
                    break;

                case LineBreakInline:
                    result.Append('\n');
                    break;

                default:
                    if (inline is ContainerInline{ FirstChild: not null } container){
                        result.Append(ConvertInlinesToText(container.FirstChild));
                    }

                    break;
            }

            inline = inline.NextSibling;
        }

        return result.ToString();
    }


    Brush GetLinkBrush(){
        try{
            var color = Color.Parse(CrunchyrollManager.Instance.CrunOptions.AccentColor ?? Brushes.LightBlue.Color.ToString());
            return new SolidColorBrush(color);
        } catch{
            return new SolidColorBrush(Brushes.LightBlue.Color);
        }
    }

    Button CreateLinkButton(string text, string url){
        var button = new Button{
            Content = new TextBlock{
                Text = text,
                FontSize = textSize,
                Foreground = GetLinkBrush(),
                TextDecorations = TextDecorations.Underline
            },
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            Cursor = new Cursor(StandardCursorType.Hand)
        };

        button.Click += (_, __) => {
            try{
                using var p = new Process();
                p.StartInfo = new ProcessStartInfo{
                    FileName = url,
                    UseShellExecute = true
                };
                p.Start();
            } catch{
                Console.Error.WriteLine($"Failed to open link: {url}");
            }
        };

        return button;
    }

    private string PreprocessMarkdown(string markdownText){
        string detailsPattern = @"<details>\s*<summary>.*?<\/summary>\s*<img\s+src=['""]([^'""]+)['""]\s+alt=['""]([^'""]+)['""]\s*\/?>\s*<\/details>";

        return Regex.Replace(markdownText, detailsPattern, match => {
            string imageUrl = match.Groups[1].Value;
            string altText = match.Groups[2].Value;

            return $"![{altText}]({imageUrl})";
        });
    }

    #endregion
}