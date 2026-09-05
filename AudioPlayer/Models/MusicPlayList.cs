using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace AudioPlayer.Models
{
    // Ez az osztály egy lejátszási lista adatait reprezentálja.
    public class MusicPlayList
    {
        // A lista neve
        public string Name { get; set; }

        // A listában tárolt dalok gyűjteménye.
        // Az ObservableCollection azért kell a sima List<> helyett, mert ez a típus "jelez" a képernyőnek (Avaloniának), 
        // ha hozzáadnak vagy törölnek egy dalt, így a felület azonnal frissül.
        public ObservableCollection<MusicData> MusicDatas { get; set; } = new ObservableCollection<MusicData>();
    }
}