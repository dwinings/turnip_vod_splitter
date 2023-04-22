using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using CsvHelper.Configuration;

namespace TurnipVodSplitter {
    public class SplitEntry: INotifyPropertyChanged{
        private TimeSpan _splitStart;
        private TimeSpan _splitEnd;
        private string _player1;
        private string _player2 = "";

        public TimeSpan splitStart {
            get => _splitStart;
            set {
                if (value.Equals(_splitStart)) return;
                _splitStart = value;
                OnPropertyChanged("splitStart");
            }
        }

        public string splitStartStr {
            get => this.splitStart.VideoTimestampFormat();
            set {
                TimeSpan? ts = value.timeSpanFromTimeStamp();
                if (ts.HasValue) {
                    this.splitStart = ts.Value;
                }
            }
        }

        public TimeSpan splitEnd {
            get => _splitEnd;
            set {
                if (value.Equals(_splitEnd)) return;
                _splitEnd = value;
                OnPropertyChanged("splitEnd");
            }
        }

        public string splitEndStr {
            get => this.splitEnd.VideoTimestampFormat();
            set {
                TimeSpan? ts = value.timeSpanFromTimeStamp();
                if (ts != null) {
                    this.splitEnd = ts.Value;
                }
            }
        }
        public string splitName => $"{player1} vs {player2}";

        public string player1 {
            get => _player1;
            set {
                if (value == _player1) return;
                _player1 = value;
                OnPropertyChanged("player1");
            }
        }
        public string player2 {
            get => _player2;
            set {
                if (value == _player2) return;
                _player2 = value;
                OnPropertyChanged("player2");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public string ffmpegArgsForSplit => $"-vcodec copy -acodec copy -ss {this.splitStartStr} -to {this.splitEndStr}";
    }

    public sealed class SplitEntryFieldMap : ClassMap<SplitEntry> {
        public SplitEntryFieldMap() {
            Map(m => m.splitStart).Index(0);
            Map(m => m.splitEnd).Index(1);
            Map(m => m.player1).Index(2);
            Map(m => m.player2).Index(3);

        }
    }

}
