using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using LibVLCSharp.Shared;

namespace TurnipVodSplitter {
    public class VlcMediaPlayer: MediaPlayer, INotifyPropertyChanged {
        public VlcMediaPlayer() : base(new LibVLC()) {
            this.PositionChanged += (s, e) => OnPropertyChanged("Position");
            this.LengthChanged += (s, e) => OnPropertyChanged("Length");
            this.TimeChanged += (s, e) => OnPropertyChanged("Time");
            this.MediaChanged += (s, e) => OnPropertyChanged("Media");
            this.Playing += (s, e) => OnPropertyChanged("State");
            this.Paused += (s, e) => OnPropertyChanged("State");
            this.EndReached += delegate { OnPropertyChanged("State"); };
            this.EnableHardwareDecoding = true;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null) {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
