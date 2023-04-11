using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace TurnipVodSplitter {
    public class MainWindowViewModel : INotifyPropertyChanged {
        private TimeSpan _mediaPosition = TimeSpan.Zero;

        public TimeSpan mediaPosition {
            get => this._mediaPosition;
            set {
                this._mediaPosition = value;
                OnPropertyChanged("mediaPosition");
                OnPropertyChanged("mediaPositionSeconds");
                OnPropertyChanged("mediaPositionStr");
            }
        }


        public Double mediaPositionSeconds {
            get => (int)_mediaPosition.TotalSeconds;
            set => this.mediaPosition = TimeSpan.FromSeconds(value);
        }

        public String mediaPositionStr => $"{mediaPosition.Hours:D2}:{mediaPosition.Minutes:D2}:{mediaPosition.Seconds:D2}";

        private ObservableCollection<SplitEntry> _splits;
        public ObservableCollection<SplitEntry> splits {
            get => this._splits;
            set {
                this._splits = value;
                OnPropertyChanged("splits");
            }
        }

        private TimeSpan _mediaTotalDuration = TimeSpan.Zero;

        public TimeSpan mediaTotalDuration {
            get => this._mediaTotalDuration;
            set {
                this._mediaTotalDuration = value;
                OnPropertyChanged("mediaTotalDuration");
                OnPropertyChanged("mediaTotalDurationStr");
                OnPropertyChanged("mediaTotalDurationSeconds");
            }
        }
        public String mediaTotalDurationStr => $"{mediaPosition.Hours:D2}:{mediaPosition.Minutes:D2}:{mediaPosition.Seconds:D2}";
        public Double mediaTotalDurationSeconds {
            get => (int)_mediaTotalDuration.TotalSeconds;
            set => this.mediaTotalDuration = TimeSpan.FromSeconds(value);
        }

        private String _mediaContentPath;

        public String mediaContentPath {
            get => this._mediaContentPath;
            set {
                this._mediaContentPath = value;
                OnPropertyChanged("mediaContentPath");

            }
        }

        private string _outputDirectory;
        public string outputDirectory {
            get => _outputDirectory;
            set {
                if (value == _outputDirectory) return;
                _outputDirectory = value;
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
