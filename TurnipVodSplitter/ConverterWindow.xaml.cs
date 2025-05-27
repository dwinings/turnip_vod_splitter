using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;

namespace TurnipVodSplitter {
    public partial class ConverterWindow : Window {
        private int _outstandingProcs = 0;
        private bool _hasAnyFailures = false;
        private readonly string _inputFile;
        private readonly string _ffmpegPath;
        private readonly string _outputDirectory;
        private readonly string _eventName = "";
        private readonly SplitCollection _splits;
        public ObservableCollection<ConversionInfo> conversions { get; } = [];

        private static readonly string FFMPEG_BASE_ARGS = "-hide_banner -loglevel info -nostats -y ";

        public ConverterWindow() : this(
            "C:\\Users\\Wisp\\Desktop\\ffmpeg.exe",
            new SplitCollection([
                new SplitEntry() {
                    SplitStart = TimeSpan.Zero,
                    SplitEnd = TimeSpan.FromSeconds(30),
                },
                new SplitEntry() {
                    SplitStart = TimeSpan.FromSeconds(30),
                    SplitEnd = TimeSpan.FromSeconds(60),
                }
            ]),
            "C:\\Users\\Wisp\\test\\00h00m00s to 00h41m40s treythetrashman 28 Mar 2023.mp4",
            "C:\\Users\\Wisp\\test",
            "-acodec copy -vcodec copy"
        ) { }

        public ConverterWindow(string ffmpegPath, SplitCollection splits,
            string inputFile, string outputDirectory, string ffmpegCodecArgs) {
            InitializeComponent();
            this._ffmpegPath = ffmpegPath;
            this._splits = splits;
            this._inputFile = inputFile;
            this._outputDirectory = outputDirectory;
        }

        public void OnLoaded(object? sender, EventArgs args) {
            Convert();
        }

        private string extension => Path.GetExtension(this._inputFile);

        private string OutputFile(string splitName) {
            string fileBaseName = "";
            if (_eventName.Length > 0) {
                fileBaseName = $"[{this._eventName}] ";
            }

            fileBaseName = String.Concat(fileBaseName, $"{splitName}{this.extension}");
            return Path.Combine(this._outputDirectory, fileBaseName);
        }

        private TimeSpan getStartSeekTs(SplitEntry split) {
            return TimeSpan.FromMilliseconds(Math.Max(0,
                split.SplitStart.TotalMilliseconds - TimeSpan.FromSeconds(30).TotalMilliseconds));
        }

        private void Convert() {
            int procIdx = 0;

            foreach (var split in this._splits.Splits) {
                if (split.SkipSplit) {
                    continue;
                }
                if (!split.Validate()) {
                    Debug.WriteLine($"Warning, could not validate split {this._splits.FilenameOf(split)}");
                    continue;
                }

                var outputFile = OutputFile(_splits.FilenameOf(split) ?? "");
                var seekTime = getStartSeekTs(split);
                var startTime = split.SplitStart - seekTime;
                var endTime = split.SplitEnd - seekTime;
                string codecArgs;
                if (this._splits.FfmpegCodecArgs != null && this._splits.FfmpegCodecArgs.Trim().Length > 0) {
                    codecArgs = this._splits.FfmpegCodecArgs;
                } else {
                    codecArgs = "-c:v copy -c:a copy";
                }

                string args = String.Join(" ", new string[] {
                    ConverterWindow.FFMPEG_BASE_ARGS,
                    $"-ss {seekTime.VideoTimestampFormat()}",
                    "-i",
                    $"\"{this._inputFile}\"",
                    codecArgs,
                    $"-ss {startTime.VideoTimestampFormat()}",
                    $"-to {endTime.VideoTimestampFormat()}",
                    $"\"{outputFile}\""
                });

                Debug.WriteLine(args);

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

                proc.Exited += conversion.onFfmpegProcessExited;
                procIdx += 1;
                this.conversions.Add(conversion);
                proc.OutputDataReceived += conversion.onFfmpegProcessCreatedOutput;
                proc.ErrorDataReceived += conversion.onFfmpegProcessCreatedOutput;

                this._outstandingProcs += 1;

                proc.Start();
                conversion.outputText = $"Started [{this._ffmpegPath} {args}] @ PID {proc.Id}\n";
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();

                conversion.Succeeded += onConversionSucceeded;
                conversion.Failed += onConversionFailed;

            }
        }

        public void onConversionSucceeded(object? sender, EventArgs e) {
            this._outstandingProcs -= 1;

            if (this._outstandingProcs == 0) {
                onAllConversionsComplete();
            }
        }

        public void onConversionFailed(object? sender, EventArgs e) {
            _hasAnyFailures = true;
            this._outstandingProcs -= 1;

            if (this._outstandingProcs == 0) {
                onAllConversionsComplete();
            }
        }

        public void onAllConversionsComplete() {
            this.btnComplete.Content = this._hasAnyFailures ? "Go Back" : "Done";
        }


        public void OnCompleteButtonClick(object? sender, EventArgs e) {
            this.Close();
        }
    }

    public class ConversionInfo : INotifyPropertyChanged {
        public event EventHandler? Completed;
        public event EventHandler? Succeeded;
        public event EventHandler? Failed;

        private Process? _proc;

        public ConversionInfo() : this(null) { }

        public ConversionInfo(ConverterWindow? window) {
            this._window = window;
            this.Completed += OnCompleted;
            this.Succeeded += OnSucceeded;
            this.Failed += OnFailed;
        }

        #region props

        public Process? proc {
            get => _proc;
            set => this.SetField(ref _proc, value);
        }

        private int _idx;

        public int idx {
            get => _idx;
            set => this.SetField(ref _idx, value);
        }

        /* converting, succeeded, failed */
        private string _status = "converting";
        private readonly ConverterWindow? _window;

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

        #endregion

        #region eventhandlers
        public void onFfmpegProcessExited(object? sender, EventArgs e) {
            this._window?.Dispatcher.InvokeAsync(() => {
                this.Completed?.Invoke(sender, e);
            });
        }

        public void onFfmpegProcessCreatedOutput(object? sender, DataReceivedEventArgs dataReceivedEventArgs) {
            this.outputText += $"\t[{proc?.Id:D7}] {dataReceivedEventArgs.Data}\n";
        }

        public void OnCompleted(object? sender, EventArgs e) {
            this.outputText += $"\t[{proc?.Id:D7}] Has exited with code {proc?.ExitCode}\n";
            if (proc?.ExitCode != 0) {
                this.Failed?.Invoke(sender, e);

            } else {
                this.Succeeded?.Invoke(sender, e);
            }
        }

        public void OnSucceeded(object? sender, EventArgs e) {
            this.outputText += "\nAll done!";
            this.status = "succeeded";
        }

        public void OnFailed(object? sender, EventArgs e) {
            this.outputText += "\nConversion Failed!";
            this.status = "failed";
        }
        #endregion

        #region notify
        public event PropertyChangedEventHandler? PropertyChanged;


        protected virtual void triggerPropertyChanged([CallerMemberName] string? propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null) {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            triggerPropertyChanged(propertyName);
            return true;
        }
        #endregion
    }
}
