using Avalonia;
using Avalonia.Input;
using PowerKit.Extensions;
using YoutubeDownloader.Framework;
using YoutubeDownloader.ViewModels.Components;

namespace YoutubeDownloader.Views.Components;

public partial class DownloadsView : UserControl<DashboardViewModel>
{
    public DownloadsView() => InitializeComponent();

    private void StatusTextBlock_OnPointerReleased(object sender, PointerReleasedEventArgs args)
    {
        if (sender is IDataContextProvider { DataContext: DownloadViewModel dataContext })
            dataContext.CopyErrorMessageCommand.ExecuteIfCan(null);
    }
}
