using CommunityToolkit.Mvvm.ComponentModel;
using Mcacc.App.Localization;

namespace Mcacc.App.ViewModels;

/// <summary>One project on the settings page, with whether the info page counts it.</summary>
/// <param name="tokens">Over all accounts; null for an excluded project no log mentions any more.</param>
public sealed partial class ProjectChoiceViewModel(string path, long? tokens, bool isCounted, Action changed)
    : ObservableObject
{
    public string Path { get; } = path;

    public long? Tokens { get; } = tokens;

    public string Name => Texts.ProjectName(Path);

    public string TokensText => Tokens is { } known ? Texts.TokenAmount(known) : Texts.SettingsProjectGone;

    [ObservableProperty]
    public partial bool IsCounted { get; set; } = isCounted;

    partial void OnIsCountedChanged(bool value) => changed();
}
