using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using PosElectro.Desktop.Data;
using PosElectro.Desktop.Debt;

namespace PosElectro.Desktop.ViewModels;

public class DebtsViewModel : ViewModelBase
{
    private readonly DebtService _debtService;
    private readonly DatabaseContext _db;

    public event Action<DebtCustomerItemDto>? RequestOpenCashierForCustomer;

    // --- KPI SUMMARY ---
    private string _totalActiveDebtText = "0 so'm";
    public string TotalActiveDebtText { get => _totalActiveDebtText; set => SetProperty(ref _totalActiveDebtText, value); }

    private string _debtorsCountText = "0 ta";
    public string DebtorsCountText { get => _debtorsCountText; set => SetProperty(ref _debtorsCountText, value); }

    private string _overdueDebtText = "0 so'm";
    public string OverdueDebtText { get => _overdueDebtText; set => SetProperty(ref _overdueDebtText, value); }

    private string _overdueDebtorsCountText = "0 ta";
    public string OverdueDebtorsCountText { get => _overdueDebtorsCountText; set => SetProperty(ref _overdueDebtorsCountText, value); }

    private string _totalCreditText = "0 so'm";
    public string TotalCreditText { get => _totalCreditText; set => SetProperty(ref _totalCreditText, value); }

    // --- SEARCH VA FILTERS ---
    private string _searchQuery = string.Empty;
    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                RefreshCustomers();
            }
        }
    }

    private DebtFilter _selectedFilter = DebtFilter.ActiveDebt;
    public DebtFilter SelectedFilter
    {
        get => _selectedFilter;
        set
        {
            if (SetProperty(ref _selectedFilter, value))
            {
                OnPropertyChanged(nameof(IsFilterAll));
                OnPropertyChanged(nameof(IsFilterActiveDebt));
                OnPropertyChanged(nameof(IsFilterOverdue));
                OnPropertyChanged(nameof(IsFilterSettled));
                OnPropertyChanged(nameof(IsFilterCredit));
                RefreshCustomers();
            }
        }
    }

    public bool IsFilterAll => SelectedFilter == DebtFilter.All;
    public bool IsFilterActiveDebt => SelectedFilter == DebtFilter.ActiveDebt;
    public bool IsFilterOverdue => SelectedFilter == DebtFilter.Overdue;
    public bool IsFilterSettled => SelectedFilter == DebtFilter.Settled;
    public bool IsFilterCredit => SelectedFilter == DebtFilter.Credit;

    // --- CUSTOMER LIST ---
    public ObservableCollection<DebtCustomerItemDto> Customers { get; } = new();

    private DebtCustomerItemDto? _selectedCustomer;
    public DebtCustomerItemDto? SelectedCustomer
    {
        get => _selectedCustomer;
        set
        {
            if (SetProperty(ref _selectedCustomer, value))
            {
                OnPropertyChanged(nameof(IsCustomerSelected));
                OnPropertyChanged(nameof(HasCredit));
                OnPropertyChanged(nameof(HasActiveDebt));
                OnPropertyChanged(nameof(CanTransferCredit));
                LoadSelectedCustomerDetails();
            }
        }
    }

    public bool IsCustomerSelected => SelectedCustomer != null;
    public bool HasCredit => SelectedCustomer != null && SelectedCustomer.BalanceMinor < 0;
    public bool HasActiveDebt => SelectedCustomer != null && SelectedCustomer.BalanceMinor > 0;
    public bool CanTransferCredit => HasCredit && SelectedCustomerAccounts.Any(a => a.BalanceMinor > 0);

    // --- CUSTOMER DETAILS (RIGHT PANEL) ---
    private DebtCustomerDetailDto? _selectedCustomerDetails;
    public DebtCustomerDetailDto? SelectedCustomerDetails
    {
        get => _selectedCustomerDetails;
        set => SetProperty(ref _selectedCustomerDetails, value);
    }

    public ObservableCollection<DebtAccountItemDto> SelectedCustomerAccounts { get; } = new();
    public ObservableCollection<DebtEventItemDto> SelectedCustomerEvents { get; } = new();

    private int _selectedDetailTab = 0; // 0 = Accounts/Cheklar, 1 = Events/Tarix
    public int SelectedDetailTab
    {
        get => _selectedDetailTab;
        set
        {
            if (SetProperty(ref _selectedDetailTab, value))
            {
                OnPropertyChanged(nameof(IsAccountsTabSelected));
                OnPropertyChanged(nameof(IsHistoryTabSelected));
            }
        }
    }

    public bool IsAccountsTabSelected => SelectedDetailTab == 0;
    public bool IsHistoryTabSelected => SelectedDetailTab == 1;

    // --- MODAL: CUSTOMER CREATE / EDIT ---
    private bool _isCustomerModalOpen;
    public bool IsCustomerModalOpen { get => _isCustomerModalOpen; set => SetProperty(ref _isCustomerModalOpen, value); }

    private bool _isEditingCustomer;
    public bool IsEditingCustomer { get => _isEditingCustomer; set => SetProperty(ref _isEditingCustomer, value); }

    public string CustomerModalTitle => IsEditingCustomer ? "Mijoz ma'lumotlarini tahrirlash" : "Yangi mijoz qo'shish";

    private string _modalCustomerName = string.Empty;
    public string ModalCustomerName { get => _modalCustomerName; set => SetProperty(ref _modalCustomerName, value); }

    private string _modalCustomerPhone = string.Empty;
    public string ModalCustomerPhone { get => _modalCustomerPhone; set => SetProperty(ref _modalCustomerPhone, value); }

    private string _modalCustomerNote = string.Empty;
    public string ModalCustomerNote { get => _modalCustomerNote; set => SetProperty(ref _modalCustomerNote, value); }

    private string _modalCustomerErrorMessage = string.Empty;
    public string ModalCustomerErrorMessage { get => _modalCustomerErrorMessage; set => SetProperty(ref _modalCustomerErrorMessage, value); }

    // --- MODAL: PAYMENT COLLECTION ---
    private bool _isPaymentModalOpen;
    public bool IsPaymentModalOpen { get => _isPaymentModalOpen; set => SetProperty(ref _isPaymentModalOpen, value); }

    private int _paymentType = 0; // 0 = Cash, 1 = Card, 2 = Split
    public int PaymentType
    {
        get => _paymentType;
        set
        {
            if (SetProperty(ref _paymentType, value))
            {
                OnPropertyChanged(nameof(IsPaymentCash));
                OnPropertyChanged(nameof(IsPaymentCard));
                OnPropertyChanged(nameof(IsPaymentSplit));
                RecalculatePaymentInputs();
            }
        }
    }

    public bool IsPaymentCash => PaymentType == 0;
    public bool IsPaymentCard => PaymentType == 1;
    public bool IsPaymentSplit => PaymentType == 2;

    private string _paymentAmountInput = string.Empty;
    public string PaymentAmountInput
    {
        get => _paymentAmountInput;
        set
        {
            if (SetProperty(ref _paymentAmountInput, value))
            {
                if (PaymentType != 2)
                {
                    RecalculatePaymentInputs();
                }
                UpdateAllocationPreview();
            }
        }
    }

    private string _paymentCashInput = string.Empty;
    public string PaymentCashInput
    {
        get => _paymentCashInput;
        set
        {
            if (SetProperty(ref _paymentCashInput, value))
            {
                if (PaymentType == 2)
                {
                    UpdateSplitTotalFromParts();
                }
            }
        }
    }

    private string _paymentCardInput = string.Empty;
    public string PaymentCardInput
    {
        get => _paymentCardInput;
        set
        {
            if (SetProperty(ref _paymentCardInput, value))
            {
                if (PaymentType == 2)
                {
                    UpdateSplitTotalFromParts();
                }
            }
        }
    }

    public ObservableCollection<DebtAccountItemDto> AvailableAccountsForPayment { get; } = new();

    private DebtAccountItemDto? _selectedTargetAccount;
    public DebtAccountItemDto? SelectedTargetAccount
    {
        get => _selectedTargetAccount;
        set
        {
            if (SetProperty(ref _selectedTargetAccount, value))
            {
                if (value != null && value.BalanceMinor > 0)
                {
                    PaymentAmountInput = (value.BalanceMinor / 100.0).ToString("0", CultureInfo.InvariantCulture);
                }
                UpdateAllocationPreview();
            }
        }
    }

    public ObservableCollection<DebtAllocationLinePreview> AllocationPreviewLines { get; } = new();

    private string _previewRemainingDebtText = string.Empty;
    public string PreviewRemainingDebtText { get => _previewRemainingDebtText; set => SetProperty(ref _previewRemainingDebtText, value); }

    private string _paymentErrorMessage = string.Empty;
    public string PaymentErrorMessage { get => _paymentErrorMessage; set => SetProperty(ref _paymentErrorMessage, value); }

    private bool _isSubmittingPayment;
    public bool IsSubmittingPayment { get => _isSubmittingPayment; set => SetProperty(ref _isSubmittingPayment, value); }

    // --- MODAL: CREDIT REFUND ---
    private bool _isRefundCreditModalOpen;
    public bool IsRefundCreditModalOpen { get => _isRefundCreditModalOpen; set => SetProperty(ref _isRefundCreditModalOpen, value); }

    private string _refundCreditCashInput = "0";
    public string RefundCreditCashInput { get => _refundCreditCashInput; set => SetProperty(ref _refundCreditCashInput, value); }

    private string _refundCreditCardInput = "0";
    public string RefundCreditCardInput { get => _refundCreditCardInput; set => SetProperty(ref _refundCreditCardInput, value); }

    private string _refundCreditMaxAmountText = string.Empty;
    public string RefundCreditMaxAmountText { get => _refundCreditMaxAmountText; set => SetProperty(ref _refundCreditMaxAmountText, value); }

    private string _refundCreditErrorMessage = string.Empty;
    public string RefundCreditErrorMessage { get => _refundCreditErrorMessage; set => SetProperty(ref _refundCreditErrorMessage, value); }

    private bool _isSubmittingRefundCredit;
    public bool IsSubmittingRefundCredit { get => _isSubmittingRefundCredit; set => SetProperty(ref _isSubmittingRefundCredit, value); }

    // --- COMMANDS ---
    public ICommand SetFilterCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand SelectDetailTabCommand { get; }

    public ICommand OpenCreateCustomerModalCommand { get; }
    public ICommand OpenEditCustomerModalCommand { get; }
    public ICommand CloseCustomerModalCommand { get; }
    public ICommand SaveCustomerModalCommand { get; }
    public ICommand ToggleArchiveCustomerCommand { get; }

    public ICommand OpenPaymentModalCommand { get; }
    public ICommand ClosePaymentModalCommand { get; }
    public ICommand SelectPaymentTypeCommand { get; }
    public ICommand ConfirmPaymentCommand { get; }

    public ICommand ReversePaymentCommand { get; }
    public ICommand OpenRefundCreditModalCommand { get; }
    public ICommand CloseRefundCreditModalCommand { get; }
    public ICommand ConfirmRefundCreditCommand { get; }
    public ICommand TransferCreditCommand { get; }

    public ICommand GoToCashierForCustomerCommand { get; }
    public ICommand PrintStatementCommand { get; }

    public DebtsViewModel(DatabaseContext db, DebtService? debtService = null)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _debtService = debtService ?? new DebtService(_db);

        SetFilterCommand = new RelayCommand<string>(f =>
        {
            if (Enum.TryParse<DebtFilter>(f, out var filter))
            {
                SelectedFilter = filter;
            }
        });

        RefreshCommand = new RelayCommand(RefreshAll);

        SelectDetailTabCommand = new RelayCommand<string>(tab =>
        {
            if (int.TryParse(tab, out var t))
            {
                SelectedDetailTab = t;
            }
        });

        OpenCreateCustomerModalCommand = new RelayCommand(OpenCreateCustomerModal);
        OpenEditCustomerModalCommand = new RelayCommand(OpenEditCustomerModal);
        CloseCustomerModalCommand = new RelayCommand(() => IsCustomerModalOpen = false);
        SaveCustomerModalCommand = new RelayCommand(SaveCustomerModal);
        ToggleArchiveCustomerCommand = new RelayCommand(ToggleArchiveCustomer);

        OpenPaymentModalCommand = new RelayCommand(OpenPaymentModal);
        ClosePaymentModalCommand = new RelayCommand(() => IsPaymentModalOpen = false);
        SelectPaymentTypeCommand = new RelayCommand<string>(p =>
        {
            if (int.TryParse(p, out var pt))
            {
                PaymentType = pt;
            }
        });
        ConfirmPaymentCommand = new RelayCommand(ConfirmPayment);

        ReversePaymentCommand = new RelayCommand<DebtEventItemDto>(ReversePayment);
        OpenRefundCreditModalCommand = new RelayCommand(OpenRefundCreditModal);
        CloseRefundCreditModalCommand = new RelayCommand(() => IsRefundCreditModalOpen = false);
        ConfirmRefundCreditCommand = new RelayCommand(ConfirmRefundCredit);
        TransferCreditCommand = new RelayCommand(TransferCredit);

        GoToCashierForCustomerCommand = new RelayCommand(() =>
        {
            if (SelectedCustomer != null && !SelectedCustomer.Archived)
            {
                RequestOpenCashierForCustomer?.Invoke(SelectedCustomer);
            }
            else if (SelectedCustomer != null && SelectedCustomer.Archived)
            {
                MessageBox.Show("Arxivlangan mijozga yangi nasiya savdosi ochib bo'lmaydi. Avval mijozni arxivdan chiqaring.", "Mijoz arxivlangan", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        });

        PrintStatementCommand = new RelayCommand(PrintStatement);

        RefreshAll();
    }

    private void PrintStatement()
    {
        if (SelectedCustomer == null || SelectedCustomerDetails == null) return;
        var printer = new PosElectro.Desktop.Services.PrinterService(_db);
        var res = printer.PrintCustomerStatement(SelectedCustomerDetails);

        if (res.Success)
        {
            MessageBox.Show("Mijoz hisob ko'chirmasi printerga yuborildi.", "Ko'chirma chop etish", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            var statementText = DebtReceiptFormatter.BuildCustomerStatementText(SelectedCustomerDetails);
            try { Clipboard.SetText(statementText); } catch { }
            MessageBox.Show($"Printerga ulanib bo'lmadi ({res.ErrorMessage}).\n\nHisob ko'chirmasi matni xotiraga (Clipboard) nusxalandi:\n\n{statementText}", "Ko'chirma", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    public void RefreshAll()
    {
        UpdateSummary();
        RefreshCustomers();
    }

    public void RefreshSummary() => UpdateSummary();

    private void UpdateSummary()
    {
        var summary = _debtService.GetSummary();
        TotalActiveDebtText = $"{summary.TotalActiveDebtMinor / 100.0:N0} so'm";
        DebtorsCountText = $"{summary.DebtorsCount} nafar";
        OverdueDebtText = $"{summary.OverdueDebtMinor / 100.0:N0} so'm";
        OverdueDebtorsCountText = $"{summary.OverdueDebtorsCount} nafar";
        TotalCreditText = $"{summary.TotalCreditMinor / 100.0:N0} so'm";
    }

    public void RefreshCustomers()
    {
        var currentSelectedGuid = SelectedCustomer?.Guid;
        var list = _debtService.GetCustomerList(SearchQuery, SelectedFilter);

        Customers.Clear();
        foreach (var c in list)
        {
            Customers.Add(c);
        }

        if (currentSelectedGuid != null)
        {
            SelectedCustomer = Customers.FirstOrDefault(c => c.Guid == currentSelectedGuid);
        }

        if (SelectedCustomer == null && Customers.Count > 0)
        {
            SelectedCustomer = Customers[0];
        }
        else if (Customers.Count == 0)
        {
            SelectedCustomer = null;
            SelectedCustomerDetails = null;
            SelectedCustomerAccounts.Clear();
            SelectedCustomerEvents.Clear();
        }
    }

    private void LoadSelectedCustomerDetails()
    {
        if (SelectedCustomer == null)
        {
            SelectedCustomerDetails = null;
            SelectedCustomerAccounts.Clear();
            SelectedCustomerEvents.Clear();
            return;
        }

        var details = _debtService.GetCustomerDetails(SelectedCustomer.Guid);
        SelectedCustomerDetails = details;

        SelectedCustomerAccounts.Clear();
        SelectedCustomerEvents.Clear();

        if (details != null)
        {
            foreach (var a in details.Accounts)
            {
                SelectedCustomerAccounts.Add(a);
            }
            foreach (var e in details.Events)
            {
                SelectedCustomerEvents.Add(e);
            }
        }

        OnPropertyChanged(nameof(HasCredit));
        OnPropertyChanged(nameof(HasActiveDebt));
        OnPropertyChanged(nameof(CanTransferCredit));
    }

    // --- CUSTOMER MODAL LOGIC ---
    private void OpenCreateCustomerModal()
    {
        IsEditingCustomer = false;
        ModalCustomerName = string.Empty;
        ModalCustomerPhone = string.Empty;
        ModalCustomerNote = string.Empty;
        ModalCustomerErrorMessage = string.Empty;
        OnPropertyChanged(nameof(CustomerModalTitle));
        IsCustomerModalOpen = true;
    }

    private void OpenEditCustomerModal()
    {
        if (SelectedCustomer == null) return;
        IsEditingCustomer = true;
        ModalCustomerName = SelectedCustomer.Name;
        ModalCustomerPhone = SelectedCustomer.Phone;
        ModalCustomerNote = SelectedCustomer.Note;
        ModalCustomerErrorMessage = string.Empty;
        OnPropertyChanged(nameof(CustomerModalTitle));
        IsCustomerModalOpen = true;
    }

    private void SaveCustomerModal()
    {
        var name = ModalCustomerName?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            ModalCustomerErrorMessage = "Mijoz ismini kiritish majburiy!";
            return;
        }

        if (_debtService.IsCustomerNameExists(name, IsEditingCustomer ? SelectedCustomer?.Guid : null))
        {
            ModalCustomerErrorMessage = "⚠️ Ushbu ismli mijoz allaqachon mavjud! Iltimos, boshqa ism kiriting.";
            return;
        }

        try
        {
            if (IsEditingCustomer)
            {
                if (SelectedCustomer == null) return;
                _debtService.UpdateCustomer(SelectedCustomer.Guid, name, ModalCustomerPhone, ModalCustomerNote, SelectedCustomer.Revision);
            }
            else
            {
                var newGuid = _debtService.CreateCustomer(name, ModalCustomerPhone, ModalCustomerNote);
                SearchQuery = string.Empty;
            }

            IsCustomerModalOpen = false;
            RefreshAll();
        }
        catch (Exception ex)
        {
            ModalCustomerErrorMessage = $"Xatolik: {ex.Message}";
        }
    }

    private void ToggleArchiveCustomer()
    {
        if (SelectedCustomer == null) return;
        var actionText = SelectedCustomer.Archived ? "faollashtirishni" : "arxivlashni";
        var res = MessageBox.Show(
            $"Haqiqatan ham '{SelectedCustomer.Name}' mijozini {actionText} istaysizmi?\n\nEslatma: Arxivlangan mijozning mavjud qarzlari saqlanib qoladi va ularga to'lov qabul qilish mumkin, biroq yangi nasiya ochib bo'lmaydi.",
            "Tasdiqlash",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (res == MessageBoxResult.Yes)
        {
            try
            {
                _debtService.ArchiveCustomer(SelectedCustomer.Guid, !SelectedCustomer.Archived);
                RefreshAll();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Xatolik: {ex.Message}", "Xatolik", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    // --- PAYMENT COLLECTION MODAL LOGIC ---
    private void OpenPaymentModal()
    {
        if (SelectedCustomer == null) return;
        if (SelectedCustomer.BalanceMinor <= 0)
        {
            MessageBox.Show("Ushbu mijozning qarzi mavjud emas.", "Qarz yo'q", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        PaymentErrorMessage = string.Empty;
        PaymentType = 0; // Naqd
        SelectedTargetAccount = null;

        AvailableAccountsForPayment.Clear();
        if (SelectedCustomerDetails != null)
        {
            foreach (var a in SelectedCustomerDetails.Accounts.Where(x => x.BalanceMinor > 0))
            {
                AvailableAccountsForPayment.Add(a);
            }
        }

        PaymentAmountInput = (SelectedCustomer.BalanceMinor / 100.0).ToString("0", CultureInfo.InvariantCulture);
        RecalculatePaymentInputs();
        UpdateAllocationPreview();

        IsPaymentModalOpen = true;
    }

    private void RecalculatePaymentInputs()
    {
        var clean = CleanNumber(PaymentAmountInput);
        if (double.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out var total) && total > 0)
        {
            if (PaymentType == 0) // Naqd
            {
                _paymentCashInput = total.ToString("0", CultureInfo.InvariantCulture);
                _paymentCardInput = "0";
            }
            else if (PaymentType == 1) // Karta
            {
                _paymentCashInput = "0";
                _paymentCardInput = total.ToString("0", CultureInfo.InvariantCulture);
            }
            else // Split
            {
                var half = Math.Round(total / 2.0);
                _paymentCashInput = half.ToString("0", CultureInfo.InvariantCulture);
                _paymentCardInput = (total - half).ToString("0", CultureInfo.InvariantCulture);
            }
        }
        else
        {
            _paymentCashInput = "0";
            _paymentCardInput = "0";
        }
        OnPropertyChanged(nameof(PaymentCashInput));
        OnPropertyChanged(nameof(PaymentCardInput));
    }

    private void UpdateSplitTotalFromParts()
    {
        double.TryParse(CleanNumber(PaymentCashInput), NumberStyles.Any, CultureInfo.InvariantCulture, out var cash);
        double.TryParse(CleanNumber(PaymentCardInput), NumberStyles.Any, CultureInfo.InvariantCulture, out var card);
        var total = Math.Max(0, cash) + Math.Max(0, card);
        _paymentAmountInput = total.ToString("0", CultureInfo.InvariantCulture);
        OnPropertyChanged(nameof(PaymentAmountInput));
        UpdateAllocationPreview();
    }

    private static string CleanNumber(string? s)
    {
        return (s ?? "").Replace(" ", "").Replace("\u00A0", "").Replace(',', '.').Trim();
    }

    private void UpdateAllocationPreview()
    {
        PaymentErrorMessage = string.Empty;
        AllocationPreviewLines.Clear();
        PreviewRemainingDebtText = string.Empty;

        if (SelectedCustomer == null) return;

        var clean = CleanNumber(PaymentAmountInput);
        if (!double.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out var totalAmount) || totalAmount <= 0)
        {
            return;
        }

        long paymentMinor = checked((long)Math.Round(totalAmount * 100));
        try
        {
            var preview = _debtService.PreviewPayment(
                SelectedCustomer.Guid,
                paymentMinor,
                SelectedTargetAccount?.AccountGuid);

            foreach (var line in preview.Lines)
            {
                AllocationPreviewLines.Add(line);
            }

            PreviewRemainingDebtText = $"Qoladigan qarz: {preview.RemainingBalanceMinor / 100.0:N0} so'm";
        }
        catch (Exception ex)
        {
            PaymentErrorMessage = ex.Message;
        }
    }

    private void ConfirmPayment()
    {
        if (SelectedCustomer == null) return;
        if (IsSubmittingPayment) return;

        double.TryParse(CleanNumber(PaymentCashInput), NumberStyles.Any, CultureInfo.InvariantCulture, out var cash);
        double.TryParse(CleanNumber(PaymentCardInput), NumberStyles.Any, CultureInfo.InvariantCulture, out var card);

        if (cash < 0 || card < 0 || (cash + card) <= 0)
        {
            PaymentErrorMessage = "Iltimos, to'lov summasini to'g'ri kiriting!";
            return;
        }

        long cashMinor = checked((long)Math.Round(cash * 100));
        long cardMinor = checked((long)Math.Round(card * 100));
        long totalMinor = cashMinor + cardMinor;

        if (totalMinor > SelectedCustomer.BalanceMinor)
        {
            PaymentErrorMessage = $"To'lov summasi ({totalMinor / 100.0:N0} so'm) mavjud qarzdan ko'p bo'lishi mumkin emas!";
            return;
        }

        IsSubmittingPayment = true;
        PaymentErrorMessage = string.Empty;

        try
        {
            var durableRequestGuid = Guid.NewGuid().ToString("D");
            _debtService.RecordPayment(
                SelectedCustomer.Guid,
                cashMinor,
                cardMinor,
                SelectedTargetAccount?.AccountGuid,
                durableRequestGuid);

            IsPaymentModalOpen = false;
            RefreshAll();

            MessageBox.Show(
                $"✅ '{SelectedCustomer.Name}' uchun {totalMinor / 100.0:N0} so'm qarz to'lovi muvaffaqiyatli qabul qilindi!",
                "To'lov qabul qilindi",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            PaymentErrorMessage = $"Xatolik: {ex.Message}";
        }
        finally
        {
            IsSubmittingPayment = false;
        }
    }

    private void ReversePayment(DebtEventItemDto? ev)
    {
        if (ev == null || !ev.CanReverse) return;

        var result = MessageBox.Show(
            $"Haqiqatan ham {ev.OccurredAtDisplay} dagi {ev.AmountDisplay} lik qarz to'lovini bekor qilmoqchimisiz?\n\nBu amal qarz qoldig'ini tiklaydi va bekor qilish yozuvini kiritadi.",
            "To'lovni bekor qilish",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            _debtService.ReversePayment(ev.EventGuid, "Kassir tomonidan bekor qilindi", ev.FeeMinor);
            RefreshAll();
            MessageBox.Show("✅ To'lov muvaffaqiyatli bekor qilindi!", "Bekor qilindi", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Xatolik yuz berdi: {ex.Message}", "Xatolik", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenRefundCreditModal()
    {
        if (SelectedCustomer == null || SelectedCustomer.BalanceMinor >= 0) return;

        var creditAccount = SelectedCustomerAccounts.FirstOrDefault(a => a.BalanceMinor < 0);
        if (creditAccount == null)
        {
            MessageBox.Show("Haqdorlik mavjud bo'lgan alohida chek/hisob topilmadi.", "Ma'lumot", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var maxRefund = Math.Abs(creditAccount.BalanceMinor) / 100.0;
        RefundCreditMaxAmountText = $"Maksimal qaytarish: {maxRefund:N0} so'm ({creditAccount.ReceiptNumber})";
        RefundCreditCashInput = maxRefund.ToString("0", CultureInfo.InvariantCulture);
        RefundCreditCardInput = "0";
        RefundCreditErrorMessage = string.Empty;
        IsRefundCreditModalOpen = true;
    }

    private void ConfirmRefundCredit()
    {
        if (SelectedCustomer == null) return;
        if (IsSubmittingRefundCredit) return;

        var creditAccount = SelectedCustomerAccounts.FirstOrDefault(a => a.BalanceMinor < 0);
        if (creditAccount == null)
        {
            RefundCreditErrorMessage = "Haqdorlik mavjud bo'lgan chek topilmadi.";
            return;
        }

        double.TryParse(CleanNumber(RefundCreditCashInput), NumberStyles.Any, CultureInfo.InvariantCulture, out var cash);
        double.TryParse(CleanNumber(RefundCreditCardInput), NumberStyles.Any, CultureInfo.InvariantCulture, out var card);

        if (cash < 0 || card < 0 || (cash + card) <= 0)
        {
            RefundCreditErrorMessage = "Iltimos, qaytariladigan summani to'g'ri kiriting!";
            return;
        }

        long cashMinor = checked((long)Math.Round(cash * 100));
        long cardMinor = checked((long)Math.Round(card * 100));
        long totalMinor = cashMinor + cardMinor;
        long maxAvailableMinor = Math.Abs(creditAccount.BalanceMinor);

        if (totalMinor > maxAvailableMinor)
        {
            RefundCreditErrorMessage = $"Qaytarish summasi mavjud haqdorlikdan ({maxAvailableMinor / 100.0:N0} so'm) ko'p bo'lishi mumkin emas!";
            return;
        }

        IsSubmittingRefundCredit = true;
        RefundCreditErrorMessage = string.Empty;

        try
        {
            _debtService.RefundCredit(
                SelectedCustomer.Guid,
                creditAccount.AccountGuid,
                cashMinor,
                cardMinor,
                "Kassadan ortiqcha to'lov qaytarildi");

            IsRefundCreditModalOpen = false;
            RefreshAll();

            MessageBox.Show(
                $"✅ '{SelectedCustomer.Name}' uchun {totalMinor / 100.0:N0} so'm ortiqcha to'lov muvaffaqiyatli qaytarildi!",
                "Pul qaytarildi",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            RefundCreditErrorMessage = $"Xatolik: {ex.Message}";
        }
        finally
        {
            IsSubmittingRefundCredit = false;
        }
    }

    private void TransferCredit()
    {
        if (SelectedCustomer == null) return;

        var source = SelectedCustomerAccounts.FirstOrDefault(a => a.BalanceMinor < 0);
        var target = SelectedCustomerAccounts.FirstOrDefault(a => a.BalanceMinor > 0);

        if (source == null || target == null)
        {
            MessageBox.Show("O'tkazish uchun bir vaqtning o'zida ham haqdorlik (kredit) va ham qarzdorlik bo'lgan cheklar mavjud bo'lishi kerak.", "O'tkazish mumkin emas", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        long transferMinor = Math.Min(Math.Abs(source.BalanceMinor), target.BalanceMinor);

        var res = MessageBox.Show(
            $"'{source.ReceiptNumber}' dagi {transferMinor / 100.0:N0} so'm ortiqcha to'lov (haqdorlik) '{target.ReceiptNumber}' dagi qarzni yopishga o'tkazilsinmi?",
            "Haqdorlikni qarzga o'tkazish",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (res != MessageBoxResult.Yes) return;

        try
        {
            _debtService.TransferCredit(
                SelectedCustomer.Guid,
                source.AccountGuid,
                target.AccountGuid,
                transferMinor,
                "Haqdorlik boshqa chekka o'tkazildi");

            RefreshAll();

            MessageBox.Show(
                $"✅ {transferMinor / 100.0:N0} so'm muvaffaqiyatli o'tkazildi va qarz kamaytirildi!",
                "O'tkazildi",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Xatolik yuz berdi: {ex.Message}", "Xatolik", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
