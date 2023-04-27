using System;

namespace TurnipVodSplitter;

public class ScrubAttempt : IFormattable {
    public TimeSpan position;
    public DateTime asOf;

    public ScrubAttempt(long newTs) {
        this.position = TimeSpan.FromMilliseconds(newTs);
        this.asOf = DateTime.Now;
    }

    public string ToString(string? format, IFormatProvider? formatProvider) {
        return $"Scrubbed @ {asOf}: {position}";
    }
}