using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using PosElectro.Desktop.Data;
using PosElectro.Desktop.Models;
using PosElectro.Desktop.Services;
using PosElectro.Desktop.Views;

namespace PosElectro.Desktop.ViewModels
{
    public class CategoryCardModel : ViewModelBase
    {
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
        public bool IsAll { get; set; }
        public bool IsLowStock { get; set; }
        public bool HasLowStockWarning => IsLowStock && Count > 0;
        public string Icon { get; set; } = "📁";
        public string CountText => $"{Count} ta tovar";
        public bool CanEdit => !IsAll && !IsLowStock;

        // ---- Maket 2.1: faqat ko'rsatish uchun ----
        public int LowCount { get; set; }
        public int OutCount { get; set; }
        public bool HasBadge => !IsLowStock && (LowCount > 0 || OutCount > 0);
        public bool IsOutBadge => OutCount > 0;
        public string BadgeText => OutCount > 0 ? $"{OutCount} tugagan" : $"{LowCount} ta kam";
        public string DisplayName => IsAll ? "Barcha tovarlar" : Name;

        /// <summary>Kategoriya nomiga qarab maketdagi chiziqli ikonka (24×24).</summary>
        public string IconData
        {
            get
            {
                if (IsAll) return "M3 3h7v7H3z M14 3h7v7h-7z M3 14h7v7H3z M14 14h7v7h-7z";
                var n = (Name ?? string.Empty).ToLowerInvariant();
                if (n.Contains("kabel") || n.Contains("sim") || n.Contains("провод") || n.Contains("кабел")) return "M4 20c4 0 4-16 8-16s4 16 8 16";
                if (n.Contains("avtomat") || n.Contains("автомат")) return "M7 3h10v18H7z M10 9h4v5h-4z";
                if (n.Contains("rozetka") || n.Contains("kalit") || n.Contains("розет") || n.Contains("выкл")) return "M5 5h14v14H5z M9.5 12h.01 M14.5 12h.01";
                if (n.Contains("yorit") || n.Contains("lamp") || n.Contains("chiroq") || n.Contains("led") || n.Contains("ламп") || n.Contains("свет")) return "M9 18h6 M10 21h4 M12 3a6 6 0 0 0-4 10.5c.7.7 1 1.5 1 2.5h6c0-1 .3-1.8 1-2.5A6 6 0 0 0 12 3z";
                if (n.Contains("asbob") || n.Contains("инструм")) return "M15 12L6 21l-3-3l9-9 M14 4l6 6l-3 3l-6-6z";
                if (n.Contains("aksessuar") || n.Contains("аксесс")) return "M14.7 6.3a4 4 0 0 0-5.4 5.4L3 18l3 3l6.3-6.3a4 4 0 0 0 5.4-5.4l-2.5 2.5l-2.4-.6l-.6-2.4z";
                return "M4 4h16v16H4z M9 9h6";
            }
        }
    }

    /// <summary>"Kam qolgan tovarlar" ro'yxatidagi kategoriya filtri qatori (belgilash katakchasi).</summary>
    public class LowStockCategoryFilterItem : ViewModelBase
    {
        private readonly Action _onChanged;
        public LowStockCategoryFilterItem(string name, int count, bool isSelected, Action onChanged)
        {
            Name = name;
            Count = count;
            _isSelected = isSelected;
            _onChanged = onChanged;
        }

        public string Name { get; }
        public int Count { get; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (SetProperty(ref _isSelected, value)) _onChanged();
            }
        }

        /// <summary>Qayta chizishni chaqirmasdan belgini o'zgartirish ("Tozalash" uchun).</summary>
        public void SetSilently(bool value)
        {
            if (_isSelected == value) return;
            _isSelected = value;
            OnPropertyChanged(nameof(IsSelected));
        }
    }

    public enum InventoryViewState
    {
        WarehousesGrid,
        CategoriesGrid,
        ProductsList,
        AddEditForm
    }

    public class InventoryViewModel : ViewModelBase
    {
        private static readonly NumberFormatInfo SpaceGroupFormat = new()
        {
            NumberGroupSeparator = " ",
            NumberGroupSizes = new[] { 3 },
            NumberDecimalDigits = 0
        };

        private static string CleanNumberInput(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            var cleaned = Regex.Replace(input, @"[\s,\u00A0']", "");
            return Regex.Replace(cleaned, @"[^\d.]", "");
        }
        private readonly DatabaseContext _db;
        private readonly ProductService _productService;
        private readonly CurrencyService _currencyService;

        private InventoryViewState _currentViewState = InventoryViewState.WarehousesGrid;
        private InventoryViewState _previousViewState = InventoryViewState.WarehousesGrid;
        private string _searchQuery = string.Empty;
        private string _selectedCategory = ProductService.CATEGORY_ALL;
        private Warehouse? _selectedWarehouse;
        private Product? _editingProduct;

        // Form Maydonlari (To'liq Sahifa uchun)
        private string _formTitle = "Yangi tovar";
        private string _formName = string.Empty;
        private string _formBarcode = string.Empty;
        private string _formCategory = "Barchasi";
        private string _formCostPrice = "0";
        private bool _formIsUsd;
        private string _formUsdConversion = string.Empty;
        private string _formSellingPrice = "0";
        private string _formSellingPrice2 = string.Empty;
        private string _formStockQuantity = "0";
        private int _formUnitTypeIndex; // 0: dona, 1: metr, 2: kg
        private string _formMinStockAlert = "3";
        private string _formNote = string.Empty;
        private string _formErrorMessage = string.Empty;

        private List<Product> _cachedAllProducts = new();
        private List<Product> _cachedDisplayProducts = new();
        private Dictionary<string, double> _cachedWhStocks = new();
        private readonly System.Windows.Threading.DispatcherTimer _searchDebounceTimer;

        public ObservableCollection<Warehouse> Warehouses { get; } = new();
        public ObservableCollection<CategoryCardModel> CategoryCards { get; } = new();
        
        private ObservableCollection<Product> _products = new();
        public ObservableCollection<Product> Products
        {
            get => _products;
            private set => SetProperty(ref _products, value);
        }

        public ObservableCollection<string> ExistingCategories { get; } = new();

        public bool HasSearchQuery => !string.IsNullOrWhiteSpace(SearchQuery);
        public bool IsEmptySearchResult => IsProductsListVisible && Products.Count == 0;

        public Warehouse? SelectedWarehouse
        {
            get => _selectedWarehouse;
            set
            {
                if (SetProperty(ref _selectedWarehouse, value))
                {
                    OnPropertyChanged(nameof(CurrentWarehouseTitle));
                    OnPropertyChanged(nameof(CanGoBackToWarehouses));
                    OnPropertyChanged(nameof(CanAddProduct));
                    OnPropertyChanged(nameof(SelectedWarehouseNameDisplay));
                    OnPropertyChanged(nameof(OverviewSearchPlaceholder));
                    OnPropertyChanged(nameof(BackButtonText));
                }
            }
        }

        public string CurrentWarehouseTitle => SelectedWarehouse != null ? SelectedWarehouse.Name : "Barcha omborlar";
        public bool CanGoBackToWarehouses => SelectedWarehouse != null && CurrentViewState == InventoryViewState.CategoriesGrid;
        public bool CanAddProduct => CurrentViewState != InventoryViewState.AddEditForm;
        public string SelectedWarehouseNameDisplay => SelectedWarehouse != null ? SelectedWarehouse.Name : "Tanlanmagan";
        public string BackButtonText => "Ortga qaytish";

        // Kategoriya qo'shish modali
        private bool _isAddCategoryModalOpen;
        public bool IsAddCategoryModalOpen
        {
            get => _isAddCategoryModalOpen;
            set => SetProperty(ref _isAddCategoryModalOpen, value);
        }

        private string _newCategoryInput = string.Empty;
        public string NewCategoryInput
        {
            get => _newCategoryInput;
            set => SetProperty(ref _newCategoryInput, value);
        }

        private string _newCategoryErrorMessage = string.Empty;
        public string NewCategoryErrorMessage
        {
            get => _newCategoryErrorMessage;
            set => SetProperty(ref _newCategoryErrorMessage, value);
        }

        // Kategoriya tahrirlash modali
        private bool _isEditCategoryModalOpen;
        public bool IsEditCategoryModalOpen
        {
            get => _isEditCategoryModalOpen;
            set => SetProperty(ref _isEditCategoryModalOpen, value);
        }

        private string _editCategoryOldName = string.Empty;
        public string EditCategoryOldName
        {
            get => _editCategoryOldName;
            set { if (SetProperty(ref _editCategoryOldName, value)) OnPropertyChanged(nameof(EditCategoryCountText)); }
        }
        /// <summary>Maket 2.13: "86 ta tovar" (faqat ko'rsatish).</summary>
        public string EditCategoryCountText => string.IsNullOrWhiteSpace(EditCategoryOldName) ? string.Empty
            : $"{_productService.GetProductsByCategory(EditCategoryOldName).Count} ta tovar";

        private string _editCategoryInput = string.Empty;
        public string EditCategoryInput
        {
            get => _editCategoryInput;
            set => SetProperty(ref _editCategoryInput, value);
        }

        private string _editCategoryErrorMessage = string.Empty;
        public string EditCategoryErrorMessage
        {
            get => _editCategoryErrorMessage;
            set => SetProperty(ref _editCategoryErrorMessage, value);
        }

        // Ombor qo'shish modali
        private bool _isAddWarehouseModalOpen;
        public bool IsAddWarehouseModalOpen
        {
            get => _isAddWarehouseModalOpen;
            set { if (SetProperty(ref _isAddWarehouseModalOpen, value)) OnPropertyChanged(nameof(CurrentPrimaryWarehouseName)); }
        }
        /// <summary>Maket 2.12: "Hozirgi asosiy: ..." (faqat ko'rsatish).</summary>
        public string CurrentPrimaryWarehouseName => Warehouses.FirstOrDefault(w => w.IsPrimary)?.Name ?? "—";

        private string _newWarehouseName = string.Empty;
        public string NewWarehouseName
        {
            get => _newWarehouseName;
            set => SetProperty(ref _newWarehouseName, value);
        }

        private bool _newWarehouseIsPrimary;
        public bool NewWarehouseIsPrimary
        {
            get => _newWarehouseIsPrimary;
            set => SetProperty(ref _newWarehouseIsPrimary, value);
        }

        private string _newWarehouseErrorMessage = string.Empty;
        public string NewWarehouseErrorMessage
        {
            get => _newWarehouseErrorMessage;
            set => SetProperty(ref _newWarehouseErrorMessage, value);
        }

        // Omborlararo ko'chirish modali
        private bool _isTransferModalOpen;
        public bool IsTransferModalOpen
        {
            get => _isTransferModalOpen;
            set { if (SetProperty(ref _isTransferModalOpen, value)) NotifyTransferPreview(); }
        }

        // ---- Maket 2.11: ko'chirish oynasidagi ko'rsatkichlar (faqat ko'rsatish; ko'chirish logikasi o'zgarmagan) ----
        private void NotifyTransferPreview()
        {
            OnPropertyChanged(nameof(HasTransferPreview));
            OnPropertyChanged(nameof(TransferAvailableText));
            OnPropertyChanged(nameof(TransferFromLine));
            OnPropertyChanged(nameof(TransferFromAfter));
            OnPropertyChanged(nameof(TransferToLine));
            OnPropertyChanged(nameof(TransferToAfter));
            OnPropertyChanged(nameof(IsTransferOverStock));
            OnPropertyChanged(nameof(TransferButtonText));
        }
        private bool TryGetTransferQty(out double qty) =>
            double.TryParse(TransferQuantity, NumberStyles.Any, CultureInfo.InvariantCulture, out qty) && qty > 0;
        private double TransferStockIn(Warehouse? w) =>
            (!IsTransferModalOpen || TransferProduct == null || w == null) ? 0 : _db.GetProductStockInWarehouse(TransferProduct.Guid, w.Guid);

        public bool HasTransferPreview => IsTransferModalOpen && TransferProduct != null && TransferFromWarehouse != null
            && TransferToWarehouse != null && TransferFromWarehouse.Guid != TransferToWarehouse.Guid;
        public string TransferAvailableText => (TransferProduct == null || TransferFromWarehouse == null) ? string.Empty
            : $"mavjud: {TransferStockIn(TransferFromWarehouse):0.##} {TransferProduct.UnitDisplay}";
        public string TransferFromLine => HasTransferPreview ? $"{TransferFromWarehouse!.Name}: {TransferStockIn(TransferFromWarehouse):0.##} → " : string.Empty;
        public string TransferFromAfter => !HasTransferPreview ? string.Empty
            : TryGetTransferQty(out var q) ? $"{TransferStockIn(TransferFromWarehouse) - q:0.##}" : "—";
        public string TransferToLine => HasTransferPreview ? $"{TransferToWarehouse!.Name}: {TransferStockIn(TransferToWarehouse):0.##} → " : string.Empty;
        public string TransferToAfter => !HasTransferPreview ? string.Empty
            : TryGetTransferQty(out var q) ? $"{TransferStockIn(TransferToWarehouse) + q:0.##}" : "—";
        public bool IsTransferOverStock => HasTransferPreview && TryGetTransferQty(out var q) && q > TransferStockIn(TransferFromWarehouse);
        public string TransferButtonText => TransferProduct != null && TryGetTransferQty(out var q)
            ? $"{q:0.##} {TransferProduct.UnitDisplay} ko'chirish" : "Ko'chirish";
        /// <summary>Qayerdan / Qayerga omborlarini almashtiradi (faqat tanlovni).</summary>
        public ICommand SwapTransferWarehousesCommand => new RelayCommand(() =>
        {
            var from = TransferFromWarehouse;
            TransferFromWarehouse = TransferToWarehouse;
            TransferToWarehouse = from;
        });

        private Warehouse? _transferFromWarehouse;
        public Warehouse? TransferFromWarehouse
        {
            get => _transferFromWarehouse;
            set { if (SetProperty(ref _transferFromWarehouse, value)) NotifyTransferPreview(); }
        }

        private Warehouse? _transferToWarehouse;
        public Warehouse? TransferToWarehouse
        {
            get => _transferToWarehouse;
            set { if (SetProperty(ref _transferToWarehouse, value)) NotifyTransferPreview(); }
        }

        private Product? _transferProduct;
        public Product? TransferProduct
        {
            get => _transferProduct;
            set { if (SetProperty(ref _transferProduct, value)) NotifyTransferPreview(); }
        }

        private string _transferQuantity = "1";
        public string TransferQuantity
        {
            get => _transferQuantity;
            set { if (SetProperty(ref _transferQuantity, value)) NotifyTransferPreview(); }
        }

        private string? _formImagePath;
        public string? FormImagePath
        {
            get => _formImagePath;
            set
            {
                if (SetProperty(ref _formImagePath, value))
                {
                    OnPropertyChanged(nameof(FormHasImage));
                }
            }
        }
        public bool FormHasImage => !string.IsNullOrWhiteSpace(FormImagePath) && System.IO.File.Exists(FormImagePath);

        private string _transferErrorMessage = string.Empty;
        public string TransferErrorMessage
        {
            get => _transferErrorMessage;
            set => SetProperty(ref _transferErrorMessage, value);
        }

        public ICommand RefreshCommand { get; }
        public ICommand SelectWarehouseCommand { get; }
        public ICommand BackToWarehousesCommand { get; }
        public ICommand OpenAddWarehouseModalCommand { get; }
        public ICommand CloseAddWarehouseModalCommand { get; }
        public ICommand SaveNewWarehouseCommand { get; }
        public ICommand SetPrimaryWarehouseCommand { get; }
        public ICommand DeleteWarehouseCommand { get; }
        public ICommand OpenTransferModalCommand { get; }
        public ICommand CloseTransferModalCommand { get; }
        public ICommand ExecuteTransferCommand { get; }

        public ICommand SelectCategoryCommand { get; }
        public ICommand BackToCategoriesCommand { get; }
        public ICommand OpenAddProductCommand { get; }
        public ICommand OpenEditProductCommand { get; }
        public ICommand GenerateBarcodeCommand { get; }
        public ICommand PrintBarcodeCommand { get; }
        public ICommand SaveProductFormCommand { get; }
        public ICommand CancelAddEditCommand { get; }
        public ICommand DeleteProductCommand { get; }
        public ICommand OpenAddCategoryModalCommand { get; }
        public ICommand CloseAddCategoryModalCommand { get; }
        public ICommand SaveNewCategoryCommand { get; }
        public ICommand OpenEditCategoryModalCommand { get; }
        public ICommand CloseEditCategoryModalCommand { get; }
        public ICommand SaveEditCategoryCommand { get; }
        public ICommand ClearSearchCommand { get; }
        public ICommand PrintProductBarcodeCommand { get; }
        public ICommand SelectAllLowStockCommand { get; }
        public ICommand ClearSelectedLowStockCommand { get; }
        public ICommand ToggleOnlySelectedFilterCommand { get; }
        public ICommand OpenReorderListDialogCommand { get; }
        public ICommand PrintReorderReceiptCommand { get; }
        public ICommand ExportReorderExcelCommand { get; }
        public ICommand OpenQuickStockAddCommand { get; }
        public ICommand PickFormImageCommand { get; }
        public ICommand RemoveFormImageCommand { get; }
        public ICommand AttachProductImageCommand { get; }
        public ICommand RemoveProductImageCommand { get; }
        public ICommand OpenProductImagePreviewCommand { get; }

        public InventoryViewModel(DatabaseContext db, ProductService productService, CurrencyService currencyService)
        {
            _db = db;
            _productService = productService;
            _currencyService = currencyService;
            Services.AdminGate.Current.Changed += () => OnPropertyChanged(nameof(IsFormCostMasked));

            _db.WarehousesChanged += () =>
            {
                var dispatcher = System.Windows.Application.Current?.Dispatcher;
                if (dispatcher != null && !dispatcher.CheckAccess())
                {
                    dispatcher.Invoke(Refresh);
                }
                else
                {
                    Refresh();
                }
            };
            _db.ProductsChanged += () =>
            {
                var dispatcher = System.Windows.Application.Current?.Dispatcher;
                if (dispatcher != null && !dispatcher.CheckAccess())
                {
                    dispatcher.Invoke(Refresh);
                }
                else
                {
                    Refresh();
                }
            };

            _searchDebounceTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            _searchDebounceTimer.Tick += (s, e) =>
            {
                _searchDebounceTimer.Stop();
                ApplyFilter();
            };

            ClearSearchCommand = new RelayCommand(() =>
            {
                SearchQuery = string.Empty;
            });

            PrintProductBarcodeCommand = new RelayCommand<Product>(p =>
            {
                if (p == null || string.IsNullOrWhiteSpace(p.Barcode))
                {
                    MessageBox.Show("Ushbu mahsulotda shtrix-kod mavjud emas!", "Ogohlantirish", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var printerService = new PrinterService(_db);
                var dialog = new BarcodePrintDialog(p.Name, p.Barcode, p.SellingPrice, p.StockQuantity, printerService)
                {
                    Owner = Application.Current.MainWindow
                };
                dialog.ShowDialog();
            });

            SelectAllLowStockCommand = new RelayCommand(() =>
            {
                foreach (var p in _cachedDisplayProducts.Where(x => x.IsLowStock))
                {
                    p.IsSelected = true;
                }
                OnPropertyChanged(nameof(SelectedLowStockCount));
                OnPropertyChanged(nameof(HasSelectedLowStock));
                OnPropertyChanged(nameof(ReorderButtonText));
                ApplyFilter();
            });

            ClearCategoryFiltersCommand = new RelayCommand(() =>
            {
                ClearLowStockCategoryFilters();
                ApplyFilter();
            });

            ClearSelectedLowStockCommand = new RelayCommand(() =>
            {
                foreach (var p in _cachedDisplayProducts.Where(x => x.IsLowStock))
                {
                    p.IsSelected = false;
                }
                OnPropertyChanged(nameof(SelectedLowStockCount));
                OnPropertyChanged(nameof(HasSelectedLowStock));
                OnPropertyChanged(nameof(ReorderButtonText));
                ApplyFilter();
            });

            ToggleOnlySelectedFilterCommand = new RelayCommand<bool?>(val =>
            {
                IsOnlySelectedFilterActive = val ?? !IsOnlySelectedFilterActive;
            });

            OpenReorderListDialogCommand = new RelayCommand(() =>
            {
                var selected = _cachedDisplayProducts.Where(p => p.IsLowStock && p.IsSelected).ToList();
                if (selected.Count == 0)
                {
                    MessageBox.Show("Buyurtma uchun hech qanday tovar tanlanmagan! Iltimos, ro'yxatdan kerakli tovarlarni tanlang.", "Ogohlantirish", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var printerService = new PrinterService(_db);
                var dialog = new ReorderListDialog(
                    selected,
                    onRemoveItem: (p) =>
                    {
                        p.IsSelected = false;
                        OnPropertyChanged(nameof(SelectedLowStockCount));
                        OnPropertyChanged(nameof(HasSelectedLowStock));
                        OnPropertyChanged(nameof(ReorderButtonText));
                        ApplyFilter();
                    },
                    onClearAll: () =>
                    {
                        foreach (var item in _cachedDisplayProducts.Where(x => x.IsLowStock))
                        {
                            item.IsSelected = false;
                        }
                        OnPropertyChanged(nameof(SelectedLowStockCount));
                        OnPropertyChanged(nameof(HasSelectedLowStock));
                        OnPropertyChanged(nameof(ReorderButtonText));
                        ApplyFilter();
                    },
                    printerService: printerService,
                    warehouseName: CurrentWarehouseTitle
                )
                {
                    Owner = Application.Current.MainWindow
                };
                dialog.ShowDialog();
            });

            PrintReorderReceiptCommand = new RelayCommand(() =>
            {
                var selected = _cachedDisplayProducts.Where(p => p.IsLowStock && p.IsSelected).ToList();
                if (selected.Count == 0)
                {
                    MessageBox.Show("Chop etish uchun hech qanday tovar tanlanmagan!", "Ogohlantirish", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var printerService = new PrinterService(_db);
                var ok = printerService.PrintReorderReceipt(selected);
                if (ok)
                {
                    MessageBox.Show("Buyurtma ro'yxati chek printeriga muvaffaqiyatli yuborildi!", "Chop etish", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Chek printeri topilmadi yoki ulanmagan. Iltimos, printer ulanishini tekshiring.", "Xatolik", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            });

            ExportReorderExcelCommand = new RelayCommand(() =>
            {
                var selected = _cachedDisplayProducts.Where(p => p.IsLowStock && p.IsSelected).ToList();
                if (selected.Count == 0)
                {
                    MessageBox.Show("Excelga saqlash uchun hech qanday tovar tanlanmagan!", "Ogohlantirish", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                ExcelExportService.ExportReorderList(selected, CurrentWarehouseTitle);
            });

            OpenQuickStockAddCommand = new RelayCommand<Product>(p =>
            {
                if (p == null) return;
                var wh = SelectedWarehouse ?? Warehouses.FirstOrDefault(w => w.IsPrimary) ?? Warehouses.FirstOrDefault() ?? _db.GetWarehouses().FirstOrDefault();
                if (wh == null)
                {
                    MessageBox.Show("Ombor topilmadi!", "Ogohlantirish", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var dialog = new QuickStockAddDialog(
                    product: p,
                    warehouseName: wh.Name,
                    onConfirm: (addedQty) =>
                    {
                        _db.AddProductStockInWarehouse(p.Guid, wh.Guid, addedQty);
                        Refresh();
                    }
                )
                {
                    Owner = Application.Current.MainWindow
                };
                dialog.ShowDialog();
            });

            RefreshCommand = new RelayCommand(Refresh);

            PickFormImageCommand = new RelayCommand(PickFormImage);
            RemoveFormImageCommand = new RelayCommand(() => FormImagePath = null);

            OpenProductImagePreviewCommand = new RelayCommand<Product>(p =>
            {
                if (p != null && p.HasImage)
                {
                    MainViewModel.ShowImage(p.ImagePath, p.Name);
                }
            });

            AttachProductImageCommand = new RelayCommand<Product>(p =>
            {
                if (p == null) return;
                try
                {
                    var dlg = new Microsoft.Win32.OpenFileDialog
                    {
                        Title = $"{p.Name} uchun rasm tanlang",
                        Filter = "Rasm fayllari (*.jpg;*.jpeg;*.png;*.webp;*.bmp)|*.jpg;*.jpeg;*.png;*.webp;*.bmp|Barcha fayllar (*.*)|*.*"
                    };

                    if (dlg.ShowDialog() == true)
                    {
                        var ext = System.IO.Path.GetExtension(dlg.FileName);
                        var imagesDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "product_images");
                        if (!System.IO.Directory.Exists(imagesDir))
                        {
                            System.IO.Directory.CreateDirectory(imagesDir);
                        }

                        var destPath = System.IO.Path.Combine(imagesDir, $"prod_{p.Guid}_{DateTime.Now.Ticks}{ext}");
                        System.IO.File.Copy(dlg.FileName, destPath, overwrite: true);

                        p.ImagePath = destPath;
                        _db.SaveProduct(p);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Rasmni saqlashda xatolik yuz berdi: {ex.Message}", "Xatolik", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            });

            RemoveProductImageCommand = new RelayCommand<Product>(p =>
            {
                if (p == null) return;
                if (MessageBox.Show($"'{p.Name}' mahsulotining rasmini o'chirishni xohlaysizmi?", "Rasmni o'chirish", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    p.ImagePath = null;
                    _db.SaveProduct(p);
                }
            });
            SelectWarehouseCommand = new RelayCommand<Warehouse>(w => { if (w != null) SelectWarehouse(w); });
            BackToWarehousesCommand = new RelayCommand(BackToWarehouses);

            OpenAddWarehouseModalCommand = new RelayCommand(() =>
            {
                NewWarehouseName = string.Empty;
                NewWarehouseIsPrimary = false;
                NewWarehouseErrorMessage = string.Empty;
                IsAddWarehouseModalOpen = true;
            });

            CloseAddWarehouseModalCommand = new RelayCommand(() =>
            {
                IsAddWarehouseModalOpen = false;
                NewWarehouseName = string.Empty;
                NewWarehouseErrorMessage = string.Empty;
            });

            SaveNewWarehouseCommand = new RelayCommand(() =>
            {
                var name = (NewWarehouseName ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(name))
                {
                    NewWarehouseErrorMessage = "Iltimos, ombor nomini kiriting!";
                    return;
                }

                if (_db.GetWarehouses().Any(w => !w.IsDeleted && string.Equals(w.Name?.Trim(), name, StringComparison.OrdinalIgnoreCase)))
                {
                    NewWarehouseErrorMessage = "Bu nomdagi ombor allaqachon mavjud!";
                    return;
                }

                _db.SaveWarehouse(new Warehouse
                {
                    Name = name,
                    IsPrimary = NewWarehouseIsPrimary
                });

                IsAddWarehouseModalOpen = false;
                NewWarehouseName = string.Empty;
                NewWarehouseErrorMessage = string.Empty;
                Refresh();
            });

            SetPrimaryWarehouseCommand = new RelayCommand<Warehouse>(w =>
            {
                if (w != null)
                {
                    if (w.IsPrimary) return;
                    _db.SetPrimaryWarehouse(w.Guid);
                    Refresh();
                }
            });

            DeleteWarehouseCommand = new RelayCommand<Warehouse>(w =>
            {
                if (w == null) return;
                if (w.IsPrimary)
                {
                    MessageBox.Show("Asosiy omborni o'chirib bo'lmaydi!", "Ogohlantirish", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var res = MessageBox.Show($"Haqiqatan ham '{w.Name}' omborini o'chirmoqchimisiz?", "O'chirishni tasdiqlash", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (res == MessageBoxResult.Yes)
                {
                    _db.DeleteWarehouse(w.Guid);
                    Refresh();
                }
            });

            OpenTransferModalCommand = new RelayCommand(() =>
            {
                TransferFromWarehouse = Warehouses.FirstOrDefault(w => w.IsPrimary) ?? Warehouses.FirstOrDefault();
                TransferToWarehouse = Warehouses.FirstOrDefault(w => w != TransferFromWarehouse) ?? TransferFromWarehouse;
                TransferProduct = Products.FirstOrDefault();
                TransferQuantity = "1";
                TransferErrorMessage = string.Empty;
                IsTransferModalOpen = true;
            });

            CloseTransferModalCommand = new RelayCommand(() =>
            {
                IsTransferModalOpen = false;
                TransferErrorMessage = string.Empty;
            });

            ExecuteTransferCommand = new RelayCommand(() =>
            {
                TransferErrorMessage = string.Empty;
                if (TransferFromWarehouse == null || TransferToWarehouse == null)
                {
                    TransferErrorMessage = "Chiqish va qabul qiluvchi omborni tanlang!";
                    return;
                }
                if (TransferFromWarehouse.Guid == TransferToWarehouse.Guid)
                {
                    TransferErrorMessage = "Chiqish va qabul qiluvchi ombor bir xil bo'lishi mumkin emas!";
                    return;
                }
                if (TransferProduct == null)
                {
                    TransferErrorMessage = "Ko'chiriladigan mahsulotni tanlang!";
                    return;
                }
                if (!double.TryParse(TransferQuantity, NumberStyles.Any, CultureInfo.InvariantCulture, out var qty) || qty <= 0)
                {
                    TransferErrorMessage = "Miqdorni to'g'ri musbat sonda kiriting!";
                    return;
                }

                double avail = _db.GetProductStockInWarehouse(TransferProduct.Guid, TransferFromWarehouse.Guid);
                if (avail < qty)
                {
                    TransferErrorMessage = $"Tanlangan '{TransferFromWarehouse.Name}' omborida bor-yo'g'i {avail:0.##} ta qoldiq bor!";
                    return;
                }

                // Refresh clears ComboBox selections through two-way bindings. Capture the
                // committed operation's labels before any collection/event refresh occurs.
                var product = TransferProduct;
                var from = TransferFromWarehouse;
                var to = TransferToWarehouse;
                _db.TransferStock(product.Guid, from.Guid, to.Guid, qty);
                IsTransferModalOpen = false;
                Refresh();
                MessageBox.Show($"Muvaffaqiyatli ko'chirildi:\n{product.Name} ({qty} ta)\n{from.Name} ➔ {to.Name}", "Omborlararo ko'chirish", MessageBoxButton.OK, MessageBoxImage.Information);
            });

            RefreshCommand = new RelayCommand(Refresh);
            SelectCategoryCommand = new RelayCommand<CategoryCardModel>(c => { if (c != null) SelectCategory(c); });
            BackToCategoriesCommand = new RelayCommand(BackToCategories);
            OpenAddProductCommand = new RelayCommand(() => OpenAddProduct());
            OpenEditProductCommand = new RelayCommand<Product>(p => { if (p != null) OpenEditProduct(p); });
            GenerateBarcodeCommand = new RelayCommand(() =>
            {
                FormBarcode = _productService.GenerateUniqueBarcode();
            });
            PrintBarcodeCommand = new RelayCommand(() =>
            {
                if (string.IsNullOrWhiteSpace(FormBarcode))
                {
                    FormErrorMessage = "Avval shtrix-kod kiriting yoki 'Yaratish' tugmasini bosing!";
                    return;
                }

                string name = string.IsNullOrWhiteSpace(FormName) ? "Mahsulot" : FormName.Trim();
                var cleanPrice = CleanNumberInput(FormSellingPrice ?? "0");
                double.TryParse(cleanPrice, NumberStyles.Any, CultureInfo.InvariantCulture, out var price);
                var cleanQty = CleanNumberInput(FormStockQuantity ?? "1");
                double.TryParse(cleanQty, NumberStyles.Any, CultureInfo.InvariantCulture, out var stock);

                var printerService = new PrinterService(_db);
                var dialog = new BarcodePrintDialog(name, FormBarcode, price, stock, printerService)
                {
                    Owner = Application.Current.MainWindow
                };
                dialog.ShowDialog();
            });
            SaveProductFormCommand = new RelayCommand(SaveProductForm);
            CancelAddEditCommand = new RelayCommand(CancelAddEdit);
            DeleteProductCommand = new RelayCommand<Product>(p => { if (p != null) DeleteProduct(p); });

            OpenAddCategoryModalCommand = new RelayCommand(() =>
            {
                NewCategoryInput = string.Empty;
                NewCategoryErrorMessage = string.Empty;
                IsAddCategoryModalOpen = true;
            });

            CloseAddCategoryModalCommand = new RelayCommand(() =>
            {
                IsAddCategoryModalOpen = false;
                NewCategoryInput = string.Empty;
                NewCategoryErrorMessage = string.Empty;
            });

            SaveNewCategoryCommand = new RelayCommand(() =>
            {
                var cat = (NewCategoryInput ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(cat))
                {
                    NewCategoryErrorMessage = "Iltimos, kategoriya nomini kiriting!";
                    return;
                }

                if (!ExistingCategories.Contains(cat))
                {
                    ExistingCategories.Add(cat);
                }

                FormCategory = cat;
                IsAddCategoryModalOpen = false;
                NewCategoryInput = string.Empty;
                NewCategoryErrorMessage = string.Empty;
            });

            OpenEditCategoryModalCommand = new RelayCommand<string?>(catName =>
            {
                var target = !string.IsNullOrWhiteSpace(catName) ? catName : FormCategory;
                if (string.IsNullOrWhiteSpace(target) || 
                    target == ProductService.CATEGORY_ALL || 
                    target == ProductService.CATEGORY_LOW_STOCK)
                {
                    return;
                }

                EditCategoryOldName = target;
                EditCategoryInput = target;
                EditCategoryErrorMessage = string.Empty;
                IsEditCategoryModalOpen = true;
            });

            CloseEditCategoryModalCommand = new RelayCommand(() =>
            {
                IsEditCategoryModalOpen = false;
                EditCategoryOldName = string.Empty;
                EditCategoryInput = string.Empty;
                EditCategoryErrorMessage = string.Empty;
            });

            SaveEditCategoryCommand = new RelayCommand(() =>
            {
                var newCat = (EditCategoryInput ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(newCat))
                {
                    EditCategoryErrorMessage = "Iltimos, kategoriya nomini kiriting!";
                    return;
                }
                if (newCat == ProductService.CATEGORY_ALL || newCat == ProductService.CATEGORY_LOW_STOCK)
                {
                    EditCategoryErrorMessage = "Ushbu nom tizim tomonidan band qilingan!";
                    return;
                }

                var oldCat = EditCategoryOldName;
                if (!string.Equals(oldCat, newCat, StringComparison.OrdinalIgnoreCase))
                {
                    _productService.RenameCategory(oldCat, newCat);

                    int idx = ExistingCategories.IndexOf(oldCat);
                    if (idx >= 0)
                    {
                        ExistingCategories[idx] = newCat;
                    }
                    else if (!ExistingCategories.Contains(newCat))
                    {
                        ExistingCategories.Add(newCat);
                    }

                    if (FormCategory == oldCat)
                    {
                        FormCategory = newCat;
                    }

                    if (SelectedCategory == oldCat)
                    {
                        SelectedCategory = newCat;
                    }

                    Refresh();
                }

                IsEditCategoryModalOpen = false;
                EditCategoryOldName = string.Empty;
                EditCategoryInput = string.Empty;
                EditCategoryErrorMessage = string.Empty;
            });

            Refresh();
        }

        public InventoryViewState CurrentViewState
        {
            get => _currentViewState;
            set
            {
                if (SetProperty(ref _currentViewState, value))
                {
                    OnPropertyChanged(nameof(IsWarehousesGridVisible));
                    OnPropertyChanged(nameof(IsCategoriesGridVisible));
                    OnPropertyChanged(nameof(IsProductsListVisible));
                    OnPropertyChanged(nameof(IsAddEditFormVisible));
                    OnPropertyChanged(nameof(IsMainViewVisible));
                    OnPropertyChanged(nameof(CanGoBackToWarehouses));
                    OnPropertyChanged(nameof(CanAddProduct));
                }
            }
        }

        public bool IsMainViewVisible => CurrentViewState != InventoryViewState.AddEditForm;
        public bool IsWarehousesGridVisible => CurrentViewState == InventoryViewState.WarehousesGrid;
        public bool IsCategoriesGridVisible => CurrentViewState == InventoryViewState.CategoriesGrid;
        public bool IsProductsListVisible => CurrentViewState == InventoryViewState.ProductsList;
        public bool IsAddEditFormVisible => CurrentViewState == InventoryViewState.AddEditForm;

        public void SelectWarehouse(Warehouse w)
        {
            SelectedWarehouse = w;
            SelectedCategory = ProductService.CATEGORY_ALL;
            SearchQuery = string.Empty;
            CurrentViewState = InventoryViewState.CategoriesGrid;
            Refresh();
        }

        public void BackToWarehouses()
        {
            SelectedWarehouse = null;
            SelectedCategory = ProductService.CATEGORY_ALL;
            SearchQuery = string.Empty;
            CurrentViewState = InventoryViewState.WarehousesGrid;
            Refresh();
        }

        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                var fixedVal = Services.KeyboardLayoutHelper.FixBarcodeString(value);
                if (SetProperty(ref _searchQuery, fixedVal))
                {
                    OnPropertyChanged(nameof(HasSearchQuery));
                    if (!string.IsNullOrWhiteSpace(_searchQuery))
                    {
                        if (CurrentViewState != InventoryViewState.AddEditForm)
                        {
                            CurrentViewState = InventoryViewState.ProductsList;
                        }
                    }

                    // 200ms kechikish bilan filtrlash (Klaviaturadan yozishda UI qotmasligi uchun)
                    _searchDebounceTimer.Stop();
                    _searchDebounceTimer.Start();
                }
            }
        }

        public string SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (SetProperty(ref _selectedCategory, value))
                {
                    _stockFilter = 0;
                    OnPropertyChanged(nameof(StockFilter));
                    _isOnlySelectedFilterActive = false;
                    ClearLowStockCategoryFilters();
                    IsCategoryFilterDropdownOpen = false;
                    OnPropertyChanged(nameof(IsOnlySelectedFilterActive));
                    OnPropertyChanged(nameof(IsLowStockCategorySelected));
                    OnPropertyChanged(nameof(CurrentCategoryTitle));
                    OnPropertyChanged(nameof(ProductSearchPlaceholder));
                    ApplyFilter();
                }
            }
        }

        private bool _isOnlySelectedFilterActive;
        public bool IsOnlySelectedFilterActive
        {
            get => _isOnlySelectedFilterActive;
            set
            {
                if (SetProperty(ref _isOnlySelectedFilterActive, value))
                {
                    ApplyFilter();
                }
            }
        }

        public bool IsLowStockCategorySelected => SelectedCategory == ProductService.CATEGORY_LOW_STOCK;
        public int SelectedLowStockCount => _cachedDisplayProducts.Count(p => p.IsLowStock && p.IsSelected);
        public int TotalLowStockCount => _cachedDisplayProducts.Count(p => p.IsLowStock);
        public bool HasSelectedLowStock => SelectedLowStockCount > 0;
        public string ReorderButtonText => SelectedLowStockCount > 0 ? $"🛒 Buyurtma ro'yxati ({SelectedLowStockCount} ta)" : "🛒 Buyurtma ro'yxati";

        // ---- Maket 2.1 / 2.2: ko'rsatish yordamchilari ----
        /// <summary>Kategoriya kartalari ("Kam qolgan" kartasisiz — u asboblar panelida tugma).</summary>
        public IEnumerable<CategoryCardModel> GridCategoryCards => CategoryCards.Where(c => !c.IsLowStock).ToList();
        public CategoryCardModel? LowStockCard => CategoryCards.FirstOrDefault(c => c.IsLowStock);

        private int _stockFilter;
        /// <summary>0 = hammasi, 1 = kam qolgan, 2 = tugagan.</summary>
        public int StockFilter
        {
            get => _stockFilter;
            set { if (SetProperty(ref _stockFilter, value)) ApplyFilter(); }
        }
        public ICommand SetStockFilterCommand => _setStockFilterCommand ??= new RelayCommand<object>(p =>
        {
            if (int.TryParse(p?.ToString(), out var v)) StockFilter = v;
        });
        private ICommand? _setStockFilterCommand;

        public string OverviewSearchPlaceholder => SelectedWarehouse != null
            ? $"{SelectedWarehouse.Name} ichidan tovar nomi yoki shtrix-kod bo'yicha qidirish…"
            : "Barcha omborlardan tovar nomi yoki shtrix-kod bo'yicha qidirish…";
        public string ProductSearchPlaceholder => $"{CurrentCategoryTitle} ichidan qidirish…";

        public int CategoryAllCount { get; private set; }
        public int CategoryLowCount { get; private set; }
        public int CategoryOutCount { get; private set; }

        // ===== Kam qolgan tovarlar: kategoriya bo'yicha filtr =====
        private const string NoCategoryFilterName = "Kategoriyasiz";
        private static string LowStockFilterKey(Product p) =>
            string.IsNullOrWhiteSpace(p.Category) ? NoCategoryFilterName : p.Category.Trim();

        public ObservableCollection<LowStockCategoryFilterItem> LowStockCategoryFilters { get; } = new();
        public ICommand ClearCategoryFiltersCommand { get; }

        private bool _isCategoryFilterDropdownOpen;
        public bool IsCategoryFilterDropdownOpen
        {
            get => _isCategoryFilterDropdownOpen;
            set => SetProperty(ref _isCategoryFilterDropdownOpen, value);
        }

        public string SelectedCategoriesFilterText
        {
            get
            {
                var selected = LowStockCategoryFilters.Where(f => f.IsSelected).ToList();
                if (selected.Count == 0) return "Kategoriya: barchasi";
                if (selected.Count == 1) return selected[0].Name;
                return $"Kategoriya: {selected.Count} ta";
            }
        }

        public bool HasLowStockCategoryFilter => LowStockCategoryFilters.Any(f => f.IsSelected);

        private void OnLowStockCategoryFilterChanged()
        {
            OnPropertyChanged(nameof(SelectedCategoriesFilterText));
            OnPropertyChanged(nameof(HasLowStockCategoryFilter));
            ApplyFilter();
        }

        private void ClearLowStockCategoryFilters()
        {
            foreach (var f in LowStockCategoryFilters) f.SetSilently(false);
            OnPropertyChanged(nameof(SelectedCategoriesFilterText));
            OnPropertyChanged(nameof(HasLowStockCategoryFilter));
        }

        /// <summary>Filtr ro'yxatini joriy ombordagi kam qolgan tovarlardan qayta tuzadi; belgilanganlar saqlanadi.</summary>
        private void RebuildLowStockCategoryFilters()
        {
            var previouslySelected = new HashSet<string>(
                LowStockCategoryFilters.Where(f => f.IsSelected).Select(f => f.Name), StringComparer.OrdinalIgnoreCase);

            LowStockCategoryFilters.Clear();
            var groups = _cachedDisplayProducts
                .Where(p => p.IsLowStock)
                .GroupBy(LowStockFilterKey, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key, StringComparer.CurrentCultureIgnoreCase);
            foreach (var g in groups)
            {
                LowStockCategoryFilters.Add(new LowStockCategoryFilterItem(
                    g.Key, g.Count(), previouslySelected.Contains(g.Key), OnLowStockCategoryFilterChanged));
            }
            OnPropertyChanged(nameof(SelectedCategoriesFilterText));
            OnPropertyChanged(nameof(HasLowStockCategoryFilter));
        }

        // Jadval sarlavhasidagi katakcha: ekranda ko'rinib turgan kam qolgan tovarlarning hammasini belgilaydi / bekor qiladi.
        private bool _isBulkSelecting;
        public bool IsAllLowStockSelected
        {
            get => IsLowStockCategorySelected && Products.Count > 0 && Products.All(p => p.IsSelected);
            set
            {
                _isBulkSelecting = true;
                try
                {
                    foreach (var p in Products) p.IsSelected = value;
                }
                finally
                {
                    _isBulkSelecting = false;
                }
                OnPropertyChanged(nameof(SelectedLowStockCount));
                OnPropertyChanged(nameof(HasSelectedLowStock));
                OnPropertyChanged(nameof(ReorderButtonText));
                if (IsOnlySelectedFilterActive) ApplyFilter();
                else OnPropertyChanged(nameof(IsAllLowStockSelected));
            }
        }

        public string CurrentCategoryTitle => SelectedCategory == ProductService.CATEGORY_ALL ? "Barcha tovarlar" : SelectedCategory;
        public string ProductsCountHeader => $"({Products.Count} ta tovar)";

        // --- FORM PROPERTIES ---
        public string FormTitle
        {
            get => _formTitle;
            set => SetProperty(ref _formTitle, value);
        }

        public string FormName
        {
            get => _formName;
            set => SetProperty(ref _formName, PosElectro.Desktop.Views.InventoryView.CapitalizeFirstLetter(value));
        }

        public string FormBarcode
        {
            get => _formBarcode;
            set => SetProperty(ref _formBarcode, Services.KeyboardLayoutHelper.FixBarcodeString(value));
        }

        public string FormCategory
        {
            get => _formCategory;
            set => SetProperty(ref _formCategory, value);
        }

        /// <summary>Admin PIN qulfi yopiq bo'lsa mavjud tovarning tan narxi formada yashiriladi (yangi tovarga kiritish mumkin).</summary>
        public bool IsFormCostMasked => Services.AdminGate.Current.IsLocked && _editingProduct != null;

        public string FormCostPrice
        {
            get => _formCostPrice;
            set
            {
                if (SetProperty(ref _formCostPrice, value))
                {
                    UpdateCostConversion();
                }
            }
        }

        public bool FormIsUsd
        {
            get => _formIsUsd;
            set
            {
                if (SetProperty(ref _formIsUsd, value))
                {
                    OnPropertyChanged(nameof(FormIsUzs));
                    OnPropertyChanged(nameof(FormCostPriceLabel));
                    OnPropertyChanged(nameof(IsFormUsdConversionVisible));
                    UpdateCostConversion();
                }
            }
        }

        public bool FormIsUzs
        {
            get => !_formIsUsd;
            set
            {
                if (value)
                {
                    FormIsUsd = false;
                }
            }
        }

        public string FormCostPriceLabel => FormIsUsd ? "Tan narxi ($) *" : "Tan narxi (so'm) *";
        public bool IsFormUsdConversionVisible => FormIsUsd && !IsFormCostMasked;

        public string FormUsdConversion
        {
            get => _formUsdConversion;
            set => SetProperty(ref _formUsdConversion, value);
        }

        public string FormSellingPrice
        {
            get => _formSellingPrice;
            set { if (SetProperty(ref _formSellingPrice, value)) { OnPropertyChanged(nameof(FormMarkupText)); OnPropertyChanged(nameof(IsFormMarkupNegative)); } }
        }

        /// <summary>Maket 2.3: "Ustama +29,7%" — faqat ko'rsatish uchun (tan narxi yashirin bo'lsa bo'sh).</summary>
        public string FormMarkupText
        {
            get
            {
                if (IsFormCostMasked) return string.Empty;
                if (!double.TryParse(CleanNumberInput(FormCostPrice ?? ""), NumberStyles.Any, CultureInfo.InvariantCulture, out var cost) || cost <= 0) return string.Empty;
                if (!double.TryParse(CleanNumberInput(FormSellingPrice ?? ""), NumberStyles.Any, CultureInfo.InvariantCulture, out var sell) || sell <= 0) return string.Empty;
                if (FormIsUsd) cost *= _currencyService.GetCachedUsdRate();
                if (cost <= 0) return string.Empty;
                var pct = (sell - cost) / cost * 100.0;
                var txt = Math.Abs(pct).ToString("0.0", CultureInfo.InvariantCulture).Replace('.', ',');
                return $"Ustama {(pct >= 0 ? "+" : "−")}{txt}%";
            }
        }
        public bool IsFormMarkupNegative => FormMarkupText.Contains('−');

        public string FormSellingPrice2
        {
            get => _formSellingPrice2;
            set => SetProperty(ref _formSellingPrice2, value);
        }

        public string FormStockQuantity
        {
            get => _formStockQuantity;
            set => SetProperty(ref _formStockQuantity, value);
        }

        public int FormUnitTypeIndex
        {
            get => _formUnitTypeIndex;
            set => SetProperty(ref _formUnitTypeIndex, value);
        }

        public string FormMinStockAlert
        {
            get => _formMinStockAlert;
            set => SetProperty(ref _formMinStockAlert, value);
        }

        public string FormNote
        {
            get => _formNote;
            set => SetProperty(ref _formNote, value);
        }

        public string FormErrorMessage
        {
            get => _formErrorMessage;
            set
            {
                if (SetProperty(ref _formErrorMessage, value))
                {
                    OnPropertyChanged(nameof(HasFormError));
                }
            }
        }

        public bool HasFormError => !string.IsNullOrWhiteSpace(FormErrorMessage);

        public void SelectCategory(CategoryCardModel card)
        {
            SelectedCategory = card.Name;
            CurrentViewState = InventoryViewState.ProductsList;
        }

        public void BackToCategories()
        {
            if (SelectedWarehouse == null)
            {
                BackToWarehouses();
                return;
            }

            _searchQuery = string.Empty;
            OnPropertyChanged(nameof(SearchQuery));
            SelectedCategory = ProductService.CATEGORY_ALL;
            CurrentViewState = InventoryViewState.CategoriesGrid;
            Refresh();
        }

        public void OpenAddProduct(string? barcode = null)
        {
            if (SelectedWarehouse == null)
            {
                // Agar ombor tanlanmagan bo'lsa (masalan kassada skaner qilinganda),
                // avtomatik ravishda Asosiy (sotuvdagi) omborni tanlaymiz
                SelectedWarehouse = Warehouses.FirstOrDefault(w => w.IsPrimary) 
                                 ?? Warehouses.FirstOrDefault() 
                                 ?? _db.GetWarehouses().FirstOrDefault();
            }

            if (SelectedWarehouse == null)
            {
                MessageBox.Show("Tizimda birorta ham ombor topilmadi! Iltimos, avval ombor yarating.", "Ombor yo'q", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _editingProduct = null;
            OnPropertyChanged(nameof(IsFormCostMasked));
            _previousViewState = CurrentViewState;

            FormTitle = "Yangi tovar";
            FormName = string.Empty;
            FormBarcode = barcode ?? string.Empty;
            var defaultCat = SelectedCategory != ProductService.CATEGORY_ALL && SelectedCategory != ProductService.CATEGORY_LOW_STOCK
                ? SelectedCategory
                : string.Empty; // aniq kategoriya tanlanmagan bo'lsa jimgina birinchisini qo'ymaymiz
            FormCategory = defaultCat;
            FormCostPrice = "0";
            FormIsUsd = false;
            FormSellingPrice = "0";
            FormSellingPrice2 = string.Empty;
            FormStockQuantity = "0";
            FormUnitTypeIndex = 0;
            FormMinStockAlert = "3";
            FormNote = string.Empty;
            FormImagePath = null;
            FormErrorMessage = string.Empty;
            UpdateCostConversion();

            CurrentViewState = InventoryViewState.AddEditForm;
        }

        public void OpenEditProduct(Product p)
        {
            _editingProduct = p;
            OnPropertyChanged(nameof(IsFormCostMasked));
            _previousViewState = CurrentViewState;

            FormTitle = $"Tahrirlash: {p.Name}";
            FormName = p.Name;
            FormBarcode = p.Barcode ?? string.Empty;
            FormCategory = p.Category;
            if (!ExistingCategories.Contains(p.Category) && !string.IsNullOrWhiteSpace(p.Category))
            {
                ExistingCategories.Add(p.Category);
            }
            FormCostPrice = p.CostCurrency == "USD" 
                ? p.CostPrice.ToString("0.##", CultureInfo.InvariantCulture) 
                : p.CostPrice.ToString("N0", SpaceGroupFormat);
            FormIsUsd = p.CostCurrency == "USD";
            FormSellingPrice = p.SellingPrice.ToString("N0", SpaceGroupFormat);
            FormSellingPrice2 = p.SellingPrice2.HasValue && p.SellingPrice2.Value > 0 ? p.SellingPrice2.Value.ToString("N0", SpaceGroupFormat) : string.Empty;
            FormStockQuantity = p.StockQuantity.ToString(CultureInfo.InvariantCulture);
            FormUnitTypeIndex = p.UnitType switch
            {
                UnitType.DONA => 0,
                UnitType.METR => 1,
                UnitType.KG => 2,
                _ => 0
            };
            FormMinStockAlert = p.MinStockAlert.ToString(CultureInfo.InvariantCulture);
            FormNote = p.Note ?? string.Empty;
            FormImagePath = p.ImagePath;
            FormErrorMessage = string.Empty;
            UpdateCostConversion();

            CurrentViewState = InventoryViewState.AddEditForm;
        }

        public void PickFormImage()
        {
            try
            {
                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Mahsulot rasmini tanlang",
                    Filter = "Rasm fayllari (*.jpg;*.jpeg;*.png;*.webp;*.bmp)|*.jpg;*.jpeg;*.png;*.webp;*.bmp|Barcha fayllar (*.*)|*.*"
                };

                if (dlg.ShowDialog() == true)
                {
                    var ext = System.IO.Path.GetExtension(dlg.FileName);
                    var imagesDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "product_images");
                    if (!System.IO.Directory.Exists(imagesDir))
                    {
                        System.IO.Directory.CreateDirectory(imagesDir);
                    }

                    var guid = _editingProduct?.Guid ?? Guid.NewGuid().ToString();
                    var destPath = System.IO.Path.Combine(imagesDir, $"prod_{guid}_{DateTime.Now.Ticks}{ext}");
                    System.IO.File.Copy(dlg.FileName, destPath, overwrite: true);

                    FormImagePath = destPath;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Rasmni tanlashda xatolik yuz berdi: {ex.Message}", "Xatolik", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void CancelAddEdit()
        {
            CurrentViewState = _previousViewState == InventoryViewState.AddEditForm 
                ? InventoryViewState.CategoriesGrid 
                : _previousViewState;
        }

        private void UpdateCostConversion()
        {
            OnPropertyChanged(nameof(FormMarkupText));
            OnPropertyChanged(nameof(IsFormMarkupNegative));
            if (!FormIsUsd)
            {
                FormUsdConversion = string.Empty;
                return;
            }

            var rate = _currencyService.GetCachedUsdRate();
            var cleanCost = CleanNumberInput(FormCostPrice ?? "0");
            if (double.TryParse(cleanCost, NumberStyles.Any, CultureInfo.InvariantCulture, out var usdVal))
            {
                var uzsVal = usdVal * rate;
                FormUsdConversion = $"≈ {uzsVal:N0} so'm (1$ = {rate:N0} so'm)";
            }
            else
            {
                FormUsdConversion = $"≈ 0 so'm (1$ = {rate:N0} so'm)";
            }
        }

        public void SaveProductForm()
        {
            FormErrorMessage = string.Empty;

            var name = PosElectro.Desktop.Views.InventoryView.CapitalizeFirstLetter(FormName.Trim());
            if (string.IsNullOrWhiteSpace(name))
            {
                FormErrorMessage = "Mahsulot nomini kiritish majburiy!";
                return;
            }

            // Android ilovadagi aqlli dublikat tekshiruvi
            var duplicate = _productService.FindDuplicateProduct(name, _editingProduct?.Id ?? 0);
            if (duplicate != null)
            {
                FormErrorMessage = $"\"{duplicate.Name}\" nomli mahsulot omborda allaqachon mavjud! Boshqa nom kiriting yoki qoldiqni yangilang.";
                return;
            }

            var cleanCost = CleanNumberInput(FormCostPrice);
            if (!double.TryParse(cleanCost, NumberStyles.Any, CultureInfo.InvariantCulture, out var costPrice) || costPrice < 0)
            {
                FormErrorMessage = "Tan narxi to'g'ri raqamda kiritilishi kerak!";
                return;
            }

            var cleanSell = CleanNumberInput(FormSellingPrice);
            if (!double.TryParse(cleanSell, NumberStyles.Any, CultureInfo.InvariantCulture, out var sellingPrice) || sellingPrice < 0)
            {
                FormErrorMessage = "Sotish narxi to'g'ri raqamda kiritilishi kerak!";
                return;
            }

            double? sellingPrice2 = null;
            var cleanSell2 = CleanNumberInput(FormSellingPrice2);
            if (!string.IsNullOrEmpty(cleanSell2))
            {
                if (double.TryParse(cleanSell2, NumberStyles.Any, CultureInfo.InvariantCulture, out var sp2) && sp2 > 0)
                {
                    sellingPrice2 = sp2;
                }
                else
                {
                    FormErrorMessage = "2-sotish narxi to'g'ri raqamda kiritilishi kerak yoki bo'sh qoldirilishi lozim!";
                    return;
                }
            }

            var cleanStock = CleanNumberInput(FormStockQuantity);
            if (!double.TryParse(cleanStock, NumberStyles.Any, CultureInfo.InvariantCulture, out var stockQuantity))
            {
                FormErrorMessage = "Qoldiq miqdori to'g'ri raqamda kiritilishi kerak!";
                return;
            }

            var cleanMin = CleanNumberInput(FormMinStockAlert);
            double minStockAlert = 3.0;
            if (!string.IsNullOrWhiteSpace(cleanMin) && double.TryParse(cleanMin, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedMin))
            {
                minStockAlert = Math.Max(0.0, parsedMin);
            }

            var unitType = FormUnitTypeIndex switch
            {
                0 => UnitType.DONA,
                1 => UnitType.METR,
                2 => UnitType.KG,
                _ => UnitType.DONA
            };

            var category = string.IsNullOrWhiteSpace(FormCategory) ? "Barchasi" : FormCategory.Trim();

            var product = _editingProduct ?? new Product();
            product.Name = name;
            product.Barcode = string.IsNullOrWhiteSpace(FormBarcode) ? null : FormBarcode.Trim();
            product.Category = category;
            product.CostCurrency = FormIsUsd ? "USD" : "UZS";
            product.CostPrice = costPrice;
            product.SellingPrice = sellingPrice;
            product.SellingPrice2 = sellingPrice2;
            product.StockQuantity = stockQuantity;
            product.UnitType = unitType;
            product.MinStockAlert = minStockAlert;
            product.Note = FormNote.Trim();
            product.ImagePath = FormImagePath;
            if (SelectedWarehouse != null)
            {
                product.WarehouseGuid = SelectedWarehouse.Guid;
            }

            try
            {
                _db.SaveProduct(product);
                Refresh();

                // Oldingi holatga qaytish: agar ProductsList da bo'lsa, o'sha kategoriyaga qaytamiz
                if (_previousViewState == InventoryViewState.ProductsList)
                {
                    CurrentViewState = InventoryViewState.ProductsList;
                }
                else
                {
                    CurrentViewState = InventoryViewState.CategoriesGrid;
                }
            }
            catch (Exception ex)
            {
                FormErrorMessage = ex.Message;
            }
        }

        // ---- Maket 2.4: o'chirishni tasdiqlash oynasi ----
        private Product? _pendingDeleteProduct;
        public bool IsDeleteConfirmOpen => _pendingDeleteProduct != null;
        public string DeleteConfirmName => _pendingDeleteProduct?.Name ?? string.Empty;
        public string DeleteConfirmNote
        {
            get
            {
                var p = _pendingDeleteProduct;
                if (p == null) return string.Empty;
                var tail = "Eski cheklar va hisobotlarda tovar nomi saqlanib qoladi.";
                return p.StockQuantity > 0
                    ? $"Ombordagi qoldig'i bor: {p.StockQuantity:#,0.##} {p.UnitDisplay}. {tail}"
                    : tail;
            }
        }
        public ICommand ConfirmDeleteProductCommand => _confirmDeleteProductCommand ??= new RelayCommand(() =>
        {
            var p = _pendingDeleteProduct;
            SetPendingDelete(null);
            if (p != null)
            {
                _productService.DeleteProduct(p.Id);
                Refresh();
            }
        });
        private ICommand? _confirmDeleteProductCommand;
        public ICommand CancelDeleteProductCommand => _cancelDeleteProductCommand ??= new RelayCommand(() => SetPendingDelete(null));
        private ICommand? _cancelDeleteProductCommand;

        private void SetPendingDelete(Product? p)
        {
            _pendingDeleteProduct = p;
            OnPropertyChanged(nameof(IsDeleteConfirmOpen));
            OnPropertyChanged(nameof(DeleteConfirmName));
            OnPropertyChanged(nameof(DeleteConfirmNote));
        }

        public void DeleteProduct(Product p) => SetPendingDelete(p);

        public void Refresh()
        {
            Warehouses.Clear();
            var whList = _db.GetWarehouses();
            var usdRate = _currencyService.GetCachedUsdRate();
            if (usdRate <= 0) usdRate = CurrencyService.DEFAULT_USD_RATE;
            foreach (var w in whList)
            {
                w.TotalStockCost = w.StockCostUzs + w.StockCostUsd * usdRate;
                Warehouses.Add(w);
            }

            // Baza so'rovini bir marta bajarib, xotirada keshlaymiz
            _cachedAllProducts = _db.GetAllProducts(includeDeleted: false);

            if (SelectedWarehouse != null)
            {
                _cachedWhStocks = _db.GetAllProductStocksForWarehouse(SelectedWarehouse.Guid);
                _cachedDisplayProducts = _cachedAllProducts
                    .Where(p => _cachedWhStocks.ContainsKey(p.Guid))
                    .Select(p => {
                        p.StockQuantity = _cachedWhStocks[p.Guid];
                        return p;
                    })
                    .ToList();
            }
            else
            {
                _cachedWhStocks.Clear();
                _cachedDisplayProducts = _cachedAllProducts;
            }

            // Mavjud kategoriyalar ro'yxatini to'ldirish
            ExistingCategories.Clear();
            foreach (var cat in _productService.GetCategories())
            {
                if (cat != ProductService.CATEGORY_LOW_STOCK && cat != ProductService.CATEGORY_ALL)
                {
                    ExistingCategories.Add(cat);
                }
            }

            // Kategoriyalar kartalarini shakllantirish (tanlangan omborga mos):
            CategoryCards.Clear();

            // 1. Barchasi
            CategoryCards.Add(new CategoryCardModel
            {
                Name = ProductService.CATEGORY_ALL,
                Count = _cachedDisplayProducts.Count,
                IsAll = true,
                Icon = "📦",
                LowCount = _cachedDisplayProducts.Count(p => p.IsLowStock && !p.IsOutOfStock),
                OutCount = _cachedDisplayProducts.Count(p => p.IsOutOfStock)
            });

            // 2. Kam qolgan tovarlar
            var lowStockCount = _cachedDisplayProducts.Count(p => p.IsLowStock);
            CategoryCards.Add(new CategoryCardModel
            {
                Name = ProductService.CATEGORY_LOW_STOCK,
                Count = lowStockCount,
                IsLowStock = true,
                Icon = "⚠️"
            });

            // 3. Qolgan kategoriyalar alifbo bo'yicha
            var distinctCategories = _cachedDisplayProducts
                .Select(p => string.IsNullOrWhiteSpace(p.Category) ? "Barchasi" : p.Category.Trim())
                .Where(c => c != ProductService.CATEGORY_ALL && c != ProductService.CATEGORY_LOW_STOCK)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(c => c, StringComparer.CurrentCultureIgnoreCase);

            foreach (var cat in distinctCategories)
            {
                var inCat = _cachedDisplayProducts.Where(p => string.Equals(p.Category, cat, StringComparison.OrdinalIgnoreCase)).ToList();
                CategoryCards.Add(new CategoryCardModel
                {
                    Name = cat,
                    Count = inCat.Count,
                    Icon = "📁",
                    LowCount = inCat.Count(p => p.IsLowStock && !p.IsOutOfStock),
                    OutCount = inCat.Count(p => p.IsOutOfStock)
                });
            }

            OnPropertyChanged(nameof(GridCategoryCards));
            OnPropertyChanged(nameof(LowStockCard));
            RebuildLowStockCategoryFilters();
            ApplyFilter();
        }

        public void ApplyFilter()
        {
            // Qayta bazaga bormaymiz, keshdagi _cachedDisplayProducts dan tezkor filtrlaymiz
            var list = _cachedDisplayProducts;
            if (SelectedCategory == ProductService.CATEGORY_LOW_STOCK)
            {
                list = list.Where(p => p.IsLowStock).ToList();
                var categoryFilter = new HashSet<string>(
                    LowStockCategoryFilters.Where(f => f.IsSelected).Select(f => f.Name), StringComparer.OrdinalIgnoreCase);
                if (categoryFilter.Count > 0)
                {
                    list = list.Where(p => categoryFilter.Contains(LowStockFilterKey(p))).ToList();
                }
                if (IsOnlySelectedFilterActive)
                {
                    list = list.Where(p => p.IsSelected).ToList();
                }
            }
            else if (SelectedCategory != ProductService.CATEGORY_ALL && !string.IsNullOrWhiteSpace(SelectedCategory))
            {
                list = list.Where(p => string.Equals(p.Category, SelectedCategory, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            // Maket 2.2: kategoriya ichidagi qoldiq filtri (0 = hammasi — avvalgidek)
            CategoryAllCount = list.Count;
            CategoryLowCount = list.Count(p => p.IsLowStock && !p.IsOutOfStock);
            CategoryOutCount = list.Count(p => p.IsOutOfStock);
            if (_stockFilter == 1) list = list.Where(p => p.IsLowStock && !p.IsOutOfStock).ToList();
            else if (_stockFilter == 2) list = list.Where(p => p.IsOutOfStock).ToList();

            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                list = SmartSearchHelper.FilterAndRank(
                    list,
                    SearchQuery,
                    p => p.Name,
                    p => p.Barcode,
                    p => p.Note
                );
            }

            // IsSelected o'zgarishlarini kuzatish
            foreach (var p in list)
            {
                p.PropertyChanged -= OnProductItemPropertyChanged;
                p.PropertyChanged += OnProductItemPropertyChanged;
            }

            // Barcha elementlarni bir martada tayinlaymiz (500 ta alohida CollectionChanged eventlari o'rniga 1 ta PropertyChanged)
            Products = new ObservableCollection<Product>(list);

            OnPropertyChanged(nameof(ProductsCountHeader));
            OnPropertyChanged(nameof(CategoryAllCount));
            OnPropertyChanged(nameof(CategoryLowCount));
            OnPropertyChanged(nameof(CategoryOutCount));
            OnPropertyChanged(nameof(HasSearchQuery));
            OnPropertyChanged(nameof(IsEmptySearchResult));
            OnPropertyChanged(nameof(IsLowStockCategorySelected));
            OnPropertyChanged(nameof(SelectedLowStockCount));
            OnPropertyChanged(nameof(TotalLowStockCount));
            OnPropertyChanged(nameof(HasSelectedLowStock));
            OnPropertyChanged(nameof(ReorderButtonText));
            OnPropertyChanged(nameof(IsAllLowStockSelected));
        }

        private void OnProductItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Product.IsSelected))
            {
                if (_isBulkSelecting) return;
                OnPropertyChanged(nameof(IsAllLowStockSelected));
                OnPropertyChanged(nameof(SelectedLowStockCount));
                OnPropertyChanged(nameof(HasSelectedLowStock));
                OnPropertyChanged(nameof(ReorderButtonText));
                if (IsOnlySelectedFilterActive)
                {
                    ApplyFilter();
                }
            }
        }
    }
}
