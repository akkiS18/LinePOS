using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using PosElectro.Desktop.Data;
using PosElectro.Desktop.Debt;
using PosElectro.Desktop.Models;
using PosElectro.Desktop.Services;
using PosElectro.Desktop.Views;

namespace PosElectro.Desktop.ViewModels
{
    public class CartItemModel : ViewModelBase
    {
        public Product Product { get; }
        private double _quantity;
        private double _priceAtSale;
        private readonly double _usdRate;
        private string _warehouseGuid = string.Empty;
        private string _warehouseName = string.Empty;

        public CartItemModel(
            Product product, 
            double quantity = 1.0, 
            double? priceAtSale = null, 
            double usdRate = CurrencyService.DEFAULT_USD_RATE,
            string? warehouseGuid = null,
            string? warehouseName = null)
        {
            Product = product;
            _quantity = quantity;
            _priceAtSale = priceAtSale ?? product.SellingPrice;
            _usdRate = usdRate;
            _warehouseGuid = warehouseGuid ?? product.WarehouseGuid ?? string.Empty;
            _warehouseName = warehouseName ?? string.Empty;
        }

        public double Quantity
        {
            get => _quantity;
            set
            {
                if (SetProperty(ref _quantity, value))
                {
                    OnPropertyChanged(nameof(TotalPrice));
                    OnPropertyChanged(nameof(TotalCost));
                }
            }
        }

        public double PriceAtSale
        {
            get => _priceAtSale;
            set
            {
                if (SetProperty(ref _priceAtSale, value))
                {
                    OnPropertyChanged(nameof(TotalPrice));
                    OnPropertyChanged(nameof(IsDiscounted));
                    OnPropertyChanged(nameof(DiscountAmount));
                    OnPropertyChanged(nameof(IsPrice2Active));
                    OnPropertyChanged(nameof(PriceNumberDisplay));
                }
            }
        }

        public string WarehouseGuid
        {
            get => _warehouseGuid;
            set => SetProperty(ref _warehouseGuid, value);
        }

        public string WarehouseName
        {
            get => _warehouseName;
            set
            {
                if (SetProperty(ref _warehouseName, value))
                {
                    OnPropertyChanged(nameof(WarehouseDisplay));
                    OnPropertyChanged(nameof(HasWarehouse));
                }
            }
        }

        public bool HasWarehouse => !string.IsNullOrWhiteSpace(WarehouseName);
        public string WarehouseDisplay => string.IsNullOrWhiteSpace(WarehouseName) ? string.Empty : $"🏢 {WarehouseName}";

        public double OriginalPrice => Product.SellingPrice;
        public double Cost => Product.CostCurrency == "USD" ? Product.CostPrice * _usdRate : Product.CostPrice;
        public bool IsDiscounted => Math.Abs(PriceAtSale - OriginalPrice) > 0.01;
        public double DiscountAmount => OriginalPrice - PriceAtSale;
        public double TotalPrice => SaleAccounting.Money(Quantity * PriceAtSale);
        public double TotalCost => Quantity * Cost;
        public string UnitDisplay => Product.UnitDisplay;

        public bool HasSellingPrice2 => Product.HasSellingPrice2;
        public bool IsPrice2Active => HasSellingPrice2 && Math.Abs(PriceAtSale - Product.SellingPrice2!.Value) < 0.01;
        public string PriceNumberDisplay => IsPrice2Active ? " 2" : " 1";

        public void TogglePrice()
        {
            if (!HasSellingPrice2) return;
            if (IsPrice2Active)
            {
                PriceAtSale = Product.SellingPrice;
            }
            else
            {
                PriceAtSale = Product.SellingPrice2!.Value;
            }
        }
    }

    public class HeldCartModel : ViewModelBase
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public List<CartItemModel> Items { get; set; } = new();
        public double TotalAmount => Items.Sum(i => i.TotalPrice);
        public int ItemCount => Items.Count;
        public DateTime HeldAt { get; set; } = DateTime.Now;
        public string DisplayText => $"{Name} ({ItemCount} ta - {TotalAmount:N0} so'm)";

        // Nasiya holati
        public string? CustomerGuid { get; set; }
        public string? CustomerName { get; set; }
        public string? DueDate { get; set; }
        public int SelectedPaymentType { get; set; } = 0;
        public string DebtCashAdvance { get; set; } = "0";
        public string DebtCardAdvance { get; set; } = "0";
    }

    public class CashierViewModel : ViewModelBase
    {
        private readonly DatabaseContext _db;
        private readonly ProductService _productService;
        private readonly CurrencyService _currencyService;
        private readonly PrinterService _printerService;
        private readonly DebtService _debtService;
        private bool _isSubmittingSale;
        private string _searchQuery = string.Empty;
        private string _selectedCategory = ProductService.CATEGORY_ALL;
        private string _statusMessage = "Kassa tayyor";
        private int _selectedReceiptPrintOption = 0; // 0 = None, 1 = Xprinter 58mm, 2 = A4

        public int SelectedReceiptPrintOption
        {
            get => _selectedReceiptPrintOption;
            set
            {
                if (SetProperty(ref _selectedReceiptPrintOption, value))
                {
                    OnPropertyChanged(nameof(IsPrintReceiptEnabled));
                    NotifyPreviewProperties();
                }
            }
        }

        public bool IsPrintReceiptEnabled
        {
            get => _selectedReceiptPrintOption == 1;
            set => SelectedReceiptPrintOption = value ? 1 : 0;
        }

        public bool IsNoReceiptPreview => SelectedReceiptPrintOption == 0;
        public bool Is58mmPreview => SelectedReceiptPrintOption == 1;
        public bool IsA4Preview => SelectedReceiptPrintOption == 2;

        private string? _selectedReceiptPrinter;
        private List<string> _availablePrinters = new();

        public List<string> AvailablePrinters
        {
            get => _availablePrinters;
            set => SetProperty(ref _availablePrinters, value);
        }

        public string? SelectedReceiptPrinter
        {
            get => _selectedReceiptPrinter ?? _printerService.FindReceiptPrinter();
            set
            {
                if (SetProperty(ref _selectedReceiptPrinter, value))
                {
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        _printerService.SaveReceiptPrinterSetting(value);
                    }
                    OnPropertyChanged(nameof(PrinterStatusText));
                    OnPropertyChanged(nameof(PrinterStatusColor));
                }
            }
        }

        public string PrinterStatusText
        {
            get
            {
                if (SelectedReceiptPrintOption == 0) return "⚪ Cheksiz to'lov rejimi (Printer talab qilinmaydi)";
                if (SelectedReceiptPrintOption == 1)
                {
                    var p = SelectedReceiptPrinter ?? _printerService.FindReceiptPrinter();
                    return !string.IsNullOrWhiteSpace(p) ? $"🟢 Ulangan: {p}" : "🔴 Chek printeri topilmadi (Ulanmagan)";
                }
                else
                {
                    var printers = AvailablePrinters.Count > 0 ? AvailablePrinters : PrinterService.GetInstalledPrinters();
                    return printers.Count > 0 ? $"🟢 Printer tayyor ({printers[0]})" : "🔴 Tizimda printer topilmadi";
                }
            }
        }

        public string PrinterStatusColor
        {
            get
            {
                if (SelectedReceiptPrintOption == 0) return "#94A3B8";
                if (SelectedReceiptPrintOption == 1)
                {
                    var p = SelectedReceiptPrinter ?? _printerService.FindReceiptPrinter();
                    return !string.IsNullOrWhiteSpace(p) ? "#10B981" : "#EF4444";
                }
                else
                {
                    var printers = AvailablePrinters.Count > 0 ? AvailablePrinters : PrinterService.GetInstalledPrinters();
                    return printers.Count > 0 ? "#10B981" : "#EF4444";
                }
            }
        }

        public string Receipt58mmPreviewText
        {
            get
            {
                if (CartItems.Count == 0) return "Savatcha bo'sh";
                var tempSale = BuildCurrentCartSale();
                return PrinterService.BuildReceiptText(tempSale);
            }
        }

        public List<A4PreviewItemRow> A4PreviewRows
        {
            get
            {
                var rows = new List<A4PreviewItemRow>();
                int idx = 1;
                foreach (var item in CartItems)
                {
                    rows.Add(new A4PreviewItemRow
                    {
                        IndexDisplay = idx++,
                        ProductName = item.Product.Name,
                        QuantityDisplay = $"{item.Quantity} {item.Product.UnitDisplay}",
                        PriceDisplay = $"{item.PriceAtSale:N0}",
                        TotalDisplay = $"{item.TotalPrice:N0}"
                    });
                }
                return rows;
            }
        }

        public string A4DateDisplay => DateTime.Now.ToString("dd.MM.yyyy HH:mm");
        public string A4PaymentDisplay => SelectedPaymentType switch
        {
            1 => "Karta orqali",
            2 => "Aralash (Naqd + Karta)",
            _ => "Naqd pul"
        };

        public void NotifyPreviewProperties()
        {
            OnPropertyChanged(nameof(IsNoReceiptPreview));
            OnPropertyChanged(nameof(Is58mmPreview));
            OnPropertyChanged(nameof(IsA4Preview));
            OnPropertyChanged(nameof(PrinterStatusText));
            OnPropertyChanged(nameof(PrinterStatusColor));
            OnPropertyChanged(nameof(Receipt58mmPreviewText));
            OnPropertyChanged(nameof(A4PreviewRows));
            OnPropertyChanged(nameof(A4DateDisplay));
            OnPropertyChanged(nameof(A4PaymentDisplay));
        }

        public ICommand PreviewCurrentCartReceiptCommand { get; }

        // Modal holatlari (Savatdagi narx va miqdorni tahrirlash - Chegirma)
        private bool _isEditModalOpen;
        private CartItemModel? _editingCartItem;
        private string _editProductName = string.Empty;
        private double _editOriginalPrice;
        private string _editPriceText = string.Empty;
        private string _editQuantityText = string.Empty;
        private string _editUnitDisplay = string.Empty;
        private string _editErrorMessage = string.Empty;

        public ObservableCollection<CartItemModel> CartItems { get; } = new();
        public ObservableCollection<HeldCartModel> HeldCarts { get; } = new();
        public ObservableCollection<Product> FilteredProducts { get; } = new();
        public ObservableCollection<Product> TopSellingProducts { get; } = new();
        public ObservableCollection<string> Categories { get; } = new();

        private bool _isShowingTopSellers = true;
        public bool IsShowingTopSellers
        {
            get => _isShowingTopSellers;
            set
            {
                if (SetProperty(ref _isShowingTopSellers, value))
                {
                    OnPropertyChanged(nameof(IsShowingSearch));
                }
            }
        }
        public bool IsShowingSearch => !_isShowingTopSellers;

        public ICommand AddToCartCommand { get; }
        public ICommand RemoveFromCartCommand { get; }
        public ICommand IncreaseQuantityCommand { get; }
        public ICommand DecreaseQuantityCommand { get; }
        public ICommand ClearCartCommand { get; }
        public ICommand CompleteSaleCommand { get; }
        public ICommand SelectCategoryCommand { get; }
        public ICommand SearchQueryEnterCommand { get; }

        // Hold Cart komandalari va modal holatlari
        private bool _isHoldModalOpen;
        public bool IsHoldModalOpen
        {
            get => _isHoldModalOpen;
            set => SetProperty(ref _isHoldModalOpen, value);
        }

        private string _holdCartName = string.Empty;
        public string HoldCartName
        {
            get => _holdCartName;
            set => SetProperty(ref _holdCartName, value);
        }

        public int HoldCartItemCount => CartItems.Count;
        public double HoldCartTotalAmount => CartItems.Sum(i => i.TotalPrice);
        public string HoldCartSummaryText => $"{HoldCartItemCount} ta tovar • {HoldCartTotalAmount:N0} so'm";

        public ICommand HoldCurrentCartCommand { get; }
        public ICommand ResumeHeldCartCommand { get; }
        public ICommand DeleteHeldCartCommand { get; }
        public ICommand CloseHoldModalCommand { get; }
        public ICommand ConfirmHoldCartCommand { get; }

        // Narx va miqdorni tahrirlash komandalari
        public ICommand OpenEditModalCommand { get; }
        public ICommand SaveEditModalCommand { get; }
        public ICommand CancelEditModalCommand { get; }

        // Topilmagan shtrix-kod modal komandalari va hodisasi
        private bool _isUnrecognizedBarcodeModalOpen;
        private string _unrecognizedBarcode = string.Empty;

        public bool IsUnrecognizedBarcodeModalOpen
        {
            get => _isUnrecognizedBarcodeModalOpen;
            set => SetProperty(ref _isUnrecognizedBarcodeModalOpen, value);
        }

        public string UnrecognizedBarcode
        {
            get => _unrecognizedBarcode;
            set => SetProperty(ref _unrecognizedBarcode, value);
        }

        public event Action<string>? RequestOpenAddProductWithBarcode;
        public ICommand CloseUnrecognizedBarcodeCommand { get; }
        public ICommand ConfirmAddUnrecognizedBarcodeCommand { get; }

        // Brak tovarlarni hisobdan chiqarish (Spisanie - 0 so'm)
        private bool _isBrakModalOpen;
        public bool IsBrakModalOpen
        {
            get => _isBrakModalOpen;
            set
            {
                if (SetProperty(ref _isBrakModalOpen, value))
                {
                    OnPropertyChanged(nameof(BrakItemsCount));
                    OnPropertyChanged(nameof(BrakTotalCost));
                    OnPropertyChanged(nameof(BrakTotalCostDisplay));
                }
            }
        }

        public int BrakItemsCount => CartItems.Count;
        public double BrakTotalCost => CartItems.Sum(item => item.TotalCost);
        public string BrakTotalCostDisplay => $"{BrakTotalCost:N0} so'm";

        public ICommand OpenBrakModalCommand { get; }
        public ICommand CloseBrakModalCommand { get; }
        public ICommand ConfirmBrakWriteOffCommand { get; }

        // Toast bildirishnoma (Next.js uslubida)
        private bool _isToastVisible;
        private string _toastTitle = string.Empty;
        private string _toastMessage = string.Empty;
        private System.Windows.Threading.DispatcherTimer? _toastTimer;
        private readonly System.Windows.Threading.DispatcherTimer _searchDebounceTimer;

        public bool IsToastVisible
        {
            get => _isToastVisible;
            set => SetProperty(ref _isToastVisible, value);
        }

        public string ToastTitle
        {
            get => _toastTitle;
            set => SetProperty(ref _toastTitle, value);
        }

        public string ToastMessage
        {
            get => _toastMessage;
            set => SetProperty(ref _toastMessage, value);
        }

        public ICommand DismissToastCommand { get; }
        public ICommand ToggleCartPriceCommand { get; }
        public ICommand ToggleAllCartPricesCommand { get; }

        public bool IsAllPrice2Active =>
            CartItems.Any(i => i.HasSellingPrice2) &&
            CartItems.Where(i => i.HasSellingPrice2).All(i => i.IsPrice2Active);

        // To'lov turi va sotish modali (Naqd / Karta / Aralash)
        private bool _isPaymentModalOpen;
        public bool IsPaymentModalOpen
        {
            get => _isPaymentModalOpen;
            set => SetProperty(ref _isPaymentModalOpen, value);
        }

        private int _selectedPaymentType = 0; // 0 = Cash, 1 = Card, 2 = Split, 3 = Debt
        public int SelectedPaymentType
        {
            get => _selectedPaymentType;
            set
            {
                if (SetProperty(ref _selectedPaymentType, value))
                {
                    OnPropertyChanged(nameof(IsCashSelected));
                    OnPropertyChanged(nameof(IsCardSelected));
                    OnPropertyChanged(nameof(IsSplitSelected));
                    OnPropertyChanged(nameof(IsDebtSelected));
                    if (value == 3)
                    {
                        RefreshActiveCustomers();
                    }
                    RecalculatePaymentAmounts();
                    NotifyPreviewProperties();
                }
            }
        }

        public bool IsCashSelected => SelectedPaymentType == 0;
        public bool IsCardSelected => SelectedPaymentType == 1;
        public bool IsSplitSelected => SelectedPaymentType == 2;
        public bool IsDebtSelected => SelectedPaymentType == 3;

        // --- NASIYA (DEBT) MAYDONLARI ---
        public ObservableCollection<DebtCustomerItemDto> ActiveCustomers { get; } = new();

        private DebtCustomerItemDto? _selectedDebtCustomer;
        public DebtCustomerItemDto? SelectedDebtCustomer
        {
            get => _selectedDebtCustomer;
            set
            {
                if (SetProperty(ref _selectedDebtCustomer, value))
                {
                    OnPropertyChanged(nameof(HasSelectedDebtCustomer));
                    OnPropertyChanged(nameof(CustomerDebtBalanceText));
                    NotifyPreviewProperties();
                }
            }
        }

        public bool HasSelectedDebtCustomer => SelectedDebtCustomer != null;

        public string CustomerDebtBalanceText
        {
            get
            {
                if (SelectedDebtCustomer == null) return string.Empty;
                if (SelectedDebtCustomer.BalanceMinor > 0)
                    return $"Mavjud qarzi: {SelectedDebtCustomer.BalanceMinor / 100.0:N0} so'm";
                if (SelectedDebtCustomer.BalanceMinor < 0)
                    return $"Haqdorligi: +{Math.Abs(SelectedDebtCustomer.BalanceMinor) / 100.0:N0} so'm";
                return "Qarzi yo'q (0 so'm)";
            }
        }

        private string _debtCashAdvanceInput = "0";
        public string DebtCashAdvanceInput
        {
            get => _debtCashAdvanceInput;
            set
            {
                if (SetProperty(ref _debtCashAdvanceInput, value))
                {
                    OnPropertyChanged(nameof(DebtRemainingAmount));
                    OnPropertyChanged(nameof(DebtRemainingAmountText));
                    NotifyPreviewProperties();
                }
            }
        }

        private string _debtCardAdvanceInput = "0";
        public string DebtCardAdvanceInput
        {
            get => _debtCardAdvanceInput;
            set
            {
                if (SetProperty(ref _debtCardAdvanceInput, value))
                {
                    OnPropertyChanged(nameof(DebtRemainingAmount));
                    OnPropertyChanged(nameof(DebtRemainingAmountText));
                    NotifyPreviewProperties();
                }
            }
        }

        private static string CleanNumber(string? s) => (s ?? string.Empty).Replace(" ", "").Replace("\u00A0", "").Replace(',', '.').Trim();

        public double DebtRemainingAmount
        {
            get
            {
                double.TryParse(CleanNumber(DebtCashAdvanceInput), NumberStyles.Any, CultureInfo.InvariantCulture, out var cashAdv);
                double.TryParse(CleanNumber(DebtCardAdvanceInput), NumberStyles.Any, CultureInfo.InvariantCulture, out var cardAdv);
                return Math.Max(0, TotalAmount - (Math.Max(0, cashAdv) + Math.Max(0, cardAdv)));
            }
        }

        public string DebtRemainingAmountText => $"{DebtRemainingAmount:N0} so'm";

        private DateTime? _debtDueDate;
        public DateTime? DebtDueDate
        {
            get => _debtDueDate;
            set
            {
                if (SetProperty(ref _debtDueDate, value))
                {
                    NotifyPreviewProperties();
                }
            }
        }

        private string _debtNote = string.Empty;
        public string DebtNote
        {
            get => _debtNote;
            set => SetProperty(ref _debtNote, value);
        }

        // Quick customer modal in cashier
        private bool _isQuickCustomerModalOpen;
        public bool IsQuickCustomerModalOpen { get => _isQuickCustomerModalOpen; set => SetProperty(ref _isQuickCustomerModalOpen, value); }

        private string _quickCustomerName = string.Empty;
        public string QuickCustomerName { get => _quickCustomerName; set => SetProperty(ref _quickCustomerName, value); }

        private string _quickCustomerPhone = string.Empty;
        public string QuickCustomerPhone { get => _quickCustomerPhone; set => SetProperty(ref _quickCustomerPhone, value); }

        private string _quickCustomerNote = string.Empty;
        public string QuickCustomerNote { get => _quickCustomerNote; set => SetProperty(ref _quickCustomerNote, value); }

        private string _quickCustomerErrorMessage = string.Empty;
        public string QuickCustomerErrorMessage
        {
            get => _quickCustomerErrorMessage;
            set
            {
                if (SetProperty(ref _quickCustomerErrorMessage, value))
                {
                    OnPropertyChanged(nameof(HasQuickCustomerError));
                }
            }
        }
        public bool HasQuickCustomerError => !string.IsNullOrWhiteSpace(QuickCustomerErrorMessage);

        public ICommand OpenQuickCustomerModalCommand { get; }
        public ICommand CloseQuickCustomerModalCommand { get; }
        public ICommand SaveQuickCustomerModalCommand { get; }

        private string _cashAmountInput = string.Empty;
        public string CashAmountInput
        {
            get => _cashAmountInput;
            set
            {
                if (SetProperty(ref _cashAmountInput, value))
                {
                    if (SelectedPaymentType == 2)
                    {
                        UpdateSplitFromCash();
                    }
                }
            }
        }

        private string _cardAmountInput = string.Empty;
        public string CardAmountInput
        {
            get => _cardAmountInput;
            set
            {
                if (SetProperty(ref _cardAmountInput, value))
                {
                    if (SelectedPaymentType == 2)
                    {
                        UpdateSplitFromCard();
                    }
                }
            }
        }

        private double _currentCardTaxRate = 1.8;
        public double CurrentCardTaxRate
        {
            get => _currentCardTaxRate;
            set => SetProperty(ref _currentCardTaxRate, value);
        }

        private double _previewCardTax = 0;
        public double PreviewCardTax
        {
            get => _previewCardTax;
            set => SetProperty(ref _previewCardTax, value);
        }

        public ICommand OpenPaymentModalCommand { get; }
        public ICommand ClosePaymentModalCommand { get; }
        public ICommand SelectPaymentTypeCommand { get; }
        public ICommand ConfirmSaleCommand { get; }

        public CashierViewModel(DatabaseContext db, ProductService productService, CurrencyService currencyService, DebtService? debtService = null)
        {
            _db = db;
            _productService = productService;
            _currencyService = currencyService;
            _debtService = debtService ?? new DebtService(_db);
            _printerService = new PrinterService(_db);

            OpenQuickCustomerModalCommand = new RelayCommand(OpenQuickCustomerModal);
            CloseQuickCustomerModalCommand = new RelayCommand(() => IsQuickCustomerModalOpen = false);
            SaveQuickCustomerModalCommand = new RelayCommand(SaveQuickCustomerModal);

            _searchDebounceTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(150)
            };
            _searchDebounceTimer.Tick += (s, e) =>
            {
                _searchDebounceTimer.Stop();
                RefreshProducts();
            };

            AddToCartCommand = new RelayCommand<Product>(p => { if (p != null) AddToCart(p); });
            RemoveFromCartCommand = new RelayCommand<CartItemModel>(c => { if (c != null) RemoveFromCart(c); });
            IncreaseQuantityCommand = new RelayCommand<CartItemModel>(c => { if (c != null) ChangeQuantity(c, 1.0); });
            DecreaseQuantityCommand = new RelayCommand<CartItemModel>(c => { if (c != null) ChangeQuantity(c, -1.0); });
            ToggleCartPriceCommand = new RelayCommand<CartItemModel>(c => { if (c != null) ToggleCartPrice(c); });
            ToggleAllCartPricesCommand = new RelayCommand(ToggleAllCartPrices);
            ClearCartCommand = new RelayCommand(ClearCart);
            CompleteSaleCommand = new RelayCommand(_ => OpenPaymentModal());
            SelectCategoryCommand = new RelayCommand<string>(cat => { if (cat != null) SelectedCategory = cat; });
            SearchQueryEnterCommand = new RelayCommand(HandleSearchQueryEnter);

            OpenPaymentModalCommand = new RelayCommand(_ => OpenPaymentModal());
            ClosePaymentModalCommand = new RelayCommand(_ => IsPaymentModalOpen = false);
            SelectPaymentTypeCommand = new RelayCommand(p => {
                if (int.TryParse(p?.ToString(), out var mode))
                {
                    SelectedPaymentType = mode;
                }
            });
            ConfirmSaleCommand = new RelayCommand(_ => ConfirmSale());
            PreviewCurrentCartReceiptCommand = new RelayCommand(_ => PreviewCurrentCartReceipt());

            HoldCurrentCartCommand = new RelayCommand(_ => OpenHoldCartModal());
            CloseHoldModalCommand = new RelayCommand(CloseHoldModal);
            ConfirmHoldCartCommand = new RelayCommand(ConfirmHoldCart);
            ResumeHeldCartCommand = new RelayCommand<HeldCartModel>(h => { if (h != null) ResumeHeldCart(h); });
            DeleteHeldCartCommand = new RelayCommand<HeldCartModel>(h => { if (h != null) DeleteHeldCart(h); });

            OpenEditModalCommand = new RelayCommand<CartItemModel>(c => { if (c != null) OpenEditModal(c); });
            SaveEditModalCommand = new RelayCommand(SaveEditModal);
            CancelEditModalCommand = new RelayCommand(CancelEditModal);
            DismissToastCommand = new RelayCommand(DismissToast);

            CloseUnrecognizedBarcodeCommand = new RelayCommand(CloseUnrecognizedBarcode);
            ConfirmAddUnrecognizedBarcodeCommand = new RelayCommand(ConfirmAddUnrecognizedBarcode);

            OpenBrakModalCommand = new RelayCommand(_ => OpenBrakModal());
            CloseBrakModalCommand = new RelayCommand(_ => IsBrakModalOpen = false);
            ConfirmBrakWriteOffCommand = new RelayCommand(_ => ConfirmBrakWriteOff());

            RefreshCategories();
            RefreshProducts();
        }

        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (SetProperty(ref _searchQuery, value))
                {
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
                    RefreshProducts();
                }
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public double TotalAmount => SaleAccounting.Money(CartItems.Sum(i => i.TotalPrice));
        public double TotalCost => CartItems.Sum(i => i.TotalCost);
        public int TotalItemsCount => CartItems.Count;
        public bool HasHeldCarts => HeldCarts.Count > 0;
        public string CompleteSaleButtonText => CartItems.Count > 0 ? $"🛒 SOTISH ({TotalAmount:N0} so'm)" : "🛒 SOTISH";

        // Modal properties
        public bool IsEditModalOpen
        {
            get => _isEditModalOpen;
            set => SetProperty(ref _isEditModalOpen, value);
        }

        public string EditProductName
        {
            get => _editProductName;
            set => SetProperty(ref _editProductName, value);
        }

        private string _editWarehouseName = string.Empty;
        public string EditWarehouseName
        {
            get => _editWarehouseName;
            set => SetProperty(ref _editWarehouseName, value);
        }

        public double EditOriginalPrice
        {
            get => _editOriginalPrice;
            set => SetProperty(ref _editOriginalPrice, value);
        }

        public string EditPriceText
        {
            get => _editPriceText;
            set => SetProperty(ref _editPriceText, value);
        }

        public string EditQuantityText
        {
            get => _editQuantityText;
            set => SetProperty(ref _editQuantityText, value);
        }

        public string EditUnitDisplay
        {
            get => _editUnitDisplay;
            set => SetProperty(ref _editUnitDisplay, value);
        }

        public string EditErrorMessage
        {
            get => _editErrorMessage;
            set => SetProperty(ref _editErrorMessage, value);
        }

        public void HandleBarcodeScanned(string barcode)
        {
            var clean = barcode.Trim().Replace("\r", "").Replace("\n", "");
            if (string.IsNullOrWhiteSpace(clean)) return;

            var product = _db.GetProductByBarcode(clean);
            if (product != null)
            {
                AddToCart(product);
                SearchQuery = string.Empty;
                StatusMessage = $"✅ '{product.Name}' savatchaga qo'shildi";
            }
            else
            {
                UnrecognizedBarcode = clean;
                IsUnrecognizedBarcodeModalOpen = true;
                StatusMessage = $"⚠️ Shtrix-kod topilmadi: {clean}";
            }
        }

        public void HandleSearchQueryEnter()
        {
            _searchDebounceTimer.Stop();
            var q = SearchQuery.Trim().Replace("\r", "").Replace("\n", "");
            if (string.IsNullOrWhiteSpace(q)) return;

            var qEn = KeyboardLayoutHelper.ConvertRuToEn(q);

            // 1. Shtrix-kod bo'yicha to'g'ridan-to'g'ri qidiruv
            var product = _db.GetProductByBarcode(q) ?? (qEn != q ? _db.GetProductByBarcode(qEn) : null);
            if (product != null)
            {
                AddToCart(product);
                SearchQuery = string.Empty;
                StatusMessage = $"✅ '{product.Name}' savatchaga qo'shildi";
                return;
            }

            // 2. Qidiruv natijasida 1 ta mahsulot chiqqan bo'lsa, o'shani qo'shish
            if (FilteredProducts.Count == 1)
            {
                var singleProduct = FilteredProducts[0];
                AddToCart(singleProduct);
                SearchQuery = string.Empty;
                StatusMessage = $"✅ '{singleProduct.Name}' savatchaga qo'shildi";
                return;
            }

            // 3. Filtrlanganlar orasidan shtrix-kodi aynan mos kelganini topish
            var byBc = FilteredProducts.FirstOrDefault(p => 
                string.Equals(p.Barcode?.Trim(), q, StringComparison.OrdinalIgnoreCase) ||
                (qEn != q && string.Equals(p.Barcode?.Trim(), qEn, StringComparison.OrdinalIgnoreCase)));
            if (byBc != null)
            {
                AddToCart(byBc);
                SearchQuery = string.Empty;
                StatusMessage = $"✅ '{byBc.Name}' savatchaga qo'shildi";
                return;
            }

            // 4. Mahsulot omborda topilmadi: "Yangi mahsulot qo'shamizmi?" dialogini ko'rsatish
            UnrecognizedBarcode = q;
            IsUnrecognizedBarcodeModalOpen = true;
            StatusMessage = $"⚠️ Shtrix-kod yoki tovar topilmadi: {q}";
        }

        public void CloseUnrecognizedBarcode()
        {
            IsUnrecognizedBarcodeModalOpen = false;
            UnrecognizedBarcode = string.Empty;
            SearchQuery = string.Empty;
        }

        public void ConfirmAddUnrecognizedBarcode()
        {
            var code = UnrecognizedBarcode;
            IsUnrecognizedBarcodeModalOpen = false;
            UnrecognizedBarcode = string.Empty;
            SearchQuery = string.Empty;

            if (!string.IsNullOrWhiteSpace(code))
            {
                RequestOpenAddProductWithBarcode?.Invoke(code);
            }
        }

        public void AddToCart(Product product, double qty = 1.0)
        {
            var (whGuid, whName) = _db.GetWarehouseForProduct(product.Guid, product.WarehouseGuid);

            var existing = CartItems.FirstOrDefault(i => i.Product.Id == product.Id && (string.IsNullOrEmpty(whGuid) || i.WarehouseGuid == whGuid));
            if (existing != null)
            {
                existing.Quantity += qty;
            }
            else
            {
                CartItems.Add(new CartItemModel(
                    product, 
                    qty, 
                    product.SellingPrice, 
                    _currencyService.GetCachedUsdRate(),
                    whGuid,
                    whName
                ));
            }

            if (product.StockQuantity <= 0)
            {
                StatusMessage = $"✅ '{product.Name}' savatchaga qo'shildi (Omborda qoldiq 0)";
            }
            else
            {
                StatusMessage = $"✅ '{product.Name}' savatchaga qo'shildi [{whName}]";
            }

            NotifyTotals();
        }

        public void RemoveFromCart(CartItemModel item)
        {
            CartItems.Remove(item);
            NotifyTotals();
        }

        public void ChangeQuantity(CartItemModel item, double delta)
        {
            var newQty = item.Quantity + delta;
            if (newQty <= 0)
            {
                CartItems.Remove(item);
            }
            else
            {
                if (newQty > item.Product.StockQuantity)
                {
                    StatusMessage = $"⚠️ Omborda yetarli qoldiq yo'q! (Mavjud: {item.Product.StockQuantity} {item.Product.UnitDisplay})";
                    return;
                }
                item.Quantity = newQty;
            }
            NotifyTotals();
        }

        public void ClearCart()
        {
            CartItems.Clear();
            NotifyTotals();
            StatusMessage = "Savatcha tozalandi";
        }

        public void ToggleCartPrice(CartItemModel item)
        {
            item.TogglePrice();
            NotifyTotals();
            var priceType = item.IsPrice2Active ? "2-narx (Usta narxi)" : "Asosiy narx";
            StatusMessage = $"🔄 '{item.Product.Name}' uchun {priceType} belgilandi ({item.PriceAtSale:N0} so'm)";
        }

        public void ToggleAllCartPrices()
        {
            if (CartItems.Count == 0) return;

            var eligibleItems = CartItems.Where(i => i.HasSellingPrice2).ToList();
            if (eligibleItems.Count == 0)
            {
                StatusMessage = "ℹ️ Savatdagi mahsulotlarda 2-narx (ulgurji/usta narxi) belgilanmagan";
                return;
            }

            bool switchToPrice2 = !eligibleItems.All(i => i.IsPrice2Active);

            foreach (var item in eligibleItems)
            {
                item.PriceAtSale = switchToPrice2 ? item.Product.SellingPrice2!.Value : item.Product.SellingPrice;
            }

            NotifyTotals();

            if (switchToPrice2)
            {
                StatusMessage = $"🔄 Barcha mos tovarlar 2-narxga (Usta narxi) o'tkazildi ({eligibleItems.Count} ta)";
            }
            else
            {
                StatusMessage = $"🔄 Barcha tovarlar asosiy 1-narxga qaytarildi ({eligibleItems.Count} ta)";
            }
        }

        // --- HOLD CART LOGIKASI ---
        public void HoldCurrentCart()
        {
            OpenHoldCartModal();
        }

        public void OpenHoldCartModal()
        {
            if (CartItems.Count == 0) return;

            HoldCartName = $"Mijoz #{HeldCarts.Count + 1}";
            OnPropertyChanged(nameof(HoldCartItemCount));
            OnPropertyChanged(nameof(HoldCartTotalAmount));
            OnPropertyChanged(nameof(HoldCartSummaryText));
            IsHoldModalOpen = true;
        }

        public void CloseHoldModal()
        {
            IsHoldModalOpen = false;
            HoldCartName = string.Empty;
        }

        public void ConfirmHoldCart()
        {
            if (CartItems.Count == 0)
            {
                IsHoldModalOpen = false;
                return;
            }

            var customName = HoldCartName?.Trim();
            if (string.IsNullOrWhiteSpace(customName))
            {
                customName = $"Mijoz #{HeldCarts.Count + 1}";
            }

            var held = new HeldCartModel
            {
                Name = $"{customName} ({DateTime.Now:HH:mm})",
                CustomerGuid = SelectedDebtCustomer?.CustomerGuid,
                CustomerName = SelectedDebtCustomer?.FullName,
                DueDate = DebtDueDate?.ToString("yyyy-MM-dd"),
                SelectedPaymentType = SelectedPaymentType,
                DebtCashAdvance = DebtCashAdvanceInput,
                DebtCardAdvance = DebtCardAdvanceInput,
                Items = CartItems.Select(i => new CartItemModel(
                    i.Product, 
                    i.Quantity, 
                    i.PriceAtSale, 
                    _currencyService.GetCachedUsdRate(),
                    i.WarehouseGuid,
                    i.WarehouseName
                )).ToList()
            };

            HeldCarts.Add(held);
            CartItems.Clear();
            SelectedDebtCustomer = null;
            DebtDueDate = null;
            DebtCashAdvanceInput = "0";
            DebtCardAdvanceInput = "0";
            DebtNote = string.Empty;

            NotifyTotals();
            OnPropertyChanged(nameof(HasHeldCarts));
            IsHoldModalOpen = false;
            HoldCartName = string.Empty;
            StatusMessage = $"⏸️ '{held.Name}' savatchasi kutishga qo'yildi";
        }

        public void ResumeHeldCart(HeldCartModel held)
        {
            if (CartItems.Count > 0)
            {
                var autoHeld = new HeldCartModel
                {
                    Name = $"Mijoz (Avto) ({DateTime.Now:HH:mm})",
                    CustomerGuid = SelectedDebtCustomer?.CustomerGuid,
                    CustomerName = SelectedDebtCustomer?.FullName,
                    DueDate = DebtDueDate?.ToString("yyyy-MM-dd"),
                    SelectedPaymentType = SelectedPaymentType,
                    DebtCashAdvance = DebtCashAdvanceInput,
                    DebtCardAdvance = DebtCardAdvanceInput,
                    Items = CartItems.Select(i => new CartItemModel(
                        i.Product, 
                        i.Quantity, 
                        i.PriceAtSale, 
                        _currencyService.GetCachedUsdRate(),
                        i.WarehouseGuid,
                        i.WarehouseName
                    )).ToList()
                };
                HeldCarts.Add(autoHeld);
            }

            HeldCarts.Remove(held);
            CartItems.Clear();
            foreach (var item in held.Items)
            {
                CartItems.Add(item);
            }

            SelectedPaymentType = held.SelectedPaymentType;
            if (!string.IsNullOrEmpty(held.CustomerGuid))
            {
                RefreshActiveCustomers();
                SelectedDebtCustomer = ActiveCustomers.FirstOrDefault(c => c.CustomerGuid == held.CustomerGuid);
            }
            else
            {
                SelectedDebtCustomer = null;
            }
            if (!string.IsNullOrEmpty(held.DueDate) && DateTime.TryParse(held.DueDate, out var dt))
            {
                DebtDueDate = dt;
            }
            else
            {
                DebtDueDate = null;
            }
            DebtCashAdvanceInput = held.DebtCashAdvance ?? "0";
            DebtCardAdvanceInput = held.DebtCardAdvance ?? "0";

            NotifyTotals();
            OnPropertyChanged(nameof(HasHeldCarts));
            StatusMessage = $"▶️ '{held.Name}' savatchasi tiklandi";
        }

        public void DeleteHeldCart(HeldCartModel held)
        {
            HeldCarts.Remove(held);
            OnPropertyChanged(nameof(HasHeldCarts));
            StatusMessage = $"🗑️ '{held.Name}' o'chirildi";
        }

        // --- SAVATDAGI NARX VA MIQDORNI TAHRIRLASH (CHEGIRMA) ---
        public void OpenEditModal(CartItemModel item)
        {
            _editingCartItem = item;
            EditProductName = item.Product.Name;
            EditWarehouseName = item.WarehouseName;
            EditOriginalPrice = item.OriginalPrice;
            EditPriceText = item.PriceAtSale.ToString("0.##");
            EditQuantityText = item.Quantity.ToString("0.##");
            EditUnitDisplay = item.UnitDisplay;
            EditErrorMessage = string.Empty;
            IsEditModalOpen = true;
        }

        public void SaveEditModal()
        {
            if (_editingCartItem == null) return;

            if (!double.TryParse(EditQuantityText, out var newQty) || newQty <= 0)
            {
                EditErrorMessage = "Miqdor 0 dan katta son bo'lishi kerak!";
                return;
            }

            if (!double.TryParse(EditPriceText, out var newPrice) || newPrice < 0)
            {
                EditErrorMessage = "Narx to'g'ri kiritilmadi!";
                return;
            }

            if (newQty > _editingCartItem.Product.StockQuantity)
            {
                EditErrorMessage = $"Omborda yetarli qoldiq yo'q! (Mavjud: {_editingCartItem.Product.StockQuantity} {_editingCartItem.Product.UnitDisplay})";
                return;
            }

            _editingCartItem.Quantity = newQty;
            _editingCartItem.PriceAtSale = newPrice;
            NotifyTotals();

            IsEditModalOpen = false;
            StatusMessage = $"✅ '{_editingCartItem.Product.Name}' narxi va miqdori yangilandi";
        }

        public void CancelEditModal()
        {
            IsEditModalOpen = false;
            _editingCartItem = null;
        }

        // --- NASIYA VA MIJOZ METODLARI ---
        public void OpenQuickCustomerModal()
        {
            QuickCustomerName = string.Empty;
            QuickCustomerPhone = string.Empty;
            QuickCustomerNote = string.Empty;
            QuickCustomerErrorMessage = string.Empty;
            IsQuickCustomerModalOpen = true;
        }

        public void SaveQuickCustomerModal()
        {
            if (string.IsNullOrWhiteSpace(QuickCustomerName))
            {
                QuickCustomerErrorMessage = "Mijoz ismi kiritilishi shart!";
                return;
            }

            try
            {
                var newCustomerGuid = _debtService.CreateCustomer(QuickCustomerName.Trim(), QuickCustomerPhone?.Trim() ?? "", QuickCustomerNote?.Trim() ?? "");
                RefreshActiveCustomers();
                SelectedDebtCustomer = ActiveCustomers.FirstOrDefault(c => c.CustomerGuid == newCustomerGuid);
                IsQuickCustomerModalOpen = false;
                ShowToast("Mijoz qo'shildi", $"'{QuickCustomerName.Trim()}' muvaffaqiyatli saqlandi");
            }
            catch (Exception ex)
            {
                QuickCustomerErrorMessage = ex.Message;
            }
        }

        public void RefreshActiveCustomers()
        {
            var prevGuid = SelectedDebtCustomer?.CustomerGuid;
            ActiveCustomers.Clear();
            var list = _debtService.GetActiveCustomers();
            foreach (var c in list)
            {
                ActiveCustomers.Add(c);
            }
            if (!string.IsNullOrEmpty(prevGuid))
            {
                SelectedDebtCustomer = ActiveCustomers.FirstOrDefault(c => c.CustomerGuid == prevGuid);
            }
        }

        public void SetDebtCustomer(DebtCustomerItemDto customer)
        {
            SelectedPaymentType = 3;
            RefreshActiveCustomers();
            SelectedDebtCustomer = ActiveCustomers.FirstOrDefault(c => c.CustomerGuid == customer.CustomerGuid);
        }

        // --- YAGONA SOTISH VA TO'LOV MANTIG'I ---
        public void OpenPaymentModal()
        {
            if (CartItems.Count == 0) return;
            CurrentCardTaxRate = _db.GetCardTaxRate();
            SelectedPaymentType = 0; // Standart Naqd
            RecalculatePaymentAmounts();

            // Printerlar ro'yxatini yangilash
            AvailablePrinters = PrinterService.GetInstalledPrinters();
            _selectedReceiptPrinter = _printerService.FindReceiptPrinter();
            OnPropertyChanged(nameof(SelectedReceiptPrinter));

            NotifyPreviewProperties();
            IsPaymentModalOpen = true;
        }

        private void RecalculatePaymentAmounts()
        {
            if (SelectedPaymentType == 0) // Naqd
            {
                _cashAmountInput = TotalAmount.ToString("0");
                _cardAmountInput = "0";
                PreviewCardTax = 0;
            }
            else if (SelectedPaymentType == 1) // Karta
            {
                _cashAmountInput = "0";
                _cardAmountInput = TotalAmount.ToString("0");
                PreviewCardTax = TotalAmount * (CurrentCardTaxRate / 100.0);
            }
            else if (SelectedPaymentType == 2) // Aralash
            {
                var half = Math.Round(TotalAmount / 2.0);
                _cashAmountInput = half.ToString("0");
                _cardAmountInput = (TotalAmount - half).ToString("0");
                PreviewCardTax = (TotalAmount - half) * (CurrentCardTaxRate / 100.0);
            }
            else // Nasiya (3)
            {
                _cashAmountInput = DebtCashAdvanceInput;
                _cardAmountInput = DebtCardAdvanceInput;
                double.TryParse(CleanNumber(DebtCardAdvanceInput), NumberStyles.Any, CultureInfo.InvariantCulture, out var cardAdv);
                PreviewCardTax = Math.Max(0, cardAdv) * (CurrentCardTaxRate / 100.0);
            }
            OnPropertyChanged(nameof(CashAmountInput));
            OnPropertyChanged(nameof(CardAmountInput));
            OnPropertyChanged(nameof(DebtRemainingAmount));
            OnPropertyChanged(nameof(DebtRemainingAmountText));
        }

        private void UpdateSplitFromCash()
        {
            var clean = (CashAmountInput ?? "").Replace(" ", "").Replace("\u00A0", "").Replace(',', '.');
            if (double.TryParse(clean, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var c))
            {
                var card = Math.Max(0, TotalAmount - c);
                _cardAmountInput = card.ToString("0");
                PreviewCardTax = card * (CurrentCardTaxRate / 100.0);
                OnPropertyChanged(nameof(CardAmountInput));
            }
        }

        private void UpdateSplitFromCard()
        {
            var clean = (CardAmountInput ?? "").Replace(" ", "").Replace("\u00A0", "").Replace(',', '.');
            if (double.TryParse(clean, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var card))
            {
                var cash = Math.Max(0, TotalAmount - card);
                _cashAmountInput = cash.ToString("0");
                PreviewCardTax = card * (CurrentCardTaxRate / 100.0);
                OnPropertyChanged(nameof(CashAmountInput));
            }
        }

        public void ConfirmSale()
        {
            if (CartItems.Count == 0) return;
            if (_isSubmittingSale) return;

            if (SelectedPaymentType == 3 && SelectedDebtCustomer == null)
            {
                MessageBox.Show(
                    "⚠️ Nasiya savdoni amalga oshirish uchun mijoz tanlanishi shart!\n\nIltimos, ro'yxatdan mijozni tanlang yoki '+ Yangi' tugmasi orqali yangi mijoz qo'shing.",
                    "Mijoz tanlanmagan",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            // 1. Printer tekshiruvi (Agar printer tanlangan bo'lsa, u ulangan bo'lishi shart!)
            if (SelectedReceiptPrintOption == 1) // Chek printeri
            {
                var targetPrinter = SelectedReceiptPrinter ?? _printerService.FindReceiptPrinter();
                if (string.IsNullOrWhiteSpace(targetPrinter))
                {
                    MessageBox.Show(
                        "⚠️ Kassa chek printeri kompyuterga ulanmagan yoki o'chirilgan!\n\nIltimos, printerni kompyuterga ulang yoki cheksiz sotish uchun '🚫 Chek chiqarilmasin' bandini tanlang.",
                        "Printer topilmadi",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }
            }
            else if (SelectedReceiptPrintOption == 2) // A4
            {
                var printers = PrinterService.GetInstalledPrinters();
                if (printers.Count == 0)
                {
                    MessageBox.Show(
                        "⚠️ Kompyuterda hech qanday A4 printer o'rnatilmagan!\n\nIltimos, printerni ulang yoki '🚫 Chek chiqarilmasin' bandini tanlang.",
                        "Printer topilmadi",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }
            }

            _isSubmittingSale = true;
            try
            {
                Sale sale;
                if (SelectedPaymentType == 3) // Nasiya
                {
                    double.TryParse(CleanNumber(DebtCashAdvanceInput), NumberStyles.Any, CultureInfo.InvariantCulture, out var cashAdv);
                    double.TryParse(CleanNumber(DebtCardAdvanceInput), NumberStyles.Any, CultureInfo.InvariantCulture, out var cardAdv);
                    cashAdv = Math.Clamp(double.IsFinite(cashAdv) ? cashAdv : 0, 0, TotalAmount);
                    cardAdv = Math.Clamp(double.IsFinite(cardAdv) ? cardAdv : 0, 0, TotalAmount - cashAdv);

                    var cashMinor = (long)Math.Round(cashAdv * 100);
                    var cardMinor = (long)Math.Round(cardAdv * 100);

                    var saleGuid = _checkoutGuid;
                    var occurredAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    var snapshot = DebtService.BuildSaleSnapshot(
                        saleGuid,
                        occurredAt,
                        CartItems.Select(i => new DebtCartItemDto(
                            i.Product.Guid,
                            i.Product.Name,
                            i.Product.Category,
                            i.UnitDisplay,
                            i.WarehouseGuid,
                            i.WarehouseName,
                            i.Quantity,
                            i.PriceAtSale,
                            i.Product.CostPrice,
                            i.Product.CostCurrency)).ToList(),
                        cashMinor,
                        cardMinor,
                        _currencyService.GetCachedUsdRate(),
                        CurrentCardTaxRate);

                    var dueDateStr = DebtDueDate?.ToString("yyyy-MM-dd");
                    var requestGuid = Guid.NewGuid().ToString("D");

                    _debtService.OpenDebtSale(
                        requestGuid,
                        SelectedDebtCustomer!.CustomerGuid,
                        snapshot,
                        dueDateStr,
                        null,
                        1L);

                    sale = BuildCurrentCartSale();
                }
                else
                {
                    sale = BuildCurrentCartSale();
                    _db.InsertSale(sale);
                }

                _checkoutGuid = Guid.NewGuid().ToString();

                if (SelectedReceiptPrintOption == 1)
                {
                    try
                    {
                        var targetPrinter = SelectedReceiptPrinter ?? _printerService.FindReceiptPrinter();
                        _printerService.PrintReceipt(sale, targetPrinter);
                    }
                    catch (Exception pex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Receipt print error: {pex.Message}");
                    }
                }
                else if (SelectedReceiptPrintOption == 2)
                {
                    try
                    {
                        _printerService.PrintInvoiceA4(sale, System.Windows.Application.Current?.MainWindow);
                    }
                    catch (Exception pex)
                    {
                        System.Diagnostics.Debug.WriteLine($"A4 print error: {pex.Message}");
                    }
                }

                var totalFormatted = sale.TotalAmount.ToString("N0");
                var itemsCount = sale.Items.Count;
                IsPaymentModalOpen = false;
                ClearCart();
                RefreshProducts();

                // Reset debt fields
                SelectedDebtCustomer = null;
                DebtCashAdvanceInput = "0";
                DebtCardAdvanceInput = "0";
                DebtDueDate = null;
                DebtNote = string.Empty;

                StatusMessage = $"🎉 Savdo muvaffaqiyatli yakunlandi! Jami: {totalFormatted} so'm ({sale.PaymentTypeDisplay})";
                ShowToast("To'lov muvaffaqiyatli amalga oshirildi! 🎉", $"Jami summa: {totalFormatted} so'm • {sale.PaymentTypeDisplay}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Xatolik yuz berdi: {ex.Message}", "Xatolik", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isSubmittingSale = false;
            }
        }

        public void OpenBrakModal()
        {
            if (CartItems.Count == 0)
            {
                ShowToast("⚠️ Savatcha bo'sh", "Brak sifatida hisobdan chiqarish uchun avval tovar qo'shing!");
                return;
            }
            IsBrakModalOpen = true;
        }

        public void ConfirmBrakWriteOff()
        {
            if (CartItems.Count == 0)
            {
                IsBrakModalOpen = false;
                return;
            }

            var brakRate = _currencyService.GetCachedUsdRate();
            var totalCost = SaleAccounting.Money(CartItems.Sum(i => i.Quantity * i.Product.CostPrice * (i.Product.CostCurrency == "USD" ? brakRate : 1)));
            var itemsCount = CartItems.Count;

            var sale = new Sale
            {
                Id = _db.GetTotalSalesCount() + 1,
                TotalAmount = 0.0,
                TotalCost = totalCost,
                UsdRate = brakRate,
                PaymentType = PaymentType.BRAK,
                CashAmount = 0.0,
                CardAmount = 0.0,
                TaxAmount = 0.0,
                TaxRate = 0.0,
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                UserId = 1L
            };

            foreach (var item in CartItems)
            {
                sale.Items.Add(new SaleItem
                {
                    ProductId = item.Product.Id,
                    ProductGuid = item.Product.Guid,
                    ProductName = item.Product.Name,
                    CategoryAtSale = item.Product.Category,
                    UnitAtSale = item.Product.UnitType.ToString(),
                    WarehouseGuid = item.WarehouseGuid,
                    WarehouseName = item.WarehouseName,
                    Quantity = item.Quantity,
                    PriceAtSale = 0.0,
                    CostAtSale = item.Product.CostPrice,
                    CostCurrency = item.Product.CostCurrency
                });
            }

            _db.InsertSale(sale);
            _checkoutGuid = Guid.NewGuid().ToString();

            IsBrakModalOpen = false;
            ClearCart();
            RefreshProducts();

            StatusMessage = $"⚠️ {itemsCount} xil brak tovar hisobdan chiqarildi (Zarar: -{totalCost:N0} so'm)";
            ShowToast("⚠️ Brak tovarlar chiqarildi!", $"Jami zarar: -{totalCost:N0} so'm (0 so'mga hisobdan chiqarildi)");
        }

        public void ShowToast(string title, string message)
        {
            ToastTitle = title;
            ToastMessage = message;
            IsToastVisible = true;

            _toastTimer?.Stop();
            _toastTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3.5)
            };
            _toastTimer.Tick += (s, e) =>
            {
                _toastTimer.Stop();
                IsToastVisible = false;
            };
            _toastTimer.Start();
        }

        private string _checkoutGuid = Guid.NewGuid().ToString();

        public Sale BuildCurrentCartSale()
        {
            double cash = 0;
            double card = 0;

            if (SelectedPaymentType == 0)
            {
                cash = TotalAmount;
                card = 0;
            }
            else if (SelectedPaymentType == 1)
            {
                cash = 0;
                card = TotalAmount;
            }
            else if (SelectedPaymentType == 2)
            {
                var cleanCash = (CashAmountInput ?? "").Replace(" ", "").Replace("\u00A0", "").Replace(',', '.');
                var cleanCard = (CardAmountInput ?? "").Replace(" ", "").Replace("\u00A0", "").Replace(',', '.');
                double.TryParse(cleanCash, NumberStyles.Any, CultureInfo.InvariantCulture, out cash);
                double.TryParse(cleanCard, NumberStyles.Any, CultureInfo.InvariantCulture, out card);
                cash = SaleAccounting.Money(Math.Clamp(double.IsFinite(cash) ? cash : 0, 0, TotalAmount));
                card = SaleAccounting.Money(TotalAmount - cash);
                if (cash + card != TotalAmount)
                {
                    if (cash <= TotalAmount) card = TotalAmount - cash;
                    else { cash = TotalAmount; card = 0; }
                }
            }

            else if (SelectedPaymentType == 3)
            {
                double.TryParse(CleanNumber(DebtCashAdvanceInput), NumberStyles.Any, CultureInfo.InvariantCulture, out cash);
                double.TryParse(CleanNumber(DebtCardAdvanceInput), NumberStyles.Any, CultureInfo.InvariantCulture, out card);
                cash = SaleAccounting.Money(Math.Clamp(double.IsFinite(cash) ? cash : 0, 0, TotalAmount));
                card = SaleAccounting.Money(Math.Clamp(double.IsFinite(card) ? card : 0, 0, TotalAmount - cash));
            }

            var saleRate = _currencyService.GetCachedUsdRate();
            var taxRate = CurrentCardTaxRate;
            var taxAmount = SaleAccounting.Money(card * (taxRate / 100.0));

            var sale = new Sale
            {
                Id = _db.GetTotalSalesCount() + 1,
                TotalAmount = TotalAmount,
                Guid = _checkoutGuid,
                TotalCost = SaleAccounting.Money(CartItems.Sum(i => i.Quantity * i.Product.CostPrice * (i.Product.CostCurrency == "USD" ? saleRate : 1))),
                UsdRate = saleRate,
                PaymentType = SelectedPaymentType switch
                {
                    1 => PaymentType.CARD,
                    2 => PaymentType.SPLIT,
                    3 => PaymentType.DEBT,
                    _ => PaymentType.CASH
                },
                CashAmount = cash,
                CardAmount = card,
                TaxAmount = taxAmount,
                TaxRate = taxRate,
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                UserId = 1L
            };

            foreach (var item in CartItems)
            {
                sale.Items.Add(new SaleItem
                {
                    ProductId = item.Product.Id,
                    ProductGuid = item.Product.Guid,
                    ProductName = item.Product.Name,
                    CategoryAtSale = item.Product.Category,
                    UnitAtSale = item.Product.UnitType.ToString(),
                    WarehouseGuid = item.WarehouseGuid,
                    WarehouseName = item.WarehouseName,
                    Quantity = item.Quantity,
                    PriceAtSale = item.PriceAtSale,
                    CostAtSale = item.Product.CostPrice,
                    CostCurrency = item.Product.CostCurrency
                });
            }

            return sale;
        }

        private void PreviewCurrentCartReceipt()
        {
            // Jonli ko'rinish to'lov modalining o'ng tomonida avtomatik chiqadi
        }

        public void DismissToast()
        {
            _toastTimer?.Stop();
            IsToastVisible = false;
        }

        public void RefreshCategories()
        {
            Categories.Clear();
            foreach (var c in _productService.GetCategories())
            {
                Categories.Add(c);
            }
        }

        public void RefreshProducts()
        {
            var whMap = _db.GetProductWarehouseNamesMap();
            var defWh = _db.GetPrimaryWarehouse()?.Name ?? "Do'kondagi ombor";

            if (string.IsNullOrWhiteSpace(SearchQuery))
            {
                // Bo'sh qidiruv: eng ko'p sotiladigan tovarlarni ko'rsat
                IsShowingTopSellers = true;
                TopSellingProducts.Clear();
                foreach (var p in _db.GetTopSellingProducts(50))
                {
                    p.WarehouseName = whMap.TryGetValue(p.Guid, out var whn) ? whn : defWh;
                    TopSellingProducts.Add(p);
                }
                FilteredProducts.Clear();
                return;
            }

            // Qidiruv rejimi
            IsShowingTopSellers = false;
            var list = _productService.GetProductsByCategory(SelectedCategory);
            var q = ProductService.NormalizeProductName(SearchQuery);
            var qEn = ProductService.NormalizeProductName(KeyboardLayoutHelper.ConvertRuToEn(SearchQuery));
            list = list.Where(p =>
                ProductService.NormalizeProductName(p.Name).Contains(q) ||
                (qEn != q && ProductService.NormalizeProductName(p.Name).Contains(qEn)) ||
                (p.Barcode != null && (p.Barcode.Contains(SearchQuery) || (qEn != q && p.Barcode.Contains(qEn))))
            ).ToList();

            FilteredProducts.Clear();
            foreach (var p in list)
            {
                p.WarehouseName = whMap.TryGetValue(p.Guid, out var whn) ? whn : defWh;
                FilteredProducts.Add(p);
            }
        }

        private void NotifyTotals()
        {
            OnPropertyChanged(nameof(TotalAmount));
            OnPropertyChanged(nameof(TotalCost));
            OnPropertyChanged(nameof(TotalItemsCount));
            OnPropertyChanged(nameof(CompleteSaleButtonText));
            OnPropertyChanged(nameof(IsAllPrice2Active));
        }
    }
}
