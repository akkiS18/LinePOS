using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using PosElectro.Desktop.Models;
using PosElectro.Desktop.ViewModels;

namespace PosElectro.Desktop.Views
{
    public partial class ReportsView : UserControl
    {
        private ReportsViewModel? _statsVm;

        public ReportsView()
        {
            InitializeComponent();
            DataContextChanged += (_, __) => HookStatsViewModel();
            Loaded += (_, __) => { HookStatsViewModel(); ApplyStatsState(animate: false); };
        }

        // ---- Statistika qismini yig'ish / ochish ----
        private void HookStatsViewModel()
        {
            var vm = DataContext as ReportsViewModel;
            if (ReferenceEquals(vm, _statsVm)) return;
            if (_statsVm != null) _statsVm.PropertyChanged -= StatsVm_PropertyChanged;
            _statsVm = vm;
            if (_statsVm != null) _statsVm.PropertyChanged += StatsVm_PropertyChanged;
            ApplyStatsState(animate: false);
        }

        private void StatsVm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ReportsViewModel.IsStatsCollapsed)) ApplyStatsState(animate: true);
        }

        /// <summary>Saqlangan holatni qo'llaydi. Ekran ochilganda animatsiyasiz, tugma bosilganda animatsiya bilan.</summary>
        private void ApplyStatsState(bool animate)
        {
            if (_statsVm == null) return;
            bool collapsed = _statsVm.IsStatsCollapsed;
            double chevronAngle = collapsed ? -90 : 0;
            double summaryOpacity = collapsed ? 1 : 0;

            if (!animate || !IsLoaded)
            {
                StatsHost.BeginAnimation(HeightProperty, null);
                StatsHost.BeginAnimation(OpacityProperty, null);
                StatsSummary.BeginAnimation(OpacityProperty, null);
                StatsChevronRotate.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, null);
                StatsHost.Height = collapsed ? 0 : double.NaN;
                StatsHost.Opacity = collapsed ? 0 : 1;
                StatsSummary.Opacity = summaryOpacity;
                StatsChevronRotate.Angle = chevronAngle;
                return;
            }

            // To'liq balandlik: ichki panel o'zi xohlagan o'lcham
            double width = StatsHost.ActualWidth > 0 ? StatsHost.ActualWidth : ActualWidth;
            StatsContent.Measure(new Size(width, double.PositiveInfinity));
            double full = StatsContent.DesiredSize.Height;
            double from = StatsHost.ActualHeight;
            double to = collapsed ? 0 : full;

            var duration = TimeSpan.FromMilliseconds(240);
            var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };

            var height = new DoubleAnimation(from, to, duration) { EasingFunction = ease };
            height.Completed += (_, __) =>
            {
                // Animatsiya davomida holat yana o'zgargan bo'lsa, yangi animatsiya o'zi yakunlaydi
                if (_statsVm == null || _statsVm.IsStatsCollapsed != collapsed) return;
                StatsHost.BeginAnimation(HeightProperty, null);
                StatsHost.Height = collapsed ? 0 : double.NaN; // ochiq holatda yana tabiiy o'lchamga qaytadi
            };
            StatsHost.BeginAnimation(HeightProperty, height);
            StatsHost.BeginAnimation(OpacityProperty, new DoubleAnimation(collapsed ? 0 : 1, duration) { EasingFunction = ease });
            StatsSummary.BeginAnimation(OpacityProperty, new DoubleAnimation(summaryOpacity, duration));
            StatsChevronRotate.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty,
                new DoubleAnimation(chevronAngle, duration) { EasingFunction = ease });
        }

        private void BtnViewDetail_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Sale sale)
            {
                OpenSaleDetail(sale);
            }
        }

        private void GridSales_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (GridSales.SelectedItem is Sale sale)
            {
                OpenSaleDetail(sale);
            }
        }

        private void OpenSaleDetail(Sale sale)
        {
            try
            {
                var dialog = new SaleDetailDialog(sale, database: (DataContext as PosElectro.Desktop.ViewModels.ReportsViewModel)?.Database)
                {
                    Owner = Window.GetWindow(this)
                };
                dialog.ShowDialog();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Tafsilotlarni ko'rsatishda xatolik: {ex.Message}", "Xatolik", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
