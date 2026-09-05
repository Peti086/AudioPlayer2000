using System;
using System.Collections.Generic;
using System.Text;
using TagLib;
using System.IO;
using AudioPlayer.Models;

namespace AudioPlayer.Services
{
    internal class MetaDataService
    {
        private MusicData musicData;
        private TagLib.File tagFile;

        // Fájl elérési útja alapján kiolvassa a zenefájlba (mp3/flac) rejtett információkat (Metaadatokat)
        public MusicData GetMetaData(string filePath)
        {
            musicData = new MusicData();

            // A TagLib library megnyitja a fájlt, hogy olvassa az adatait
            tagFile = TagLib.File.Create(filePath);
            musicData.FilePath = filePath;

            // Ha nincs a fájlnak rendes "Cím" (Title) mezője kitöltve, akkor magát a fájlnevet használjuk
            if (string.IsNullOrEmpty(tagFile.Tag.Title))
            {
                musicData.Title = Path.GetFileName(filePath);
            }
            else
            {
                musicData.Title = tagFile.Tag.Title;
            }

            // Ha nincs előadó vagy album megadva, alapértelmezett szöveget írunk be
            musicData.Performer = tagFile.Tag.FirstPerformer ?? "Unknown Artist";
            musicData.Album = tagFile.Tag.Album ?? "Unknown Album";

            // A dal hossza percekre és másodpercekre formázva (pl. "04:30")
            musicData.Length = tagFile.Properties.Duration.ToString(@"m\:ss");

            return musicData;
        }
    }
}