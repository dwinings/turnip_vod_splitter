using System;
using System.Globalization;
using System.Windows.Data;
using LibVLCSharp.Shared;

namespace TurnipVodSplitter.WpfValueConverters {
    internal class PlayPauseFaIcon : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            VLCState vlcState = (VLCState)value;

            if (vlcState is VLCState.Paused or VLCState.Stopped or VLCState.Ended or VLCState.NothingSpecial) {
                return "Solid_Play";
            } else {
                return "Solid_Pause";
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
            return (long)TimeSpan.ParseExact((string) value, "g", culture).TotalMilliseconds;
        }
    }

    public class ConvertTimeSpan : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            var lenTs = (TimeSpan)value;
            return $"{lenTs.Hours:D2}:{lenTs.Minutes:D2}:{lenTs.Seconds:D2}";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return TimeSpan.ParseExact((string) value, "g", culture);
        }
    }

    internal class ConversionStateSpinType : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            string? status = value as string;
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
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            string? status = value as string;
            return status switch {
                "converting" => "Solid_Cog",
                "succeeded" => "Solid_Check",
                "failed" => "Solid_Times",
                _ => ""
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }

    
}
