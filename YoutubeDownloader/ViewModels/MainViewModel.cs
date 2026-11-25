using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Md3.Avalonia.Controls;
using PowerKit.Extensions;
using YoutubeDownloader.Framework;
using YoutubeDownloader.Localization;
using YoutubeDownloader.Services;
using YoutubeDownloader.Utils.Extensions;
using YoutubeDownloader.ViewModels.Components;
using YoutubeDownloader.ViewModels.Dialogs;

namespace YoutubeDownloader.ViewModels;

public partial class MainViewModel(
    ViewModelManager viewModelManager,
    DialogManager dialogManager,
    MdSnackbarService snackbarService,
    SnackbarManager snackbarManager,
    LocalizationManager localizationManager,
    SettingsService settingsService,
    UpdateService updateService
) : ViewModelBase
{
    public string Title { get; } = $"{Program.Name} v{Program.VersionString}";

    public LocalizationManager LocalizationManager { get; } = localizationManager;

    public MdSnackbarService SnackbarService { get; } = snackbarService;

    public DashboardViewModel Dashboard { get; } = viewModelManager.GetDashboardViewModel();

    public SettingsViewModel Settings { get; } = viewModelManager.GetSettingsPageViewModel();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHomeSelected))]
    [NotifyPropertyChangedFor(nameof(IsDownloadsSelected))]
    [NotifyPropertyChangedFor(nameof(IsSettingsSelected))]
    public partial MainPage SelectedPage { get; set; } = MainPage.Home;

    public bool IsHomeSelected => SelectedPage == MainPage.Home;
    public bool IsDownloadsSelected => SelectedPage == MainPage.Downloads;
    public bool IsSettingsSelected => SelectedPage == MainPage.Settings;

    [RelayCommand]
    private void NavigateHome() => SelectedPage = MainPage.Home;

    [RelayCommand]
    private void NavigateDownloads() => SelectedPage = MainPage.Downloads;

    [RelayCommand]
    private void NavigateSettings() => SelectedPage = MainPage.Settings;

    private async Task ShowUkraineSupportMessageAsync()
    {
        if (!settingsService.IsUkraineSupportMessageEnabled)
            return;

        var dialog = viewModelManager.GetMessageBoxViewModel(
            LocalizationManager.UkraineSupportTitle,
            LocalizationManager.UkraineSupportMessage,
            LocalizationManager.LearnMoreButton,
            LocalizationManager.CloseButton
        );

        // Disable this message in the future
        settingsService.IsUkraineSupportMessageEnabled = false;
        settingsService.Save();

        if (await dialogManager.ShowDialogAsync(dialog) == true)
            Process.StartShellExecute("https://tyrrrz.me/ukraine?source=youtubedownloader");
    }

    private async Task ShowDevelopmentBuildMessageAsync()
    {
        if (!Program.IsDevelopmentBuild)
            return;

        // If debugging, the user is likely a developer
        if (Debugger.IsAttached)
            return;

        var dialog = viewModelManager.GetMessageBoxViewModel(
            LocalizationManager.UnstableBuildTitle,
            string.Format(LocalizationManager.UnstableBuildMessage, Program.Name),
            LocalizationManager.SeeReleasesButton,
            LocalizationManager.CloseButton
        );

        if (await dialogManager.ShowDialogAsync(dialog) == true)
            Process.StartShellExecute(Program.ProjectReleasesUrl);
    }

    private async Task CheckForUpdatesAsync()
    {
        try
        {
            var updateVersion = await updateService.CheckForUpdatesAsync();
            if (updateVersion is null)
                return;

            snackbarManager.Notify(
                string.Format(
                    LocalizationManager.UpdateDownloadingMessage,
                    Program.Name,
                    updateVersion
                )
            );
            await updateService.PrepareUpdateAsync(updateVersion);

            snackbarManager.Notify(
                LocalizationManager.UpdateReadyMessage,
                LocalizationManager.UpdateInstallNowButton,
                () =>
                {
                    updateService.FinalizeUpdate(true);

                    if (Application.Current?.ApplicationLifetime?.TryShutdown(2) != true)
                        Environment.Exit(2);
                }
            );
        }
        catch
        {
            // Failure to update shouldn't crash the application
            snackbarManager.Notify(LocalizationManager.UpdateFailedMessage);
        }
    }

    public override async Task InitializeAsync()
    {
        await ShowUkraineSupportMessageAsync();
        await ShowDevelopmentBuildMessageAsync();
        await CheckForUpdatesAsync();

        // The dashboard is hosted directly by the main view now, so it no longer
        // receives its own view load event to initialize from.
        await Dashboard.InitializeAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Save settings
            settingsService.Save();

            // Finalize pending updates
            updateService.FinalizeUpdate(false);
        }

        base.Dispose(disposing);
    }
}
