using LibVLCSharp.Shared;

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Navigation;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = System.Windows.Forms.OpenFileDialog;
using System.Threading.Tasks;

namespace TurnipVodSplitter {
    public partial class MainWindow : Window {
        bool _isPaused = true;

        private bool canSplitVideo => this.viewModel.CanSplitVideo;

        private readonly Uri? _initialVod = null;
        private ScrubAttempt? _trynaScrub;

        private readonly DispatcherTimer _timer;

        public MainWindowViewModel viewModel;

        public MainWindow(Uri? initialVod = null) {
            InitializeComponent();

            this._initialVod = initialVod;

            // Bind a bunch of things in the viewModel;
            var debouncedResize = Debounce(resizePlayer);
            this.vlcVideoView.SizeChanged += delegate { debouncedResize(); };
            this.viewModel = this.DataContext as MainWindowViewModel ??
                             throw new InvalidOperationException("Invalid view model type.");
            this.viewModel.PropertyChanged += this.OnViewModelPropertyChanged;
            this.viewModel.VlcPlayer.EnableHardwareDecoding = true;
            this.viewModel.VlcPlayer.PositionChanged += (o, e) => Dispatcher.Invoke(delegate {
                onVideoPositionChanged(o, e);
            });
            bindVMCommands(this.viewModel);

            string? currentDirectory = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule?.FileName);
            this.viewModel.FfmpegPath = Downloader.FFMPEG_PATH;

            this.viewModel.VlcPlayer.Buffering += (o, e) => {
                Debug.WriteLine($"Buffering... {e.Cache}");
            };


            this.sliderMedia.PreviewMouseDown += delegate { Debug.WriteLine("PreviewMouseDown"); };
            this.sliderMedia.PreviewMouseUp += delegate { Debug.WriteLine("PreviewMouseUp");};


            if (_timer == null) {
                _timer = new DispatcherTimer();
                _timer.Tick += (o, e) => this.Dispatcher.Invoke(() => processDeferredScrubTick(o, e));
                _timer.Interval = new TimeSpan(0, 0, 0, 0, 300);
                _timer.Start();
            }
        }

        private void bindVMCommands(MainWindowViewModel model) {
            model.LoadVodFileCommand = this.LoadVodFileCommand;
            model.BeginConvertCommand = this.BeginConvertCommand;
            model.TogglePlayCommand = this.TogglePlayCommand;
            model.ShowAboutWindowCommand = this.ShowAboutWindowCommand;
        }

        private void onLoaded(object? sender, RoutedEventArgs e) {
            if (!File.Exists(this.viewModel.FfmpegPath)) {
                var downloader = new Downloader();
                this.IsEnabled = false;
                downloader.ShowDialog();
                this.IsEnabled = true;
            }

            if (!File.Exists(this.viewModel.FfmpegPath)) {
                MessageBox.Show(
                    $"Could not find ffmpeg @ {this.viewModel.FfmpegPath}\n. If the downloader isn't working, please download ffmpeg yourself and place ffmpeg.exe inside %LOCALAPPDATA%/TurnipVODSplitter",
                    "Turnip Vod Downloader", MessageBoxButton.OK, MessageBoxImage.Error);
                this.Close();
            }

            if (this._initialVod != null) {
                loadVodFile(this._initialVod);
            }
        }

        #region File Selection

        private void onTextFieldFocused(object? sender, EventArgs e) {
            this.viewModel.IsTextFieldFocused = true;

        }

        private void onTextFieldLostFocus(object? sender, EventArgs e) {
            this.viewModel.IsTextFieldFocused = false;
        }

        [RelayCommand]
        private void LoadVodFile() {
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

        private ICommand? _openVodHistoryCommand;
        public ICommand openVodHistoryCommand {
            get {
                if (_openVodHistoryCommand == null) {
                    _openVodHistoryCommand = new RelayCommand<string>((s) => {
                        if (s != null && File.Exists(s)) {
                            loadVodFile(new Uri(s));
                        }
                    });
                }

                return _openVodHistoryCommand;
            }
        }


        private void updateVodHistory(System.Uri uri) {
            var historyLimit = 5;
            var filePath = Uri.UnescapeDataString(uri.AbsolutePath);
            var history = this.viewModel.vodHistory;
            var existingFileIdx = history.IndexOf(filePath);

            if (existingFileIdx >= 0) {
                history.RemoveAt(existingFileIdx);
            }

            if (history.Count > historyLimit) {
                history.RemoveAt(historyLimit);
            }

            history.Insert(0, filePath);
            viewModel.PersistVodHistory();
        }

        public void loadVodFile(System.Uri uri, bool autoplay = false) {
            EventHandler<MediaPlayerMediaChangedEventArgs>? onMediaChanged = null;
            EventHandler<MediaParsedChangedEventArgs>? onMediaParsed = null;

            updateVodHistory(uri);

            onMediaParsed = delegate {
                if (this.viewModel.VlcPlayer.Media == null) { return; }

                this.viewModel.VlcPlayer.Media.ParsedChanged -= onMediaParsed;

                this.Dispatcher.Invoke(() => {
                    this.viewModel.VlcPlayer.Volume = 0;
                    this.viewModel.IsMediaLoaded = true;
                    resizePlayer();

                    EventHandler<MediaPlayerPositionChangedEventArgs>? onMediaPlayingFirstTime = null;
                    onMediaPlayingFirstTime = delegate {
                        ThreadPool.QueueUserWorkItem(delegate {
                            this.viewModel.VlcPlayer.SetPause(true);
                            this.viewModel.VlcPlayer.PositionChanged -= onMediaPlayingFirstTime;
                        });
                    };

                    ThreadPool.QueueUserWorkItem(delegate {
                        if (autoplay) {
                            this._isPaused = false;
                        } else {
                            this.viewModel.VlcPlayer.PositionChanged += onMediaPlayingFirstTime;
                            this._isPaused = true;
                        }
                        this.viewModel.VlcPlayer.Play();
                    });
                });
            };

            onMediaChanged = delegate {
                this.IsEnabled = true;
                this.viewModel.VlcPlayer.MediaChanged -= onMediaChanged;
                var media = this.viewModel.VlcPlayer.Media;
                if (media == null) { return; }
                media.ParsedChanged += onMediaParsed;
                media.Parse();
            };
            
            this.viewModel.MediaContentPath = Uri.UnescapeDataString(uri.AbsolutePath);
            this.viewModel.VlcPlayer.MediaChanged += onMediaChanged;
            this.IsEnabled = false;
            this.viewModel.VlcPlayer.Media?.Dispose();
            this.viewModel.VlcPlayer.Media = new Media(
                this.viewModel.VlcPlayer.libvlc,
                uri, "--start-paused", "--input-fast-seek");

        }

        private string? chooseConvertOutputDir() {
            var startPath = Properties.Settings.Default.lastOutputDirectory ?? "";
            using var dialog = new FolderBrowserDialog() {SelectedPath = startPath};

            dialog.ShowNewFolderButton = true;
            var result = dialog.ShowDialog();

            if (result != System.Windows.Forms.DialogResult.OK) return null;

            Properties.Settings.Default.lastOutputDirectory = dialog.SelectedPath;
            Properties.Settings.Default.Save();
            return dialog.SelectedPath;
        }

        [RelayCommand(CanExecute = nameof(canSplitVideo))]
        public void BeginConvert() {
            if (this.viewModel.IsMediaLoaded == false) {
                return;
            }

            string? dir;
            if ((dir = chooseConvertOutputDir()) == null) {
                return;
            }

            this.viewModel.VlcPlayer.SetPause(true);
            this._isPaused = true;

            var converterWindow = new ConverterWindow(
                this.viewModel.FfmpegPath,
                this.viewModel.Splits,
                this.viewModel.MediaContentPath,
                dir,
                this.viewModel.EventName
            );

            this.IsEnabled = false;
            converterWindow.ShowDialog();
            this.IsEnabled = true;
        }

        #endregion


        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs propertyChangedEventArgs) {
            // Maybe we should enable the split video button.
            this.Dispatcher.Invoke(delegate {
                if (this.viewModel.MediaContentPath == null) {
                    return;
                }

                if (this.viewModel.Splits.Splits.Count == 0) {
                    return;
                }

                if (this.viewModel.FfmpegPath == null) {
                    return;
                }

                this.viewModel.CanSplitVideo = true;
                this.BeginConvertCommand.NotifyCanExecuteChanged();
            });
        }


        #region Video Player Logic


        [RelayCommand]
        private void TogglePlay() {
            onPlayClick(this, new RoutedEventArgs());
        }

        private void onPlayClick(object? sender, RoutedEventArgs e) {
            if (!this.viewModel.IsMediaLoaded) {
                return;
            }

            switch (this.viewModel.VlcPlayer.State) {
                case VLCState.Playing:
                    this._isPaused = true;
                    this.viewModel.VlcPlayer.SetPause(true);
                    break;
                case VLCState.Ended:
                    loadVodFile(new Uri(this.viewModel.MediaContentPath), true);
                    break;
                case VLCState.NothingSpecial:
                case VLCState.Opening:
                case VLCState.Buffering:
                case VLCState.Paused:
                case VLCState.Stopped:
                case VLCState.Error:
                default:
                    this._isPaused = false;
                    this.viewModel.VlcPlayer.SetPause(false);
                    break;
            }
        }
        public Action Debounce(Action func, int milliseconds = 300) {
            var last = 0;
            return () =>
            {
                var current = Interlocked.Increment(ref last);
                Task.Delay(milliseconds).ContinueWith(task =>
                {
                    if (current == last) {
                        Dispatcher.Invoke(func);
                    }
                    task.Dispose();
                });
            };
        }

        private void resizePlayer() {
            if (!(this.viewModel.VlcPlayer.Media?.IsParsed ?? false)) { return; }

            Debounce(delegate {
                Debug.WriteLine("resizing...");
                uint vidX = 0;
                uint vidY = 0;
                this.viewModel.VlcPlayer.Size(0, ref vidX, ref vidY);
                if (vidX == 0 || vidY == 0) { return; }
                double yRatio = this.vlcVideoView.ActualHeight / vidY;
                uint actualX = (uint)Math.Floor(vidX * yRatio);
                this.vlcVideoView.Width = actualX;
            })();

        }


        private bool _seeking = false;
        private void processDeferredScrubTick(object? sender, EventArgs e) {

            // If VLC is moving, don't make it move again. If we have no scrub attempts, nothing to be done.
            if (this._seeking || this._trynaScrub == null) return;

            Debug.WriteLine($@"{this._trynaScrub}");

            // Save this so that it doesn't get overwritten during our seek.
            var scrub = this._trynaScrub;
            this._trynaScrub = null;
            this._seeking = true;

            this.viewModel.VlcPlayer.SeekTo(scrub.position);
            if (scrub.dragEnded) {
                this.viewModel.VlcPlayer.SetPause(_isPaused);
            }
            this._seeking = false;
        }


        private void onVideoPositionChanged(object? sender, MediaPlayerPositionChangedEventArgs e) {
            if (this._trynaScrub == null && !this._seeking) {
                this.sliderMedia.ValueChanged -= onVideoScrubberPositionChanged;
                this.sliderMedia.Value = this.viewModel.VlcPlayer.Position;
                this.sliderMedia.ValueChanged += onVideoScrubberPositionChanged;
            }
        }


        private void endVideoScrubberDrag() {
            this.viewModel.VlcPlayer.SetPause(true);
            scrubNow(true);
        }

        private void startVideoScrubberDrag() {
            this.viewModel.VlcPlayer.SetPause(true);
        }


        private void onVideoScrubberPreviewMouseUp(object? sender, EventArgs e) {
            Debug.WriteLine("Mouse up");
            endVideoScrubberDrag();

        }

        private void onVideoScrubberDragStarted(object? sender, DragStartedEventArgs e) {
            Debug.WriteLine("Drag Start");
            startVideoScrubberDrag();
        }

        private void scrubNow(bool endDrag = false) {
            var len = this.viewModel.VlcPlayer.Length;
            var ratio = this.sliderMedia.Value;

            // Elaborate L + ratio joke
            this._trynaScrub = new ScrubAttempt((long)(Math.Floor(len * ratio)), endDrag);
        }

        private void onVideoScrubberPositionChanged(object sender, RoutedPropertyChangedEventArgs<double> routedPropertyChangedEventArgs) {
            scrubNow();
        }

        private void NavigateToFfmpegHelp(object sender, RequestNavigateEventArgs e) {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) {UseShellExecute = true});
            e.Handled = true;
        }
        #endregion


        [RelayCommand]
        void ShowAboutWindow() {
            var aboutWindow = new AboutWindow();
            this.viewModel.VlcPlayer.SetPause(true);
            this._isPaused = true;
            this.IsEnabled = false;
            aboutWindow.ShowDialog();
            this.IsEnabled = true;
        }
    }
}
