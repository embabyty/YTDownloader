using System;
using CommunityToolkit.Mvvm.Input;
using Md3.Avalonia.Controls;

namespace YoutubeDownloader.Framework;

public class SnackbarManager(MdSnackbarService snackbarService)
{
    private readonly TimeSpan _defaultDuration = TimeSpan.FromSeconds(5);

    public void Notify(string message, TimeSpan? duration = null) =>
        snackbarService.Show(
            new MdSnackbarMessage(message) { Duration = duration ?? _defaultDuration }
        );

    public void Notify(
        string message,
        string actionText,
        Action actionHandler,
        TimeSpan? duration = null
    ) =>
        snackbarService.Show(
            new MdSnackbarMessage(message)
            {
                Duration = duration ?? _defaultDuration,
                ActionContent = actionText,
                ActionCommand = new RelayCommand(actionHandler),
            }
        );
}
