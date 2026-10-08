using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;

namespace PosElectro.Desktop.Models
{
    public class CalendarDayItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public DateTime Date { get; set; }
        public int DayNumber => Date.Day;

        private bool _isCurrentMonth;
        public bool IsCurrentMonth
        {
            get => _isCurrentMonth;
            set { if (_isCurrentMonth != value) { _isCurrentMonth = value; NotifyVisuals(); } }
        }

        private bool _isToday;
        public bool IsToday
        {
            get => _isToday;
            set { if (_isToday != value) { _isToday = value; NotifyVisuals(); } }
        }

        private bool _isSelectedStart;
        public bool IsSelectedStart
        {
            get => _isSelectedStart;
            set { if (_isSelectedStart != value) { _isSelectedStart = value; NotifyVisuals(); } }
        }

        private bool _isSelectedEnd;
        public bool IsSelectedEnd
        {
            get => _isSelectedEnd;
            set { if (_isSelectedEnd != value) { _isSelectedEnd = value; NotifyVisuals(); } }
        }

        private bool _isSingleSelected;
        public bool IsSingleSelected
        {
            get => _isSingleSelected;
            set { if (_isSingleSelected != value) { _isSingleSelected = value; NotifyVisuals(); } }
        }

        private bool _isInRange;
        public bool IsInRange
        {
            get => _isInRange;
            set { if (_isInRange != value) { _isInRange = value; NotifyVisuals(); } }
        }

        public bool IsSelectedEdge => IsSelectedStart || IsSelectedEnd || IsSingleSelected;

        public CornerRadius ItemCornerRadius
        {
            get
            {
                if (IsSingleSelected) return new CornerRadius(16);
                if (IsSelectedStart) return new CornerRadius(16, 0, 0, 16);
                if (IsSelectedEnd) return new CornerRadius(0, 16, 16, 0);
                if (IsInRange) return new CornerRadius(0);
                return new CornerRadius(8);
            }
        }

        public Brush ItemBackground
        {
            get
            {
                if (IsSelectedEdge) return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0284C7"));
                if (IsInRange) return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E3A5F"));
                return Brushes.Transparent;
            }
        }

        public Brush ItemForeground
        {
            get
            {
                if (IsSelectedEdge) return Brushes.White;
                if (IsInRange) return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#38BDF8"));
                if (IsCurrentMonth) return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F1F5F9"));
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#475569"));
            }
        }

        public FontWeight ItemFontWeight => (IsSelectedEdge || IsToday) ? FontWeights.Bold : FontWeights.Normal;

        public Thickness ItemBorderThickness => (IsToday && !IsSelectedEdge && !IsInRange) ? new Thickness(1) : new Thickness(0);

        public Brush ItemBorderBrush => (IsToday && !IsSelectedEdge && !IsInRange)
            ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0284C7"))
            : Brushes.Transparent;

        public void NotifyVisuals()
        {
            OnPropertyChanged(nameof(IsCurrentMonth));
            OnPropertyChanged(nameof(IsToday));
            OnPropertyChanged(nameof(IsSelectedStart));
            OnPropertyChanged(nameof(IsSelectedEnd));
            OnPropertyChanged(nameof(IsSingleSelected));
            OnPropertyChanged(nameof(IsInRange));
            OnPropertyChanged(nameof(IsSelectedEdge));
            OnPropertyChanged(nameof(ItemCornerRadius));
            OnPropertyChanged(nameof(ItemBackground));
            OnPropertyChanged(nameof(ItemForeground));
            OnPropertyChanged(nameof(ItemFontWeight));
            OnPropertyChanged(nameof(ItemBorderThickness));
            OnPropertyChanged(nameof(ItemBorderBrush));
        }
    }
}
