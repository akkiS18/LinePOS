using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PosElectro.Desktop.Services
{
    /// <summary>
    /// Tovar rasmlarini diskka ixcham saqlash: eng uzun tomoni <see cref="MaxSide"/> px gacha kichraytiriladi,
    /// shaffof joylar oq fonga aralashtiriladi va JPEG (<see cref="Quality"/>) qilib yoziladi.
    /// Telefon kamerasining 3–8 MB lik rasmi ~150–300 KB bo'ladi; ko'rinishi (kassa, katta rasm oynasi) o'zgarmaydi.
    /// </summary>
    public static class ProductImageFiles
    {
        public const int MaxSide = 1280;
        public const int Quality = 85;
        /// <summary>Shundan kichik, allaqachon ixcham JPEG qayta siqilmaydi (sifat yo'qolmasin).</summary>
        private const long KeepAsIsBytes = 300 * 1024;

        /// <summary>
        /// Rasm faylini <paramref name="destPathWithoutExt"/> + ".jpg" ga ixcham saqlaydi va yakuniy yo'lni qaytaradi.
        /// Fayl o'qib bo'lmaydigan formatda bo'lsa (masalan, kodek yo'q), avvalgidek o'zgarishsiz nusxalanadi.
        /// </summary>
        public static string SaveFromFile(string sourcePath, string destPathWithoutExt)
        {
            var ext = Path.GetExtension(sourcePath).ToLowerInvariant();
            BitmapFrame frame;
            try
            {
                using var fs = File.OpenRead(sourcePath);
                var decoder = BitmapDecoder.Create(fs, BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.OnLoad);
                frame = decoder.Frames[0];
            }
            catch
            {
                var copyPath = destPathWithoutExt + ext;
                File.Copy(sourcePath, copyPath, overwrite: true);
                return copyPath;
            }

            var rotation = ReadRotation(frame);
            bool isJpeg = ext is ".jpg" or ".jpeg";
            if (isJpeg && rotation == 0 && new FileInfo(sourcePath).Length <= KeepAsIsBytes
                && Math.Max(frame.PixelWidth, frame.PixelHeight) <= MaxSide)
            {
                var keepPath = destPathWithoutExt + ".jpg";
                File.Copy(sourcePath, keepPath, overwrite: true);
                return keepPath;
            }

            BitmapSource src = frame;
            if (rotation != 0) src = new TransformedBitmap(src, new RotateTransform(rotation));
            return SaveFromBitmap(src, destPathWithoutExt);
        }

        /// <summary>Bitmapni (bufer, sudrab tashlash) ixcham JPEG qilib saqlaydi; yakuniy yo'lni qaytaradi.</summary>
        public static string SaveFromBitmap(BitmapSource src, string destPathWithoutExt)
        {
            if (src.PixelWidth <= 0 || src.PixelHeight <= 0) throw new InvalidOperationException("Rasm bo'sh.");
            double scale = Math.Min(1.0, (double)MaxSide / Math.Max(src.PixelWidth, src.PixelHeight));
            BitmapSource scaled = scale < 1.0 ? new TransformedBitmap(src, new ScaleTransform(scale, scale)) : src;
            var bgra = new FormatConvertedBitmap(scaled, PixelFormats.Bgra32, null, 0);
            int w = bgra.PixelWidth, h = bgra.PixelHeight, stride = w * 4;
            var px = new byte[stride * h];
            bgra.CopyPixels(px, stride, 0);
            var rgb = FlattenBgraOnWhite(px);
            var flat = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgr24, null, rgb, w * 3);
            var encoder = new JpegBitmapEncoder { QualityLevel = Quality };
            encoder.Frames.Add(BitmapFrame.Create(flat));
            var destPath = destPathWithoutExt + ".jpg";
            using (var fs = File.Create(destPath)) encoder.Save(fs);
            return destPath;
        }

        /// <summary>
        /// Bgra32 piksellarni oq fonga aralashtirib Bgr24 qiladi.
        /// Telegram, brauzer, "Qirqish" kabi dasturlar buferga 32-bitli DIB qo'yadi va alfa baytini 0 qoldiradi;
        /// WPF uni Bgra32 deb o'qiydi — hamma piksel "to'liq shaffof" bo'lib, rasm oq varaq bo'lib qolardi.
        /// Shuning uchun alfa hamma joyda 0 bo'lsa, rasm shaffof emas deb hisoblanadi.
        /// </summary>
        public static byte[] FlattenBgraOnWhite(byte[] px)
        {
            bool hasAlpha = false;
            for (int i = 3; i < px.Length; i += 4) { if (px[i] != 0) { hasAlpha = true; break; } }
            var rgb = new byte[px.Length / 4 * 3];
            for (int i = 0, j = 0; i + 3 < px.Length; i += 4, j += 3)
            {
                int a = hasAlpha ? px[i + 3] : 255;
                rgb[j] = (byte)((px[i] * a + 255 * (255 - a)) / 255);
                rgb[j + 1] = (byte)((px[i + 1] * a + 255 * (255 - a)) / 255);
                rgb[j + 2] = (byte)((px[i + 2] * a + 255 * (255 - a)) / 255);
            }
            return rgb;
        }

        /// <summary>Telefon rasmidagi EXIF burilishi (3=180°, 6=90°, 8=270°); qayta saqlanganda EXIF yo'qoladi, shuning uchun piksellarni o'zimiz buramiz.</summary>
        private static double ReadRotation(BitmapFrame frame)
        {
            try
            {
                if (frame.Metadata is BitmapMetadata md && md.ContainsQuery("System.Photo.Orientation")
                    && md.GetQuery("System.Photo.Orientation") is ushort o)
                    return o switch { 3 => 180, 6 => 90, 8 => 270, _ => 0 };
            }
            catch { }
            return 0;
        }
    }
}
