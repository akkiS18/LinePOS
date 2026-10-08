using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using PosElectro.Desktop.Models;

namespace PosElectro.Desktop.ViewModels
{
    public class DateRangePickerViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private DateTime _currentLeftMonth;
        public DateTime CurrentLeftMonth
        {
            get => _currentLeftMonth;
            set
            {
                if (_currentLeftMonth != value)
                {
                    _currentLeftMonth = new DateTime(value.Year, value.Month, 1);
                    RebuildMonths();
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CurrentRightMonth));
                    OnPropertyChanged(nameof(LeftMonthTitle));
                    OnPropertyChanged(nameof(RightMonthTitle));
                }
            }
        }

        public DateTime CurrentRightMonth => CurrentLeftMonth.AddMonths(1);

        public string LeftMonthTitle => $"{GetUzbekMonthName(CurrentLeftMonth.Month)} {CurrentLeftMonth.Year}";
        public string RightMonthTitle => $"{GetUzbekMonthName(CurrentRightMonth.Month)} {CurrentRightMonth.Year}";

        public ObservableCollection<CalendarDayItem> LeftDays { get; } = new();
        public ObservableCollection<CalendarDayItem> RightDays { get; } = new();

        private DateTime? _rangeStartDate;
        public DateTime? RangeStartDate
        {
            get => _rangeStartDate;
            set
            {
                if (_rangeStartDate != value)
                {
                    _rangeStartDate = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SelectedRangeText));
                }
            }
        }

        private DateTime? _rangeEndDate;
        public DateTime? RangeEndDate
        {
            get => _rangeEndDate;
            set
            {
                if (_rangeEndDate != value)
                {
                    _rangeEndDate = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SelectedRangeText));
                }
            }
        }

        public string SelectedRangeText
        {
            get
            {
                if (!RangeStartDate.HasValue) return "Sana tanlanmagan";
                if (!RangeEndDate.HasValue || RangeStartDate.Value.Date == RangeEndDate.Value.Date)
                {
                    return $"{RangeStartDate.Value:dd.MM.yyyy} (1 kun)";
                }
                int totalDays = (int)(RangeEndDate.Value.Date - RangeStartDate.Value.Date).TotalDays + 1;
                return $"{RangeStartDate.Value:dd.MM.yyyy} — {RangeEndDate.Value:dd.MM.yyyy} ({totalDays} kun)";
            }
        }

        public ICommand PreviousMonthCommand { get; }
        public ICommand NextMonthCommand { get; }
        public ICommand SelectDayCommand { get; }
        public ICommand PresetCommand { get; }
        public ICommand ResetCommand { get; }

        public DateRangePickerViewModel()
        {
            PreviousMonthCommand = new RelayCommand(() => CurrentLeftMonth = CurrentLeftMonth.AddMonths(-1));
            NextMonthCommand = new RelayCommand(() => CurrentLeftMonth = CurrentLeftMonth.AddMonths(1));
            SelectDayCommand = new RelayCommand<CalendarDayItem>(SelectDay);
            PresetCommand = new RelayCommand<string>(SetPreset);
            ResetCommand = new RelayCommand(ResetToToday);

            var today = DateTime.Today;
            _currentLeftMonth = new DateTime(today.Year, today.Month, 1).AddMonths(-1);
            _rangeStartDate = today;
            _rangeEndDate = today;

            RebuildMonths();
        }

        public void SetRange(DateTime start, DateTime end)
        {
            RangeStartDate = start.Date;
            RangeEndDate = end.Date;

            // Ko'rinadigan oylarni moslashtirish (tanlangan sana ko'rinib tursin)
            if (end.Year != CurrentRightMonth.Year || end.Month != CurrentRightMonth.Month)
            {
                CurrentLeftMonth = new DateTime(end.Year, end.Month, 1).AddMonths(-1);
            }
            else
            {
                UpdateHighlights();
            }
        }

        public void SelectDay(CalendarDayItem? item)
        {
            if (item == null) return;
            var clickedDate = item.Date.Date;

            if (!RangeStartDate.HasValue || (RangeStartDate.HasValue && RangeEndDate.HasValue))
            {
                // Birinchi bosish: faqat boshlanish sanasi
                RangeStartDate = clickedDate;
                RangeEndDate = null;
            }
            else
            {
                // Ikkinchi bosish: tugash sanasi
                if (clickedDate < RangeStartDate.Value.Date)
                {
                    RangeStartDate = clickedDate;
                    RangeEndDate = null;
                }
                else
                {
                    RangeEndDate = clickedDate;
                }
            }

            UpdateHighlights();
        }

        public void SetPreset(string? preset)
        {
            var today = DateTime.Today;
            switch (preset?.ToLowerInvariant())
            {
                case "today":
                    RangeStartDate = today;
                    RangeEndDate = today;
                    break;
                case "yesterday":
                    RangeStartDate = today.AddDays(-1);
                    RangeEndDate = today.AddDays(-1);
                    break;
                case "last7":
                    RangeStartDate = today.AddDays(-6);
                    RangeEndDate = today;
                    break;
                case "this_month":
                    RangeStartDate = new DateTime(today.Year, today.Month, 1);
                    RangeEndDate = new DateTime(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));
                    break;
                case "last_month":
                    var lastMonth = today.AddMonths(-1);
                    RangeStartDate = new DateTime(lastMonth.Year, lastMonth.Month, 1);
                    RangeEndDate = new DateTime(lastMonth.Year, lastMonth.Month, DateTime.DaysInMonth(lastMonth.Year, lastMonth.Month));
                    break;
            }

            if (RangeEndDate.HasValue)
            {
                CurrentLeftMonth = new DateTime(RangeEndDate.Value.Year, RangeEndDate.Value.Month, 1).AddMonths(-1);
            }
            else
            {
                UpdateHighlights();
            }
        }

        public void ResetToToday()
        {
            SetPreset("today");
        }

        private void RebuildMonths()
        {
            PopulateMonthDays(CurrentLeftMonth, LeftDays);
            PopulateMonthDays(CurrentRightMonth, RightDays);
            UpdateHighlights();
        }

        private static void PopulateMonthDays(DateTime monthDate, ObservableCollection<CalendarDayItem> targetCollection)
        {
            targetCollection.Clear();
            int year = monthDate.Year;
            int month = monthDate.Month;

            var firstDayOfMonth = new DateTime(year, month, 1);
            int dayOfWeek = (int)firstDayOfMonth.DayOfWeek;
            int mondayOffset = dayOfWeek == 0 ? 6 : dayOfWeek - 1;

            var startDate = firstDayOfMonth.AddDays(-mondayOffset);
            var today = DateTime.Today;

            for (int i = 0; i < 42; i++)
            {
                var date = startDate.AddDays(i);
                targetCollection.Add(new CalendarDayItem
                {
                    Date = date,
                    IsCurrentMonth = (date.Month == month && date.Year == year),
                    IsToday = (date.Date == today)
                });
            }
        }

        public void UpdateHighlights()
        {
            ApplyHighlightsToCollection(LeftDays);
            ApplyHighlightsToCollection(RightDays);
        }

        private void ApplyHighlightsToCollection(ObservableCollection<CalendarDayItem> collection)
        {
            foreach (var day in collection)
            {
                var d = day.Date.Date;
                bool isStart = RangeStartDate.HasValue && d == RangeStartDate.Value.Date;
                bool isEnd = RangeEndDate.HasValue && d == RangeEndDate.Value.Date;
                bool isSingle = isStart && (!RangeEndDate.HasValue || (RangeEndDate.HasValue && RangeStartDate.HasValue && RangeStartDate.Value.Date == RangeEndDate.Value.Date));
                bool inRange = RangeStartDate.HasValue && RangeEndDate.HasValue &&
                               d > RangeStartDate.Value.Date && d < RangeEndDate.Value.Date;

                day.IsSingleSelected = isSingle;
                day.IsSelectedStart = isStart && !isSingle;
                day.IsSelectedEnd = isEnd && !isSingle;
                day.IsInRange = inRange;
                day.NotifyVisuals();
            }
        }

        private static string GetUzbekMonthName(int month) => month switch
        {
            1 => "Yanvar",
            2 => "Fevral",
            3 => "Mart",
            4 => "Aprel",
            5 => "May",
            6 => "Iyun",
            7 => "Iyul",
            8 => "Avgust",
            9 => "Sentabr",
            10 => "Oktabr",
            11 => "Noyabr",
            12 => "Dekabr",
            _ => ""
        };
    }
}
