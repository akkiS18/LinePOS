using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using PosElectro.Desktop.Data;
using PosElectro.Desktop.Models;
using PosElectro.Desktop.Services;

namespace PosElectro.Desktop.ViewModels
{
    public enum ReportTimeFilter
    {
        Today,
        Yesterday,
        ThisMonth,
        Custom
    }

    public class ReportsViewModel : ViewModelBase
    {
        private readonly DatabaseContext _db;
        private readonly CurrencyService _currencyService;

        private DateTime _startDate = DateTime.Today;
        private DateTime _endDate = DateTime.Today.AddDays(1).AddTicks(-1);
        private DateTime _customStartDate = DateTime.Today;
        private DateTime _customEndDate = DateTime.Today;

        private ReportTimeFilter _selectedFilter = ReportTimeFilter.Today;
        private bool _isRefreshingRate;
        private string _statusMessage = string.Empty;

        public ObservableCollection<Sale> Sales { get; } = new();
        public ObservableCollection<string> Categories { get; } = new();
        public ObservableCollection<Warehouse> Warehouses { get; } = new();

        private string _selectedCategory = "Barchasi";
        public string SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (SetProperty(ref _selectedCategory, value))
                {
                    LoadData();
                }
            }
        }

        private Warehouse? _selectedWarehouse;
        public Warehouse? SelectedWarehouse
        {
            get => _selectedWarehouse;
            set
            {
                if (SetProperty(ref _selectedWarehouse, value))
                {
                    LoadData();
                }
            }
        }

        public ICommand FilterTodayCommand { get; }
        public ICommand FilterYesterdayCommand { get; }
        public ICommand FilterThisMonthCommand { get; }
        public ICommand FilterCustomCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand RefreshUsdRateCommand { get; }
        public ICommand OpenExportModalCommand { get; }
        public ICommand CloseExportModalCommand { get; }
        public ICommand ExecuteExportCommand { get; }
        public ICommand SetExportFilterCommand { get; }

        // --- EXCEL EXPORT MODAL PROPERTIES ---
        private bool _isExportModalOpen;
        public bool IsExportModalOpen
        {
            get => _isExportModalOpen;
            set => SetProperty(ref _isExportModalOpen, value);
        }

        private ReportTimeFilter _exportFilter = ReportTimeFilter.Today;
        public ReportTimeFilter ExportFilter
        {
            get => _exportFilter;
            set
            {
                if (SetProperty(ref _exportFilter, value))
                {
                    OnPropertyChanged(nameof(IsExportToday));
                    OnPropertyChanged(nameof(IsExportYesterday));
                    OnPropertyChanged(nameof(IsExportThisMonth));
                    OnPropertyChanged(nameof(IsExportCustom));
                    OnPropertyChanged(nameof(IsExportCustomDateVisible));
                }
            }
        }

        public bool IsExportToday => ExportFilter == ReportTimeFilter.Today;
        public bool IsExportYesterday => ExportFilter == ReportTimeFilter.Yesterday;
        public bool IsExportThisMonth => ExportFilter == ReportTimeFilter.ThisMonth;
        public bool IsExportCustom => ExportFilter == ReportTimeFilter.Custom;
        public bool IsExportCustomDateVisible => ExportFilter == ReportTimeFilter.Custom;

        private DateTime _exportCustomStartDate = DateTime.Today;
        public DateTime ExportCustomStartDate
        {
            get => _exportCustomStartDate;
            set => SetProperty(ref _exportCustomStartDate, value);
        }

        private DateTime _exportCustomEndDate = DateTime.Today;
        public DateTime ExportCustomEndDate
        {
            get => _exportCustomEndDate;
            set => SetProperty(ref _exportCustomEndDate, value);
        }

        private Warehouse? _exportSelectedWarehouse;
        public Warehouse? ExportSelectedWarehouse
        {
            get => _exportSelectedWarehouse;
            set => SetProperty(ref _exportSelectedWarehouse, value);
        }

        private string _exportSelectedCategory = "Barchasi";
        public string ExportSelectedCategory
        {
            get => _exportSelectedCategory;
            set => SetProperty(ref _exportSelectedCategory, value);
        }

        public ReportsViewModel(DatabaseContext db, CurrencyService currencyService)
        {
            _db = db;
            _currencyService = currencyService;

            _currencyService.RateUpdated += _ =>
            {
                OnPropertyChanged(nameof(UsdRate));
                OnPropertyChanged(nameof(UsdRateText));
                RecalculateSummary();
            };

            _db.ProductsChanged += () =>
            {
                var dispatcher = System.Windows.Application.Current?.Dispatcher;
                if (dispatcher != null && !dispatcher.CheckAccess())
                {
                    dispatcher.Invoke(LoadFilterOptions);
                }
                else
                {
                    LoadFilterOptions();
                }
            };
            _db.WarehousesChanged += () =>
            {
                var dispatcher = System.Windows.Application.Current?.Dispatcher;
                if (dispatcher != null && !dispatcher.CheckAccess())
                {
                    dispatcher.Invoke(LoadFilterOptions);
                }
                else
                {
                    LoadFilterOptions();
                }
            };

            FilterTodayCommand = new RelayCommand(SetToday);
            FilterYesterdayCommand = new RelayCommand(SetYesterday);
            FilterThisMonthCommand = new RelayCommand(SetThisMonth);
            FilterCustomCommand = new RelayCommand(SetCustom);
            RefreshCommand = new RelayCommand(RefreshData);
            RefreshUsdRateCommand = new RelayCommand(RefreshUsdRate);
            OpenExportModalCommand = new RelayCommand(OpenExportModal);
            CloseExportModalCommand = new RelayCommand(() => IsExportModalOpen = false);
            ExecuteExportCommand = new RelayCommand(ExecuteExport);
            SetExportFilterCommand = new RelayCommand<string>(filter =>
            {
                if (filter == "today") ExportFilter = ReportTimeFilter.Today;
                else if (filter == "yesterday") ExportFilter = ReportTimeFilter.Yesterday;
                else if (filter == "this_month") ExportFilter = ReportTimeFilter.ThisMonth;
                else if (filter == "custom") ExportFilter = ReportTimeFilter.Custom;
            });

            LoadFilterOptions();
            LoadData();
        }

        public void LoadFilterOptions()
        {
            var curCat = _selectedCategory;
            Categories.Clear();
            Categories.Add("Barchasi");
            var prods = _db.GetAllProducts();
            var cats = prods.Select(p => p.Category).Where(c => !string.IsNullOrWhiteSpace(c) && c != "Barchasi").Distinct().OrderBy(c => c);
            foreach (var c in cats) Categories.Add(c);
            _selectedCategory = Categories.Contains(curCat) ? curCat : "Barchasi";
            OnPropertyChanged(nameof(SelectedCategory));

            var curWhGuid = _selectedWarehouse?.Guid;
            Warehouses.Clear();
            var allWh = new Warehouse { Guid = "all", Name = "Barcha omborlar" };
            Warehouses.Add(allWh);
            var whs = _db.GetWarehouses();
            foreach (var w in whs) Warehouses.Add(w);
            _selectedWarehouse = Warehouses.FirstOrDefault(w => w.Guid == curWhGuid) ?? allWh;
            OnPropertyChanged(nameof(SelectedWarehouse));
        }

        public ReportTimeFilter SelectedFilter
        {
            get => _selectedFilter;
            set
            {
                if (SetProperty(ref _selectedFilter, value))
                {
                    OnPropertyChanged(nameof(IsTodaySelected));
                    OnPropertyChanged(nameof(IsYesterdaySelected));
                    OnPropertyChanged(nameof(IsThisMonthSelected));
                    OnPropertyChanged(nameof(IsCustomSelected));
                    OnPropertyChanged(nameof(IsCustomDateVisible));
                }
            }
        }

        public bool IsTodaySelected => SelectedFilter == ReportTimeFilter.Today;
        public bool IsYesterdaySelected => SelectedFilter == ReportTimeFilter.Yesterday;
        public bool IsThisMonthSelected => SelectedFilter == ReportTimeFilter.ThisMonth;
        public bool IsCustomSelected => SelectedFilter == ReportTimeFilter.Custom;
        public bool IsCustomDateVisible => SelectedFilter == ReportTimeFilter.Custom;

        public DateTime CustomStartDate
        {
            get => _customStartDate;
            set
            {
                if (SetProperty(ref _customStartDate, value))
                {
                    if (SelectedFilter == ReportTimeFilter.Custom)
                    {
                        _startDate = _customStartDate.Date;
                        LoadData();
                    }
                }
            }
        }

        public DateTime CustomEndDate
        {
            get => _customEndDate;
            set
            {
                if (SetProperty(ref _customEndDate, value))
                {
                    if (SelectedFilter == ReportTimeFilter.Custom)
                    {
                        _endDate = _customEndDate.Date.AddDays(1).AddTicks(-1);
                        LoadData();
                    }
                }
            }
        }

        public double UsdRate => _currencyService.GetCachedUsdRate();
        public string UsdRateText => UsdRate % 1 == 0 ? $"1$ = {UsdRate:N0} so'm" : $"1$ = {UsdRate:N2} so'm";

        public bool IsRefreshingRate
        {
            get => _isRefreshingRate;
            set => SetProperty(ref _isRefreshingRate, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        // --- 4 TA KPI KO'RSATKICHI (Mobil ilova bilan 100% bir xil) ---
        public double TotalRevenue => Sales.Sum(s => s.TotalAmount);
        public double TotalCashRevenue => Sales.Sum(s => s.CashAmount);
        public double TotalCardRevenue => Sales.Sum(s => s.CardAmount);
        public double TotalTaxAmount => Sales.Sum(s => s.TaxAmount);

        public double TotalCost => Sales.Sum(s => s.Items != null && s.Items.Count > 0
            ? s.Items.Sum(i => (i.CostCurrency == "USD" ? i.CostAtSale * UsdRate : i.CostAtSale) * i.Quantity)
            : s.TotalCost);

        public double TotalProfit => (TotalRevenue - TotalTaxAmount) - TotalCost;
        public double TotalProfitUsd => UsdRate > 0 ? TotalProfit / UsdRate : 0.0;
        public int TotalSalesCount => Sales.Count;
        public double TotalItemsCount => Sales.Sum(s => s.Items?.Sum(i => i.Quantity) ?? 0.0);

        public string TotalRevenueText => $"{TotalRevenue:N0} so'm";
        public string TotalCashRevenueText => $"{TotalCashRevenue:N0} so'm";
        public string TotalCardRevenueText => $"{TotalCardRevenue:N0} so'm";
        public string TotalTaxAmountText => $"{TotalTaxAmount:N0} so'm";
        public string TotalProfitText => $"+{TotalProfit:N0} so'm";
        public string TotalProfitUsdText => $"(${TotalProfitUsd:N2})";
        public string TotalSalesCountText => $"{TotalSalesCount} ta chek";
        public string TotalItemsCountText => $"{TotalItemsCount:0.##} ta/m";

        public void SetToday()
        {
            SelectedFilter = ReportTimeFilter.Today;
            _startDate = DateTime.Today;
            _endDate = DateTime.Today.AddDays(1).AddTicks(-1);
            LoadData();
        }

        public void SetYesterday()
        {
            SelectedFilter = ReportTimeFilter.Yesterday;
            _startDate = DateTime.Today.AddDays(-1);
            _endDate = DateTime.Today.AddTicks(-1);
            LoadData();
        }

        public void SetThisMonth()
        {
            SelectedFilter = ReportTimeFilter.ThisMonth;
            _startDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1, 0, 0, 0);
            _endDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, DateTime.DaysInMonth(DateTime.Today.Year, DateTime.Today.Month), 23, 59, 59, 999);
            LoadData();
        }

        public void SetCustom()
        {
            SelectedFilter = ReportTimeFilter.Custom;
            _startDate = CustomStartDate.Date;
            _endDate = CustomEndDate.Date.AddDays(1).AddTicks(-1);
            LoadData();
        }

        public async void RefreshUsdRate()
        {
            IsRefreshingRate = true;
            StatusMessage = "Markaziy bankdan kurs yuklanmoqda...";
            var (success, rate, msg) = await _currencyService.FetchLatestUsdRateAsync();
            IsRefreshingRate = false;
            StatusMessage = msg;

            MessageBox.Show(msg, "Dollar Kursi (CBU)", MessageBoxButton.OK,
                success ? MessageBoxImage.Information : MessageBoxImage.Warning);

            RecalculateSummary();
        }

        public async void RefreshData()
        {
            LoadData();
            IsRefreshingRate = true;
            await _currencyService.FetchLatestUsdRateAsync();
            IsRefreshingRate = false;
            RecalculateSummary();
            StatusMessage = $"✅ Ma'lumotlar yangilandi ({DateTime.Now:HH:mm:ss})";
        }

        public void LoadData()
        {
            var rawSales = _db.GetSales(_startDate, _endDate);
            Sales.Clear();

            string? catFilter = (SelectedCategory == "Barchasi") ? null : SelectedCategory;
            string? whGuidFilter = (_selectedWarehouse == null || _selectedWarehouse.Guid == "all") ? null : _selectedWarehouse.Guid;

            var allProds = _db.GetAllProducts(includeDeleted: true).ToDictionary(p => p.Id, p => p);

            foreach (var s in rawSales)
            {
                if (catFilter == null && whGuidFilter == null)
                {
                    Sales.Add(s);
                }
                else
                {
                    bool match = s.Items.Any(item =>
                    {
                        bool catMatch = catFilter == null || (allProds.TryGetValue(item.ProductId, out var prod) && string.Equals(prod.Category, catFilter, StringComparison.OrdinalIgnoreCase));
                        bool whMatch = whGuidFilter == null || item.WarehouseGuid == whGuidFilter;
                        return catMatch && whMatch;
                    });
                    if (match) Sales.Add(s);
                }
            }

            RecalculateSummary();
        }

        public void RecalculateSummary()
        {
            OnPropertyChanged(nameof(TotalRevenue));
            OnPropertyChanged(nameof(TotalCost));
            OnPropertyChanged(nameof(TotalProfit));
            OnPropertyChanged(nameof(TotalProfitUsd));
            OnPropertyChanged(nameof(TotalSalesCount));
            OnPropertyChanged(nameof(TotalItemsCount));

            OnPropertyChanged(nameof(TotalRevenueText));
            OnPropertyChanged(nameof(TotalCashRevenueText));
            OnPropertyChanged(nameof(TotalCardRevenueText));
            OnPropertyChanged(nameof(TotalTaxAmountText));
            OnPropertyChanged(nameof(TotalProfitText));
            OnPropertyChanged(nameof(TotalProfitUsdText));
            OnPropertyChanged(nameof(TotalSalesCountText));
            OnPropertyChanged(nameof(TotalItemsCountText));
            OnPropertyChanged(nameof(UsdRateText));
        }

        public void OpenExportModal()
        {
            ExportFilter = SelectedFilter;
            ExportCustomStartDate = CustomStartDate;
            ExportCustomEndDate = CustomEndDate;
            ExportSelectedWarehouse = SelectedWarehouse ?? Warehouses.FirstOrDefault();
            ExportSelectedCategory = SelectedCategory ?? "Barchasi";
            IsExportModalOpen = true;
        }

        public void ExecuteExport()
        {
            DateTime start;
            DateTime end;
            string periodTitle;

            switch (ExportFilter)
            {
                case ReportTimeFilter.Yesterday:
                    start = DateTime.Today.AddDays(-1);
                    end = DateTime.Today.AddTicks(-1);
                    periodTitle = "Kecha";
                    break;
                case ReportTimeFilter.ThisMonth:
                    start = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1, 0, 0, 0);
                    end = new DateTime(DateTime.Today.Year, DateTime.Today.Month, DateTime.DaysInMonth(DateTime.Today.Year, DateTime.Today.Month), 23, 59, 59, 999);
                    periodTitle = "Shu oy";
                    break;
                case ReportTimeFilter.Custom:
                    start = ExportCustomStartDate.Date;
                    end = ExportCustomEndDate.Date.AddDays(1).AddTicks(-1);
                    periodTitle = $"{ExportCustomStartDate:dd.MM.yyyy} - {ExportCustomEndDate:dd.MM.yyyy}";
                    break;
                case ReportTimeFilter.Today:
                default:
                    start = DateTime.Today;
                    end = DateTime.Today.AddDays(1).AddTicks(-1);
                    periodTitle = "Bugun";
                    break;
            }

            string catFilter = ExportSelectedCategory ?? "Barchasi";
            string? whGuidFilter = (ExportSelectedWarehouse == null || ExportSelectedWarehouse.Guid == "all") ? null : ExportSelectedWarehouse.Guid;
            string whTitle = ExportSelectedWarehouse?.Name ?? "Barcha omborlar";

            var detailedItems = _db.GetDetailedReportItems(start, end, UsdRate, catFilter, whGuidFilter);

            double rev = 0;
            double cost = 0;
            double prof = 0;
            foreach (var it in detailedItems)
            {
                rev += it.TotalPrice;
                cost += it.CostPrice * it.Quantity;
                prof += it.Profit;
            }

            var rawSales = _db.GetSales(start, end);
            int salesCount = rawSales.Count;

            bool exported = ExcelExportService.ExportReport(periodTitle, rev, cost, prof, salesCount, detailedItems, catFilter, whTitle);
            if (exported)
            {
                IsExportModalOpen = false;
                StatusMessage = "✅ Excel hisoboti muvaffaqiyatli saqlandi";
            }
        }
    }
}
