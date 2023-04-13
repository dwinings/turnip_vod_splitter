using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace TurnipVodSplitter.WpfValueConverters {
    internal class PlayPauseFaIcon : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            bool isPlaying = (bool)value;

            if (!isPlaying) {
                return "Play";
            } else {
                return "Pause";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }

    internal class LongMsToTime: IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            var lenTs = TimeSpan.FromMilliseconds((long)value);
            return $"{lenTs.Hours:D2}:{lenTs.Minutes:D2}:{lenTs.Seconds:D2}";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }
}
