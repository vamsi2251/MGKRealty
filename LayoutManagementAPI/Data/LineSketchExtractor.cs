using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using LayoutManagementAPI.Contracts;
using Tesseract;

namespace LayoutManagementAPI.Data;

public static class LineSketchExtractor
{
    private static readonly object EngineGate = new();
    private static TesseractEngine? _engine;
    private static bool _engineUnavailable;

    public static LayoutDocument Extract(LayoutExtractionRequest request, bool readLabels = true)
    {
        var document = LayoutExtractionService.EmptyDraft(request, "line-sketch");
        document.Extraction.Warnings.Add("Outlines were traced from image lines. Printed labels are kept only when the text can be read. These coordinates are image fractions, not survey coordinates.");
        try
        {
            using var image = LoadBitmap(request.Content, out var originalWidth, out var originalHeight);
            document.CanvasWidth = request.Width > 0 ? request.Width : originalWidth;
            document.CanvasHeight = request.Height > 0 ? request.Height : originalHeight;
            var cells = FindCells(image);
            if (cells.Count == 0)
            {
                document.Extraction.Warnings.Add("No closed outlines were detected. Add plots manually.");
                return document;
            }

            var labels = readLabels ? ReadLabels(image, cells) : cells.Select(_ => "").ToList();
            var plotNumber = 1;
            for (var index = 0; index < cells.Count; index++)
            {
                var cell = cells[index];
                var text = labels[index];
                var area = ReadMeasure(text, @"(?<value>\d+(?:\.\d+)?)\s*(scen\w*|cen\w*)");
                var feet = ReadMeasure(text, @"(?<value>\d+(?:\.\d+)?)\s*(foot|feet|ft)\b");
                var road = feet is not null || text.Contains("road", StringComparison.OrdinalIgnoreCase) || IsThin(cell);
                var ring = Ring(cell, image.Width, image.Height);
                if (road && area is null)
                {
                    document.Roads.Add(new DocumentRoad
                    {
                        Id = $"road-{index + 1}",
                        Name = string.IsNullOrWhiteSpace(text) ? null : Collapse(text),
                        Width = feet is >= 100 ? null : feet,
                        WidthUnit = feet is null or >= 100 ? null : "feet",
                        Vertices = ring,
                        Confidence = feet is null ? 0.45 : 0.7,
                        Warnings = feet is null ? ["Road width was not read."] : feet >= 100 ? ["The road width reading was discarded because it was not reliable."] : []
                    });
                    continue;
                }

                var warnings = new List<string>();
                if (area is null)
                {
                    warnings.Add(string.IsNullOrWhiteSpace(text)
                        ? "No area was read inside this outline."
                        : $"No area was read. Text inside the outline was \"{text}\".");
                }

                warnings.Add("No plot number is printed on this outline. The number follows outline order, top to bottom and left to right.");
                document.Plots.Add(new DocumentPlot
                {
                    Id = $"plot-{index + 1}",
                    PlotNumber = plotNumber.ToString(CultureInfo.InvariantCulture),
                    Vertices = ring,
                    Area = area is null ? null : new MeasuredValue { Value = area, Unit = "cent", Source = "extracted" },
                    Status = "unknown",
                    Confidence = area is null ? 0.4 : 0.7,
                    Warnings = warnings
                });
                plotNumber++;
            }

            if (document.Plots.Any(plot => plot.Area?.Unit == "cent"))
            {
                document.Coordinates.AreaUnit = "cent";
            }

            if (document.Roads.Any(road => road.WidthUnit == "feet"))
            {
                document.Coordinates.DimensionUnit = "feet";
            }

            var agreedWidth = document.Roads
                .Where(road => road.Width is > 0 and < 100)
                .GroupBy(road => road.Width)
                .OrderByDescending(group => group.Count())
                .FirstOrDefault(group => group.Count() >= 2);
            if (agreedWidth is not null)
            {
                foreach (var road in document.Roads.Where(road => road.Width is not null && road.Width != agreedWidth.Key))
                {
                    road.Warnings.Add($"The width reading {road.Width} {road.WidthUnit} disagreed with the other roads, so it was not kept.");
                    road.Name = null;
                    road.Width = null;
                    road.WidthUnit = null;
                    road.Confidence = 0.4;
                }
            }

            document.Extraction.Confidence = document.Plots.Count == 0 ? 0.3 : document.Plots.Average(plot => plot.Confidence ?? 0.4);
            document.Extraction.Status = "needs-review";
            return document;
        }
        catch (Exception)
        {
            document.Plots.Clear();
            document.Roads.Clear();
            document.Extraction.Status = "failed";
            document.Extraction.Warnings.Add("The image could not be traced. No plot geometry was inferred.");
            return document;
        }
    }

    private static bool IsThin(Cell cell)
    {
        var width = cell.X2 - cell.X1;
        var height = cell.Y2 - cell.Y1;
        return Math.Min(width, height) / Math.Max(width, height) < 0.22;
    }

    private static List<NormPoint> Ring(Cell cell, int width, int height) =>
    [
        new() { X = cell.X1 / width, Y = cell.Y1 / height },
        new() { X = cell.X2 / width, Y = cell.Y1 / height },
        new() { X = cell.X2 / width, Y = cell.Y2 / height },
        new() { X = cell.X1 / width, Y = cell.Y2 / height }
    ];

    private static Bitmap LoadBitmap(byte[] content, out int originalWidth, out int originalHeight)
    {
        using var stream = new MemoryStream(content);
        using var source = new Bitmap(stream);
        originalWidth = source.Width;
        originalHeight = source.Height;
        var scale = Math.Min(1d, 1400d / Math.Max(source.Width, source.Height));
        var width = Math.Max(1, (int)Math.Round(source.Width * scale));
        var height = Math.Max(1, (int)Math.Round(source.Height * scale));
        var image = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(image);
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.DrawImage(source, 0, 0, width, height);
        return image;
    }

    private static List<Cell> FindCells(Bitmap image)
    {
        var width = image.Width;
        var height = image.Height;
        var ink = new bool[height, width];
        var data = image.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var bytes = new byte[data.Stride * height];
            Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var index = (y * data.Stride) + (x * 4);
                    var luminance = (bytes[index + 2] * 3 + bytes[index + 1] * 6 + bytes[index]) / 10;
                    ink[y, x] = bytes[index + 3] > 180 && luminance < 150;
                }
            }
        }
        finally
        {
            image.UnlockBits(data);
        }

        var minLength = Math.Max(28, Math.Min(width, height) / 30);
        var horizontal = Merge(Scan(ink, true, minLength));
        var vertical = Merge(Scan(ink, false, minLength));
        var join = Math.Max(8, minLength / 4);
        horizontal = horizontal.Where(line => Anchored(line, vertical, join)).ToList();
        vertical = vertical.Where(line => Anchored(line, horizontal, join)).ToList();

        var cells = new List<Cell>();
        foreach (var top in horizontal)
        {
            foreach (var bottom in horizontal)
            {
                if (bottom.Position - top.Position < minLength)
                {
                    continue;
                }

                var sides = vertical
                    .Where(line => line.Start <= top.Position + join && line.End >= bottom.Position - join)
                    .OrderBy(line => line.Position)
                    .ToList();
                for (var index = 0; index < sides.Count - 1; index++)
                {
                    var left = sides[index];
                    var right = sides[index + 1];
                    if (right.Position - left.Position < minLength)
                    {
                        continue;
                    }

                    if (top.Start > left.Position + join || top.End < right.Position - join || bottom.Start > left.Position + join || bottom.End < right.Position - join)
                    {
                        continue;
                    }

                    var cell = new Cell(left.Position, top.Position, right.Position, bottom.Position);
                    if (HasInteriorLine(cell, horizontal, vertical, join))
                    {
                        continue;
                    }

                    cells.Add(cell);
                }
            }
        }

        return cells
            .GroupBy(cell => $"{Math.Round(cell.X1 / 6)},{Math.Round(cell.Y1 / 6)},{Math.Round(cell.X2 / 6)},{Math.Round(cell.Y2 / 6)}")
            .Select(group => group.First())
            .OrderBy(cell => cell.Y1)
            .ThenBy(cell => cell.X1)
            .ToList();
    }

    private static bool HasInteriorLine(Cell cell, List<Segment> horizontal, List<Segment> vertical, int join)
    {
        var inset = join;
        return horizontal.Any(line => line.Position > cell.Y1 + inset && line.Position < cell.Y2 - inset && line.End > cell.X1 + inset && line.Start < cell.X2 - inset)
            || vertical.Any(line => line.Position > cell.X1 + inset && line.Position < cell.X2 - inset && line.End > cell.Y1 + inset && line.Start < cell.Y2 - inset);
    }

    private static bool Anchored(Segment line, List<Segment> crossing, int join) =>
        crossing.Any(other => Math.Abs(other.Position - line.Start) <= join && other.Start <= line.Position + join && other.End >= line.Position - join)
        && crossing.Any(other => Math.Abs(other.Position - line.End) <= join && other.Start <= line.Position + join && other.End >= line.Position - join);

    private static List<Segment> Scan(bool[,] ink, bool horizontal, int minLength)
    {
        var major = horizontal ? ink.GetLength(0) : ink.GetLength(1);
        var minor = horizontal ? ink.GetLength(1) : ink.GetLength(0);
        var runs = new List<Segment>();
        for (var a = 0; a < major; a++)
        {
            var b = 0;
            while (b < minor)
            {
                while (b < minor && !InkAt(ink, horizontal, a, b))
                {
                    b++;
                }

                if (b >= minor)
                {
                    break;
                }

                var start = b;
                var last = b;
                var gap = 0;
                while (b < minor && gap <= 2)
                {
                    if (InkAt(ink, horizontal, a, b))
                    {
                        last = b;
                        gap = 0;
                    }
                    else
                    {
                        gap++;
                    }

                    b++;
                }

                if (last - start + 1 >= minLength)
                {
                    runs.Add(new Segment(start, last, a));
                }
            }
        }

        return runs;
    }

    private static bool InkAt(bool[,] ink, bool horizontal, int major, int minor) =>
        horizontal ? ink[major, minor] : ink[minor, major];

    private static List<Segment> Merge(List<Segment> runs)
    {
        var merged = new List<Segment>();
        foreach (var run in runs.OrderBy(item => item.Position).ThenBy(item => item.Start))
        {
            var match = merged.FindIndex(item => Math.Abs(item.Position - run.Position) <= 10 && run.Start <= item.End + 10 && run.End >= item.Start - 10);
            if (match < 0)
            {
                merged.Add(run);
                continue;
            }

            var current = merged[match];
            merged[match] = new Segment(Math.Min(current.Start, run.Start), Math.Max(current.End, run.End), (current.Position + run.Position) / 2);
        }

        return merged;
    }

    private static List<string> ReadLabels(Bitmap image, List<Cell> cells)
    {
        var engine = Engine();
        if (engine is null)
        {
            return cells.Select(_ => "").ToList();
        }

        var labels = new List<string>();
        foreach (var cell in cells)
        {
            labels.Add(ReadCell(engine, image, cell));
        }

        return labels;
    }

    private static string ReadCell(TesseractEngine engine, Bitmap image, Cell cell)
    {
        var insetX = Math.Max(8, (cell.X2 - cell.X1) * (IsThin(cell) ? 0.22 : 0.12));
        var insetY = Math.Max(8, (cell.Y2 - cell.Y1) * (IsThin(cell) ? 0.22 : 0.12));
        var x = (int)Math.Clamp(cell.X1 + insetX, 0, image.Width - 2);
        var y = (int)Math.Clamp(cell.Y1 + insetY, 0, image.Height - 2);
        var width = (int)Math.Clamp(cell.X2 - cell.X1 - (2 * insetX), 2, image.Width - x);
        var height = (int)Math.Clamp(cell.Y2 - cell.Y1 - (2 * insetY), 2, image.Height - y);
        using var crop = image.Clone(new Rectangle(x, y, width, height), PixelFormat.Format32bppArgb);
        var turns = height > width * 1.2
            ? new[] { RotateFlipType.RotateNoneFlipNone, RotateFlipType.Rotate90FlipNone, RotateFlipType.Rotate270FlipNone }
            : new[] { RotateFlipType.RotateNoneFlipNone };
        var chosen = ChooseLabel(ReadCandidates(engine, crop, turns, 4));
        if (chosen.Contains('?') || !Regex.IsMatch(chosen, @"\d"))
        {
            using var tight = TightCrop(crop);
            var larger = ChooseLabel(ReadCandidates(engine, tight, turns, 8));
            if (!larger.Contains('?') && Score(larger) >= Score(chosen))
            {
                chosen = larger;
            }
        }

        return chosen;
    }

    private static List<string> ReadCandidates(TesseractEngine engine, Bitmap crop, RotateFlipType[] turns, int scale)
    {
        var candidates = new List<string>();
        foreach (var mode in turns)
        {
            using var prepared = Prepare(crop, mode, scale);
            foreach (var modeName in new[] { PageSegMode.SingleLine, PageSegMode.SingleBlock })
            {
                var text = Recognize(engine, prepared, modeName);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    candidates.Add(text);
                }
            }
        }

        return candidates;
    }

    private static string ChooseLabel(List<string> candidates)
    {
        if (candidates.Count == 0)
        {
            return "";
        }

        var patterned = candidates.Where(text => Regex.IsMatch(text, @"\d+(?:\.\d+)?\s*(scen|cen|feet|foot|road)", RegexOptions.IgnoreCase)).ToList();
        var pool = patterned.Count == 0 ? candidates : patterned;
        return pool
            .GroupBy(text => Regex.Match(text, @"\d+(?:\.\d+)?").Value)
            .OrderByDescending(group => group.Count())
            .ThenByDescending(group => Score(group.First()))
            .First()
            .First();
    }

    private static Bitmap TightCrop(Bitmap source)
    {
        var data = source.LockBits(new Rectangle(0, 0, source.Width, source.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var minX = source.Width;
        var minY = source.Height;
        var maxX = 0;
        var maxY = 0;
        try
        {
            var bytes = new byte[data.Stride * source.Height];
            Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
            for (var y = 0; y < source.Height; y++)
            {
                for (var x = 0; x < source.Width; x++)
                {
                    var index = (y * data.Stride) + (x * 4);
                    var luminance = (bytes[index + 2] * 3 + bytes[index + 1] * 6 + bytes[index]) / 10;
                    if (bytes[index + 3] > 180 && luminance < 150)
                    {
                        minX = Math.Min(minX, x);
                        minY = Math.Min(minY, y);
                        maxX = Math.Max(maxX, x);
                        maxY = Math.Max(maxY, y);
                    }
                }
            }
        }
        finally
        {
            source.UnlockBits(data);
        }

        if (maxX <= minX || maxY <= minY)
        {
            return (Bitmap)source.Clone();
        }

        var pad = 6;
        var x0 = Math.Max(0, minX - pad);
        var y0 = Math.Max(0, minY - pad);
        var x1 = Math.Min(source.Width - 1, maxX + pad);
        var y1 = Math.Min(source.Height - 1, maxY + pad);
        return source.Clone(new Rectangle(x0, y0, x1 - x0 + 1, y1 - y0 + 1), PixelFormat.Format32bppArgb);
    }

    private static Bitmap Prepare(Bitmap crop, RotateFlipType mode, int scale)
    {
        var prepared = (Bitmap)crop.Clone();
        if (mode != RotateFlipType.RotateNoneFlipNone)
        {
            prepared.RotateFlip(mode);
        }

        var scaled = new Bitmap(Math.Max(1, prepared.Width * scale), Math.Max(1, prepared.Height * scale), PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(scaled))
        {
            graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            graphics.PixelOffsetMode = PixelOffsetMode.Half;
            graphics.DrawImage(prepared, 0, 0, scaled.Width, scaled.Height);
        }

        prepared.Dispose();
        return scaled;
    }

    private static string Recognize(TesseractEngine engine, Bitmap image, PageSegMode mode)
    {
        using var buffer = new MemoryStream();
        image.Save(buffer, System.Drawing.Imaging.ImageFormat.Png);
        lock (EngineGate)
        {
            using var pix = Pix.LoadFromMemory(buffer.ToArray());
            using var page = engine.Process(pix, mode);
            return NormalizeOcr(page.GetText());
        }
    }

    private static int Score(string text)
    {
        var score = text.Count(char.IsLetterOrDigit);
        if (Regex.IsMatch(text, @"scents?|cents?|feet|foot|road", RegexOptions.IgnoreCase))
        {
            score += 40;
        }

        if (Regex.IsMatch(text, @"\d+\.\d+"))
        {
            score += 15;
        }

        return score;
    }

    private static string NormalizeOcr(string text)
    {
        text = Collapse(text).Replace(',', '.');
        return Regex.Replace(text, @"(\d)\s*\.\s*(\d)", "$1.$2");
    }

    private static decimal? ReadMeasure(string text, string pattern)
    {
        var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        return match.Success && decimal.TryParse(match.Groups["value"].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static string Collapse(string text) =>
        Regex.Replace(text, @"\s+", " ").Trim();

    private static string? TessdataDirectory()
    {
        foreach (var candidate in TessdataCandidates())
        {
            if (File.Exists(Path.Combine(candidate, "eng.traineddata")))
            {
                return candidate;
            }
        }

        return null;
    }

    private static IEnumerable<string> TessdataCandidates()
    {
        var prefix = Environment.GetEnvironmentVariable("TESSDATA_PREFIX");
        if (!string.IsNullOrWhiteSpace(prefix))
        {
            yield return prefix;
        }

        yield return "/usr/share/tesseract-ocr/5/tessdata";
        yield return "/usr/share/tesseract-ocr/4.00/tessdata";
        yield return "/usr/share/tessdata";

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            yield return Path.Combine(dir.FullName, "tessdata");
            yield return Path.Combine(dir.FullName, "LayoutManagementAPI", "tessdata");
            dir = dir.Parent;
        }
    }

    private static TesseractEngine? Engine()
    {
        if (_engine is not null || _engineUnavailable)
        {
            return _engine;
        }

        lock (EngineGate)
        {
            if (_engine is not null || _engineUnavailable)
            {
                return _engine;
            }

            var path = TessdataDirectory();
            if (path is null)
            {
                _engineUnavailable = true;
                return null;
            }

            try
            {
                _engine = new TesseractEngine(path, "eng", EngineMode.LstmOnly);
                return _engine;
            }
            catch (Exception)
            {
                _engineUnavailable = true;
                return null;
            }
        }
    }

    private readonly record struct Segment(double Start, double End, double Position);

    public readonly record struct Cell(double X1, double Y1, double X2, double Y2);
}
