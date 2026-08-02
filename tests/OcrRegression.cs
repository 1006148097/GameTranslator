using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Text;
using System.Threading;
using GameTranslator;

internal static class OcrRegression
{
    private sealed class Fixture
    {
        public string Name { get; set; }
        public string Text { get; set; }
        public int FontSize { get; set; }
        public bool DarkBackground { get; set; }
        public bool Outline { get; set; }
        public bool NoisyBackground { get; set; }
        public string FontName { get; set; }
        public Color ForegroundColor { get; set; }
        public bool MixedPolarity { get; set; }
        public bool AllowWhitespaceDifferences { get; set; }
        public string ImageFileName { get; set; }
    }

    private static int Main()
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        var fixtures = new[]
        {
            new Fixture
            {
                Name = "clean-dialogue",
                Text = "게임을 시작하시겠습니까?",
                FontSize = 30
            },
            new Fixture
            {
                Name = "outlined-subtitle",
                Text = "저장하지 않은 데이터는 사라집니다.\n정말 종료하시겠습니까?",
                FontSize = 25,
                DarkBackground = true,
                Outline = true
            },
            new Fixture
            {
                Name = "game-ui-numbers",
                Text = "남은 체력 125 / 300",
                FontSize = 22,
                DarkBackground = true,
                Outline = true
            },
            new Fixture
            {
                Name = "small-noisy-subtitle",
                Text = "오늘은 여기서 쉬었다 가자.",
                FontSize = 16,
                DarkBackground = true,
                Outline = true,
                NoisyBackground = true
            },
            new Fixture
            {
                Name = "english-menu",
                Text = "Press ENTER to continue",
                FontSize = 25,
                DarkBackground = true,
                Outline = true,
                FontName = "Segoe UI"
            },
            new Fixture
            {
                Name = "japanese-dialogue",
                Text = "セーブデータを読み込みますか？",
                FontSize = 26,
                DarkBackground = true,
                Outline = true,
                FontName = "Yu Gothic UI"
            },
            new Fixture
            {
                Name = "simplified-chinese-menu",
                Text = "是否读取存档？",
                FontSize = 28,
                DarkBackground = true,
                Outline = true,
                FontName = "Microsoft YaHei UI"
            },
            new Fixture
            {
                Name = "mixed-japanese-menu",
                Text = "NEW GAMEを開始しますか？",
                FontSize = 25,
                DarkBackground = true,
                Outline = true,
                FontName = "Yu Gothic UI"
            },
            new Fixture
            {
                Name = "mixed-chinese-menu",
                Text = "读取 DLC 存档？",
                FontSize = 27,
                DarkBackground = true,
                Outline = true,
                FontName = "Microsoft YaHei UI"
            },
            new Fixture
            {
                Name = "red-korean-warning",
                Text = "위험! 즉시 대피하세요",
                FontSize = 24,
                DarkBackground = true,
                Outline = true,
                ForegroundColor = Color.FromArgb(215, 48, 42),
                AllowWhitespaceDifferences = true
            },
            new Fixture
            {
                Name = "mixed-polarity-chinese",
                Text = "任务完成\n点击继续",
                FontSize = 28,
                DarkBackground = true,
                FontName = "Microsoft YaHei UI",
                MixedPolarity = true
            },
            new Fixture
            {
                Name = "real-korean-dps",
                Text = "나는 아직 탱크와 DPS를 등급을 정하지 않았어",
                ImageFileName = "real-korean-dps.png"
            }
        };

        var failureCount = 0;
        var engine = new WindowsOcrEngine();
        var fixtureDirectory = Environment.GetEnvironmentVariable(
            "GAME_TRANSLATOR_OCR_FIXTURES");
        if (!string.IsNullOrWhiteSpace(fixtureDirectory))
        {
            Directory.CreateDirectory(fixtureDirectory);
        }
        foreach (var fixture in fixtures)
        {
            using (var bitmap = RenderFixture(fixture))
            {
                if (!string.IsNullOrWhiteSpace(fixtureDirectory))
                {
                    bitmap.Save(
                        Path.Combine(fixtureDirectory, fixture.Name + ".png"),
                        System.Drawing.Imaging.ImageFormat.Png);
                }
                var result = engine.RecognizeAsync(
                    bitmap,
                    CancellationToken.None).GetAwaiter().GetResult();
                var accuracy = CharacterAccuracy(fixture.Text, result.Text);
                Console.WriteLine(
                    fixture.Name + " ACCURACY=" + accuracy.ToString("0.000")
                    + " SECONDS=" + result.Duration.TotalSeconds.ToString("0.000"));
                Console.WriteLine("EXPECTED=" + fixture.Text.Replace("\n", " | "));
                Console.WriteLine("ACTUAL=" + result.Text.Replace("\n", " | "));
                var exactMatch = string.Equals(
                    fixture.Text,
                    result.Text,
                    StringComparison.Ordinal);
                var whitespaceOnlyDifference = fixture.AllowWhitespaceDifferences
                    && string.Equals(
                        WithoutWhitespace(fixture.Text),
                        WithoutWhitespace(result.Text),
                        StringComparison.Ordinal);
                if (accuracy < 0.82
                    || (!exactMatch && !whitespaceOnlyDifference))
                {
                    failureCount++;
                }
            }
        }

        if (failureCount > 0)
        {
            Console.Error.WriteLine("OCR_REGRESSION_FAILED=" + failureCount);
            return 1;
        }
        Console.WriteLine("OCR_REGRESSION_OK");
        return 0;
    }

    private static Bitmap RenderFixture(Fixture fixture)
    {
        if (!string.IsNullOrWhiteSpace(fixture.ImageFileName))
        {
            return new Bitmap(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "fixtures",
                fixture.ImageFileName));
        }
        var bitmap = new Bitmap(
            900,
            fixture.MixedPolarity
                ? 180
                : (fixture.Text.IndexOf('\n') >= 0 ? 190 : 110));
        bitmap.SetResolution(96, 96);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.HighQuality;
            graphics.TextRenderingHint =
                System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            if (fixture.DarkBackground)
            {
                using (var background = new LinearGradientBrush(
                    new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                    Color.FromArgb(25, 37, 55),
                    Color.FromArgb(77, 48, 68),
                    18f))
                {
                    graphics.FillRectangle(background, 0, 0, bitmap.Width, bitmap.Height);
                }
            }
            else
            {
                graphics.Clear(Color.FromArgb(245, 243, 236));
            }

            if (fixture.NoisyBackground)
            {
                var random = new Random(1337);
                using (var noisePen = new Pen(Color.FromArgb(55, 180, 200, 220), 1f))
                {
                    for (var index = 0; index < 80; index++)
                    {
                        graphics.DrawLine(
                            noisePen,
                            random.Next(bitmap.Width),
                            random.Next(bitmap.Height),
                            random.Next(bitmap.Width),
                            random.Next(bitmap.Height));
                    }
                }
            }

            if (fixture.MixedPolarity)
            {
                using (var panel = new SolidBrush(Color.FromArgb(238, 238, 232)))
                {
                    graphics.FillRectangle(panel, 0, 90, bitmap.Width, 90);
                }
            }

            using (var font = new Font(
                string.IsNullOrWhiteSpace(fixture.FontName)
                    ? "Malgun Gothic"
                    : fixture.FontName,
                fixture.FontSize,
                FontStyle.Bold,
                GraphicsUnit.Pixel))
            using (var foreground = new SolidBrush(
                !fixture.ForegroundColor.IsEmpty
                    ? fixture.ForegroundColor
                    : (fixture.DarkBackground
                        ? Color.White
                        : Color.FromArgb(20, 20, 20))))
            {
                var point = new PointF(24, 20);
                if (fixture.MixedPolarity)
                {
                    var textLines = fixture.Text.Split('\n');
                    graphics.DrawString(
                        textLines[0],
                        font,
                        Brushes.White,
                        new PointF(24, 18));
                    graphics.DrawString(
                        textLines[1],
                        font,
                        Brushes.Black,
                        new PointF(24, 105));
                }
                else
                {
                    if (fixture.Outline)
                    {
                        using (var outline = new SolidBrush(Color.FromArgb(220, 0, 0, 0)))
                        {
                            foreach (var offset in OutlineOffsets())
                            {
                                graphics.DrawString(
                                    fixture.Text,
                                    font,
                                    outline,
                                    point.X + offset.X,
                                    point.Y + offset.Y);
                            }
                        }
                    }
                    graphics.DrawString(fixture.Text, font, foreground, point);
                }
            }
        }
        return bitmap;
    }

    private static IEnumerable<Point> OutlineOffsets()
    {
        yield return new Point(-2, -2);
        yield return new Point(0, -2);
        yield return new Point(2, -2);
        yield return new Point(-2, 0);
        yield return new Point(2, 0);
        yield return new Point(-2, 2);
        yield return new Point(0, 2);
        yield return new Point(2, 2);
    }

    private static double CharacterAccuracy(string expected, string actual)
    {
        var left = Comparable(expected);
        var right = Comparable(actual);
        if (left.Length == 0)
        {
            return right.Length == 0 ? 1.0 : 0.0;
        }
        var previous = new int[right.Length + 1];
        var current = new int[right.Length + 1];
        for (var column = 0; column <= right.Length; column++)
        {
            previous[column] = column;
        }
        for (var row = 1; row <= left.Length; row++)
        {
            current[0] = row;
            for (var column = 1; column <= right.Length; column++)
            {
                var substitution = previous[column - 1]
                    + (left[row - 1] == right[column - 1] ? 0 : 1);
                current[column] = Math.Min(
                    Math.Min(previous[column] + 1, current[column - 1] + 1),
                    substitution);
            }
            var swap = previous;
            previous = current;
            current = swap;
        }
        var distance = previous[right.Length];
        return Math.Max(0.0, 1.0 - ((double)distance / left.Length));
    }

    private static string Comparable(string text)
    {
        var builder = new StringBuilder();
        foreach (var character in text ?? "")
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
            }
        }
        return builder.ToString();
    }

    private static string WithoutWhitespace(string text)
    {
        var builder = new StringBuilder();
        foreach (var character in text ?? "")
        {
            if (!char.IsWhiteSpace(character))
            {
                builder.Append(character);
            }
        }
        return builder.ToString();
    }
}
