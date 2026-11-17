using PosElectro.Desktop.Services;
using Xunit;

namespace PosElectro.Desktop.Tests
{
    /// <summary>Fon skaneri: qaysi COM portlar tinglanadi (USB-COM ha; platadagi COM1 va Bluetooth yo'q).</summary>
    public sealed class SerialScannerPortFilterTests
    {
        [Theory]
        [InlineData(@"\Device\USBSER000", false)]   // standart Windows USB-COM drayveri (usbser.sys)
        [InlineData(@"\Device\VCP0", false)]         // ishlab chiqaruvchi virtual COM drayveri
        [InlineData(@"\Device\Silabser0", false)]
        [InlineData(@"\Device\Serial0", true)]       // ona platadagi COM1
        [InlineData(@"\Device\Serial1", true)]
        [InlineData(@"\Device\BthModem0", true)]     // Bluetooth virtual portlari
        [InlineData(@"\Device\BTHMODEM12", true)]
        public void Only_usb_serial_ports_are_listened_to(string device, bool excluded)
        {
            Assert.Equal(excluded, SerialScannerService.IsExcludedDevice(device));
        }
    }
}
