using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.IO.Compression;
using System.Reflection;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace TurnipVodSplitter {
    public class DownloadProgress: IProgress<int>, INotifyPropertyChanged {
        private DateTime _lastReport;
        private DateTime _lastUIUpdate;
        private decimal _currentXferRateKb;
        private string _currentOperation;

        public string CurrentOperation {
            get => _currentOperation;
            set => _currentOperation = value;
        }

        public int Downloaded { get; set; }
        public int TotalBytes { get; set; }

        public string CurrentXferRateKb {
            get {
                if (this.PercentDone == 100) {
                    return "Done";
                }

                return $"{_currentXferRateKb:N0} kB/s";
            }
        }

        public int PercentDone {
            get {
                if (TotalBytes == 0) return 0;
                return (int)(((decimal)Downloaded / (decimal)TotalBytes) * 100);
            }
        }

        public DownloadProgress() {
            _lastReport = DateTime.Now;
            _lastUIUpdate = DateTime.Now;
            _stopWatch = new Stopwatch();
            _stopWatch.Start();
        }

        public void Report(int bytes) {
            this.Downloaded += bytes;
            decimal timePassed = (decimal)_stopWatch.ElapsedTicks / Stopwatch.Frequency;
            this._currentXferRateKb = ((decimal)bytes / (timePassed * 1024));
            _stopWatch.Restart();
            _lastReport = DateTime.Now;
            UpdateAll();
        }

        public void ChangeOperation(string operation) {
            this.CurrentOperation = operation;
            this._currentXferRateKb = 0;
            this.TotalBytes = 0;
            this.Downloaded = 0;
            UpdateAll();
            _stopWatch.Restart();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private object _catchUpTask = null;
        private static readonly int _debounceInterval = 100;
        private readonly Stopwatch _stopWatch;

        /* Debouncing update. */
        public void UpdateAll() {
            if (System.DateTime.Now - _lastUIUpdate >= TimeSpan.FromMilliseconds(_debounceInterval)) {
                OnPropertyChanged("Downloaded");
                OnPropertyChanged("TotalBytes");
                OnPropertyChanged("CurrentXferRateKb");
                OnPropertyChanged("CurrentOperation");
                OnPropertyChanged("PercentDone");
                _lastUIUpdate = DateTime.Now;
            } else {
                if (_catchUpTask == null) {
                    _catchUpTask = Dispatcher.CurrentDispatcher.InvokeAsync(async () => {
                        await Task.Delay(_debounceInterval + 1);
                        UpdateAll();
                        _catchUpTask = null;
                    }, DispatcherPriority.Background);
                }
            }
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null) {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }

    public partial class Downloader : Window {
        private static readonly int _bufferSize = 8192 * 2;
        private readonly byte[] _buffer = new byte[_bufferSize];
        private CancellationToken _cancellationToken = new CancellationToken();
        private readonly string _defaultDownloadFilename = "ffmpeg.zip";

        public Downloader() {
            InitializeComponent();

            if (!Directory.Exists(_appData)) {
                Directory.CreateDirectory(_appData);
            }

            DownloadWorkflow();
        }

        private static readonly string _appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), CompanyName);
        private static readonly string _downloadPath = @"https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-lgpl.zip";
        public static readonly string FFMPEG_PATH = Path.Combine(Downloader._appData, "ffmpeg.exe");

        private static string CompanyName {
            get {
                Assembly currentAssembly = typeof(Downloader).Assembly;
                var attribs = currentAssembly.GetCustomAttributes(typeof(AssemblyCompanyAttribute)).ToArray<Attribute>();

                if (attribs.Length > 0) {
                    return ((AssemblyCompanyAttribute)attribs[0]).Company;

                } else {
                    return "Unknown";
                }
            }
        }

        public async void DownloadWorkflow() {
            var progress = this.DataContext as DownloadProgress;
            var downloadPath = await DownloadFile(_downloadPath, progress);
            if (downloadPath == null) {
                MessageBox.Show("Download of ffmpeg failed :(", "Turnip Video Splitter", MessageBoxButton.OK, MessageBoxImage.Error);
                this.Close();
            }

            if (! await ExtractFfmpeg(downloadPath, FFMPEG_PATH, progress)) {
                this.Close();
            }

            try {
                File.Delete(downloadPath);
            } catch (Exception) {
                // Don't care even a little.
            }
        }

        public async Task<bool> ExtractFfmpeg(string srcPath, string dstPath, DownloadProgress progress) {
            progress.ChangeOperation("Extracting");

            try {
                using var archive = ZipFile.Open(srcPath, ZipArchiveMode.Read);
                var archiveEntry = archive.Entries
                    .FirstOrDefault(entry => entry.FullName.EndsWith("ffmpeg.exe"));

                if (archiveEntry == null) {
                    MessageBox.Show($"Couldn't extract ffmpeg from archive {srcPath}, could not find EXE file.", "Turnip Vod Splitter", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }

                progress.TotalBytes = (int)archiveEntry.Length;

                using var zipStream = archiveEntry.Open();
                using var fileStream = new FileStream(dstPath, FileMode.Create, FileAccess.Write, FileShare.None);
                int bytesThisChunk;

                while ((bytesThisChunk = await zipStream.ReadAsync(_buffer, 0, _buffer.Length, _cancellationToken)) > 0) {
                    await fileStream.WriteAsync(_buffer, 0, bytesThisChunk, _cancellationToken);
                    progress.Report(bytesThisChunk);
                }

                progress.Downloaded = progress.TotalBytes;
                progress.UpdateAll();
                return true;
            } catch (Exception ex) {
                MessageBox.Show($"An error occurred: {ex.Message}", "Turnip Vod Splitter", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }


        public async Task<string> DownloadFile(string url, DownloadProgress progress) {
            progress.ChangeOperation("Downloading");

            Uri.TryCreate(url, UriKind.Absolute, out var uri);
            var httpClient = new HttpClient();
            var getResp = await httpClient.SendAsync(new HttpRequestMessage() {
                Method = HttpMethod.Get,
                RequestUri = uri
            }, HttpCompletionOption.ResponseHeadersRead);

            progress.TotalBytes = (int)(getResp.Content.Headers.ContentLength ?? 0);
            var remoteFileName = getResp.ContentDispositionFileName(_defaultDownloadFilename);
            var fullDstPath = Path.GetFullPath(Path.Combine(_appData, remoteFileName));

            try {
                var responseStream = await getResp.Content.ReadAsStreamAsync();

                using var fileStream = new FileStream(fullDstPath, FileMode.Create, FileAccess.Write, FileShare.None);
                int bytesThisChunk;

                while ((bytesThisChunk = await responseStream.ReadAsync(_buffer, 0, _buffer.Length, _cancellationToken)) > 0) {
                    await fileStream.WriteAsync(_buffer, 0, bytesThisChunk, _cancellationToken);
                    progress.Report(bytesThisChunk);
                }
            } catch (IOException e) {
                MessageBox.Show($"An error happened while downloading :( {e.Message}", "Turnip Vod Splitter", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }

            if (getResp.IsSuccessStatusCode) {
                return fullDstPath;
            } else {
                MessageBox.Show($"Converter download returned a bad status code: {getResp.StatusCode}", "Turnip Vod Splitter", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        private void BtnDone_OnClick(object sender, RoutedEventArgs e) {
            this.Close();
        }
    }
}
