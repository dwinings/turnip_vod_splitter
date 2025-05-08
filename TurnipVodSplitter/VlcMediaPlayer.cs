using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using LibVLCSharp.Shared;

namespace TurnipVodSplitter {
    public class VlcMediaPlayer: MediaPlayer, INotifyPropertyChanged {
        public readonly LibVLC libvlc;

        public VlcMediaPlayer() : this(new LibVLC(
            "--repeat",
            "--avcodec-threads=1"
            )) {
        }

        public VlcMediaPlayer(LibVLC libvlc) : base(libvlc) {
            this.libvlc = libvlc;
            this.PositionChanged += (s, e) => OnPropertyChanged("Position");
            this.LengthChanged += (s, e) => OnPropertyChanged("Length");
            this.TimeChanged += (s, e) => OnPropertyChanged("Time");
            this.MediaChanged += (s, e) => OnPropertyChanged("Media");
            this.Playing += (s, e) => OnPropertyChanged("State");
            this.Paused += (s, e) => OnPropertyChanged("State");
            this.EndReached += delegate { OnPropertyChanged("State"); };
            this.EnableHardwareDecoding = true;
            this.FileCaching = 60000;

            this.libvlc.Log += (s, e) => {
                if (e.Level >= LogLevel.Notice) {
                    Debug.WriteLine(e.FormattedLog);
                }
            };
        }

        public TimeSpan PositionTs {
            get {
                if (this.Media == null) {
                    return TimeSpan.Zero;
                }

                var currentMillis = (long)(this.Length * this.Position);
                return TimeSpan.FromMilliseconds(currentMillis);
            }
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
