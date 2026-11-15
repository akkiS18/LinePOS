using System;
using System.Threading;
using System.Windows.Controls;
using PosElectro.Desktop.Views;
using Xunit;

namespace PosElectro.Desktop.Tests
{
    /// <summary>Kassada Space = Naqd to'lash: tugmadagi "Space · F8" yozuvi rost bo'lishi kerak.</summary>
    public sealed class CashierSpaceShortcutTests
    {
        private static void OnSta(Action action)
        {
            Exception? error = null;
            var t = new Thread(() => { try { action(); } catch (Exception ex) { error = ex; } });
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            t.Join();
            if (error != null) throw new Xunit.Sdk.XunitException(error.ToString());
        }

        [Fact]
        public void Space_pays_when_nothing_or_a_non_text_element_is_focused()
        {
            OnSta(() =>
            {
                Assert.True(CashierView.SpaceShouldPay(null));
                Assert.True(CashierView.SpaceShouldPay(new Button()));
            });
        }

        [Fact]
        public void Space_pays_when_the_search_box_is_empty()
        {
            OnSta(() => Assert.True(CashierView.SpaceShouldPay(new TextBox { Text = "" })));
        }

        [Fact]
        public void Space_types_normally_when_the_search_box_has_text()
        {
            OnSta(() => Assert.False(CashierView.SpaceShouldPay(new TextBox { Text = "kabel" })));
        }

        [Fact]
        public void Space_never_pays_from_a_password_box()
        {
            OnSta(() => Assert.False(CashierView.SpaceShouldPay(new PasswordBox())));
        }
    }
}
