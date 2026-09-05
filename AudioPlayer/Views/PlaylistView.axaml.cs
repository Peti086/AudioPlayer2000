using AudioPlayer.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using System.Collections.Generic;

namespace AudioPlayer;

public partial class Playlist : UserControl
{
    public Playlist()
    {
        InitializeComponent();
    }

    private async void GetFileAndInit(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var settings = new FilePickerOpenOptions
        {
            Title = "Válassz zenéket", // A címet is átírtam többesszámra
            AllowMultiple = true,      //Engedélyezzük a több fájl kijelölését
            FileTypeFilter = new List<FilePickerFileType>
                {
                    new FilePickerFileType("Audio fájlok") { Patterns = new[] { "*.mp3", "*.wav", "*.flac" } },
                    new FilePickerFileType("Összes fájl") { Patterns = new[] { "*.*" } }
                }
        };

        var topLevel = TopLevel.GetTopLevel(this) as Window;

        if (topLevel == null) return; // Biztonsági ellenőrzés

        IReadOnlyList<IStorageFile> result = await topLevel.StorageProvider.OpenFilePickerAsync(settings);

        if (result.Count > 0)
        {
            // --- A HÍD A VIEW ÉS A VIEWMODEL KÖZÖTT ---
            if (this.DataContext is MainWindowViewModel viewModel)
            {
                //Ciklussal végigmegyünk az összes kiválasztott fájlon
                foreach (var file in result)
                {
                    string filePath = file.Path.LocalPath;
                    viewModel.AddMusic(filePath);
                }
            }
        }
    }
}