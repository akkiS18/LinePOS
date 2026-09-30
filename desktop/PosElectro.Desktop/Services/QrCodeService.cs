using System;
using System.IO;
using System.Windows.Media.Imaging;
using QRCoder;

namespace PosElectro.Desktop.Services
{
    public class QrCodeService
    {
        public static BitmapImage GenerateQrBitmap(string content, int pixelsPerModule = 10)
        {
            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(content, QRCodeGenerator.ECCLevel.M);
            using var qrCode = new PngByteQRCode(qrCodeData);
            byte[] qrCodeBytes = qrCode.GetGraphic(pixelsPerModule);

            var bitmapImage = new BitmapImage();
            using (var mem = new MemoryStream(qrCodeBytes))
            {
                mem.Position = 0;
                bitmapImage.BeginInit();
                bitmapImage.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.UriSource = null;
                bitmapImage.StreamSource = mem;
                bitmapImage.EndInit();
            }
            bitmapImage.Freeze();
            return bitmapImage;
        }
    }
}
