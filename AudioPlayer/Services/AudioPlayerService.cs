using System;
using System.Collections.Generic;
using NAudio.Wasapi;
using NAudio.Wave;
using NAudio.CoreAudioApi;

namespace AudioPlayer.Services
{
    public class AudioPlayerService
    {
        // A kimeneti hangfolyamot (ami a hangszórókra megy) kezelő NAudio osztály
        private WasapiOut _outputDevice;

        // A betöltött zenefájlt (mp3, wav, stb.) beolvasó osztály
        private AudioFileReader _audioFile;

        // A Windows hangeszközeit lekérdező segéd
        private MMDeviceEnumerator _deviceEnumerator;

        // Az aktuálisan használt hangszóró/fejhallgató (alapértelmezetten a Windows fő eszköze)
        private MMDevice _currentDevice = new MMDeviceEnumerator().GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

        // Védelmi változó: jelzi, ha a felhasználó gombnyomással (Stop/Pause/Next) állította meg a zenét.
        // Ez kell ahhoz, hogy ne ugorjon automatikusan a következő dalra, amikor mi magunk állítjuk meg.
        private bool _isManualStop;

        // Esemény, ami jelzi a külvilágnak (ViewModel), ha a zene magától végigért
        public event Action PlaybackEnded;

        // Jelenlegi pozíció lekérdezése és beállítása 
        public TimeSpan CurrentTime
        {
            get => _audioFile?.CurrentTime ?? TimeSpan.Zero;
            set
            {
                if (_audioFile != null) _audioFile.CurrentTime = value;
            }
        }

        // A zene teljes hossza
        public TimeSpan TotalTime => _audioFile?.TotalTime ?? TimeSpan.Zero;

        // Hangerő lekérdezése és beállítása (0.0 - 1.0)
        public float Volume
        {
            get => _audioFile?.Volume ?? 1.0f;
            set
            {
                if (_audioFile != null) _audioFile.Volume = value;
            }
        }

        // Lekéri a géphez csatlakoztatott összes aktív hangeszközt (ComboBoxhoz)
        public MMDeviceCollection? PopulateDevicesItem()
        {
            _deviceEnumerator = new MMDeviceEnumerator();
            return _deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
        }

        // Hangeszköz váltása lejátszás közben
        public void SwitchDevice(MMDevice newDevice)
        {
            if (newDevice.ID == _currentDevice?.ID) return; // Ha ugyanazt választotta, nem csinálunk semmit
            _currentDevice = newDevice;
            if (_audioFile == null) return;

            // Megjegyezzük, szólt-e a zene a váltás pillanatában
            bool wasPlaying = _outputDevice?.PlaybackState == PlaybackState.Playing;

            if (_outputDevice != null)
            {
                _isManualStop = true; // Kézi beavatkozás, ne jelezzen dal végét!
                _outputDevice.PlaybackStopped -= OnPlaybackStopped;
                _outputDevice.Stop();
                _outputDevice.Dispose();
            }

            // Új kimenet létrehozása az új eszközzel
            _outputDevice = new WasapiOut(_currentDevice, AudioClientShareMode.Shared, true, 50);
            _outputDevice.PlaybackStopped += OnPlaybackStopped;
            _outputDevice.Init(_audioFile);

            // Ha szólt a zene, az új eszközön is elindítjuk
            if (wasPlaying)
            {
                _isManualStop = false;
                _outputDevice.Play();
            }
        }

        // Új fájl betöltése a memóriába és lejátszó inicializálása
        public void Load(string filePath)
        {
            _isManualStop = true;
            Cleanup(); // Előző fájl takarítása

            _audioFile = new AudioFileReader(filePath);
            _outputDevice = new WasapiOut(_currentDevice, AudioClientShareMode.Shared, true, 50);
            _outputDevice.PlaybackStopped += OnPlaybackStopped;
            _audioFile.Volume = 1.0f; // Alapértelmezett hangerő

            _outputDevice.Init(_audioFile);
        }

        // Lejátszás indítása
        public void Play()
        {
            _isManualStop = false; // Innentől kezdve figyeljük a természetes végét
            _outputDevice?.Play();
        }

        // Lejátszás teljes leállítása
        public void Stop()
        {
            _isManualStop = true;
            _outputDevice?.Stop();
        }

        // Lejátszás szüneteltetése
        public void Pause()
        {
            _isManualStop = true;
            _outputDevice?.Pause();
        }

        // Memória és hardveres erőforrások felszabadítása (pl. fájl zárolásának feloldása)
        public void Cleanup()
        {
            if (_outputDevice != null)
            {
                _isManualStop = true;
                _outputDevice.PlaybackStopped -= OnPlaybackStopped; // ELŐBB leiratkozunk, hogy a Stop ne lője fel az eseményt!
                _outputDevice.Stop();
                _outputDevice.Dispose();
                _outputDevice = null;
            }

            if (_audioFile != null)
            {
                _audioFile.Dispose();
                _audioFile = null;
            }
        }

        // Belső eseménykezelő, ami akkor fut le, ha a NAudio megáll (akár magától, akár gombnyomásra)
        private void OnPlaybackStopped(object sender, StoppedEventArgs e)
        {
            // Biztonsági ellenőrzés: ha nem az aktuális lejátszó küldte a jelet (mert már betöltöttünk egy újat), ignoráljuk!
            if (sender != _outputDevice) return;

            // Csak akkor jelezzük a felületnek, hogy vége a dalnak, ha NEM mi állítottuk meg (nem kézi Stop/Next)
            if (!_isManualStop)
            {
                PlaybackEnded?.Invoke();
            }
        }
    }
}