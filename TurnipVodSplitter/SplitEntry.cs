using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CsvHelper.Configuration;

namespace TurnipVodSplitter {
    public partial class SplitEntry : ObservableObject {
        [ObservableProperty] private TimeSpan splitStart = TimeSpan.Zero;
        [ObservableProperty] private TimeSpan splitEnd = TimeSpan.Zero;
        [ObservableProperty] private bool skipSplit = false;
        [ObservableProperty] private string description = "";

        [ObservableProperty]
        private ObservableCollection<TextProperty> extraProperties = [];

        public static ISet<string> BUILTIN_FIELDS = new HashSet<string> {
            "SplitStart",
            "SplitEnd",
            "SkipSplit",
            "Description"
        };

        partial void OnExtraPropertiesChanged(ObservableCollection<TextProperty> value) {
            foreach (var prop in value) {
                OnPropertyChanged($"Item[{prop.Name}]");
            }
        }

        [IndexerName("Item")]
        public string? this[string propName] {
            get {
                return this.ExtraProperties.FirstOrDefault(p => p.Name.ToLower().Equals(propName.ToLower()))?.Data;
            }
            set {
                var prop = this.ExtraProperties.FirstOrDefault(p => PropNormalize(p.Name).Equals(PropNormalize(propName)));

                if (prop == null) {
                    prop = new TextProperty(propName, value ?? "");
                    this.ExtraProperties.Add(prop);
                } else {
                    prop.Data = value ?? "";
                }

                OnPropertyChanged($"Item[{prop.Name}]");
            }
        }

        public static string PropNormalize(string propName) {
            return propName.ToLower().Replace(" ", "");

        }

        public bool Validate() {
            if (SplitEnd <= SplitStart) return false;
            return true;
        }
    }

}
