using System;

namespace TurnipVodSplitter;

public class ScrubAttempt : IFormattable {
    public TimeSpan position;
    public DateTime asOf;
    public bool dragEnded;


    public ScrubAttempt(long newTs, bool dragEnded) {
        this.position = TimeSpan.FromMilliseconds(newTs);
        this.asOf = DateTime.Now;
        this.dragEnded = dragEnded;
    }

    public ScrubAttempt(long newTs) : this(newTs, false) { }

    public string ToString(string? format, IFormatProvider? formatProvider) {
        return $"Scrubbed @ {asOf}: {position}";
    }
}