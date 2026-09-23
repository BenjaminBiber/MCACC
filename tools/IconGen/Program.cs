using System.Buffers.Binary;
using SkiaSharp;

namespace Mcacc.Tools.IconGen;

/// <summary>
/// Draws the MCACC app icon: a rounded square in Claude orange with a white swap_horiz glyph of the Material Icons
/// Round font. Run once; the outputs are committed.
/// </summary>
internal static class Program
{
    private const int Master = 1024;
    private const string Accent = "#D97757";
    private static readonly string SwapHoriz = char.ConvertFromUtf32(0xE8D4);

    private static readonly int[] IcoSizes = [16, 24, 32, 48, 64, 128, 256];

    public static int Main(string[] args)
    {
        var repoRoot = args.Length > 0 ? args[0] : FindRepoRoot();
        var fontFile = Path.Combine(repoRoot, "src", "Mcacc.App", "Assets", "Fonts", "MaterialIconsRound-Regular.otf");
        if (!File.Exists(fontFile))
        {
            Console.Error.WriteLine($"Icon font not found: {fontFile}");
            return 1;
        }

        var outputDir = Path.Combine(repoRoot, "assets", "icon");
        Directory.CreateDirectory(outputDir);

        using var master = Render(Master, fontFile);
        WritePng(master, Path.Combine(outputDir, "mcacc-1024.png"));
        using var small = Resize(master, 256);
        WritePng(small, Path.Combine(outputDir, "mcacc-256.png"));
        WriteIco(master, Path.Combine(outputDir, "mcacc.ico"));

        Console.WriteLine(outputDir);
        return 0;
    }

    private static SKBitmap Render(int size, string fontFile)
    {
        var bitmap = new SKBitmap(size, size, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        var radius = size * 0.22f;
        using var background = new SKPaint { Color = SKColor.Parse(Accent), IsAntialias = true };
        canvas.DrawRoundRect(new SKRect(0, 0, size, size), radius, radius, background);

        using var typeface = SKTypeface.FromFile(fontFile);
        using var font = new SKFont(typeface, size * 0.62f);
        using var glyph = new SKPaint { Color = SKColors.White, IsAntialias = true };
        font.MeasureText(SwapHoriz, out var bounds, glyph);
        var x = (size - bounds.Width) / 2 - bounds.Left;
        var y = (size - bounds.Height) / 2 - bounds.Top;
        canvas.DrawText(SwapHoriz, x, y, font, glyph);
        canvas.Flush();
        return bitmap;
    }

    private static SKBitmap Resize(SKBitmap source, int size) =>
        source.Resize(new SKImageInfo(size, size), new SKSamplingOptions(SKCubicResampler.Mitchell))
        ?? throw new InvalidOperationException($"Resizing to {size} px failed.");

    private static void WritePng(SKBitmap bitmap, string path)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(path);
        data.SaveTo(stream);
    }

    /// <summary>Writes an .ico whose entries are PNGs, which every Windows version since Vista reads.</summary>
    private static void WriteIco(SKBitmap master, string path)
    {
        var entries = IcoSizes.Select(size =>
        {
            using var resized = Resize(master, size);
            using var image = SKImage.FromBitmap(resized);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            return (Size: size, Bytes: data.ToArray());
        }).ToList();

        using var stream = File.Create(path);
        Span<byte> header = stackalloc byte[6];
        BinaryPrimitives.WriteUInt16LittleEndian(header[2..], 1);
        BinaryPrimitives.WriteUInt16LittleEndian(header[4..], (ushort)entries.Count);
        stream.Write(header);

        var offset = 6 + (entries.Count * 16);
        Span<byte> entry = stackalloc byte[16];
        foreach (var (size, bytes) in entries)
        {
            entry.Clear();
            // 256 does not fit in a byte and is written as 0, which the format defines as 256.
            entry[0] = (byte)(size >= 256 ? 0 : size);
            entry[1] = (byte)(size >= 256 ? 0 : size);
            entry[4] = 1;
            entry[5] = 0;
            BinaryPrimitives.WriteUInt16LittleEndian(entry[6..], 32);
            BinaryPrimitives.WriteUInt32LittleEndian(entry[8..], (uint)bytes.Length);
            BinaryPrimitives.WriteUInt32LittleEndian(entry[12..], (uint)offset);
            stream.Write(entry);
            offset += bytes.Length;
        }

        foreach (var (_, bytes) in entries)
        {
            stream.Write(bytes);
        }
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MCACC.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? Directory.GetCurrentDirectory();
    }
}
