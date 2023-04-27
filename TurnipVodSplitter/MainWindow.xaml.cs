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
        private ScrubAttempt _trynaScrub = null;
        private SplitEntry _currentSplit = null;
        private readonly DispatcherTimer _timer = null;

        private MainWindowViewModel viewModel => this.DataContext as MainWindowViewModel;

        public MainWindow() {
            InitializeComponent();

            this.viewModel.outputDirectory = Properties.Settings.Default.lastOutputDirectory ?? "";
            this.viewModel.splits = new BindingList<SplitEntry>();
            this.viewModel.PropertyChanged += this.OnViewModelPropertyChanged;
            this.viewModel.vlcMediaPlayer.EnableHardwareDecoding = true;
            this.viewModel.vlcMediaPlayer.PositionChanged += onVideoPositionChanged;

            string currentDirectory = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
            this.viewModel.ffmpegPath = Downloader.FFMPEG_PATH;


            if (_timer == null) {
                _timer = new DispatcherTimer();
                _timer.Tick += (o, e) => this.Dispatcher.Invoke(() => processDeferredScrubTick(o, e));
                _timer.Interval = new TimeSpan(0, 0, 0, 0, 300);
                _timer.Start();
            }
        }

        private void onLoaded(object sender, RoutedEventArgs e) {
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

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e) {
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
        private void onTextFieldFocused(object sender, EventArgs e) {
          this.viewModel.isTextFieldFocused = true;

        }

        private void onTextFieldLostFocus(object sender, EventArgs e) {
          this.viewModel.isTextFieldFocused = false;
        }

        private void onOpenFileClick(object sender, RoutedEventArgs e) {
            OpenFileDialog openFileDialog = new OpenFileDialog() {
                RestoreDirectory = true,
                DereferenceLinks = false,
                ValidateNames = false,
            };

            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
                System.Uri uri = null;
                System.Uri.TryCreate(openFileDialog.FileName, UriKind.Absolute, out uri);
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

        private void onChooseOutputDirClick(object sender, RoutedEventArgs e) {
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

        private void onBeginConvertClick(object sender, RoutedEventArgs e) {
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


        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs propertyChangedEventArgs) {
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
        private void onPlayClick(object sender, RoutedEventArgs e) {
            if (this.viewModel.vlcMediaPlayer.IsPlaying) {
                this._isPaused = false;
                this.viewModel.vlcMediaPlayer.Play();
            } else {
                this._isPaused = true;
                this.viewModel.vlcMediaPlayer.Pause();
            }
        }

        private void onMediaChanged(object sender, MediaPlayerMediaChangedEventArgs e) {
            this.IsEnabled = true;
            this.viewModel.vlcMediaPlayer.MediaChanged -= onMediaChanged;
            this.viewModel.vlcMediaPlayer.Volume = 0;
            this.viewModel.vlcMediaPlayer.Play();
            this._isPaused = false;
        }


        private void onSeekCompleted(object sender, EventArgs e) {
            this.Dispatcher.Invoke(() => {
                // Only ever called one at a time... after calling SeekTo()
                this.viewModel.vlcMediaPlayer.PositionChanged -= onSeekCompleted;
                if (!this._isPaused) {
                    this.viewModel.vlcMediaPlayer.Play();
                }

                this.sliderMedia.Value = this.viewModel.vlcMediaPlayer.Position;
            });
        }

        private void processDeferredScrubTick(object sender, EventArgs e) {
            if (this._trynaScrub != null) {
                Console.WriteLine($@"{this._trynaScrub}");
                var pos = this._trynaScrub.position;
                this._trynaScrub = null;

                if (!this._isPaused) {
                    this.viewModel.vlcMediaPlayer.Pause();
                }

                this._videoScrubberIgnoreUpdates = false;

                this.viewModel.vlcMediaPlayer.PositionChanged += onSeekCompleted;
                this.viewModel.vlcMediaPlayer.SeekTo(pos);
            }
        }


        private void onVideoPositionChanged(object sender, MediaPlayerPositionChangedEventArgs e) {
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

        private void onVideoScrubberDragLeave(object sender, DragCompletedEventArgs dragCompletedEventArgs) {
            endVideoScrubberDrag();
        }

        private void onVideoScrubberDragStarted(object sender, DragStartedEventArgs e) {
            startVideoScrubberDrag();

        }

        private void onVideoScrubberMouseDown(object sender, MouseButtonEventArgs e) {
            startVideoScrubberDrag();
        }

        private void onVideoScrubberMouseUp(object sender, MouseButtonEventArgs e) {
            endVideoScrubberDrag();
        }
        #endregion

        #region Split Recording

        private SplitEntry newSplitAtCurrentTime() {
                return new SplitEntry {
                    splitStart = TimeSpan.FromMilliseconds(this.viewModel.vlcMediaPlayer.Time),
                };
        }

        private void onSplitSelectionChanged(object sender, SelectionChangedEventArgs e) {
           this._currentSplit = e.AddedItems[0] as SplitEntry;
        }

        private void onEndSplitClick(object sender, RoutedEventArgs e) {
            SplitEntry newSplit;
            if (_currentSplit == null) {
                newSplit = new SplitEntry {
                    splitStart = TimeSpan.Zero,
                    splitEnd = TimeSpan.FromMilliseconds(this.viewModel.vlcMediaPlayer.Time)
                };
                _currentSplit = newSplit;
                this.viewModel.splits.Add(newSplit);

            }
            else {
                int splitIdx = this.viewModel.splits.IndexOf(_currentSplit);
                this._currentSplit.splitEnd = TimeSpan.FromMilliseconds(this.viewModel.vlcMediaPlayer.Time);

                if (splitIdx == 0) {
                    this._currentSplit.splitStart = TimeSpan.Zero;
                } else {
                    // We're not the first split, so steal the beginning from the last split we made.
                    if (this._currentSplit.splitStart == TimeSpan.Zero) {
                        this._currentSplit.splitStart = this.viewModel.splits[splitIdx - 1].splitEnd;
                    }

                    if (splitIdx == this.viewModel.splits.Count - 1) { // Last split
                        this._currentSplit = new SplitEntry();
                        this.dgSplits.SelectedItem = this._currentSplit;
                    }
                    else {
                        this._currentSplit = this.viewModel.splits[splitIdx + 1];
                        this.dgSplits.SelectedItem = this._currentSplit;
                    }
                }
            }
        }

        private void onBeginSplitClick(object sender, RoutedEventArgs e) {
            if (_currentSplit == null) {
              var newSplit = newSplitAtCurrentTime();
                this.viewModel.splits.Add(newSplit);
                this.dgSplits.SelectedItem = newSplit;
            } else {
                _currentSplit.splitStart = TimeSpan.FromMilliseconds(this.viewModel.vlcMediaPlayer.Time);
            }
        }

        private async void onSaveSplitsClicked(object sender, RoutedEventArgs e) {
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

        private void onLoadSplitsClicked(object sender, RoutedEventArgs e) {
            OpenFileDialog openFileDialog = new OpenFileDialog() {
                RestoreDirectory = true,
                DereferenceLinks = false,
                ValidateNames = false,
                Multiselect = false
            };

            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
                System.Uri uri = null;
                System.Uri.TryCreate(openFileDialog.FileName, UriKind.Absolute, out uri);
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
