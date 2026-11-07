using System;
using System.Printing;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PosElectro.Desktop.ViewModels;

namespace PosElectro.Desktop.Services
{
    /// <summary>
    /// Mahsulot shtrix-kod stikerlarini fonda (asinxron STA oqimida) chop etish,
    /// progress monitoring va sticky vidjet boshqaruvi xizmati.
    /// </summary>
    public class LabelPrintService : ViewModelBase
    {
        private static LabelPrintService? _instance;
        public static LabelPrintService Instance => _instance ??= new LabelPrintService();

        private CancellationTokenSource? _cts;

        private bool _isPrinting;
        public bool IsPrinting
        {
            get => _isPrinting;
            set
            {
                if (SetProperty(ref _isPrinting, value))
                {
                    OnPropertyChanged(nameof(StatusText));
                    OnPropertyChanged(nameof(CanCancel));
                }
            }
        }

        private bool _isModalOpen;
        public bool IsModalOpen
        {
            get => _isModalOpen;
            set => SetProperty(ref _isModalOpen, value);
        }

        private bool _isStickyVisible;
        public bool IsStickyVisible
        {
            get => _isStickyVisible;
            set => SetProperty(ref _isStickyVisible, value);
        }

        private bool _isCompleted;
        public bool IsCompleted
        {
            get => _isCompleted;
            set
            {
                if (SetProperty(ref _isCompleted, value))
                {
                    OnPropertyChanged(nameof(StatusText));
                    OnPropertyChanged(nameof(CanCancel));
                }
            }
        }

        private bool _isCancelled;
        public bool IsCancelled
        {
            get => _isCancelled;
            set
            {
                if (SetProperty(ref _isCancelled, value))
                {
                    OnPropertyChanged(nameof(StatusText));
                    OnPropertyChanged(nameof(CanCancel));
                }
            }
        }

        private bool _hasError;
        public bool HasError
        {
            get => _hasError;
            set
            {
                if (SetProperty(ref _hasError, value))
                {
                    OnPropertyChanged(nameof(StatusText));
                }
            }
        }

        private string _errorMessage = string.Empty;
        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        private string _productName = string.Empty;
        public string ProductName
        {
            get => _productName;
            set => SetProperty(ref _productName, value);
        }

        private int _currentCount;
        public int CurrentCount
        {
            get => _currentCount;
            set
            {
                if (SetProperty(ref _currentCount, value))
                {
                    OnPropertyChanged(nameof(ProgressPercentage));
                    OnPropertyChanged(nameof(StatusText));
                    OnPropertyChanged(nameof(CountDisplay));
                }
            }
        }

        private int _totalCount;
        public int TotalCount
        {
            get => _totalCount;
            set
            {
                if (SetProperty(ref _totalCount, value))
                {
                    OnPropertyChanged(nameof(ProgressPercentage));
                    OnPropertyChanged(nameof(StatusText));
                    OnPropertyChanged(nameof(CountDisplay));
                }
            }
        }

        public double ProgressPercentage
        {
            get => TotalCount > 0 ? Math.Min(100.0, (double)CurrentCount / TotalCount * 100.0) : 0.0;
            set { }
        }

        public string CountDisplay => $"{CurrentCount} / {TotalCount} dona";

        public bool CanCancel => IsPrinting && !IsCompleted && !IsCancelled;

        public string StatusText
        {
            get
            {
                if (HasError) return $"Xatolik: {ErrorMessage}";
                if (IsCompleted) return $"✅ Tayyor ({TotalCount} dona)";
                if (IsCancelled) return $"⚠️ To'xtatildi ({CurrentCount}/{TotalCount})";
                return $"Chop etilmoqda: {CurrentCount} / {TotalCount} dona";
            }
        }

        public ICommand SendToBackgroundCommand { get; }
        public ICommand CancelPrintCommand { get; }
        public ICommand CloseStickyCommand { get; }
        public ICommand CloseModalCommand { get; }

        public LabelPrintService()
        {
            SendToBackgroundCommand = new RelayCommand(() =>
            {
                IsModalOpen = false;
                IsStickyVisible = true;
            });

            CancelPrintCommand = new RelayCommand(() =>
            {
                CancelPrint();
            });

            CloseStickyCommand = new RelayCommand(() =>
            {
                IsStickyVisible = false;
                if (!IsPrinting)
                {
                    ResetState();
                }
            });

            CloseModalCommand = new RelayCommand(() =>
            {
                if (IsPrinting)
                {
                    // Agar chop etish hali davom etayotgan bo'lsa, yopish o'rniga orqa fonga o'tkazamiz
                    IsModalOpen = false;
                    IsStickyVisible = true;
                }
                else
                {
                    IsModalOpen = false;
                    ResetState();
                }
            });
        }

        public void CancelPrint()
        {
            if (!IsPrinting) return;
            try
            {
                _cts?.Cancel();
            }
            catch { }
            IsCancelled = true;
            IsPrinting = false;
        }

        private void ResetState()
        {
            _isPrinting = false;
            _isCompleted = false;
            _isCancelled = false;
            _hasError = false;
            _errorMessage = string.Empty;
            _currentCount = 0;
            _totalCount = 0;
            OnPropertyChanged(nameof(IsPrinting));
            OnPropertyChanged(nameof(IsCompleted));
            OnPropertyChanged(nameof(IsCancelled));
            OnPropertyChanged(nameof(HasError));
            OnPropertyChanged(nameof(ProgressPercentage));
            OnPropertyChanged(nameof(StatusText));
        }

        /// <summary>
        /// Mahsulot stikerlarini orqa fonda alohida STA oqimida xavfsiz chop etishni boshlaydi.
        /// </summary>
        public bool StartPrintJob(
            string barcode,
            string productName,
            double price,
            int copies,
            bool showName,
            bool showPrice,
            bool showBarcode,
            string? printerName,
            string labelSizeId,
            double offsetMm,
            double nameOffsetMm,
            string nameAlignment,
            int nameLines)
        {
            if (IsPrinting)
            {
                return false;
            }

            if (copies <= 0) copies = 1;

            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            ProductName = productName;
            CurrentCount = 0;
            TotalCount = copies;
            IsCompleted = false;
            IsCancelled = false;
            HasError = false;
            ErrorMessage = string.Empty;
            IsPrinting = true;
            IsModalOpen = true;
            IsStickyVisible = false;

            var thread = new Thread(() =>
            {
                try
                {
                    var printDialog = new PrintDialog();
                    var printServer = new LocalPrintServer();
                    PrintQueue? targetQueue = null;

                    var targetPrinter = printerName;
                    if (string.IsNullOrWhiteSpace(targetPrinter))
                    {
                        var ps = new PrinterService();
                        targetPrinter = ps.FindLabelPrinter();
                    }

                    if (!string.IsNullOrWhiteSpace(targetPrinter))
                    {
                        try
                        {
                            targetQueue = printServer.GetPrintQueue(targetPrinter);
                        }
                        catch
                        {
                            foreach (var q in printServer.GetPrintQueues())
                            {
                                if (q.Name.Equals(targetPrinter, StringComparison.OrdinalIgnoreCase))
                                {
                                    targetQueue = q;
                                    break;
                                }
                            }
                        }
                    }

                    if (targetQueue != null)
                    {
                        printDialog.PrintQueue = targetQueue;
                    }

                    var config = LabelSizeConfig.Get(labelSizeId);

                    try
                    {
                        if (printDialog.PrintTicket != null)
                        {
                            double dipW = config.WidthMm * (96.0 / 25.4);
                            double dipH = config.HeightMm * (96.0 / 25.4);
                            printDialog.PrintTicket.PageMediaSize = new PageMediaSize(dipW, dipH);
                        }
                    }
                    catch { }

                    // DrawingVisual alohida STA oqimida yaratiladi (Thread-safety 100%)
                    var visual = PrinterService.CreateLabelVisual(
                        barcode,
                        productName,
                        price,
                        showName,
                        showPrice,
                        showBarcode,
                        config,
                        offsetMm,
                        nameOffsetMm,
                        nameAlignment,
                        nameLines);

                    for (int i = 0; i < copies; i++)
                    {
                        if (token.IsCancellationRequested)
                        {
                            Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                IsPrinting = false;
                                IsCancelled = true;
                            }));
                            return;
                        }

                        printDialog.PrintVisual(visual, $"Stiker: {productName}");
                        int current = i + 1;

                        Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            CurrentCount = current;
                        }));

                        // Printer buferiga silliq yuborilishi va CPU ni to'liq band qilmasligi uchun qisqa oraliq
                        Thread.Sleep(30);
                    }

                    Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        IsPrinting = false;
                        IsCompleted = true;
                    }));
                }
                catch (Exception ex)
                {
                    Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        IsPrinting = false;
                        HasError = true;
                        ErrorMessage = ex.Message;
                    }));
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();

            return true;
        }
    }
}
