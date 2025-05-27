using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TurnipVodSplitter {
    public class TextProperty(string name, string data) : INotifyPropertyChanged {
        private string _data = data;
        public string Data {
            get => _data;
            set => SetField(ref _data, value);
        }

        private string _name = name;
        public string Name {
            get => _name;
            set => SetField(ref _name, value);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null) {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
