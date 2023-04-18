using LibVLCSharp.Shared;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using TurnipVodSplitter.Properties;

namespace TurnipVodSplitter {
    public class MainWindowViewModel : INotifyPropertyChanged {
        public static LibVLC libVlc = new LibVLC();
        private VlcMediaPlayer _vlcMediaPlayer = new VlcMediaPlayer();
        public VlcMediaPlayer vlcMediaPlayer { get { return _vlcMediaPlayer; } }


        private ObservableCollection<SplitEntry> _splits;
        public ObservableCollection<SplitEntry> splits {
            get => this._splits;
            set {
                this._splits = value;
                OnPropertyChanged("splits");
            }
        }

        private String _mediaContentPath;

        public String mediaContentPath {
            get => this._mediaContentPath;
            set {
                this._mediaContentPath = value;
                Settings.Default.lastVodLoaded = _mediaContentPath;
                Settings.Default.Save();
                OnPropertyChanged("mediaContentPath");

            }
        }

        private string _outputDirectory;
        public string outputDirectory {
            get => _outputDirectory;
            set {
                if (value == _outputDirectory) return;
                _outputDirectory = value;
                Settings.Default.lastOutputDirectory = _outputDirectory;
                Settings.Default.Save();
                OnPropertyChanged("outputDirectory");
            }
        }

        private string _ffmpegPath;

        public string ffmpegPath {
            get => _ffmpegPath;
            set {
                if (value == _ffmpegPath) return;
                _ffmpegPath = value;
                OnPropertyChanged("ffmpegPath");
            }
        }

        private string _eventName = "";
        public string eventName {
            get {
                if (_eventName == null) {
                    return "";
                }
                return _eventName;
            }
            set {
                if (value == _eventName) return;
                _eventName = value;
                OnPropertyChanged("eventName");
            }
        }


        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null) {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
