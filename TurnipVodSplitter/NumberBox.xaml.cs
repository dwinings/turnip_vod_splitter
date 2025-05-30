using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TurnipVodSplitter
{
    public partial class NumberBox : UserControl
    {
        public NumberBox()
        {
            InitializeComponent();
        }

        public int? Maximum {
            get => (int?)GetValue(MaximumProperty);
            set => SetValue(MaximumProperty, value);
        }

        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register(nameof(Maximum), typeof(int?),
                typeof(NumberBox), new UIPropertyMetadata(null));

        public int? Minimum
        {
            get => (int?)GetValue(MinimumProperty);
            set => SetValue(MinimumProperty, value);
        }

        // Using a DependencyProperty as the backing store for Minimum.  
        // This enables animation, styling, binding, etc...
        public static readonly DependencyProperty MinimumProperty =
            DependencyProperty.Register(nameof(Minimum), typeof(int),
                typeof(NumberBox), new UIPropertyMetadata(null));

        private Regex _numRegex = new Regex(@"[0-9]+");

        public int Value {
            get => (int)GetValue(ValueProperty);
            set {
                TextBoxValue.Text = value.ToString();
                SetValue(ValueProperty, value);
            }
        }

        // Using a DependencyProperty as the backing store for Value. 
        // This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(int), typeof(NumberBox),
                new PropertyMetadata(0, OnSomeValuePropertyChanged));

        private static void OnSomeValuePropertyChanged(
            DependencyObject target,
            DependencyPropertyChangedEventArgs e
        ) {
            if (target is NumberBox numberBox) {
                numberBox.TextBoxValue.Text = e.NewValue.ToString() ?? string.Empty;
            }
        }

        private void value_TextChanged(object sender, TextChangedEventArgs e)
        {
            var tb = (TextBox)sender;

            if (!_numRegex.IsMatch(tb.Text)) {
                tb.Text = Value.ToString();
            }

            Value = Convert.ToInt32(tb.Text);
            if (Minimum != null && Value < Minimum) Value = (int)Minimum;
            if (Maximum != null && Value > Maximum) Value = (int)Maximum;
            RaiseEvent(new RoutedEventArgs(ValueChangedEvent));
        }

        private static readonly RoutedEvent ValueChangedEvent =
            EventManager.RegisterRoutedEvent("ValueChanged", RoutingStrategy.Bubble,
                typeof(RoutedEventHandler), typeof(NumberBox));

        public event RoutedEventHandler ValueChanged {
            add => AddHandler(ValueChangedEvent, value);
            remove => RemoveHandler(ValueChangedEvent, value);
        }

        private void TextBoxValue_OnPreviewTextInput(object sender, TextCompositionEventArgs e) {
            var tb = (TextBox)sender;
            var text = tb.Text.Insert(tb.CaretIndex, e.Text);
            e.Handled = !_numRegex.IsMatch(text);
        }

        private void Increase_OnClick(object sender, RoutedEventArgs e) {
            if (Maximum != null && Value == Maximum) {
                Value = (int)Maximum;
            } else {
                Value += 1;
                RaiseEvent(new RoutedEventArgs(ValueChangedEvent));
            }
        }

        private void Decrease_OnClick(object sender, RoutedEventArgs e) {
            if (Minimum != null && Value == Minimum) {
                Value = (int)Minimum;
            } else {
                Value -= 1;
                RaiseEvent(new RoutedEventArgs(ValueChangedEvent));
            }
        }
    }
}
