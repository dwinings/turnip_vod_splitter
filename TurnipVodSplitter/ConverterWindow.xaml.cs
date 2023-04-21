using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TurnipVodSplitter {
    public partial class ConverterWindow : Window {
        private int outstandingProcs = 0;
        private bool _isConverting = false;
        private readonly string _inputFile = null;
        private readonly string _ffmpegPath = null;
        private readonly string _outputDirectory = null;
        private readonly string _eventName = "";
        private readonly IEnumerable<SplitEntry> _splits = null;
        private readonly ObservableCollection<ConversionInfo> _conversions = new ObservableCollection<ConversionInfo>();
        public ObservableCollection<ConversionInfo> conversions => _conversions;

        public event EventHandler ConversionStarted;
        public event EventHandler ConversionCompleted;
        public event EventHandler ConversionFailed;
        public event EventHandler<DataReceivedEventArgs> ConversionOutput;

        private static readonly string FFMPEG_BASE_ARGS = "-hide_banner -loglevel info -nostats -y -i";

        public ConverterWindow() : this(
            "C:\\Users\\Wisp\\Desktop\\ffmpeg.exe",
            new SplitEntry[] {
                new SplitEntry() {
                    splitStart = TimeSpan.Zero,
                    splitEnd = TimeSpan.FromSeconds(30),
                    player1 = "me",
                    player2 = "them"
                },
                new SplitEntry() {
                    splitStart = TimeSpan.FromSeconds(30),
                    splitEnd = TimeSpan.FromSeconds(60),
                    player1 = "me",
                    player2 = "them"
                }
            },
            "C:\\Users\\Wisp\\test\\00h00m00s to 00h41m40s treythetrashman 28 Mar 2023.mp4",
            "C:\\Users\\Wisp\\test",
            "dummy event") {
        }

        public ConverterWindow(string ffmpegPath, IEnumerable<SplitEntry> splits,
            string inputFile, string outputDirectory, string eventName = "") {
            InitializeComponent();
            this._ffmpegPath = ffmpegPath;
            this._splits = splits;
            this._inputFile = inputFile;
            this._outputDirectory = outputDirectory;
            this._eventName = eventName;

            /*
            this.ConversionStarted += this.OnConversionStarted;
            this.ConversionFailed += this.OnConversionFailed;
            this.ConversionCompleted += this.OnConversionCompleted;
            this.ConversionOutput += this.onFfmpegProcessCreatedOutput;
            */
        }

        public void OnLoaded(object sender, EventArgs args) {
            Convert();
        }

        private string Extension {
            get {
                return Path.GetExtension(this._inputFile);
            }
        }

        private string OutputFile(string splitName) {
            string fileBaseName = "";
            if (_eventName.Length > 0) {
                fileBaseName = $"[{this._eventName}] ";
            }

            fileBaseName = String.Concat(fileBaseName, $"{splitName}{this.Extension}");
            return Path.Combine(this._outputDirectory, fileBaseName);
        }


        private void Convert() {
            int procIdx = 0;
            this.ConversionStarted?.Invoke(this, EventArgs.Empty);

            foreach (var split in this._splits) {
                var outputFile = OutputFile(split.splitName);

                string args = String.Join(" ", new string[] {
                    ConverterWindow.FFMPEG_BASE_ARGS,
                    $"\"{this._inputFile}\"",
                    split.ffmpegArgsForSplit,
                    $"\"{outputFile}\""
                });

                Console.WriteLine(args);

                ProcessStartInfo invokeDefinition = new ProcessStartInfo {
                    FileName = this._ffmpegPath,
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

                ConversionInfo conversion = new ConversionInfo(this) {
                    proc = proc,
                    idx = procIdx,
                };

                proc.OutputDataReceived += conversion.onFfmpegProcessCreatedOutput;
                proc.ErrorDataReceived += conversion.onFfmpegProcessCreatedOutput;

                this.conversions.Add(conversion);
                proc.Exited += conversion.onFfmpegProcessExited;
                procIdx += 1;

                this.outstandingProcs += 1;
                proc.Start();
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();



                // this.tbFfmpegOutput.AppendText($"Started [{this._ffmpegPath} {args}] @ PID {proc.Id}\n");
                // this.svFfmpegOutput.ScrollToEnd();

                /*
                proc.Exited += (o, e) => {
                    this.Dispatcher.Invoke(() => {
                        this.onFfmpegProcessExited(o, e);
                    });
                };
                */
            }
        }
        public void OnCompleteButtonClick(object sender, EventArgs e) {
            this.Close();
        }


        /*
        private void onFfmpegProcessExited(object sender, EventArgs e) {
                this.outstandingProcs -= 1;
                var proc = sender as Process;
                this.tbFfmpegOutput.AppendText($"\t[{proc.Id:D7}] Has exited with code {proc.ExitCode}\n");
                this.svFfmpegOutput.ScrollToEnd();

                if (proc.ExitCode != 0) {
                    this.ConversionFailed?.Invoke(sender, e);

                } else if (this.outstandingProcs == 0) {
                    this.ConversionCompleted?.Invoke(this, EventArgs.Empty);
                } 
        }

        private void onFfmpegProcessCreatedOutput(object sender, DataReceivedEventArgs dataReceivedEventArgs) {
            var proc = sender as Process;
            this.tbFfmpegOutput.AppendText($"\t[{proc.Id:D7}] {dataReceivedEventArgs.Data}\n");
            this.svFfmpegOutput.ScrollToEnd();
        }

        public void OnConversionStarted(object sender, EventArgs e) {
            _isConverting = true;
            this.tbFfmpegOutput.Text = "";

        }

        public void OnConversionCompleted(object sender, EventArgs e) {
            this._isConverting = false;
            tbFfmpegOutput.AppendText("\nAll done!");
            this.btnComplete.Content = "Ok";
        }

        public void OnConversionFailed(object sender, EventArgs e) {
            this._isConverting = false;
            tbFfmpegOutput.AppendText("\nConversion Failed!");
            this.btnComplete.Content = "Go back";
        }
        */
    }
    public class ConversionInfo : INotifyPropertyChanged {
        private Process _proc;

        public Process proc {
            get => _proc;
            set => this.SetField(ref _proc, value);
        }

        private int _idx;

        public int idx {
            get => _idx;
            set => this.SetField(ref _idx, value);
        }

        private string _status;
        private readonly ConverterWindow _window;

        public string status {
            get => _status;
            set => this.SetField(ref _status, value);
        }

        public string tabName => $"Split {idx}";
        private string _outputText = "";

        public string outputText {
            get => _outputText;
            set => SetField(ref _outputText, value);
        }

        public ConversionInfo() : this(null) { }

        public ConversionInfo(ConverterWindow window) {
            this._window = window;
        }

        public void onFfmpegProcessExited(object sender, EventArgs e) {
            this._outputText += $"\t[{proc.Id:D7}] Has exited with code {proc.ExitCode}\n";
            if (proc.ExitCode != 0) {
                // this.ConversionFailed?.Invoke(sender, e);

            } else if (this.outstandingProcs == 0) {
                // this.ConversionCompleted?.Invoke(this, EventArgs.Empty);
            }
        }

        public void onFfmpegProcessCreatedOutput(object sender, DataReceivedEventArgs dataReceivedEventArgs) {
            this.outputText += $"\t[{proc.Id:D7}] {dataReceivedEventArgs.Data}\n";
        }

        private int outstandingProcs = 0;

        public void OnConversionCompleted(object sender, EventArgs e) {
            // this._isConverting = false;
            // tbFfmpegOutput.AppendText("\nAll done!");
            // this.btnComplete.Content = "Ok";
        }

        public void OnConversionFailed(object sender, EventArgs e) {
            // this._isConverting = false;
            // tbFfmpegOutput.AppendText("\nConversion Failed!");
            // this.btnComplete.Content = "Go back";
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void triggerPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null) {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            triggerPropertyChanged(propertyName);
            return true;
        }
    }
}
