using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CsvHelper;
using Microsoft.Win32;

namespace TurnipVodSplitter {
    public partial class SplitCollection: ObservableObject, INotifyPropertyChanged {
        public SplitCollection() {
            this.Splits.Add(new SplitEntry());
        }

        [ObservableProperty] 
        private BindingList<SplitEntry> splits = [];

        private TimeSpan mediaLength = TimeSpan.Zero;

        public TimeSpan MediaLength {
            get => mediaLength;
            set {
                var lastSplit = this.Splits.LastOrDefault();
                if (lastSplit != null) {
                    lastSplit.SplitEnd = value;
                }
                mediaLength = value;
                this.OnPropertyChanged();
            }
        }

        public int GetSplitIndexForTime(TimeSpan splitPoint) {
            for (int i = 0; i < this.Splits.Count; i++) {
                var split = this.Splits[i];
                if (split.SplitStart <= splitPoint && splitPoint <= split.SplitEnd) {
                    return i;
                }
            }
            return -1;
        }

        public SplitEntry? At(TimeSpan pos) {
            var idx = GetSplitIndexForTime(pos);
            if (idx < 0) {
                return null;
            }

            return this.Splits[idx];
        }

        public SplitEntry GetSplitByIndex(int idx) {
            if (idx >= 0 && idx < this.Splits.Count) {
                return this.Splits[idx];
            }

            return this.Splits.Last();
        }

        public SplitEntry NewSplitAtPoint(TimeSpan splitPoint) {
            var splitIdx = GetSplitIndexForTime(splitPoint);
            var originalSplit = this.Splits.Last();

            if (splitIdx < 0) {
                splitIdx = 0;
            } else {
                originalSplit = this.Splits[splitIdx];
            }

            var newSplit = new SplitEntry {
                SplitStart = splitPoint,
                SplitEnd = originalSplit.SplitEnd
            };

            this.Splits.Insert(splitIdx + 1, newSplit);
            originalSplit.SplitEnd = splitPoint;
            return newSplit;
        }

        public void DeleteSplitAtPoint(TimeSpan splitPoint, bool mergeBefore = false) {
            var idx = GetSplitIndexForTime(splitPoint);
            if (idx < 0) {
                return;
            }

            if (Splits.Count == 1) {
                return;
            }

            // If we're the last split, we want to merge with the previous one.
            if (idx == this.Splits.Count - 1) {
                this.Splits.RemoveAt(idx);
                this.Splits.Last().SplitEnd = this.MediaLength;
            } else if (idx == 0) {
                this.Splits.RemoveAt(idx);
                this.Splits.First().SplitStart = TimeSpan.Zero;
            } else {
                if (mergeBefore) {
                    this.Splits[idx - 1].SplitEnd = this.Splits[idx].SplitEnd;
                } else {
                    this.Splits[idx + 1].SplitStart = this.Splits[idx].SplitStart;
                }

                this.Splits.RemoveAt(idx);
            }
        }

        public IList<TimeSpan> GetSplitBoundaries() {
            var set = new SortedSet<TimeSpan>();
            foreach (var split in this.Splits) {
                set.Add(split.SplitStart);
                set.Add(split.SplitEnd);
            }

            return set.ToList();
        }

        public bool InLastSplit(TimeSpan pos) {
            return this.GetSplitIndexForTime(pos) == (this.Splits.Count - 1);
        }
        public bool InFirstSplit(TimeSpan pos) {
            return this.GetSplitIndexForTime(pos) == (this.Splits.Count - 1);
        }

        public void Normalize() {
            // Go backward so we don't fuck up the indices by adding to spots before our iteration point.
            var maxLen = this.Splits.Count;
            for (int i = maxLen-1; i >= 0; i--) {
                var thisSplit = this.Splits[i];
                var lastSplit = (i - 1 >= 0) ? this.Splits[i - 1] : null; 

                if (thisSplit.SplitEnd < thisSplit.SplitStart) {
                    thisSplit.SplitEnd = thisSplit.SplitStart;
                }

                if (lastSplit != null) {
                    if (lastSplit.SplitEnd < thisSplit.SplitStart) {
                        // Gap
                        this.Splits.Insert(i, new() {
                            SplitStart=lastSplit.SplitEnd,
                            SplitEnd=thisSplit.SplitStart,
                            Description="nothing",
                            SkipSplit=true
                        });
                    } else if (lastSplit.SplitEnd > thisSplit.SplitStart) {
                        // Overlap
                        lastSplit.SplitEnd = thisSplit.SplitStart;
                    }
                } else if (thisSplit.SplitStart != TimeSpan.Zero) {
                    // Fill in gap from zero to first split
                    this.Splits.Insert(0, new() {
                        SplitStart=TimeSpan.Zero,
                        SplitEnd = thisSplit.SplitStart,
                        Description = "nothing",
                        SkipSplit = true
                    });
                }

                // Hammer down last split.
                if (i == maxLen - 1) {
                    if (thisSplit.SplitEnd < this.MediaLength) {
                        this.Splits.Add(new() {
                            SplitStart = thisSplit.SplitEnd,
                            SplitEnd = this.MediaLength,
                            Description = "nothing",
                            SkipSplit = true
                        });
                    } else if (thisSplit.SplitEnd > this.MediaLength) {
                        thisSplit.SplitEnd = this.MediaLength;
                    }
                }
            }
        }

        public bool Validate() {
            bool foundFailure = false;
            if (this.Splits[0].SplitStart != TimeSpan.Zero) {
                foundFailure = true;
                Debug.WriteLine($"WARNING: First split @ {this.Splits[0].SplitStart.VideoTimestampFormat()} starts after zero.");
            };

            if (this.Splits.Last().SplitEnd != MediaLength) {
                foundFailure = true;
                Debug.WriteLine($"WARNING: Last split ends after media end {this.Splits[0].SplitEnd.VideoTimestampFormat()} > {MediaLength.VideoTimestampFormat()}");
            }

            for (int i = 0; i < this.Splits.Count; i++) {
                var cur = this.Splits[i];
                var nxt = i < this.Splits.Count-1 ? this.Splits[i + 1] : null;

                if (nxt != null && cur.SplitEnd < nxt.SplitStart) {
                    foundFailure = true;
                    Debug.WriteLine($"WARNING: Gap between splits found between {cur.SplitEnd.VideoTimestampFormat()} and {nxt.SplitStart.VideoTimestampFormat()}");
                }

                if (cur.SplitStart > cur.SplitEnd) {
                    foundFailure = true;
                    Debug.WriteLine($"WARNING: Split with negative length found {cur.SplitStart.VideoTimestampFormat()} to {cur.SplitEnd.VideoTimestampFormat()}");
                }
            }

            return !foundFailure;
        }

        public String YoutubeChapterFormat() {
            this.Normalize();
            this.Validate();
            StringBuilder builder = new();

            foreach (var s in this.Splits) {
                builder.AppendFormat("{0} - {1} vs {2} {3}\n", s.SplitStart.VideoTimestampFormat(), s.Player1, s.Player2, s.Description);
            }

            return builder.ToString();
        }

        public async Task saveToFile(Stream outputStream) {
            this.Normalize();
            this.Validate();

            StreamWriter writer = new(outputStream);
            await using var csvWriter = new CsvWriter(writer, CultureInfo.InvariantCulture);
            csvWriter.Context.RegisterClassMap(new SplitEntryFieldMap());
            await csvWriter.WriteRecordsAsync(this.Splits);
            await csvWriter.FlushAsync();
        }

        public async Task LoadFromFile(Stream inputStream) {
            StreamReader streamReader = new StreamReader(inputStream);
            using var csvReader = new CsvReader(streamReader, CultureInfo.InvariantCulture);
            csvReader.Context.RegisterClassMap(new SplitEntryFieldMap());

            this.Splits.Clear();
            await foreach (var split in csvReader.GetRecordsAsync<SplitEntry>()) {
                this.Splits.Add(split);
            }

            if (this.Splits.Count == 0) {
                this.Splits.Add(new SplitEntry());
            }
        }

        public new event PropertyChangedEventHandler? PropertyChanged;

        protected new virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
