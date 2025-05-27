using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LibVLCSharp.Shared;
using TurnipVodSplitter.Properties;

namespace TurnipVodSplitter {
    public partial class MainWindowViewModel : ObservableObject {
        public MainWindowViewModel() {
            this.VlcPlayer.LengthChanged += delegate {
                ThreadPool.QueueUserWorkItem(delegate {
                    this.Splits.MediaLength = TimeSpan.FromMilliseconds(this.VlcPlayer.Length);
                });
            };

            this.VlcPlayer.TimeChanged += (o, e) => {
                this.MediaPosition = e.Time;
                this.OnPropertyChanged(nameof(CurrentSplit));
                this.OnPropertyChanged(nameof(CurrentSplitIdx));
            };

            this.VlcPlayer.StateChanged += (o, e) => {
                this.PlayerState = e.State;
            };
        }

        public readonly System.Threading.Lock PlayerLock = new();

        [ObservableProperty]
        private VlcMediaPlayer vlcPlayer = new() {EnableHardwareDecoding = true};


        [ObservableProperty]
        private SplitCollection splits = new();

        [ObservableProperty]
        private long mediaDuration = -1;

        [ObservableProperty]
        private long mediaPosition = -1;

        public SplitEntry? CurrentSplit {
            get {
                return this.Splits.At(this.VlcPlayer.PositionTs);
            }
        }

        public int CurrentSplitIdx => this.Splits.GetSplitIndexForTime(this.VlcPlayer.PositionTs);

        [ObservableProperty] private string? mediaContentPath;

        partial void OnMediaContentPathChanging(string? value) {
            if (value != null) {
                Settings.Default.lastVodLoaded = value;
                Settings.Default.Save();
            }
        }

        [ObservableProperty]
        private string ffmpegPath = "";


        [ObservableProperty]
        private string eventName = "";

        [ObservableProperty]
        private bool isTextFieldFocused = false;

        [ObservableProperty]
        private bool isMediaLoaded = false;

        [ObservableProperty] private bool canSplitVideo = false;

        [ObservableProperty] private VLCState playerState = VLCState.NothingSpecial;

        private ObservableCollection<string>? _vodHistory = null;
        public ObservableCollection<string> vodHistory {
            get {
                if (_vodHistory == null) {
                    var propVods = Properties.Settings.Default.recentVods;
                    if (propVods == null) {
                        _vodHistory = [];
                    } else {
                        _vodHistory = new ObservableCollection<string>(propVods.Cast<string>());
                    }
                }

                return _vodHistory;
            }
        }

        public Dispatcher? dispatcher = null;



        [RelayCommand]
        public void ClearVodHistory() {
            vodHistory.Clear();
            PersistVodHistory();
        }

        [RelayCommand]
        public void SplitNow() {
            var position =
                TimeSpan.FromMilliseconds(Math.Floor(this.VlcPlayer.Position * this.VlcPlayer.Length));
            this.Splits.NewSplitAtPoint(position);
        }

        [RelayCommand]
        public void DeleteSplit() {
            this.Splits.DeleteSplitAtPoint(this.VlcPlayer.PositionTs);
        }

        [RelayCommand]
        public void SaveYoutubeToClipboard() {
            System.Windows.Clipboard.SetText(this.Splits.YoutubeChapterFormat(""));
        }

        [RelayCommand]
        public async Task SaveSplits()  {
            SaveFileDialog saveFileDialog = new SaveFileDialog() {
                RestoreDirectory = true,
                DereferenceLinks = false,
                ValidateNames = false,
                Filter = "CSV Files|*.csv",
                AddExtension = true,
                SupportMultiDottedExtensions = true
            };

            if (saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
                await this.Splits.saveToFile(saveFileDialog.OpenFile());

            }
        }

        [RelayCommand]
        public async Task LoadSplits() {
            var openFileDialog = new OpenFileDialog() {
                RestoreDirectory = true,
                DereferenceLinks = false,
                ValidateNames = false,
                Multiselect = false,
                Filter = "CSV Files|*.csv",
                SupportMultiDottedExtensions = true
            };

            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
                await this.Splits.LoadFromFile(openFileDialog.OpenFile());
            }
        }

        [GeneratedRegex(@"\{}\\", RegexOptions.IgnoreCase)]
        private static partial Regex BannedColumnChars();

        [RelayCommand]
        public void AddColumn() {
            var addColDialog = new AddColumnDialog();

            if (!(addColDialog.ShowDialog() ?? false)) return;
            var name = addColDialog.NewColumnName;
            if (name == null) { return; }

            name = BannedColumnChars().Replace(name, "");
            this.AddColumn(name);
        }

        public void AddColumn(string name) {
            this.Splits.AddProperty(name);
        }

        [RelayCommand]
        public void DeleteColumn(DataGridColumn column) {
            this.Splits.DeleteProperty(column.Header as string ?? "");
        }


        // Set externally
        [ObservableProperty] private ICommand? loadVodFileCommand;
        [ObservableProperty] private ICommand? showAboutWindowCommand;
        [ObservableProperty] private ICommand? beginConvertCommand;
        [ObservableProperty] private ICommand? togglePlayCommand;

        public void PersistVodHistory() {
            Properties.Settings.Default.recentVods = new StringCollection();
            Properties.Settings.Default.recentVods.AddRange(vodHistory.ToArray());
        }

        public new event PropertyChangedEventHandler? PropertyChanged;

        protected new virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null) {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected void expireProperties() {
            this.OnPropertyChanged();
        }
    }
}
