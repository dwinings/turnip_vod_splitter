using System;
using System.Collections.Generic;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CsvHelper.Configuration;

namespace TurnipVodSplitter {
    public partial class SplitEntry: ObservableObject {
        [ObservableProperty] private TimeSpan splitStart = TimeSpan.Zero;
        [ObservableProperty] private TimeSpan splitEnd = TimeSpan.Zero;
        [ObservableProperty] private string player1 = "";
        [ObservableProperty] private string player2 = "";
        [ObservableProperty] private bool skipSplit = false;
        [ObservableProperty] private string description = "";

        public string splitName => $"{Player1} vs {Player2}";

        public string ffmpegArgsForSplit => $"-vcodec copy -acodec copy -ss {this.SplitStart.VideoTimestampFormat()} -to {this.SplitEnd.VideoTimestampFormat()}";
    }

    public sealed class SplitEntryFieldMap : ClassMap<SplitEntry> {
        public SplitEntryFieldMap() {
            Map(m => m.SplitStart).Index(0);
            Map(m => m.SplitEnd).Index(1);
            Map(m => m.Player1).Index(2);
            Map(m => m.Player2).Index(3);
            Map(m => m.Description).Index(4);
            Map(m => m.SkipSplit).Index(5);

        }
    }
}
