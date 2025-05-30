using System;
using System.Globalization;
using System.Windows.Data;
using FontAwesome5;
using LibVLCSharp.Shared;

namespace TurnipVodSplitter.WpfValueConverters {
    internal class PlayPauseFaIcon : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            VLCState vlcState = (VLCState)value;

            if (vlcState is VLCState.Paused or VLCState.Stopped or VLCState.NothingSpecial) {
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
            ConversionStatus? status = (ConversionStatus)value;
            return status == ConversionStatus.InProgress ? "True" : "False";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }

    internal class ConversionStateIconName : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            ConversionStatus? status = (ConversionStatus)value;
            return status switch {
                ConversionStatus.Pending => EFontAwesomeIcon.Solid_HourglassEnd,
                ConversionStatus.InProgress => EFontAwesomeIcon.Solid_Cog,
                ConversionStatus.Succeeded=> EFontAwesomeIcon.Solid_Check,
                ConversionStatus.Failed=> EFontAwesomeIcon.Solid_Times,
                ConversionStatus.Cancelled => EFontAwesomeIcon.Solid_Times,
                _ => EFontAwesomeIcon.None
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }

    
}
