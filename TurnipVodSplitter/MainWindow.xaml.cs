using LibVLCSharp.Shared;
using LibVLCSharp.WPF;

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Threading;
using CsvHelper;
using CsvHelper.Configuration;
using TurnipVodSplitter.Properties;
using DragEventArgs = System.Windows.DragEventArgs;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = System.Windows.Forms.OpenFileDialog;

namespace TurnipVodSplitter {
    public partial class MainWindow : Window {
        bool _isPaused = true;
        private ScrubAttempt? _trynaScrub;

        private SplitEntry? _currentSplit;

        private SplitEntry? currentSplit {
            get => _currentSplit;
            set {
                _currentSplit = value;
                this.dgSplits.SelectedIndex = this.viewModel.splits.IndexOf(_currentSplit);

            }
        }

        private readonly DispatcherTimer _timer;

        public MainWindowViewModel viewModel;

        public MainWindow() {
            InitializeComponent();

            this.viewModel = this.DataContext as MainWindowViewModel ?? throw new InvalidOperationException("Invalid view model type.");
            this.viewModel.outputDirectory = Properties.Settings.Default.lastOutputDirectory ?? "";
            this.viewModel.PropertyChanged += this.OnViewModelPropertyChanged;
            this.viewModel.vlcMediaPlayer.EnableHardwareDecoding = true;
            this.viewModel.vlcMediaPlayer.PositionChanged += onVideoPositionChanged;

            string? currentDirectory = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule?.FileName);
            this.viewModel.ffmpegPath = Downloader.FFMPEG_PATH;


            if (_timer == null) {
                _timer = new DispatcherTimer();
                _timer.Tick += (o, e) => this.Dispatcher.Invoke(() => processDeferredScrubTick(o, e));
                _timer.Interval = new TimeSpan(0, 0, 0, 0, 300);
                _timer.Start();
            }
        }

        private void onLoaded(object? sender, RoutedEventArgs e) {
            if (!File.Exists(this.viewModel.ffmpegPath)) {
                var downloader = new Downloader();
                downloader.ShowDialog();
            }

            if (!File.Exists(this.viewModel.ffmpegPath)) {
                MessageBox.Show($"Could not find ffmpeg @ {this.viewModel.ffmpegPath}\n. If the downloader isn't working, please download ffmpeg yourself and place ffmpeg.exe inside %LOCALAPPDATA%/TurnipVODSplitter",
                    "Turnip Vod Downloader", MessageBoxButton.OK, MessageBoxImage.Error);
                this.Close();
            }
        }

        private void Window_PreviewKeyDown(object? sender, KeyEventArgs e) {
          if (!this.viewModel.isTextFieldFocused) {
            if (e.Key == Key.Enter) {
              onEndSplitClick(sender, e);
              e.Handled = true;
            }

            if (e.Key == Key.Space) {
              onPlayClick(sender, e);
              e.Handled = true;
            }
          }
        }


        #region File Selection
        private void onTextFieldFocused(object? sender, EventArgs e) {
          this.viewModel.isTextFieldFocused = true;

        }

        private void onTextFieldLostFocus(object? sender, EventArgs e) {
          this.viewModel.isTextFieldFocused = false;
        }

        private void onOpenFileClick(object? sender, RoutedEventArgs e) {
            OpenFileDialog openFileDialog = new OpenFileDialog() {
                RestoreDirectory = true,
                DereferenceLinks = false,
                ValidateNames = false,
            };

            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
                System.Uri? uri = null;
                System.Uri.TryCreate(openFileDialog.FileName, UriKind.Absolute, out uri);

                if (uri == null) {
                    throw new InvalidOperationException($"Couldn't open the file at {openFileDialog.FileName}");
                }
                loadVodFile(uri);
            }
        }

        private void loadVodFile(System.Uri uri) {
            this.viewModel.mediaContentPath = Uri.UnescapeDataString(uri.AbsolutePath);
            this.viewModel.vlcMediaPlayer.MediaChanged += onMediaChanged;
            this.IsEnabled = false;
            this.viewModel.vlcMediaPlayer.Media = new Media(MainWindowViewModel.libVlc, uri);
            this.viewModel.splits.Clear();
        }

        private void onChooseOutputDirClick(object? sender, RoutedEventArgs e) {
            var startPath = Properties.Settings.Default.lastOutputDirectory ?? "";
            using var dialog = new FolderBrowserDialog() {SelectedPath = startPath};

            dialog.ShowNewFolderButton = true;
            DialogResult result = dialog.ShowDialog();

            if (result == System.Windows.Forms.DialogResult.OK) {
                this.viewModel.outputDirectory = dialog.SelectedPath;
                Properties.Settings.Default.lastOutputDirectory = dialog.SelectedPath;
                Properties.Settings.Default.Save();
            }
        }

        private void onBeginConvertClick(object? sender, RoutedEventArgs e) {
            if (this.viewModel.vlcMediaPlayer.CanPause) {
                this.viewModel.vlcMediaPlayer.Pause();
            }

            var converterWindow = new ConverterWindow(
                this.viewModel.ffmpegPath,
                this.viewModel.splits,
                this.viewModel.mediaContentPath,
                this.viewModel.outputDirectory,
                this.viewModel.eventName
            );

            this.IsEnabled = false;
            converterWindow.ShowDialog();
            this.IsEnabled = true;
        }

        #endregion


        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs propertyChangedEventArgs) {
            // Maybe we should enable the split video button.
            if (this.viewModel.outputDirectory == null) {
                return;
            }

            if (this.viewModel.mediaContentPath == null) {
                return;
            }

            if (this.viewModel.splits.Count == 0) {
                return;
            }

            if (this.viewModel.ffmpegPath == null) {
                return;
            }

            this.btnBeginConvert.IsEnabled = true;
        }


        #region Video Player Logic
        private void onPlayClick(object? sender, RoutedEventArgs e) {
            if (this.viewModel.vlcMediaPlayer.IsPlaying) {
                this._isPaused = true;
                this.viewModel.vlcMediaPlayer.Pause();
            } else {
                this._isPaused = false;
                this.viewModel.vlcMediaPlayer.Play();
            }
        }

        private void onMediaChanged(object? sender, MediaPlayerMediaChangedEventArgs e) {
            this.IsEnabled = true;
            this.viewModel.vlcMediaPlayer.MediaChanged -= onMediaChanged;
            this.viewModel.vlcMediaPlayer.Volume = 0;
            this.viewModel.vlcMediaPlayer.Play();
            this._isPaused = false;
        }


        private void onSeekCompleted(object? sender, EventArgs e) {
            this.Dispatcher.Invoke(() => {
                // Only ever called one at a time... after calling SeekTo()
                this.viewModel.vlcMediaPlayer.PositionChanged -= onSeekCompleted;
                this.sliderMedia.Value = this.viewModel.vlcMediaPlayer.Position;
            });
        }

        private void processDeferredScrubTick(object? sender, EventArgs e) {
            if (this._trynaScrub != null) {
                Console.WriteLine($@"{this._trynaScrub}");
                var pos = this._trynaScrub.position;
                this._trynaScrub = null;

                this._videoScrubberIgnoreUpdates = false;

                this.viewModel.vlcMediaPlayer.PositionChanged += onSeekCompleted;
                this.viewModel.vlcMediaPlayer.TimeChanged += (o, e) => {
                    Console.WriteLine("TimeChanged was called");
                };

                this.viewModel.vlcMediaPlayer.SeekableChanged += (o, e) => {
                    Console.WriteLine("SeekableChanged was fired.");
                };

                this.viewModel.vlcMediaPlayer.SeekTo(pos);
            }
        }


        private void onVideoPositionChanged(object? sender, MediaPlayerPositionChangedEventArgs e) {
            if (!this._videoScrubberIgnoreUpdates) {
                this.Dispatcher.Invoke(() => {
                    this.sliderMedia.Value = this.viewModel.vlcMediaPlayer.Position;
                });
            }
        }


        private bool _videoScrubberIgnoreUpdates = false;
        private void endVideoScrubberDrag() {
            var len = this.viewModel.vlcMediaPlayer.Length;
            var ratio = this.sliderMedia.Value;

            // Elaborate L + ratio joke
            this._trynaScrub = new ScrubAttempt((long)(Math.Floor(len * ratio)));
        }

        private void startVideoScrubberDrag() {
            _videoScrubberIgnoreUpdates = true;

        }

        private void onVideoScrubberDragLeave(object? sender, DragCompletedEventArgs dragCompletedEventArgs) {
            endVideoScrubberDrag();
        }

        private void onVideoScrubberDragStarted(object? sender, DragStartedEventArgs e) {
            startVideoScrubberDrag();

        }

        private void onVideoScrubberMouseDown(object? sender, MouseButtonEventArgs e) {
            startVideoScrubberDrag();
        }

        private void onVideoScrubberMouseUp(object? sender, MouseButtonEventArgs e) {
            endVideoScrubberDrag();
        }
        #endregion

        #region Split Recording

        private SplitEntry newSplitAtCurrentTime() {
                return new SplitEntry {
                    splitStart = TimeSpan.FromMilliseconds(this.viewModel.vlcMediaPlayer.Time),
                };
        }

        private void onSplitSelectionChanged(object? sender, SelectionChangedEventArgs e) {
            if (e.AddedItems.Count > 0) {
                this.currentSplit = e.AddedItems[0] as SplitEntry;
            }

            this._wasEndSplitClickedTwice = false;
        }


        private bool _wasEndSplitClickedTwice = false;
        private void onEndSplitClick(object? sender, RoutedEventArgs e) {
            /*
             * Three cases here
             *   - We're the first split, so we make a new one and set start to 0 and end to NOW
             *   - We're an unremarkable split so we simply update the end time of the current split
             *   - The button has been clicked twice in a row without the user interacting the the split datagrid.
             *     This means we should me "smart" and create a new split spanning
             *             [current split's end] <----------> [Video Player Current Time]
             *
             *     ADDITIONALLY, we should only be "smart" if the current split is the latest one.
             *
             */

            var currentPlayerTime = TimeSpan.FromMilliseconds(this.viewModel.vlcMediaPlayer.Time);
            int splitIdx;
            SplitEntry newSplit;

            if (currentSplit == null) {
                newSplit = new SplitEntry {
                    splitStart = TimeSpan.Zero,
                    splitEnd = TimeSpan.FromMilliseconds(this.viewModel.vlcMediaPlayer.Time)
                };
                currentSplit = newSplit;
                this.viewModel.splits.Add(newSplit);
            } else {
                splitIdx = this.viewModel.splits.IndexOf(currentSplit);

                if (this._wasEndSplitClickedTwice && isCurrentSplitLast()) {

                    newSplit = new SplitEntry {
                        splitStart = this.currentSplit.splitEnd,
                        splitEnd = currentPlayerTime
                    };
                    currentSplit = newSplit;
                    this.viewModel.splits.Add(newSplit);

                } else {
                    this.currentSplit.splitEnd = currentPlayerTime;
                    if (!isCurrentSplitLast()) {
                        this.currentSplit = this.viewModel.splits[splitIdx + 1];
                    }
                }

            }

            splitIdx = this.viewModel.splits.IndexOf(this.currentSplit);
            this.viewModel.splits[splitIdx] = null;
            this.viewModel.splits[splitIdx] = currentSplit;
            this.dgSplits.SelectedIndex = splitIdx;
            this._wasEndSplitClickedTwice = true;
            this.dgSplits.ItemsSource = viewModel.splits;
        }

        private bool isCurrentSplitLast() {
            return this.viewModel.splits.IndexOf(this.currentSplit) == this.viewModel.splits.Count - 1;
        }

        private void onBeginSplitClick(object? sender, RoutedEventArgs e) {
            this._wasEndSplitClickedTwice = false;

            if (currentSplit == null) {
              var newSplit = newSplitAtCurrentTime();
                this.viewModel.splits.Add(newSplit);
                this.dgSplits.SelectedItem = newSplit;
            } else {
                int splitIdx = this.viewModel.splits.IndexOf(currentSplit);
                currentSplit.splitStart = TimeSpan.FromMilliseconds(this.viewModel.vlcMediaPlayer.Time);
                this.viewModel.splits[splitIdx] = null;
                this.viewModel.splits[splitIdx] = currentSplit;
            }

        }

        private async void onSaveSplitsClicked(object? sender, RoutedEventArgs e) {
            SaveFileDialog saveFileDialog = new SaveFileDialog() {
                RestoreDirectory = true,
                DereferenceLinks = false,
                ValidateNames = false,
            };

            if (saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
                StreamWriter outputStream = new StreamWriter(saveFileDialog.OpenFile());
                using var csvWriter = new CsvWriter(outputStream, CultureInfo.InvariantCulture);
                csvWriter.Context.RegisterClassMap(new SplitEntryFieldMap());
                await csvWriter.WriteRecordsAsync<SplitEntry>(this.viewModel.splits);
                await csvWriter.FlushAsync();
            }
        }

        private void onLoadSplitsClicked(object? sender, RoutedEventArgs e) {
            OpenFileDialog openFileDialog = new OpenFileDialog() {
                RestoreDirectory = true,
                DereferenceLinks = false,
                ValidateNames = false,
                Multiselect = false
            };

            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
                StreamReader inputStream = new StreamReader(openFileDialog.OpenFile());
                using var csvReader = new CsvReader(inputStream, CultureInfo.InvariantCulture);
                csvReader.Context.RegisterClassMap(new SplitEntryFieldMap());

                this.viewModel.splits.Clear();
                foreach (var split in csvReader.GetRecords<SplitEntry>()) {
                    this.viewModel.splits.Add(split);
                }
            }
        }

        #endregion

    }
}
