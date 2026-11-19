using PosElectro.Desktop.ViewModels;
using Xunit;

namespace PosElectro.Desktop.Tests
{
    /// <summary>
    /// Buferdan qo'yilgan rasm: 32-bitli DIB alfa baytini 0 qoldiradi (Telegram, brauzer, "Qirqish").
    /// Avval bunday rasm oq varaq bo'lib saqlanardi.
    /// </summary>
    public sealed class PastedImageAlphaTests
    {
        [Fact]
        public void Dib_with_zero_alpha_keeps_its_colors()
        {
            // 2 piksel BGRA: qizil va ko'k, alfa = 0
            var px = new byte[] { 0, 0, 255, 0, 255, 0, 0, 0 };
            Assert.Equal(new byte[] { 0, 0, 255, 255, 0, 0 }, InventoryViewModel.FlattenBgraOnWhite(px));
        }

        [Fact]
        public void Real_transparency_is_blended_on_white()
        {
            // 1-piksel to'liq shaffof (oq bo'ladi), 2-piksel to'liq qora
            var px = new byte[] { 0, 0, 255, 0, 0, 0, 0, 255 };
            Assert.Equal(new byte[] { 255, 255, 255, 0, 0, 0 }, InventoryViewModel.FlattenBgraOnWhite(px));
        }
    }
}
