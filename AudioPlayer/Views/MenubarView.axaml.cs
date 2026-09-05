using AudioPlayer.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using System.Collections.Generic;

namespace AudioPlayer;

public partial class Menubar : UserControl
{
    public Menubar()
    {
        InitializeComponent();
    }

    private async void GetFileAndInit(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var settings = new FilePickerOpenOptions
        {
            Title = "Válassz egy fájlt",
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType>
                {
                    new FilePickerFileType("Audio fájlok") { Patterns = new[] { "*.mp3", "*.wav", "*.flac" } },
                    new FilePickerFileType("Összes fájl") { Patterns = new[] { "*.*" } }
                }
        };

        var topLevel = TopLevel.GetTopLevel(this) as Window;

        IReadOnlyList<IStorageFile> result = await topLevel.StorageProvider.OpenFilePickerAsync(settings);

        if (result.Count > 0)
        {
            string filePath = result[0].Path.LocalPath;

            // --- A HÍD A VIEW ÉS A VIEWMODEL KÖZÖTT ---
            // Az ablak "DataContext"-e tartalmazza a ViewModel-ünket.
            // Megnézzük, hogy tényleg az-e, és ha igen, betesszük a 'viewModel' változóba.
            if (this.DataContext is MainWindowViewModel viewModel)
            {
                // Meghívjuk a ViewModel publikus metódusát, és átadjuk neki a fájlt!
                viewModel.Load(filePath);
            }
        }
    }
}