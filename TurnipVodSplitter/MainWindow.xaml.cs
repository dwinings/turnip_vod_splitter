using LibVLCSharp.Shared;
using LibVLCSharp.WPF;

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Threading;
using DragEventArgs = System.Windows.DragEventArgs;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace TurnipVodSplitter {
    public partial class MainWindow : Window {

        bool _isPaused = true;
        private ScrubAttempt _trynaScrub = null;
        private SplitEntry _currentSplit = null;
        private DispatcherTimer dispatcher = null;

        private MainWindowViewModel viewModel => this.DataContext as MainWindowViewModel;

        public MainWindow() {
            InitializeComponent();
            this.viewModel.splits = new ObservableCollection<SplitEntry>();
            this.viewModel.PropertyChanged += this.OnViewModelPropertyChanged;
            this.viewModel.vlcMediaPlayer.EnableHardwareDecoding = true;
            this.viewModel.vlcMediaPlayer.PositionChanged += onVideoPositionChanged;

            string currentDirectory = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
            this.viewModel.ffmpegPath = Downloader.FullPath;

            if (dispatcher == null) {
                dispatcher = new DispatcherTimer();
                dispatcher.Tick += (o, e) => this.Dispatcher.Invoke(() => processDeferredScrubTick(o, e));
                dispatcher.Interval = new TimeSpan(0, 0, 0, 0, 1000);
                dispatcher.Start();
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter) {
                onRecordSplitClick(sender, e);
                e.Handled = true;
            }

            if (e.Key == Key.Space) {
                onPlayClick(sender, e);
                e.Handled = true;
            }
        }

        private void onOpenFileClick(object sender, RoutedEventArgs e) {
            OpenFileDialog openFileDialog = new OpenFileDialog() {
                RestoreDirectory = true,
                DereferenceLinks = false,
                ValidateNames = false,
            };

            if (openFileDialog.ShowDialog() == true) {
                System.Uri uri = null;
                System.Uri.TryCreate(openFileDialog.FileName, UriKind.Absolute, out uri);
                this.viewModel.mediaContentPath = openFileDialog.FileName;
                this.viewModel.vlcMediaPlayer.MediaChanged += onMediaChanged;
                this.IsEnabled = false;
                this.vlcLoadingSpinner.Visibility = Visibility.Visible;
                this.vlcLoadingSpinner.SpinDuration = 1;

                this.Dispatcher.InvokeAsync(() => { this.viewModel.vlcMediaPlayer.Media = new Media(MainWindowViewModel.libVlc, uri); });
            }

            this.viewModel.splits.Clear();
            this.tbFfmpegOutput.Text = "";

        }

        private void onMediaChanged(object sender, MediaPlayerMediaChangedEventArgs e) {
            this.IsEnabled = true;
            this.viewModel.vlcMediaPlayer.MediaChanged -= onMediaChanged;
            this.viewModel.vlcMediaPlayer.Play();
            this.vlcLoadingSpinner.Visibility = Visibility.Hidden;
            this.vlcLoadingSpinner.SpinDuration = 0;
        }


        private void onPlayClick(object sender, RoutedEventArgs e) {
            if (this._isPaused) {
                this.viewModel.vlcMediaPlayer.Play();
            } else {
                this.viewModel.vlcMediaPlayer.Pause();
            }

            this._isPaused = !this._isPaused;
        }

        private void onRecordSplitClick(object sender, RoutedEventArgs e) {
            SplitEntry newSplit;
            if (_currentSplit == null) {
                newSplit = new SplitEntry {
                    splitStart = TimeSpan.Zero,
                    splitEnd = TimeSpan.FromMilliseconds(this.viewModel.vlcMediaPlayer.Time),
                    context = this.viewModel,
                };

            } else {
                newSplit = new SplitEntry {
                    splitStart = this._currentSplit.splitEnd,
                    splitEnd = TimeSpan.FromMilliseconds(this.viewModel.vlcMediaPlayer.Time),
                };

            }

            _currentSplit = newSplit;
            this.viewModel.splits.Add(newSplit);
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
        private void onChooseOutputDirClick(object sender, RoutedEventArgs e) {
            using (var dialog = new FolderBrowserDialog()) {
                dialog.ShowNewFolderButton = true;
                DialogResult result = dialog.ShowDialog();

                if (result == System.Windows.Forms.DialogResult.OK) {
                    this.viewModel.outputDirectory = dialog.SelectedPath;
                }
            }
        }

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

            this.btnEngageSplit.IsEnabled = true;
        }

        private void onEngageSplitClick(object sender, RoutedEventArgs e) {
            this.tbFfmpegOutput.Text = "";

            foreach (var split in this.viewModel.splits) {
                var extension = Path.GetExtension(this.viewModel.mediaContentPath);
                var outputFile = Path.Combine(this.viewModel.outputDirectory, $"[{this.viewModel.eventName}]{split.splitName}{extension}");

                string args = $"-hide_banner -loglevel info -nostats -y -i \"{this.viewModel.mediaContentPath}\" {split.ffmpegArgsForSplit} \"{outputFile}\"";

                ProcessStartInfo invokeDefinition = new ProcessStartInfo {
                    FileName = this.viewModel.ffmpegPath,
                    WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    Arguments = args
                };


                Process proc = new Process {
                    StartInfo = invokeDefinition,
                    EnableRaisingEvents = true,
                };

                proc.OutputDataReceived += processFfmpegProcessOutput;
                proc.ErrorDataReceived += processFfmpegProcessOutput;
                proc.Start();
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();

                this.outstandingProcs += 1;
                this.tbFfmpegOutput.AppendText($"Started [{this.viewModel.ffmpegPath} {args}] @ PID {proc.Id}\n");
                this.svFfmpegOutput.ScrollToEnd();

                proc.Exited += (o, e) => {
                    this.Dispatcher.Invoke(() => {
                        this.outstandingProcs -= 1;
                        var innerProc = o as Process;
                        this.tbFfmpegOutput.AppendText($"\t[{innerProc.Id:D7}] Has exited with code {innerProc.ExitCode}\n");
                        this.svFfmpegOutput.ScrollToEnd();
                        if (this.outstandingProcs == 0) {
                            MessageBox.Show("All done!", "Turnip Video Splitter", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    });
                };
            }
        }

        private void processFfmpegProcessOutput(object sender, DataReceivedEventArgs dataReceivedEventArgs) {
            this.Dispatcher.Invoke(() => {
                var proc = sender as Process;
                this.tbFfmpegOutput.AppendText($"\t[{proc.Id:D7}] {dataReceivedEventArgs.Data}\n");
                this.svFfmpegOutput.ScrollToEnd();
            });
        }

        private int outstandingProcs = 0;

        private void onLoaded(object sender, RoutedEventArgs e) {
            if (!File.Exists(this.viewModel.ffmpegPath)) {
                var downloader = new Downloader();
                downloader.ShowDialog();
            }

            if (!File.Exists(this.viewModel.ffmpegPath)) {
                MessageBox.Show($"Could not find ffmpeg @ {this.viewModel.ffmpegPath}\n. If the downloader isn't working, please download ffmpeg yourself and place ffmpeg.exe inside %LOCALAPPDATA%.",
                    "Turnip Vod Downloader", MessageBoxButton.OK, MessageBoxImage.Error);
                this.Close();
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

    }

    class ScrubAttempt: IFormattable {
        public TimeSpan position;
        public DateTime asOf;

        public ScrubAttempt(long newTs) {
            this.position = TimeSpan.FromMilliseconds(newTs);
            this.asOf = DateTime.Now;
        }

        public string ToString(string format, IFormatProvider formatProvider) {
            return $"Scrubbed @ {asOf}: {position}";
        }
    }
}
