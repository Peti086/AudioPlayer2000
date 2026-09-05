using AudioPlayer.Models;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NAudio.CoreAudioApi;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Linq;
using AudioPlayer.Services;

namespace AudioPlayer.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        // --- SZOLGÁLTATÁSOK (SERVICES) ---
        // A tényleges hanglejátszásért és a hardver kezeléséért felelős szerviz
        private AudioPlayerService _musicPlayer = new AudioPlayerService();
        // A zenefájlok metaadatainak (cím, előadó, hossz) beolvasását végző szerviz
        private MetaDataService _exportedData = new MetaDataService();

        // --- HÁTTÉRFOLYAMATOK ---
        // Időzítő, ami rendszeres időközönként frissíti a csúszkát és a lejátszási időt
        private DispatcherTimer _timer = new DispatcherTimer();

        // --- ADATOK ÉS ÁLLAPOTOK (VÁLTOZÓK) ---

        // Az éppen aktuálisan betöltött zene adatai (cím, előadó, stb.)
        [ObservableProperty]
        private MusicData? _musicDatas = new MusicData();

        // A listában a felhasználó által éppen kijelölt (kattintott) zene
        [ObservableProperty]
        private MusicData _selectedMusic;

        // Az összes lejátszási listát tároló gyűjtemény. Automatikusan frissíti a UI-t változáskor.
        public ObservableCollection<MusicPlayList> PlayLists { get; } = new ObservableCollection<MusicPlayList>();

        // A jelenlegi lejátszási idő formázott szövegként (pl. "01:23")
        [ObservableProperty]
        private string _currentTime;

        // A zene teljes hossza másodpercben (a csúszka maximum értéke)
        [ObservableProperty]
        private double _maxPosition;

        // A zene jelenlegi pozíciója másodpercben (a csúszka jelenlegi értéke)
        [ObservableProperty]
        private double _currentPosition;

        // Hangerő (0.0 és 1.0 közötti érték)
        [ObservableProperty]
        private float _volume;

        // A zene teljes hossza formázott szövegként (pl. "03:45")
        [ObservableProperty]
        private string _totalTime = "-:--";

        // Új lista létrehozásakor a szövegdobozba beírt név
        [ObservableProperty]
        private string _listName;

        // A számítógéphez csatlakoztatott hangeszközök (hangszórók, fülesek) listája
        [ObservableProperty]
        private MMDeviceCollection? _devices;

        // A kiválasztott hangeszköz
        [ObservableProperty]
        private MMDevice _selectedDevice;

        // Belső állapot: Éppen szól-e a zene?
        [ObservableProperty]
        private bool _isPlaying;

        // A Play/Pause gomb ikonját tárolja dinamikusan (► vagy Ⅱ)
        [ObservableProperty]
        private string _playPauseIcon = "►";

        // Az aktuálisan betöltött hangfájl elérési útja
        private string _currentFilePath;

        // Jelzi, ha a felhasználó épp az egerével húzza a csúszkát (hogy ne ugráljon vissza)
        public bool IsDragging { get; set; }

        // Bekapcsolt-e a véletlenszerű lejátszás?
        [ObservableProperty]
        private bool _isShuffleEnabled;

        // Bekapcsolt-e az ismétlés?
        [ObservableProperty]
        private bool _isLoopEnabled;

        // Véletlenszám generátor a shuffle funkcióhoz
        private Random _random = new Random();

        // UI ÁLLAPOTOK A FELÜLET VEZÉRLÉSÉHEZ
        [ObservableProperty]
        private bool _isAddingList; // Új lista beviteli mező láthatósága

        [ObservableProperty]
        private bool _isRenamingList; // Átnevezés beviteli mező láthatósága

        [ObservableProperty]
        private string _tempListName; // Ideiglenes változó az átnevezéshez

        [ObservableProperty]
        private bool _isAllMusicActive; // A virtuális "Minden Zene" nézet aktív-e?

        // A mentési fájl (appstate.json) elérési útja a felhasználó AppData/Local mappájában
        private readonly string _configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AudioPlayer", "appstate.json");

        // A bal oldalon kiválasztott lejátszási lista rejtett változója
        private MusicPlayList? _selectedPlayList;

        // Tulajdonság a kiválasztott listához.
        public MusicPlayList? SelectedPlayList
        {
            get => _selectedPlayList;
            set
            {
                // Ha a ListBox lenullázná a kiválasztást, miközben a Minden Zene aktív, ignoráljuk
                if (value == null && IsAllMusicActive)
                    return;

                SetProperty(ref _selectedPlayList, value);

                // Ha a felhasználó egy VALÓDI listára kattint, kikapcsoljuk a "Minden Zene" módot
                if (value != null && value.Name != "Minden Zene")
                {
                    IsAllMusicActive = false;
                    IsRenamingList = false;
                }
            }
        }

        // --- KONSTRUKTOR (Indításkor lefutó kód) ---
        public MainWindowViewModel()
        {
            _timer.Interval = TimeSpan.FromMilliseconds(500); // Fél másodperces frissítés
            _timer.Tick += OnTimerTick;

            // Feliratkozás az eseményre: mi történjen, ha egy dal magától véget ér?
            _musicPlayer.PlaybackEnded += () =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    // Ha be van kapcsolva az ismétlés, automatikusan UGYANAZT a zenét töltjük be újra
                    if (IsLoopEnabled && !string.IsNullOrEmpty(_currentFilePath))
                    {
                        Load(_currentFilePath);
                    }
                    else
                    {
                        // Különben lépünk a következőre
                        NextSong();
                    }
                });
            };

            // Kezdeti értékek beállítása
            Volume = _musicPlayer.Volume;
            CurrentPosition = 0.0;
            CurrentTime = "-:--";
            TotalTime = "-:--";
            Devices = _musicPlayer.PopulateDevicesItem();
            MusicDatas = null;

            // Mentés betöltése és a "Minden Zene" alapértelmezett nézet beállítása
            LoadState();
            ShowAllMusic();
        }

        // --- METÓDUSOK ÉS PARANCSOK ---

        // Állapot kimentése JSON fájlba (Listák, hangerő, utolsó pozíció)
        public void SaveState()
        {
            try
            {
                var directory = Path.GetDirectoryName(_configPath);
                if (!Directory.Exists(directory)) Directory.CreateDirectory(directory!);

                var state = new AppState
                {
                    Playlists = PlayLists.ToList(),
                    Volume = Volume,
                    LastFilePath = _currentFilePath,
                    LastPosition = CurrentPosition,
                };

                string json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_configPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error while saving: {ex.Message}");
            }
        }

        // Állapot visszatöltése a JSON fájlból
        private void LoadState()
        {
            if (!File.Exists(_configPath)) return;

            try
            {
                string json = File.ReadAllText(_configPath);
                var state = JsonSerializer.Deserialize<AppState>(json);

                if (state == null) return;

                PlayLists.Clear();
                foreach (var list in state.Playlists)
                {
                    PlayLists.Add(list);
                }

                Volume = state.Volume;

                // Ha volt utoljára játszott zene és még létezik a fájl, betöltjük (de nem indítjuk el automatikusan)
                if (!string.IsNullOrEmpty(state.LastFilePath) && File.Exists(state.LastFilePath))
                {
                    _currentFilePath = state.LastFilePath;
                    _musicPlayer.Load(_currentFilePath);
                    MusicDatas = _exportedData.GetMetaData(_currentFilePath);

                    _musicPlayer.CurrentTime = TimeSpan.FromSeconds(state.LastPosition);
                    CurrentPosition = state.LastPosition;
                    MaxPosition = _musicPlayer.TotalTime.TotalSeconds;
                    CurrentTime = _musicPlayer.CurrentTime.ToString(@"mm\:ss");
                    TotalTime = MusicDatas.Length;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error while loading: {ex.Message}");
            }
        }

        // UI Parancs: Megjeleníti az "Új lista" beviteli mezőit
        [RelayCommand]
        public void ShowAddList() => IsAddingList = true;

        // UI Parancs: Elrejti az "Új lista" mezőt és törli a beírt szöveget
        [RelayCommand]
        public void CancelAddList()
        {
            IsAddingList = false;
            ListName = string.Empty;
        }

        // UI Parancs: Létrehozza a listát a beírt név alapján
        [RelayCommand]
        public void ConfirmAddList()
        {
            if (!string.IsNullOrWhiteSpace(ListName))
            {
                PlayLists.Add(new MusicPlayList { Name = ListName });
                ListName = string.Empty;
                IsAddingList = false;
            }
        }

        // UI Parancs: Átnevezés mód indítása
        [RelayCommand]
        public void StartRename()
        {
            if (SelectedPlayList != null && !IsAllMusicActive)
            {
                TempListName = SelectedPlayList.Name;
                IsRenamingList = true;
            }
        }

        // UI Parancs: Átnevezés megerősítése
        [RelayCommand]
        public void ConfirmRename()
        {
            if (SelectedPlayList != null && !string.IsNullOrWhiteSpace(TempListName))
            {
                SelectedPlayList.Name = TempListName;
                IsRenamingList = false;

                // Kivesszük és visszatesszük a listát, hogy a felület biztosan észrevegye a névváltozást
                var index = PlayLists.IndexOf(SelectedPlayList);
                if (index != -1)
                {
                    var temp = SelectedPlayList;
                    PlayLists.RemoveAt(index);
                    PlayLists.Insert(index, temp);
                    SelectedPlayList = temp;
                }
            }
        }

        // UI Parancs: Generál egy virtuális listát az összes egyedi zenéből duplikációk nélkül
        [RelayCommand]
        public void ShowAllMusic()
        {
            IsAllMusicActive = true;
            var allMusicList = new MusicPlayList { Name = "Minden Zene" };
            var addedPaths = new System.Collections.Generic.HashSet<string>();

            foreach (var list in PlayLists)
            {
                foreach (var song in list.MusicDatas)
                {
                    if (addedPaths.Add(song.FilePath)) // HashSet gondoskodik róla, hogy ne legyen duplán
                    {
                        allMusicList.MusicDatas.Add(song);
                    }
                }
            }
            SelectedPlayList = allMusicList;
        }

        // Hangszóró/kimenet váltása futásidőben
        [RelayCommand]
        public void SetDevice()
        {
            if (SelectedDevice != null)
            {
                _musicPlayer.SwitchDevice(SelectedDevice);
                _musicPlayer.Volume = Volume;
            }
        }

        // Betölt egy új zenét és előkészíti a lejátszásra
        public void Load(string filePath)
        {
            _currentFilePath = filePath;
            MusicDatas = null;
            _musicPlayer.Load(filePath);

            MusicDatas = _exportedData.GetMetaData(filePath);
            MaxPosition = _musicPlayer.TotalTime.TotalSeconds;
            TotalTime = MusicDatas.Length;
            _musicPlayer.Volume = Volume;

            IsPlaying = false;
            PausePlaySong(); // Automatikus elindítás betöltés után
        }

        [RelayCommand]
        public void Close()
        {
            System.Environment.Exit(0);
        }

        // Zene törlése az aktuálisan kiválasztott listából
        [RelayCommand]
        public void DeleteMusic(MusicData needToDelete)
        {
            SelectedPlayList?.MusicDatas.Remove(needToDelete);
        }

        // Play / Pause váltógomb logikája dinamikus ikonnal
        [RelayCommand]
        public void PausePlaySong()
        {
            if (MusicDatas == null) return;

            if (IsPlaying)
            {
                _musicPlayer.Pause();
                _timer.Stop();
                IsPlaying = false;
                PlayPauseIcon = "►";
            }
            else
            {
                _musicPlayer.Play();
                _timer.Start();
                IsPlaying = true;
                PlayPauseIcon = "Ⅱ";
            }
        }

        // Lejátszás teljes leállítása (visszaugrik az elejére)
        [RelayCommand]
        public void Stop()
        {
            _musicPlayer.Stop();
            _timer.Stop();
            CurrentPosition = 0;
            CurrentTime = "-:--";
            TotalTime = "-:--";
            MusicDatas = null;
            _currentFilePath = null;
            IsPlaying = false;
            PlayPauseIcon = "►";
        }

        // Segédmetódus: Megkeresi az épp játszott zene indexét a jelenlegi listában
        private int GetCurrentSongIndex()
        {
            if (SelectedPlayList == null || SelectedPlayList.MusicDatas.Count == 0 || string.IsNullOrEmpty(_currentFilePath))
                return -1;

            for (int i = 0; i < SelectedPlayList.MusicDatas.Count; i++)
            {
                if (SelectedPlayList.MusicDatas[i].FilePath == _currentFilePath)
                    return i;
            }
            return -1;
        }

        // Ugrás az előző zenére
        [RelayCommand]
        public void PreviousSong()
        {
            if (SelectedPlayList == null || SelectedPlayList.MusicDatas.Count == 0) return;

            int currentIndex = GetCurrentSongIndex();
            int prevIndex;

            if (IsShuffleEnabled && SelectedPlayList.MusicDatas.Count > 1)
            {
                prevIndex = _random.Next(0, SelectedPlayList.MusicDatas.Count);
                while (prevIndex == currentIndex)
                {
                    prevIndex = _random.Next(0, SelectedPlayList.MusicDatas.Count);
                }
            }
            else
            {
                prevIndex = currentIndex - 1;
                if (prevIndex < 0)
                {
                    return; // Nincs hova visszaugrani, megállunk
                }
            }

            if (prevIndex >= 0)
            {
                var prevSong = SelectedPlayList.MusicDatas[prevIndex];
                SelectedMusic = prevSong;
                Load(prevSong.FilePath);
            }
        }

        // Ugrás a következő zenére
        [RelayCommand]
        public void NextSong()
        {
            if (SelectedPlayList == null || SelectedPlayList.MusicDatas.Count == 0) return;

            int currentIndex = GetCurrentSongIndex();
            int nextIndex;

            if (IsShuffleEnabled && SelectedPlayList.MusicDatas.Count > 1)
            {
                nextIndex = _random.Next(0, SelectedPlayList.MusicDatas.Count);
                while (nextIndex == currentIndex)
                {
                    nextIndex = _random.Next(0, SelectedPlayList.MusicDatas.Count);
                }
            }
            else
            {
                nextIndex = currentIndex + 1;
                if (nextIndex >= SelectedPlayList.MusicDatas.Count)
                {
                    // Ha a lista végére értünk, megállítjuk a lejátszást
                    Stop();
                    return;
                }
            }

            if (nextIndex < SelectedPlayList.MusicDatas.Count)
            {
                var nextSong = SelectedPlayList.MusicDatas[nextIndex];
                SelectedMusic = nextSong;
                Load(nextSong.FilePath);
            }
        }

        // Új lejátszási lista létrehozása
        [RelayCommand]
        public void CreateNewList()
        {
            PlayLists.Add(new MusicPlayList { Name = ListName });
        }

        // Új zene hozzáadása a listához fájlútvonal alapján (duplikáció szűréssel)
        [RelayCommand]
        public void AddMusic(string filePath)
        {
            var newMusic = _exportedData.GetMetaData(filePath);
            foreach (var item in SelectedPlayList.MusicDatas)
            {
                if (item.Title == newMusic.Title) return; // Már benne van
            }
            SelectedPlayList.MusicDatas.Add(newMusic);
        }

        // Aktuális lista törlése
        [RelayCommand]
        public void DeleteList()
        {
            PlayLists.Remove(SelectedPlayList);
        }

        // Félmásodpercenként lefutó frissítő metódus a UI-nak
        public void OnTimerTick(object sender, EventArgs e)
        {
            if (_musicPlayer == null || IsDragging) return;
            CurrentTime = _musicPlayer.CurrentTime.ToString(@"mm\:ss");
            CurrentPosition = _musicPlayer.CurrentTime.TotalSeconds;
        }

        [RelayCommand]
        public void IncreaseVolume() { if (Volume < 1.0f) Volume += 0.05f; }

        [RelayCommand]
        public void DecreaseVolume() { if (Volume > 0.0f) Volume -= 0.05f; }

        // A hangerő csúszka változásakor azonnal átadjuk az értéket a hardvernek
        partial void OnVolumeChanged(float newVolume) { _musicPlayer.Volume = newVolume; }

        // Amikor a felhasználó megfogja a csúszkát az egerével
        public void BeginSeek() => _timer.Stop();

        // Amikor a felhasználó elengedi a csúszkát az új pozíción
        public void EndSeek(double position)
        {
            _musicPlayer.CurrentTime = TimeSpan.FromSeconds(position);
            System.Threading.Thread.Sleep(50); // Kis késleltetés a stabilizáláshoz
            _timer.Start();
        }
    }
}