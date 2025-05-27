using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Threading;
using LibVLCSharp.Shared;

namespace TurnipVodSplitter {
    public class VlcMediaPlayer: MediaPlayer, INotifyPropertyChanged {
        public readonly LibVLC libvlc;

        public VlcMediaPlayer() : this(new LibVLC(
            "--repeat",
            "--avcodec-threads=4",
            "--input-fast-seek"
            )) {
        }

        public VlcMediaPlayer(LibVLC libvlc) : base(libvlc) {
            this.libvlc = libvlc;
            this.PositionChanged += (s, e) => OnPropertyChanged("Position");
            this.LengthChanged += (s, e) => OnPropertyChanged("Length");
            this.TimeChanged += (s, e) => OnPropertyChanged("Time");
            this.MediaChanged += (s, e) => OnPropertyChanged("Media");

            this.Buffering += (s, e) => this.StateChanged?.Invoke(this, new(VLCState.Buffering));
            this.Playing += (s, e) => this.StateChanged?.Invoke(this, new(VLCState.Playing));
            this.Paused += (s, e) => this.StateChanged?.Invoke(this, new(VLCState.Paused));
            this.Stopped += (s, e) => this.StateChanged?.Invoke(this, new(VLCState.Stopped));
            this.EndReached += (s, e) => this.StateChanged?.Invoke(this, new(VLCState.Ended));
            this.NothingSpecial += (s, e) => this.StateChanged?.Invoke(this, new(VLCState.NothingSpecial));
            this.Opening += (s, e) => this.StateChanged?.Invoke(this, new(VLCState.Opening));
            this.EncounteredError += (s, e) => this.StateChanged?.Invoke(this, new(VLCState.Error));

            this.Corked += (s, e) => Debug.WriteLine($"Media player corked: {e}");
            this.Uncorked += (s, e) => Debug.WriteLine($"Media player Uncorked {e}");
            this.SeekableChanged += (s, e) => Debug.WriteLine($"Media player seekable changed: {e.Seekable}");

            this.StateChanged += (s, e) => {
                Debug.WriteLine($"Player is now in state {e.State}");
            };

            this.EnableHardwareDecoding = false;
            this.FileCaching = 0;

            this.StateChanged += (o, e) => {
                /* Debug.WriteLine($"State changed to {e.State} on thread {Thread.CurrentThread.ManagedThreadId}");*/
            };

            this.libvlc.Log += (s, e) => {
                if (e.Level >= LogLevel.Notice) {
                    Debug.WriteLine(e.FormattedLog);
                }
            };

            this.EncounteredError += (s, e) => {
                Debug.WriteLine($"VlcPlayer encountered an error :{this.libvlc.LastLibVLCError}");
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

        public class StateChangedEventArgs(VLCState state) : EventArgs {
            public virtual VLCState State => state;
        }
        public delegate void StateChangedEventHandler(object sender, StateChangedEventArgs args);
        public event StateChangedEventHandler? StateChanged;

        public void SeekTo(TimeSpan time, Action callbackAction) {
            var targetTime = (long)time.TotalMilliseconds;
            EventHandler<MediaPlayerTimeChangedEventArgs>? tempTimeChangedHandler = null;
            tempTimeChangedHandler = (s, e) => {
                if ((e.Time - targetTime) < 300 /* ms */) {
                    this.TimeChanged -= tempTimeChangedHandler;
                    this.SetPause(true);
                    callbackAction.Invoke();
                }
            };

            this.TimeChanged += tempTimeChangedHandler;
            this.SetPause(false);
            base.SeekTo(time);
        }

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
