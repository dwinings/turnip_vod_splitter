using System;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Net.Mime;
using System.Security.Cryptography;
using System.Text;

namespace TurnipVodSplitter {
    public static class TurnipExtensions {
        public static string VideoTimestampFormat(this TimeSpan ts) {
            return $"{ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
        }

        public static TimeSpan? timeSpanFromTimeStamp(this string vtfstr) {
            string fmt = @"hh\:mm\:ss";
            var culture = CultureInfo.InvariantCulture;

            if (!TimeSpan.TryParseExact(vtfstr, fmt, culture, TimeSpanStyles.None, out var result)) {
                return null;
            }

            return result;
        }

        public static string ContentDispositionFileName(this HttpResponseMessage response, string defaultName) {
            if (response.Content.Headers.TryGetValues("Content-Disposition", out var contentDispositionValues)) {
                var fileName = contentDispositionValues
                    .Select(cdv => (new ContentDisposition(cdv)).FileName)
                    .FirstOrDefault(fname => fname is { Length: > 0 });

                if (fileName == null) {
                    return defaultName;
                } else {
                    return fileName;
                }
            } else {
                return defaultName;
            }
        }
    }
}
