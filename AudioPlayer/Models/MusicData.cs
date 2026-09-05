using System;
using System.Collections.Generic;
using System.Text;

namespace AudioPlayer.Models
{
    // Ez az osztály egyetlen dal adatait tárolja. 
    // Fontos: Itt nincsenek bonyolult logikák, csak egyszerű adattárolás (get; set;).
    public class MusicData
    {
        public string FilePath { get; set; }  // A fájl helye a gépen
        public string Title { get; set; }     // A dal címe
        public string Performer { get; set; } // Előadó neve
        public string Length { get; set; }    // Hossz formázva (pl. "03:12")
        public string Album { get; set; }     // Album neve
    }
}