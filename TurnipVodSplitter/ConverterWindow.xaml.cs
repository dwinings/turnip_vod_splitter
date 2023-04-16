using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class ConverterWindow : Window {
        private bool _isConverting = false;
        private readonly string _inputFile = null;
        private readonly string _ffmpegPath = null;
        private readonly string _outputDirectory = null;
        private readonly string _eventName = "";
        private readonly IEnumerable<SplitEntry> _splits = null;

        public event EventHandler ConversionStarted;
        public event EventHandler ConversionCompleted;
        public event EventHandler ConversionFailed;
        public event EventHandler<DataReceivedEventArgs> ConversionOutput;

        private static readonly string FFMPEG_BASE_ARGS = "-hide_banner -loglevel info -nostats -y -i";

        public ConverterWindow() : this(
            "/dummy/ffmpeg.exe",
            new SplitEntry[]{},
    "/dummy/input.mp4",
            "/dummy/output/",
            "dummy event") { }

        public ConverterWindow(string ffmpegPath, IEnumerable<SplitEntry> splits,
            string inputFile, string outputDirectory, string eventName = "") {
            InitializeComponent();
            this._ffmpegPath = ffmpegPath;
            this._splits = splits;
            this._inputFile = inputFile;
            this._outputDirectory = outputDirectory;
            this._eventName = eventName;

            this.ConversionStarted += this.OnConversionStarted;
            this.ConversionFailed += this.OnConversionFailed;
            this.ConversionCompleted += this.OnConversionCompleted;
            this.ConversionOutput += this.onFfmpegProcessCreatedOutput;
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

                proc.OutputDataReceived += (o,e) => {
                    this.Dispatcher.Invoke(() => {
                        this.ConversionOutput?.Invoke(o, e);
                    });
                };

                proc.ErrorDataReceived += (o,e) => {
                    this.Dispatcher.Invoke(() => {
                        this.ConversionOutput?.Invoke(o, e);
                    });
                };

                proc.Start();
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();

                this.outstandingProcs += 1;
                this.tbFfmpegOutput.AppendText($"Started [{this._ffmpegPath} {args}] @ PID {proc.Id}\n");
                this.svFfmpegOutput.ScrollToEnd();

                proc.Exited += (o, e) => {
                    this.Dispatcher.Invoke(() => {
                        this.onFfmpegProcessExited(o, e);
                    });
                };
            }
        }

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

        private int outstandingProcs = 0;

        public void OnCompleteButtonClick(object sender, EventArgs e) {
            this.Close();
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
    }
}
