using AudioPlayer.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Microsoft.VisualBasic;
using System.Collections.Generic;

namespace AudioPlayer.Views
{
    public partial class MainWindow : Window
    {
        private MainWindowViewModel viewModel = new MainWindowViewModel();
        public MainWindow()
        {
            InitializeComponent();
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            if (DataContext is MainWindowViewModel vm) 
            {
                vm.SaveState();
            }
            base.OnClosing(e);
        }
    }
}