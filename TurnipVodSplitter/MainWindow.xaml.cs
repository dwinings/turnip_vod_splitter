using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Threading;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace TurnipVodSplitter {
    public partial class MainWindow : Window {

        bool _isPaused = true;
        private ScrubAttempt _trynaScrub = null;
        private SplitEntry _currentSplit = null;

        private MainWindowViewModel viewModel => this.DataContext as MainWindowViewModel;

        public MainWindow() {
            InitializeComponent();
            this.viewModel.splits = new ObservableCollection<SplitEntry>();
            this.viewModel.PropertyChanged += this.OnViewModelPropertyChanged;

            string currentDirectory = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
            this.viewModel.ffmpegPath = Downloader.FullPath;
            DispatcherTimer dispatcher = new DispatcherTimer();
            dispatcher.Tick += dispatcherTimer_onTick;
            dispatcher.Interval = new TimeSpan(0, 0, 0, 0, 300);
            dispatcher.Start();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter) {
                BtnRecordSplit_Click(sender, e);
                e.Handled = true;
            }

            if (e.Key == Key.Space) {
                BtnPlay_Click(sender, e);
                e.Handled = true;
            }
        }

        private void BtnOpenFile_Click(object sender, RoutedEventArgs e) {
            OpenFileDialog openFileDialog = new OpenFileDialog();

            if (openFileDialog.ShowDialog() == true) {
                System.Uri uri = null;
                System.Uri.TryCreate(openFileDialog.FileName, UriKind.Absolute, out uri);
                this.viewModel.mediaContentPath = openFileDialog.FileName;
                this.meVideoPlayer.Source = uri;
                this.meVideoPlayer.Play();
                this.meVideoPlayer.Pause();
            }

            this.viewModel.splits.Clear();
            this.tbFfmpegOutput.Text = "";

        }


        private void BtnPlay_Click(object sender, RoutedEventArgs e) {
            if (this._isPaused) {
                this.meVideoPlayer.Play();
            } else {
                this.meVideoPlayer.Pause();
            }

            this._isPaused = !this._isPaused;
        }

        private void BtnRecordSplit_Click(object sender, RoutedEventArgs e) {
            SplitEntry newSplit;
            if (_currentSplit == null) {
                newSplit = new SplitEntry {
                    splitStart = TimeSpan.Zero,
                    splitEnd = meVideoPlayer.Position,
                    context = this.viewModel,
                };

            } else {
                newSplit = new SplitEntry {
                    splitStart = this._currentSplit.splitEnd,
                    splitEnd = meVideoPlayer.Position,
                };

            }

            _currentSplit = newSplit;
            this.viewModel.splits.Add(newSplit);
        }

        private void MeVideoPlayer_MediaOpened(object sender, RoutedEventArgs e) {
            this.viewModel.mediaTotalDuration = meVideoPlayer.NaturalDuration.TimeSpan;
        }

        private void dispatcherTimer_onTick(object sender, EventArgs e) {
            if (this._trynaScrub != null) {
                this.meVideoPlayer.Position = this._trynaScrub.position;
                Console.WriteLine($@"{this._trynaScrub}");
                this._trynaScrub = null;
            }

            this.viewModel.mediaPosition = meVideoPlayer.Position;

            // Make sure we don't create an update loop.
            this.sliderMedia.ValueChanged -= SliderMedia_OnValueChanged;
            this.sliderMedia.Value = this.meVideoPlayer.Position.TotalSeconds;
            this.sliderMedia.ValueChanged += SliderMedia_OnValueChanged;

        }

        private void SliderMedia_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            this._trynaScrub = new ScrubAttempt(this.sliderMedia.Value);
        }

        private void BtnChooseOutputDir_OnClick(object sender, RoutedEventArgs e) {
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

        private void BtnEngageSplit_OnClick(object sender, RoutedEventArgs e) {
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

                proc.OutputDataReceived += ProcessFfmpegProcessOutput;
                proc.ErrorDataReceived += ProcessFfmpegProcessOutput;
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

        private void ProcessFfmpegProcessOutput(object sender, DataReceivedEventArgs dataReceivedEventArgs) {
            this.Dispatcher.Invoke(() => {
                var proc = sender as Process;
                this.tbFfmpegOutput.AppendText($"\t[{proc.Id:D7}] {dataReceivedEventArgs.Data}\n");
                this.svFfmpegOutput.ScrollToEnd();
            });
        }

        private int outstandingProcs = 0;

        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e) {
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
    }

    class ScrubAttempt: IFormattable {
        public TimeSpan position;
        public DateTime asOf;

        public ScrubAttempt(Double newPosition) {
            this.position = TimeSpan.FromSeconds(newPosition);
            this.asOf = DateTime.Now;
        }

        public string ToString(string format, IFormatProvider formatProvider) {
            return $"Scrubbed @ {asOf}: {position}";
        }
    }
}
