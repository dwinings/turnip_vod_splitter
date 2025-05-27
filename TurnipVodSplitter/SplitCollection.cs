using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CsvHelper;
using CsvHelper.Configuration;
using TurnipVodSplitter.WpfValueConverters;
using Binding = System.Windows.Data.Binding;

namespace TurnipVodSplitter {
    public partial class SplitCollection : ObservableObject, IBindingList, IList<SplitEntry>, INotifyPropertyChanged {
        public static readonly string DEFAULT_ENCODING_ARGS =
            "-c:v libx264 -preset slow -crf 23 -c:a copy -pix_fmt yuv420p";

        public SplitCollection() {
            this.Splits.Add(new SplitEntry());
            this.Splits.ListChanged += (s, e) => { this.ListChanged?.Invoke(s, e); };

            this.ffmpegCodecArgs = Properties.Settings.Default.lastFfmpegArgs == ""
                ? DEFAULT_ENCODING_ARGS
                : Properties.Settings.Default.lastFfmpegArgs;

            this.staticColumns.Add(
                new DataGridTextColumn {
                    Header = "Split Start",
                    MinWidth = 110,
                    CanUserReorder = false,
                    IsReadOnly = true,
                    Binding = new Binding("SplitStart") {
                        Converter = new ConvertTimeSpan(),
                    }
                }
            );
            this.staticColumns.Add(
                new DataGridTextColumn {
                    Header = "Split End",
                    MinWidth = 110,
                    CanUserReorder = false,
                    IsReadOnly = true,
                    Binding = new Binding("SplitEnd") {
                        Converter = new ConvertTimeSpan(),
                    }
                }
            );
            this.staticColumns.Add(
                new DataGridCheckBoxColumn {
                    Header = "Skip?",
                    CanUserReorder = false,
                    Binding = new Binding("SkipSplit")
                }
            );
            this.staticColumns.Add(
                new DataGridTextColumn {
                    Header = "Description",
                    CanUserReorder = false,
                    MinWidth = 110,
                    Binding = new Binding("Description")
                }
            );

            SyncRoot = new object();
        }

        public SplitCollection(IEnumerable<SplitEntry> splitEntries) : this() {
            foreach (var se in splitEntries) {
                this.splits.Add(se);
            }
        }

        [ObservableProperty] private string filenameFormat = "{index} - ({start}) {desc}";
        [ObservableProperty] private string? ffmpegCodecArgs = DEFAULT_ENCODING_ARGS;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Columns))]
        private ObservableCollection<DataGridColumn> staticColumns = [];
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Columns))]
        private ObservableCollection<DataGridColumn> extraColumns = [];

        public IEnumerable<DataGridColumn> Columns {
            get => StaticColumns.Concat(ExtraColumns);
            set { }
    }

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
            DeleteSplitByIdx(idx, mergeBefore = false);

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

        public bool InLastSplit(TimeSpan pos) { return this.GetSplitIndexForTime(pos) == (this.Splits.Count - 1); }
        public bool InFirstSplit(TimeSpan pos) { return this.GetSplitIndexForTime(pos) == (this.Splits.Count - 1); }

        public void Normalize() {
            // Go backward so we don't fuck up the indices by adding to spots before our iteration point.
            var maxLen = this.Splits.Count;

            for (int i = maxLen - 1; i >= 0; i--) {
                var thisSplit = this.Splits[i];
                var lastSplit = (i - 1 >= 0) ? this.Splits[i - 1] : null;

                if (thisSplit.SplitEnd < thisSplit.SplitStart) {
                    thisSplit.SplitEnd = thisSplit.SplitStart;
                }

                if (lastSplit != null) {
                    if (lastSplit.SplitEnd < thisSplit.SplitStart) {
                        // Gap
                        this.Splits.Insert(i, new() {
                            SplitStart = lastSplit.SplitEnd,
                            SplitEnd = thisSplit.SplitStart,
                            Description = "nothing",
                            SkipSplit = true
                        });
                    } else if (lastSplit.SplitEnd > thisSplit.SplitStart) {
                        // Overlap
                        lastSplit.SplitEnd = thisSplit.SplitStart;
                    }
                } else if (thisSplit.SplitStart != TimeSpan.Zero) {
                    // Fill in gap from zero to first split
                    this.Splits.Insert(0, new() {
                        SplitStart = TimeSpan.Zero,
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
                Debug.WriteLine(
                    $"WARNING: First split @ {this.Splits[0].SplitStart.VideoTimestampFormat()} starts after zero.");
            }

            ;

            if (this.Splits.Last().SplitEnd != MediaLength) {
                foundFailure = true;
                Debug.WriteLine(
                    $"WARNING: Last split ends after media end {this.Splits[0].SplitEnd.VideoTimestampFormat()} > {MediaLength.VideoTimestampFormat()}");
            }

            for (int i = 0; i < this.Splits.Count; i++) {
                var cur = this.Splits[i];
                var nxt = i < this.Splits.Count - 1 ? this.Splits[i + 1] : null;

                if (nxt != null && cur.SplitEnd < nxt.SplitStart) {
                    foundFailure = true;
                    Debug.WriteLine(
                        $"WARNING: Gap between splits found between {cur.SplitEnd.VideoTimestampFormat()} and {nxt.SplitStart.VideoTimestampFormat()}");
                }

                if (cur.SplitStart > cur.SplitEnd) {
                    foundFailure = true;
                    Debug.WriteLine(
                        $"WARNING: Split with negative length found {cur.SplitStart.VideoTimestampFormat()} to {cur.SplitEnd.VideoTimestampFormat()}");
                }
            }

            return !foundFailure;
        }

        public String YoutubeChapterFormat(string format) {
            this.Normalize();
            this.Validate();
            StringBuilder builder = new();

            foreach (var s in this.Splits) {
                builder.AppendFormat("{0} - {3}\n", s.SplitStart.VideoTimestampFormat(),
                    s.Description);
            }

            return builder.ToString();
        }

        public async Task saveToFile(Stream outputStream) {
            this.Normalize();
            this.Validate();

            StreamWriter writer = new(outputStream);
            await using var csvWriter = new CsvWriter(writer, CultureInfo.InvariantCulture);
            csvWriter.Context.RegisterClassMap(SplitEntryFieldMap);
            await csvWriter.WriteRecordsAsync(this.Splits);
            await csvWriter.FlushAsync();
        }

        public async Task LoadFromFile(Stream inputStream) {
            StreamReader streamReader = new StreamReader(inputStream);
            using var csvReader = new CsvReader(streamReader, CultureInfo.InvariantCulture);

            await csvReader.ReadAsync();
            if (!csvReader.ReadHeader()) {
                throw new Exception("malformed file no header row.");
            }

            this.Splits.Clear();
            this.ExtraColumns.Clear();

            List<string> headers = [];
            foreach (var header in csvReader.HeaderRecord ?? []) {
                if (!SplitEntry.BUILTIN_FIELDS.Contains(header)) {
                    headers.Add(header);
                    this.AddProperty(header);
                }
            }

            while (await csvReader.ReadAsync()) {
                var split = new SplitEntry {
                    SplitStart = csvReader.GetField<TimeSpan>("SplitStart"),
                    SplitEnd = csvReader.GetField<TimeSpan>("SplitEnd"),
                    SkipSplit = csvReader.GetField<bool>("SkipSplit"),
                    Description = csvReader.GetField<string>("Description") ?? "",
                };

                foreach (var header in headers) {
                    split[header] = csvReader.GetField<string>(header) ?? "";
                }

                this.Splits.Add(split);
            }

            if (this.Splits.Count == 0) {
                this.Splits.Add(new SplitEntry());
            }
        }

        public void AddProperty(string? name = null) {
            var extraColsCount = ExtraColumns.Count;

            string newColName = name ?? $"col {extraColsCount + 1}";

            if (HasProperty(newColName)) {
                Debug.WriteLine("Can't add duplicate property!");
                return;
            }

            foreach (var split in this.Splits) {
                split.ExtraProperties.Add(new TextProperty(newColName, ""));
            }

            ExtraColumns.Add(new DataGridTextColumn {
                Header = newColName,
                MinWidth = 110,
                CanUserSort = false,
                Binding = new Binding($"[{newColName}]")
            });

            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Columns"));
        }

        public bool HasProperty(string name) {
            return GetProperty(name) != null;
        }

        public DataGridColumn? GetProperty(string name) {
            return ExtraColumns.FirstOrDefault(c =>
                SplitEntry.PropNormalize(c.Header as string ?? "")
                    .Equals(SplitEntry.PropNormalize(name))
            );
        }

        public void RenameProperty(string currentName, string newName) {
            throw new NotImplementedException();
        }

        public void DeleteProperty(string propertyName) {
            var col = this.ExtraColumns.FirstOrDefault(c => c.Header.Equals(propertyName));

            if (col != null) {
                this.ExtraColumns.Remove(col);
            }
        }


        private void SetColumnsFromCsvHeader(string[] csvHeader) {
            int baseIdx = StaticColumns.Count;

            if (csvHeader.Length <= 4) {
                ExtraColumns.Clear();
                return;
            }


            for (int i = baseIdx; i < csvHeader.Length; i++) {
                ExtraColumns.Add(new DataGridTextColumn {
                    MinWidth = 110,
                    Header = csvHeader[i],
                    CanUserSort = false,
                    CanUserReorder = true,
                    Binding = new Binding($"[{csvHeader[i]}]")
                });
            }
        }

        private void ReconcileColumnsWithSplits() {


        }

        public ClassMap<SplitEntry> SplitEntryFieldMap {
            get {
                var mapType = typeof(DefaultClassMap<>).MakeGenericType(typeof(SplitEntry));
                var map = (ClassMap<SplitEntry>)ObjectResolver.Current.Resolve(mapType);

                int baseIdx = 0;
                map.Map(m => m.SplitStart).Index(baseIdx++);
                map.Map(m => m.SplitEnd).Index(baseIdx++);
                map.Map(m => m.SkipSplit).Index(baseIdx++);
                map.Map(m => m.Description).Index(baseIdx++);

                int extraIdx = 0;
                foreach (var col in this.ExtraColumns) {
                    string header = SplitEntry.PropNormalize(col.Header as string ?? "");

                    var mm = new CsvHelper.Configuration.MemberMap<SplitEntry, string>(null);
                    mm
                        .Convert(args => args.Value[header])
                        .Index(baseIdx + extraIdx)
                        .Name(header);
                    map.MemberMaps.Add(mm);

                    extraIdx++;
                }

                return map;
            }
        }

        public string? FilenameOf(SplitEntry split) {
            var format = this.FilenameFormat;
            int depth = 0;
            int splitIdx = this.Splits.IndexOf(split);

            if (splitIdx < 0) {
                throw new Exception("Can't get the index of a SplitEntry that doesn't belong to the collection.");
            }

            bool seenBackslash = false;
            bool seenDoubleBackslash = false;
            bool commonWrite = false;
            StringBuilder outputBuffer = new();
            StringBuilder attrNameBuilder = new();
            var currentBuffer = outputBuffer;

            for (int i = 0; i < format.Length; i++) {
                var currentChar = format[i];

                if (currentChar == '\\') {
                    if (seenBackslash) {
                        seenDoubleBackslash = true;
                    } else if (seenDoubleBackslash) {
                        seenDoubleBackslash = false;
                        currentBuffer.Append(@"\\");
                        seenBackslash = true;
                    } else {
                        seenBackslash = true;
                    }
                } else if (currentChar == '{') {
                    if (seenBackslash) {
                        commonWrite = true;
                    } else {
                        depth += 1;

                        if (depth == 1) {
                            currentBuffer = attrNameBuilder;
                        }
                    }

                } else if (currentChar == '}') {
                    if (!seenBackslash) {
                        switch (depth) {
                            case 0:
                                // uneven brace close, treat as normal text.
                                commonWrite = true;
                                break;
                            case 1:
                                // top-level brace close, do the thing.
                                var attrName = attrNameBuilder.ToString();

                                string? templateValue = attrName switch {
                                    "idx" => this.Splits.IndexOf(split).ToString(),
                                    "index" => this.Splits.IndexOf(split).ToString(),
                                    "description" => split.Description,
                                    "desc" => split.Description,
                                    "start" => split.SplitStart.VideoTimestampFormat(),
                                    "end" => split.SplitEnd.VideoTimestampFormat(),
                                    _ => split[attrName]
                                };

                                if (templateValue == null) {
                                    return null;
                                }

                                currentBuffer = outputBuffer;
                                attrNameBuilder.Clear();
                                outputBuffer.Append(templateValue);
                                commonWrite = false;
                                depth -= 1;

                                break;
                            default:
                                depth -= 1;
                                commonWrite = true;
                                break;
                        }
                    }
                } else {
                    commonWrite = true;
                }

                if (commonWrite) {
                    if (seenDoubleBackslash) {
                        currentBuffer.Append(@"\\");
                    }

                    seenBackslash = false;
                    seenDoubleBackslash = false;
                    currentBuffer.Append(currentChar);
                }

                commonWrite = false;
            }

            if (depth > 0) {
                return null;
            }

            return outputBuffer.ToString()
                .Replace(":", "_")
                .Replace("/", "_")
                .Replace(">", "_")
                .Replace("*", "_")
                .Replace("?", "_")
                .Replace(@"\", "_")
                .Replace("|", "_");


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
            var val = (SplitEntry?)value;

            if (val == null) {
                return -1;
            }

            return this.Splits.IndexOf(val);
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
            set {
                var val = (SplitEntry?) value;

                if (val != null) {
                    this[index] = val;
                }
            }
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
