using System;
using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json.Linq;

namespace PosElectro.Desktop.Sync;

/// <summary>
/// Tovar rasmlarining Wi‑Fi sinxroni (telefon ⇄ kompyuter), tovar sinxronidan alohida.
/// Kompyuterda rasm hamon <c>products.image_path</c> dagi fayl; bu ustun sinxron triggerlarida yo'q,
/// shuning uchun rasm almashganda tovar revizyasi o'zgarmaydi va "Qaysi tahrir saqlansin?" chiqmaydi.
/// Telefon har bir rasm uchun "oxirgi kelishilgan" xeshni o'zida saqlaydi va kim o'zgarganini o'zi hal qiladi.
/// </summary>
public sealed class ProductImageSync
{
    public const string Capability = "productImagesV1";
    public const int PhoneMaxSide = 600;
    private const int MaxUploadBytes = 4 * 1024 * 1024;

    private readonly string databasePath;
    private readonly string imagesDir;
    private static readonly ConcurrentDictionary<string, (long Length, long Ticks, string Hash)> HashCache = new();

    public ProductImageSync(string databasePath, string? imagesDir = null)
    {
        this.databasePath = databasePath;
        this.imagesDir = imagesDir ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "product_images");
    }

    private SqliteConnection Open()
    {
        var c = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = databasePath, DefaultTimeout = 15 }.ToString());
        c.Open();
        return c;
    }

    /// <summary>Rasmli barcha faol tovarlar: guid, xesh, o'zgargan vaqt. Fayli topilmaganlar "missing" ga tushadi — telefon ularga tegmaydi.</summary>
    public JObject Manifest()
    {
        var images = new JArray();
        var missing = new JArray();
        using var db = Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT guid, image_path FROM products WHERE is_deleted=0 AND image_path IS NOT NULL AND image_path<>''";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            string guid = r.GetString(0), path = r.GetString(1);
            var info = new FileInfo(path);
            if (!info.Exists) { missing.Add(guid); continue; }
            images.Add(new JObject { ["guid"] = guid, ["hash"] = HashOf(info), ["modifiedAt"] = ModifiedAt(info) });
        }
        return new JObject { ["images"] = images, ["missing"] = missing };
    }

    /// <summary>Telefonga: rasm ≤600 px JPEG (base64). "hash" — kompyuterdagi asl faylning xeshi.</summary>
    public JObject Get(string guid)
    {
        var path = ImagePath(guid) ?? throw new ArgumentException("Bu tovarda rasm yo'q.");
        var info = new FileInfo(path);
        if (!info.Exists) throw new ArgumentException("Rasm fayli kompyuterda topilmadi.");
        var jpeg = ToPhoneJpeg(File.ReadAllBytes(path));
        return new JObject { ["guid"] = guid, ["hash"] = HashOf(info), ["modifiedAt"] = ModifiedAt(info), ["data"] = Convert.ToBase64String(jpeg) };
    }

    /// <summary>Telefondan kelgan rasmni saqlaydi va tovarga biriktiradi. Javobdagi xesh — saqlangan baytlar xeshi.</summary>
    public JObject Put(string guid, string base64)
    {
        RequireGuid(guid);
        byte[] bytes;
        try { bytes = Convert.FromBase64String(base64); }
        catch (FormatException) { throw new ArgumentException("Rasm ma'lumoti buzilgan."); }
        if (bytes.Length == 0 || bytes.Length > MaxUploadBytes) throw new ArgumentException("Rasm hajmi noto'g'ri (4 MB gacha).");
        ValidateImage(bytes);
        using (var db = Open())
            if (Scalar(db, "SELECT 1 FROM products WHERE guid=@g AND is_deleted=0", guid) == null)
                throw new ArgumentException("Tovar kompyuterda topilmadi; rasm keyingi sinxronda yuboriladi.");

        Directory.CreateDirectory(imagesDir);
        var dest = Path.Combine(imagesDir, $"prod_{guid}_{DateTime.Now.Ticks}.jpg");
        File.WriteAllBytes(dest, bytes);
        SetImagePath(guid, dest);
        var info = new FileInfo(dest);
        return new JObject { ["guid"] = guid, ["hash"] = HashOf(info), ["modifiedAt"] = ModifiedAt(info) };
    }

    /// <summary>Telefonda rasm olib tashlandi: kompyuterdagi bog'lanish uziladi (fayl o'chirilmaydi — kompyuter UI'dagi kabi).</summary>
    public void Delete(string guid)
    {
        RequireGuid(guid);
        SetImagePath(guid, "");
    }

    private void SetImagePath(string guid, string path)
    {
        using var db = Open();
        using var cmd = db.CreateCommand();
        // image_path / updated_at sinxron triggerida yo'q — tovar jurnaliga yozilmaydi.
        cmd.CommandText = "UPDATE products SET image_path=@img, updated_at=@now WHERE guid=@g";
        cmd.Parameters.AddWithValue("@img", path);
        cmd.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        cmd.Parameters.AddWithValue("@g", guid);
        cmd.ExecuteNonQuery();
    }

    private string? ImagePath(string guid)
    {
        RequireGuid(guid);
        using var db = Open();
        return Scalar(db, "SELECT image_path FROM products WHERE guid=@g AND is_deleted=0", guid) is string p && p.Length > 0 ? p : null;
    }

    private static object? Scalar(SqliteConnection db, string sql, string guid)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@g", guid);
        var v = cmd.ExecuteScalar();
        return v is DBNull ? null : v;
    }

    private static void RequireGuid(string guid)
    {
        if (!Guid.TryParse(guid, out _)) throw new ArgumentException("Tovar GUIDi noto'g'ri.");
    }

    /// <summary>Fayl qo'shilgan (yaratilgan) vaqt; nusxa olingan eski fayllarda LastWrite eskirgan bo'lishi mumkin.</summary>
    private static long ModifiedAt(FileInfo info)
    {
        var t = info.CreationTimeUtc > info.LastWriteTimeUtc ? info.CreationTimeUtc : info.LastWriteTimeUtc;
        return new DateTimeOffset(t).ToUnixTimeMilliseconds();
    }

    public static string HashOf(FileInfo info)
    {
        long ticks = info.LastWriteTimeUtc.Ticks;
        if (HashCache.TryGetValue(info.FullName, out var c) && c.Length == info.Length && c.Ticks == ticks) return c.Hash;
        using var s = info.OpenRead();
        var hash = Convert.ToHexString(SHA256.HashData(s)).ToLowerInvariant();
        HashCache[info.FullName] = (info.Length, ticks, hash);
        return hash;
    }

    private static void ValidateImage(byte[] bytes)
    {
        try
        {
            using var ms = new MemoryStream(bytes);
            var frame = BitmapDecoder.Create(ms, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
            if (frame.PixelWidth <= 0 || frame.PixelHeight <= 0) throw new ArgumentException("Rasm o'qilmadi.");
        }
        catch (Exception e) when (e is not ArgumentException) { throw new ArgumentException("Yuborilgan fayl rasm emas."); }
    }

    /// <summary>Har qanday rasm (png/webp/bmp/jpg) → oq fonda, eng uzun tomoni ≤600 px JPEG.</summary>
    public static byte[] ToPhoneJpeg(byte[] source)
    {
        using var input = new MemoryStream(source);
        BitmapSource src = BitmapDecoder.Create(input, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.OnLoad).Frames[0];
        double scale = Math.Min(1.0, (double)PhoneMaxSide / Math.Max(src.PixelWidth, src.PixelHeight));
        if (scale < 1.0) src = new TransformedBitmap(src, new ScaleTransform(scale, scale));
        var bgra = new FormatConvertedBitmap(src, PixelFormats.Bgra32, null, 0);
        int w = bgra.PixelWidth, h = bgra.PixelHeight, stride = w * 4;
        var px = new byte[stride * h];
        bgra.CopyPixels(px, stride, 0);
        var rgb = new byte[w * 3 * h];
        for (int i = 0, j = 0; i < px.Length; i += 4, j += 3)
        {
            int a = px[i + 3];
            rgb[j] = (byte)((px[i] * a + 255 * (255 - a)) / 255);
            rgb[j + 1] = (byte)((px[i + 1] * a + 255 * (255 - a)) / 255);
            rgb[j + 2] = (byte)((px[i + 2] * a + 255 * (255 - a)) / 255);
        }
        var flat = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgr24, null, rgb, w * 3);
        var encoder = new JpegBitmapEncoder { QualityLevel = 82 };
        encoder.Frames.Add(BitmapFrame.Create(flat));
        using var output = new MemoryStream();
        encoder.Save(output);
        return output.ToArray();
    }
}
