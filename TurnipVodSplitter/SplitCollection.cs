using System;
using System.Collections;
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
using TurnipVodSplitter.Properties;

namespace TurnipVodSplitter {
    public partial class SplitCollection: ObservableObject, IBindingList, IList<SplitEntry>, INotifyPropertyChanged {
        public static readonly string DEFAULT_ENCODING_ARGS = "-c:v libx264 -preset slow -crf 23 -c:a copy -pix_fmt yuv420p";
        public SplitCollection() {
            this.Splits.Add(new SplitEntry());
            this.Splits.ListChanged += (s, e) => {
                this.ListChanged?.Invoke(s, e);
            };

            this.ffmpegCodecArgs = Properties.Settings.Default.lastFfmpegArgs == "" 
                ? DEFAULT_ENCODING_ARGS 
                : Properties.Settings.Default.lastFfmpegArgs;
        }

        public SplitCollection(IEnumerable<SplitEntry> splitEntries): this() {
            foreach (var se in splitEntries) {
                this.splits.Add(se);
            }
        }

        [ObservableProperty] private string eventName = "";
        [ObservableProperty] private string? ffmpegCodecArgs = DEFAULT_ENCODING_ARGS;
        partial void OnFfmpegCodecArgsChanged(string? value) {
            Debug.WriteLine($"ffmpeg codec setting to {value}");
            Properties.Settings.Default.lastFfmpegArgs = value;
            Properties.Settings.Default.Save();

            if (value == "") {
                FfmpegCodecArgs = DEFAULT_ENCODING_ARGS;
            }
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
            DeleteSplitByIdx(idx, mergeBefore=false);

        }

        public bool DeleteSplitByIdx(int idx, bool mergeBefore = false) {
            bool deleted = false;
            if (idx < 0) {
                return false;
            }

            if (Splits.Count == 1) {
                return false;
            }

            // If we're the last split, we want to merge with the previous one.
            if (idx == this.Splits.Count - 1) {
                this.Splits.RemoveAt(idx);
                deleted = true;
                this.Splits.Last().SplitEnd = this.MediaLength;
            } else if (idx == 0) {
                this.Splits.RemoveAt(idx);
                deleted = true;
                this.Splits.First().SplitStart = TimeSpan.Zero;
            } else {
                if (mergeBefore) {
                    this.Splits[idx - 1].SplitEnd = this.Splits[idx].SplitEnd;
                } else {
                    this.Splits[idx + 1].SplitStart = this.Splits[idx].SplitStart;
                }

                this.Splits.RemoveAt(idx);
                deleted = true;
            }

            if (this.Splits.Count == 0) {
                this.Splits.Add(new SplitEntry());
            }

            return deleted;
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

        public IEnumerator<SplitEntry> GetEnumerator() {
            return Splits.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }
        public void Add(SplitEntry item) {
            this.Splits.Add(item);
        }

        public int Add(object? value) {
            return ((IBindingList)this.Splits).Add(value);
        }

        public void Clear() {
            this.Splits.Clear();
            this.Splits.Add(new SplitEntry());
        }

        public bool Contains(SplitEntry item) {
            return Splits.Contains(item);
        }

        public void CopyTo(SplitEntry[] array, int arrayIndex) {
            this.Splits.CopyTo(array, arrayIndex);
        }

        public bool Remove(SplitEntry item) {
            var idx = this.IndexOf(item);
            return this.DeleteSplitByIdx(idx);
        }

        public int IndexOf(object? value) {
            return this.Splits.IndexOf((SplitEntry)value);
        }

        public void Insert(int index, object? value) {
            ((IBindingList)Splits).Insert(index, value);
        }

        public void Remove(object? value) {
            var idx = this.IndexOf(value);
            this.DeleteSplitByIdx(idx);
        }

        public bool Contains(object? item) {
            return this.Splits.Contains(item);
        }

        public void CopyTo(Array array, int index) {
            ((IBindingList)this.Splits).CopyTo(array, index);
        }

        public int Count => this.Splits.Count;
        public bool IsSynchronized { get; }
        public object SyncRoot { get; }
        public bool IsReadOnly => false;
        object? IList.this[int index] {
            get => this[index];
            set => this[index] = (SplitEntry)value;
        }

        public int IndexOf(SplitEntry item) {
            return this.Splits.IndexOf(item);
        }
        public void Insert(int index, SplitEntry item) {
            this.Splits.Insert(index, item);
        }
        public void RemoveAt(int index) {
            this.DeleteSplitByIdx(index);
        }

        public bool IsFixedSize => false;

        public SplitEntry this[int index] {
            get => Splits[index];
            set => Splits[index] = value;
        }

        public void AddIndex(PropertyDescriptor property) {
            throw new NotImplementedException();
        }
        public object? AddNew() {
            var split = new SplitEntry();
            this.Splits.Add(split);
            return split;

        }

        public void ApplySort(PropertyDescriptor property, ListSortDirection direction) {
            throw new NotImplementedException();
        }
        public int Find(PropertyDescriptor property, object key) {
            throw new NotImplementedException();
        }
        public void RemoveIndex(PropertyDescriptor property) {
            throw new NotImplementedException();
        }
        public void RemoveSort() {
            throw new NotImplementedException();
        }

        public bool AllowEdit => true;
        public bool AllowNew => true;
        public bool AllowRemove => true;
        public bool IsSorted => throw new NotSupportedException();
        public ListSortDirection SortDirection => throw new NotSupportedException();
        public PropertyDescriptor? SortProperty => throw new NotSupportedException();

        public bool SupportsChangeNotification => true;
        public bool SupportsSearching => false;
        public bool SupportsSorting => false;
        public event ListChangedEventHandler? ListChanged;
    }
}
