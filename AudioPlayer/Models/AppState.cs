using System;
using System.Collections.Generic;
using System.Text;

namespace AudioPlayer.Models
{
    // Ez az osztály a "mentés-struktúra". A JSON fájl (appstate.json) pontosan így fog kinézni, amikor a program kimenti az állapotát bezáráskor.
    public class AppState
    {
        public List<MusicPlayList> Playlists { get; set; } = new List<MusicPlayList>();
        public float Volume { get; set; } = 1.0f;
        public string? LastFilePath { get; set; }
        public double LastPosition { get; set; }
    }
}