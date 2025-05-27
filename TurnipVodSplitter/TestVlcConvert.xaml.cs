using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using LibVLCSharp;
using LibVLCSharp.Shared;

namespace TurnipVodSplitter {
    /// <summary>
    /// Interaction logic for TestVlcConvert.xaml
    /// </summary>
    public partial class TestVlcConvert : Window {
        public TestVlcConvert() {
            InitializeComponent();
        }

        private void BtnGo_OnClick(object sender, RoutedEventArgs e) {
            var libvlc = new LibVLC(
                "--no-osd",
                "--no-spu",
                //"--sout-file-overwrite",
                "--file-caching=60000"
                );
            libvlc.Log += (s, e) => {
                if (e.Level >= LogLevel.Notice) {
                    Debug.WriteLine(e.FormattedLog);
                }
            };

            var mediaPlayer = new LibVLCSharp.Shared.MediaPlayer(libvlc);
            mediaPlayer.PositionChanged +=
                (s, e) => {
                    Debug.WriteLine($"{e.Position * 100:F0}%");
            };

            var media = new LibVLCSharp.Shared.Media(libvlc, this.fileName.Text);
            var destination = @"""C:\Users\Wisp\test_output3.mp4""";
            var transcodeOpts =
                @"{vcodec=h265,vb=2048,scale=auto,acodec=mp4a,ab=96,channels=2,samplerate=44100}";
            var outputOpts = "{access=file,mux=mkv,dst=" + destination + "}";

            //mediaPlayer.SetRate(999);
            var mediaXcodeOpts = $":sout=#transcode{transcodeOpts}:standard{outputOpts}";
            Debug.WriteLine(mediaXcodeOpts);
            media.AddOption(mediaXcodeOpts);
            media.AddOption(":no-sout-all");
            media.AddOption(":sout-mux-caching=5000");

            media.AddOption(":no-audio");
            media.AddOption(":no-video");

            mediaPlayer.Stopped +=
                delegate {
                    Debug.WriteLine("All done!");
                    ThreadPool.QueueUserWorkItem(delegate {
                        mediaPlayer.Media = null;
                        media.Dispose();
                    });
                };

            mediaPlayer.EncounteredError += (s, e) => {
                Debug.WriteLine(e.ToString());
            };

            mediaPlayer.Media = media;
            mediaPlayer.Play();
            mediaPlayer.SetPause(false);
        }
    }
}
