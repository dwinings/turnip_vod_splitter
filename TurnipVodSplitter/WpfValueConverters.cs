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

    internal class ConversionStateSpinType : IValueConverter {
        public new object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            string status = value as string;
            if (status == "converting") {
                return "True";
            } else {
                return "False";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }

    internal class ConversionStateIconName : IValueConverter {
        public new object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            string status = value as string;
            switch (status) {
                case "converting":
                    return "gear";
                case "succeeded":
                    return "check";
                case "failed":
                    return "xmark";
            }

            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }

    
}
