using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Globalization;
using System.Net;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace GameTranslator
{
    public sealed class SettingsStore
    {
        private readonly string _directory;
        private readonly string _path;

        public SettingsStore()
        {
            _directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "GameTranslator");
            _path = Path.Combine(_directory, "settings.json");
        }

        public AppSettings Load()
        {
            try
            {
                if (!File.Exists(_path))
                {
                    return AppSettings.CreateDefault();
                }

                using (var stream = File.OpenRead(_path))
                {
                    var serializer = new DataContractJsonSerializer(typeof(AppSettings));
                    var settings = serializer.ReadObject(stream) as AppSettings;
                    if (settings == null)
                    {
                        return AppSettings.CreateDefault();
                    }

                    NormalizeSettings(settings);
                    if (!TranslationProviderFactory.IsKnown(
                        settings.TranslationProvider))
                    {
                        settings.TranslationProvider =
                            settings.TranslationProvider == "LibreTranslate"
                            ? (string.IsNullOrWhiteSpace(
                                settings.LibreTranslateApiKey)
                                ? "LibreTranslate 免密钥"
                                : "LibreTranslate API")
                            : "智能在线翻译";
                    }
                    if (string.IsNullOrWhiteSpace(
                        settings.TextReplacementHotkey))
                    {
                        settings.TextReplacementHotkey = "F6";
                    }
                    if ((string.IsNullOrWhiteSpace(settings.Hotkey)
                            || settings.Hotkey.Equals(
                                "Alt+Shift+T",
                                StringComparison.OrdinalIgnoreCase))
                        && !string.Equals(
                            settings.ToggleOverlayHotkey,
                            "F8",
                            StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(
                            settings.TextReplacementHotkey,
                            "F8",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        settings.Hotkey = "F8";
                    }
                    if ((string.IsNullOrWhiteSpace(
                                settings.ToggleOverlayHotkey)
                            || settings.ToggleOverlayHotkey.Equals(
                                "Alt+Shift+O",
                                StringComparison.OrdinalIgnoreCase))
                        && !string.Equals(
                            settings.Hotkey,
                            "F7",
                            StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(
                            settings.TextReplacementHotkey,
                            "F7",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        settings.ToggleOverlayHotkey = "F7";
                    }
                    if (!TranslationLanguages.IsSupported(
                        settings.TextReplacementTargetLanguage))
                    {
                        settings.TextReplacementTargetLanguage = "zh-Hans";
                    }
                    if (string.IsNullOrWhiteSpace(
                        settings.LibreTranslateEndpoint))
                    {
                        settings.LibreTranslateEndpoint =
                            "https://translate.argosopentech.com";
                    }
                    if (string.IsNullOrWhiteSpace(settings.LingvaEndpoint))
                    {
                        settings.LingvaEndpoint =
                            "https://lingva.lunar.icu";
                    }
                    if (string.IsNullOrWhiteSpace(
                        settings.MicrosoftTranslatorEndpoint))
                    {
                        settings.MicrosoftTranslatorEndpoint =
                            "https://api.cognitive.microsofttranslator.com";
                    }
                    if (string.IsNullOrWhiteSpace(settings.DeepLApiKey))
                    {
                        settings.DeepLUseFreeApi = true;
                    }
                    NormalizeSettings(settings);
                    return settings;
                }
            }
            catch
            {
                return AppSettings.CreateDefault();
            }
        }

        public void Save(AppSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException("settings");
            }

            NormalizeSettings(settings);
            Directory.CreateDirectory(_directory);
            var tempPath = Path.Combine(
                _directory,
                "settings-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var stream = new FileStream(
                    tempPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None))
                {
                    var serializer =
                        new DataContractJsonSerializer(typeof(AppSettings));
                    serializer.WriteObject(stream, settings);
                    stream.Flush(true);
                }

                if (File.Exists(_path))
                {
                    File.Replace(tempPath, _path, null);
                }
                else
                {
                    File.Move(tempPath, _path);
                }
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    try
                    {
                        File.Delete(tempPath);
                    }
                    catch
                    {
                    }
                }
            }
        }

        private static void NormalizeSettings(AppSettings settings)
        {
            var defaults = AppSettings.CreateDefault();
            if (string.IsNullOrWhiteSpace(settings.Hotkey))
            {
                settings.Hotkey = defaults.Hotkey;
            }
            if (string.IsNullOrWhiteSpace(settings.ToggleOverlayHotkey))
            {
                settings.ToggleOverlayHotkey =
                    defaults.ToggleOverlayHotkey;
            }
            if (string.IsNullOrWhiteSpace(
                settings.TextReplacementHotkey))
            {
                settings.TextReplacementHotkey =
                    defaults.TextReplacementHotkey;
            }
            if (HotkeyRules.IsUnsafeTextReplacementHotkey(
                settings.TextReplacementHotkey))
            {
                settings.TextReplacementHotkey =
                    defaults.TextReplacementHotkey;
            }
            if (string.Equals(
                    settings.Hotkey,
                    settings.ToggleOverlayHotkey,
                    StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    settings.Hotkey,
                    settings.TextReplacementHotkey,
                    StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    settings.ToggleOverlayHotkey,
                    settings.TextReplacementHotkey,
                    StringComparison.OrdinalIgnoreCase))
            {
                settings.Hotkey = defaults.Hotkey;
                settings.ToggleOverlayHotkey =
                    defaults.ToggleOverlayHotkey;
                settings.TextReplacementHotkey =
                    defaults.TextReplacementHotkey;
            }

            if (double.IsNaN(settings.FontSize)
                || double.IsInfinity(settings.FontSize)
                || settings.FontSize <= 0)
            {
                settings.FontSize = defaults.FontSize;
            }
            settings.FontSize = Math.Max(
                14,
                Math.Min(36, settings.FontSize));

            if (double.IsNaN(settings.BackgroundOpacity)
                || double.IsInfinity(settings.BackgroundOpacity)
                || settings.BackgroundOpacity <= 0)
            {
                settings.BackgroundOpacity =
                    defaults.BackgroundOpacity;
            }
            settings.BackgroundOpacity = Math.Max(
                0.05,
                Math.Min(0.85, settings.BackgroundOpacity));

            if (double.IsNaN(settings.OverlayLeft)
                || double.IsInfinity(settings.OverlayLeft))
            {
                settings.OverlayLeft = defaults.OverlayLeft;
            }
            if (double.IsNaN(settings.OverlayTop)
                || double.IsInfinity(settings.OverlayTop))
            {
                settings.OverlayTop = defaults.OverlayTop;
            }
        }
    }

    public sealed class WindowsOcrEngine : IOcrEngine
    {
        private sealed class TesseractCandidate
        {
            public string Text { get; set; }
            public double Confidence { get; set; }
            public int LowConfidenceWords { get; set; }
            public int PageSegmentationMode { get; set; }
        }

        private sealed class TesseractLine
        {
            public string Key { get; set; }
            public List<string> Words { get; private set; }

            public TesseractLine()
            {
                Words = new List<string>();
            }
        }

        private sealed class WindowsOcrCandidate
        {
            public string Text { get; set; }
            public string Language { get; set; }
            public int Score { get; set; }
        }

        public async Task<OcrResult> RecognizeAsync(
            Bitmap bitmap,
            CancellationToken cancellationToken)
        {
            var watch = Stopwatch.StartNew();
            cancellationToken.ThrowIfCancellationRequested();
            var bridgePath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "OcrBridge.ps1");
            if (!File.Exists(bridgePath))
            {
                throw new FileNotFoundException("缺少 Windows OCR 桥接文件。", bridgePath);
            }

            var tempPath = Path.Combine(
                Path.GetTempPath(),
                "GameTranslator-" + Guid.NewGuid().ToString("N") + ".png");
            bitmap.Save(tempPath, System.Drawing.Imaging.ImageFormat.Png);

            var windowsInputPath = tempPath;
            try
            {
                var tesseractPath = FindTesseract();
                Task<string> koreanTask = null;
                if (tesseractPath != null)
                {
                    windowsInputPath = CreateTesseractInput(tempPath);
                    koreanTask = TryRecognizeKoreanWithTesseractAsync(
                        tesseractPath,
                        tempPath,
                        cancellationToken);
                }

                var windowsTask = RunWindowsOcrCandidatesAsync(
                    bridgePath,
                    windowsInputPath,
                    tempPath,
                    cancellationToken);
                var koreanText = koreanTask == null ? "" : await koreanTask;
                var windowsCandidates = await windowsTask;
                var selectedText = SelectBestOcrText(
                    koreanText,
                    windowsCandidates);
                watch.Stop();
                return new OcrResult
                {
                    Text = selectedText.Trim(),
                    Duration = watch.Elapsed
                };
            }
            finally
            {
                if (!windowsInputPath.Equals(
                    tempPath,
                    StringComparison.OrdinalIgnoreCase))
                {
                    TryDeleteFile(windowsInputPath);
                }
                TryDeleteFile(tempPath);
            }
        }

        private static async Task<List<WindowsOcrCandidate>>
            RunWindowsOcrCandidatesAsync(
                string bridgePath,
                string imagePath,
                string alternateImagePath,
                CancellationToken cancellationToken)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -ExecutionPolicy Bypass -File \""
                    + bridgePath + "\" -ImagePath \"" + imagePath
                    + "\" -AlternateImagePath \"" + alternateImagePath
                    + "\" -Diagnostic",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using (var process = Process.Start(startInfo))
            using (cancellationToken.Register(() =>
            {
                try
                {
                    if (process != null && !process.HasExited)
                    {
                        process.Kill();
                    }
                }
                catch
                {
                }
            }))
            {
                if (process == null)
                {
                    throw new InvalidOperationException("无法启动 Windows OCR。");
                }

                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();
                await Task.Run(() => process.WaitForExit(), cancellationToken);
                var output = await outputTask;
                var error = await errorTask;
                cancellationToken.ThrowIfCancellationRequested();
                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        "Windows OCR 失败：" + (error.Trim().Length > 0
                            ? error.Trim()
                            : output.Trim()));
                }
                return ParseWindowsOcrCandidates(output);
            }
        }

        private static List<WindowsOcrCandidate> ParseWindowsOcrCandidates(
            string json)
        {
            var result = new List<WindowsOcrCandidate>();
            var serializer = new JavaScriptSerializer();
            var deserialized = serializer.DeserializeObject(json);
            var items = deserialized as object[];
            if (items == null)
            {
                var singleItem = deserialized as Dictionary<string, object>;
                if (singleItem == null)
                {
                    return result;
                }
                items = new object[] { singleItem };
            }
            foreach (var item in items)
            {
                var values = item as Dictionary<string, object>;
                if (values == null)
                {
                    continue;
                }
                result.Add(new WindowsOcrCandidate
                {
                    Text = values.ContainsKey("Text")
                        ? Convert.ToString(values["Text"])
                        : "",
                    Language = values.ContainsKey("Language")
                        ? Convert.ToString(values["Language"])
                        : "",
                    Score = values.ContainsKey("Score")
                        ? Convert.ToInt32(values["Score"])
                        : int.MinValue
                });
            }
            return result;
        }

        private static string SelectBestOcrText(
            string koreanText,
            IList<WindowsOcrCandidate> windowsCandidates)
        {
            WindowsOcrCandidate bestWindows = null;
            foreach (var candidate in windowsCandidates)
            {
                candidate.Text = NormalizeWindowsOcrText(
                    candidate.Text,
                    candidate.Language);
                if (bestWindows == null || candidate.Score > bestWindows.Score)
                {
                    bestWindows = candidate;
                }
            }

            if (LooksLikeKorean(koreanText))
            {
                if (bestWindows != null
                    && HasStrongNativeScript(bestWindows)
                    && bestWindows.Language.Equals(
                        "ko",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return ScoreKoreanCandidate(bestWindows.Text)
                        > ScoreKoreanCandidate(koreanText)
                        ? bestWindows.Text
                        : koreanText;
                }
                if (bestWindows != null
                    && HasStrongNativeScript(bestWindows))
                {
                    return bestWindows.Text;
                }
                return koreanText;
            }
            return bestWindows == null ? "" : bestWindows.Text;
        }

        private static bool HasStrongNativeScript(WindowsOcrCandidate candidate)
        {
            var text = candidate.Text ?? "";
            if (candidate.Language.Equals("ja", StringComparison.OrdinalIgnoreCase))
            {
                return CountKana(text) >= 2;
            }
            if (candidate.Language.Equals("zh", StringComparison.OrdinalIgnoreCase))
            {
                return CountHan(text) >= 2;
            }
            if (candidate.Language.Equals("ko", StringComparison.OrdinalIgnoreCase))
            {
                return CountHangul(text) >= 3;
            }
            if (candidate.Language.Equals("en", StringComparison.OrdinalIgnoreCase))
            {
                var latin = CountLatin(text);
                var lettersAndDigits = CountLettersAndDigits(text);
                return latin >= 4 && lettersAndDigits > 0
                    && ((latin * 100) / lettersAndDigits) >= 60;
            }
            return false;
        }

        private static string FindTesseract()
        {
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var projectDirectory = new DirectoryInfo(baseDirectory).Parent;
            var candidates = new List<string>
            {
                Path.Combine(baseDirectory, "Tesseract-OCR", "tesseract.exe")
            };

            if (projectDirectory != null)
            {
                candidates.Add(Path.Combine(
                    projectDirectory.FullName,
                    "tools",
                    "Tesseract-OCR",
                    "tesseract.exe"));
            }

            var programFiles = Environment.GetFolderPath(
                Environment.SpecialFolder.ProgramFiles);
            if (!string.IsNullOrWhiteSpace(programFiles))
            {
                candidates.Add(Path.Combine(
                    programFiles,
                    "Tesseract-OCR",
                    "tesseract.exe"));
            }

            foreach (var candidate in candidates)
            {
                var dataPath = Path.Combine(
                    Path.GetDirectoryName(candidate),
                    "tessdata",
                    "kor.traineddata");
                if (File.Exists(candidate) && File.Exists(dataPath))
                {
                    return candidate;
                }
            }
            return null;
        }

        private static async Task<string> TryRecognizeKoreanWithTesseractAsync(
            string executablePath,
            string imagePath,
            CancellationToken cancellationToken)
        {
            var preparedPath = imagePath;
            var colorPreparedPath = imagePath;
            try
            {
                preparedPath = CreateTesseractInput(imagePath);
                colorPreparedPath = CreateColorOcrInput(imagePath);
                var tasks = new List<Task<TesseractCandidate>>();
                foreach (var mode in new[] { 3, 4, 6, 7, 11 })
                {
                    tasks.Add(RunTesseractCandidateAsync(
                        executablePath,
                        preparedPath,
                        mode,
                        cancellationToken));
                }
                foreach (var mode in new[] { 6, 7, 11 })
                {
                    tasks.Add(RunTesseractCandidateAsync(
                        executablePath,
                        colorPreparedPath,
                        mode,
                        cancellationToken));
                }
                await Task.WhenAll(tasks.ToArray());

                var bestText = "";
                var bestScore = int.MinValue;
                var candidates = new List<TesseractCandidate>();
                foreach (var task in tasks)
                {
                    var candidate = await task;
                    candidate.Text = NormalizeOcrLines(candidate.Text);
                    candidates.Add(candidate);
                }

                foreach (var candidate in candidates)
                {
                    var score = ScoreTesseractCandidate(
                        candidate,
                        candidates);
                    if (score > bestScore)
                    {
                        bestText = candidate.Text;
                        bestScore = score;
                    }
                }
                return bestText;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return "";
            }
            finally
            {
                if (!preparedPath.Equals(
                    imagePath,
                    StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        File.Delete(preparedPath);
                    }
                    catch
                    {
                    }
                }
                if (!colorPreparedPath.Equals(
                    imagePath,
                    StringComparison.OrdinalIgnoreCase))
                {
                    TryDeleteFile(colorPreparedPath);
                }
            }
        }

        private static async Task<string> RunTesseractAsync(
            string executablePath,
            string imagePath,
            int pageSegmentationMode,
            CancellationToken cancellationToken)
        {
            var candidate = await RunTesseractCandidateAsync(
                executablePath,
                imagePath,
                pageSegmentationMode,
                cancellationToken);
            return candidate.Text;
        }

        private static async Task<TesseractCandidate> RunTesseractCandidateAsync(
            string executablePath,
            string imagePath,
            int pageSegmentationMode,
            CancellationToken cancellationToken)
        {
            var languageModel = FindKoreanModelName(executablePath);
            var outputBase = Path.Combine(
                Path.GetTempPath(),
                "GameTranslator-Tesseract-" + Guid.NewGuid().ToString("N"));
            var textPath = outputBase + ".txt";
            var tsvPath = outputBase + ".tsv";
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = executablePath,
                    WorkingDirectory = Path.GetDirectoryName(executablePath),
                    Arguments = "\"" + imagePath + "\" \"" + outputBase + "\""
                        + " --tessdata-dir tessdata"
                        + " -l " + languageModel
                        + " --oem 1 --psm " + pageSegmentationMode
                        + " --dpi 300"
                        + " -c preserve_interword_spaces=1 txt tsv",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                using (var process = Process.Start(startInfo))
                using (cancellationToken.Register(() =>
                {
                    try
                    {
                        if (process != null && !process.HasExited)
                        {
                            process.Kill();
                        }
                    }
                    catch
                    {
                    }
                }))
                {
                    if (process == null)
                    {
                        return new TesseractCandidate
                        {
                            Text = "",
                            Confidence = -1,
                            PageSegmentationMode = pageSegmentationMode
                        };
                    }

                    var outputTask = process.StandardOutput.ReadToEndAsync();
                    var errorTask = process.StandardError.ReadToEndAsync();
                    await Task.Run(() => process.WaitForExit(), cancellationToken);
                    await outputTask;
                    await errorTask;
                    cancellationToken.ThrowIfCancellationRequested();
                    if (process.ExitCode != 0)
                    {
                        return new TesseractCandidate
                        {
                            Text = "",
                            Confidence = -1,
                            PageSegmentationMode = pageSegmentationMode
                        };
                    }

                    var tsv = File.Exists(tsvPath)
                        ? File.ReadAllText(tsvPath, Encoding.UTF8)
                        : "";
                    var candidate = ParseTesseractTsv(
                        tsv,
                        pageSegmentationMode);
                    if (File.Exists(textPath))
                    {
                        candidate.Text = File.ReadAllText(
                            textPath,
                            Encoding.UTF8).Trim();
                    }
                    return candidate;
                }
            }
            finally
            {
                TryDeleteFile(textPath);
                TryDeleteFile(tsvPath);
            }
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
            }
        }

        private static string FindKoreanModelName(string executablePath)
        {
            var directory = Path.GetDirectoryName(executablePath);
            var bestModel = Path.Combine(
                directory,
                "tessdata",
                "kor_best.traineddata");
            return File.Exists(bestModel) ? "kor_best" : "kor";
        }

        private static TesseractCandidate ParseTesseractTsv(
            string tsv,
            int pageSegmentationMode)
        {
            var result = new TesseractCandidate
            {
                Text = "",
                Confidence = -1,
                PageSegmentationMode = pageSegmentationMode
            };
            if (string.IsNullOrWhiteSpace(tsv))
            {
                return result;
            }

            var lines = new List<TesseractLine>();
            var lineLookup = new Dictionary<string, TesseractLine>();
            var confidenceTotal = 0.0;
            var confidenceWeight = 0;
            var lowConfidenceWords = 0;
            var rows = tsv.Replace("\r\n", "\n").Replace('\r', '\n')
                .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var row in rows)
            {
                var columns = row.Split(new[] { '\t' }, 12);
                if (columns.Length < 12 || columns[0] != "5")
                {
                    continue;
                }

                var word = columns[11].Trim();
                if (word.Length == 0)
                {
                    continue;
                }

                var key = columns[1] + ":" + columns[2] + ":"
                    + columns[3] + ":" + columns[4];
                TesseractLine line;
                if (!lineLookup.TryGetValue(key, out line))
                {
                    line = new TesseractLine { Key = key };
                    lineLookup.Add(key, line);
                    lines.Add(line);
                }
                line.Words.Add(word);

                double confidence;
                if (double.TryParse(
                    columns[10],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out confidence)
                    && confidence >= 0)
                {
                    var weight = Math.Max(1, CountLettersAndDigits(word));
                    confidenceTotal += confidence * weight;
                    confidenceWeight += weight;
                    if (confidence < 45)
                    {
                        lowConfidenceWords++;
                    }
                }
            }

            var textLines = new List<string>();
            foreach (var line in lines)
            {
                textLines.Add(string.Join(" ", line.Words.ToArray()));
            }
            result.Text = string.Join("\n", textLines.ToArray());
            result.Confidence = confidenceWeight > 0
                ? confidenceTotal / confidenceWeight
                : -1;
            result.LowConfidenceWords = lowConfidenceWords;
            return result;
        }

        private static string CreateTesseractInput(string imagePath)
        {
            return CreateOcrInput(imagePath, true, "Binary");
        }

        private static string CreateColorOcrInput(string imagePath)
        {
            return CreateOcrInput(imagePath, false, "Color");
        }

        private static string CreateOcrInput(
            string imagePath,
            bool binarize,
            string variantName)
        {
            using (var source = new Bitmap(imagePath))
            {
                var scaledPath = Path.Combine(
                    Path.GetTempPath(),
                    "GameTranslator-" + variantName + "-"
                    + Guid.NewGuid().ToString("N")
                    + ".png");
                var scale = source.Width <= 1400 && source.Height <= 700
                    ? 3
                    : 1;
                const int padding = 18;
                using (var scaled = new Bitmap(
                    (source.Width * scale) + (padding * 2),
                    (source.Height * scale) + (padding * 2),
                    System.Drawing.Imaging.PixelFormat.Format24bppRgb))
                using (var graphics = Graphics.FromImage(scaled))
                {
                    graphics.Clear(Color.White);
                    graphics.InterpolationMode =
                        System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    graphics.PixelOffsetMode =
                        System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                    graphics.DrawImage(
                        source,
                        padding,
                        padding,
                        source.Width * scale,
                        source.Height * scale);
                    if (binarize)
                    {
                        BinarizeForOcr(
                            scaled,
                            new Rectangle(
                                padding,
                                padding,
                                source.Width * scale,
                                source.Height * scale));
                    }
                    scaled.Save(
                        scaledPath,
                        System.Drawing.Imaging.ImageFormat.Png);
                }
                return scaledPath;
            }
        }

        private static void BinarizeForOcr(Bitmap bitmap, Rectangle content)
        {
            var rectangle = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            var data = bitmap.LockBits(
                rectangle,
                System.Drawing.Imaging.ImageLockMode.ReadWrite,
                System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            try
            {
                var stride = Math.Abs(data.Stride);
                var bytes = stride * bitmap.Height;
                var pixels = new byte[bytes];
                Marshal.Copy(data.Scan0, pixels, 0, bytes);

                var histogram = new int[256];
                long brightnessTotal = 0;
                var pixelCount = 0;
                for (var y = content.Top; y < content.Bottom; y++)
                {
                    for (var x = content.Left; x < content.Right; x++)
                    {
                        var index = (y * stride) + (x * 3);
                        var brightness = ((pixels[index + 2] * 299)
                            + (pixels[index + 1] * 587)
                            + (pixels[index] * 114)) / 1000;
                        histogram[brightness]++;
                        brightnessTotal += brightness;
                        pixelCount++;
                    }
                }

                if (pixelCount == 0)
                {
                    return;
                }
                var meanBrightness = (int)(brightnessTotal / pixelCount);
                var lightBackground = meanBrightness >= 128;
                var threshold = CalculateOtsuThreshold(histogram, pixelCount);
                threshold = lightBackground
                    ? Math.Min(threshold, Math.Max(25, meanBrightness - 45))
                    : Math.Max(threshold, Math.Min(230, meanBrightness + 55));

                for (var y = 0; y < bitmap.Height; y++)
                {
                    for (var x = 0; x < bitmap.Width; x++)
                    {
                        var index = (y * stride) + (x * 3);
                        var inside = content.Contains(x, y);
                        var brightness = ((pixels[index + 2] * 299)
                            + (pixels[index + 1] * 587)
                            + (pixels[index] * 114)) / 1000;
                        var foreground = inside && (lightBackground
                            ? brightness <= threshold
                            : brightness >= threshold);
                        var value = foreground ? (byte)0 : (byte)255;
                        pixels[index] = value;
                        pixels[index + 1] = value;
                        pixels[index + 2] = value;
                    }
                }
                Marshal.Copy(pixels, 0, data.Scan0, bytes);
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }

        private static int CalculateOtsuThreshold(int[] histogram, int total)
        {
            long weightedTotal = 0;
            for (var value = 0; value < histogram.Length; value++)
            {
                weightedTotal += (long)value * histogram[value];
            }

            long backgroundWeight = 0;
            long backgroundTotal = 0;
            var bestVariance = -1.0;
            var bestThreshold = 127;
            for (var value = 0; value < histogram.Length; value++)
            {
                backgroundWeight += histogram[value];
                if (backgroundWeight == 0)
                {
                    continue;
                }
                var foregroundWeight = total - backgroundWeight;
                if (foregroundWeight == 0)
                {
                    break;
                }
                backgroundTotal += (long)value * histogram[value];
                var backgroundMean = (double)backgroundTotal / backgroundWeight;
                var foregroundMean = (double)(weightedTotal - backgroundTotal)
                    / foregroundWeight;
                var difference = backgroundMean - foregroundMean;
                var variance = (double)backgroundWeight
                    * foregroundWeight * difference * difference;
                if (variance > bestVariance)
                {
                    bestVariance = variance;
                    bestThreshold = value;
                }
            }
            return bestThreshold;
        }

        private static string NormalizeOcrLines(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return "";
            }

            var rawLines = text
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var lines = new List<string>();
            foreach (var rawLine in rawLines)
            {
                var line = RemoveDecorativeOcrTokens(rawLine.Trim());
                if (line.Length == 0 || IsStandaloneJamoNoise(line))
                {
                    continue;
                }
                lines.Add(line);
            }
            return string.Join("\n", lines.ToArray()).Trim();
        }

        private static string NormalizeWindowsOcrText(
            string text,
            string language)
        {
            var normalized = NormalizeOcrLines(text);
            if (normalized.Length == 0)
            {
                return normalized;
            }

            var builder = new StringBuilder();
            for (var index = 0; index < normalized.Length; index++)
            {
                var character = normalized[index];
                if (character == '\n')
                {
                    while (builder.Length > 0
                        && builder[builder.Length - 1] == ' ')
                    {
                        builder.Length--;
                    }
                    if (builder.Length > 0
                        && builder[builder.Length - 1] != '\n')
                    {
                        builder.Append('\n');
                    }
                    continue;
                }
                if (!char.IsWhiteSpace(character))
                {
                    if ((char.IsPunctuation(character) || char.IsSymbol(character))
                        && builder.Length > 0
                        && builder[builder.Length - 1] == ' ')
                    {
                        builder.Length--;
                    }
                    if ((language.Equals("ja", StringComparison.OrdinalIgnoreCase)
                            || language.Equals(
                                "zh",
                                StringComparison.OrdinalIgnoreCase))
                        && builder.Length > 0
                        && IsCjkCharacter(builder[builder.Length - 1]))
                    {
                        character = ToCjkPunctuation(character);
                    }
                    builder.Append(character);
                    continue;
                }

                var previous = builder.Length > 0
                    ? builder[builder.Length - 1]
                    : '\0';
                var nextIndex = index + 1;
                while (nextIndex < normalized.Length
                    && normalized[nextIndex] != '\n'
                    && char.IsWhiteSpace(normalized[nextIndex]))
                {
                    nextIndex++;
                }
                var next = nextIndex < normalized.Length
                    ? normalized[nextIndex]
                    : '\0';
                var removesCjkSpacing = language.Equals(
                    "ja",
                    StringComparison.OrdinalIgnoreCase)
                    || language.Equals(
                        "zh",
                        StringComparison.OrdinalIgnoreCase);
                if ((removesCjkSpacing
                        && IsCjkCharacter(previous)
                        && IsCjkCharacter(next))
                    || (language.Equals("ja", StringComparison.OrdinalIgnoreCase)
                        && IsLatinOrDigit(previous)
                        && IsCjkCharacter(next)))
                {
                    continue;
                }
                if (builder.Length > 0
                    && builder[builder.Length - 1] != ' '
                    && builder[builder.Length - 1] != '\n')
                {
                    builder.Append(' ');
                }
            }
            return builder.ToString().Trim();
        }

        private static bool IsLatinOrDigit(char character)
        {
            return (character >= 'A' && character <= 'Z')
                || (character >= 'a' && character <= 'z')
                || char.IsDigit(character);
        }

        private static char ToCjkPunctuation(char character)
        {
            switch (character)
            {
                case '?': return '？';
                case '!': return '！';
                case ',': return '，';
                case ':': return '：';
                case ';': return '；';
                default: return character;
            }
        }

        private static bool IsCjkCharacter(char character)
        {
            return (character >= '\u3040' && character <= '\u30ff')
                || (character >= '\u31f0' && character <= '\u31ff')
                || (character >= '\u3400' && character <= '\u9fff')
                || (character >= '\u1100' && character <= '\u11ff')
                || (character >= '\u3130' && character <= '\u318f')
                || (character >= '\uac00' && character <= '\ud7af');
        }

        private static string RemoveDecorativeOcrTokens(string line)
        {
            var tokens = (line ?? "").Split(
                (char[])null,
                StringSplitOptions.RemoveEmptyEntries);
            var kept = new List<string>();
            foreach (var token in tokens)
            {
                var decorative = token.Length > 0;
                foreach (var character in token)
                {
                    if ("_~-=|<>".IndexOf(character) < 0)
                    {
                        decorative = false;
                        break;
                    }
                }
                if (!decorative)
                {
                    kept.Add(token);
                }
            }
            return string.Join(" ", kept.ToArray());
        }

        private static bool IsStandaloneJamoNoise(string line)
        {
            var jamoCount = 0;
            foreach (var character in line ?? "")
            {
                if (char.IsWhiteSpace(character)
                    || char.IsPunctuation(character)
                    || char.IsSymbol(character))
                {
                    continue;
                }
                if (character != '\u3145'
                    && character != '\u3134'
                    && character != '\u3141')
                {
                    return false;
                }
                jamoCount++;
            }
            return jamoCount > 0 && jamoCount <= 4;
        }

        private static int CountNonEmptyLines(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return 0;
            }
            return text.Split(
                new[] { '\n' },
                StringSplitOptions.RemoveEmptyEntries).Length;
        }

        private static int ScoreTesseractCandidate(
            TesseractCandidate candidate,
            IList<TesseractCandidate> candidates)
        {
            var score = ScoreKoreanCandidate(candidate.Text);
            if (score == int.MinValue)
            {
                return score;
            }

            if (candidate.Confidence >= 0)
            {
                score += (int)Math.Round(candidate.Confidence * 4.0);
                score -= candidate.LowConfidenceWords * 18;
            }

            var similarities = new List<int>();
            foreach (var other in candidates)
            {
                if (object.ReferenceEquals(candidate, other)
                    || string.IsNullOrWhiteSpace(other.Text))
                {
                    continue;
                }
                similarities.Add(TextSimilarityPercent(
                    candidate.Text,
                    other.Text));
            }
            similarities.Sort();
            similarities.Reverse();
            if (similarities.Count > 0)
            {
                score += similarities[0] * 2;
            }
            if (similarities.Count > 1)
            {
                score += similarities[1];
            }
            return score;
        }

        private static int TextSimilarityPercent(string first, string second)
        {
            var left = GetComparableOcrText(first);
            var right = GetComparableOcrText(second);
            if (left.Length == 0 || right.Length == 0)
            {
                return 0;
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

            var maximumLength = Math.Max(left.Length, right.Length);
            var similarity = 100
                - ((previous[right.Length] * 100) / maximumLength);
            return Math.Max(0, similarity);
        }

        private static string GetComparableOcrText(string text)
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

        private static int CountLettersAndDigits(string text)
        {
            var count = 0;
            foreach (var character in text ?? "")
            {
                if (char.IsLetterOrDigit(character))
                {
                    count++;
                }
            }
            return count;
        }

        private static int CountKana(string text)
        {
            var count = 0;
            foreach (var character in text ?? "")
            {
                if ((character >= '\u3040' && character <= '\u30ff')
                    || (character >= '\u31f0' && character <= '\u31ff'))
                {
                    count++;
                }
            }
            return count;
        }

        private static int CountHan(string text)
        {
            var count = 0;
            foreach (var character in text ?? "")
            {
                if (character >= '\u3400' && character <= '\u9fff')
                {
                    count++;
                }
            }
            return count;
        }

        private static int CountLatin(string text)
        {
            var count = 0;
            foreach (var character in text ?? "")
            {
                if ((character >= 'A' && character <= 'Z')
                    || (character >= 'a' && character <= 'z'))
                {
                    count++;
                }
            }
            return count;
        }

        private static int ScoreKoreanCandidate(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return int.MinValue;
            }

            var hangul = CountHangul(text);
            var lettersAndDigits = 0;
            var suspicious = 0;
            var digits = 0;
            var compatibilityJamo = 0;
            foreach (var character in text)
            {
                if (char.IsLetterOrDigit(character))
                {
                    lettersAndDigits++;
                }
                else if (!char.IsWhiteSpace(character)
                    && !char.IsPunctuation(character)
                    && !char.IsSymbol(character))
                {
                    suspicious++;
                }
                if (char.IsDigit(character))
                {
                    digits++;
                }
                if (character >= '\u3130' && character <= '\u318f')
                {
                    compatibilityJamo++;
                }
            }

            if (lettersAndDigits == 0)
            {
                return int.MinValue;
            }

            var koreanRatio = (hangul * 100) / lettersAndDigits;
            var lineCount = CountNonEmptyLines(text);
            var excessiveLines = Math.Max(0, lineCount - 8);
            var isolatedHangul = CountIsolatedHangulTokens(text);
            var wideSpaceRuns = CountWideSpaceRuns(text);
            var uppercaseAcronymCharacters =
                CountUppercaseAcronymCharacters(text);
            return (hangul * 20)
                + koreanRatio
                + Math.Min(lettersAndDigits, 120)
                + (uppercaseAcronymCharacters * 18)
                - (suspicious * 20)
                - (Math.Max(0, digits - 4) * 2)
                - (compatibilityJamo * 18)
                - (Math.Max(0, isolatedHangul - 2) * 15)
                - (wideSpaceRuns * 35)
                - (excessiveLines * 30);
        }

        private static int CountUppercaseAcronymCharacters(string text)
        {
            var total = 0;
            var runLength = 0;
            foreach (var character in (text ?? "") + " ")
            {
                if (character >= 'A' && character <= 'Z')
                {
                    runLength++;
                    continue;
                }
                if (runLength >= 2 && runLength <= 10)
                {
                    total += runLength;
                }
                runLength = 0;
            }
            return total;
        }

        private static int CountIsolatedHangulTokens(string text)
        {
            var count = 0;
            var tokens = (text ?? "").Split(
                (char[])null,
                StringSplitOptions.RemoveEmptyEntries);
            foreach (var token in tokens)
            {
                if (token.Length == 1 && CountHangul(token) == 1)
                {
                    count++;
                }
            }
            return count;
        }

        private static int CountWideSpaceRuns(string text)
        {
            var count = 0;
            var runLength = 0;
            foreach (var character in text ?? "")
            {
                if (character == ' ' || character == '\t')
                {
                    runLength++;
                    continue;
                }
                if (runLength >= 3)
                {
                    count++;
                }
                runLength = 0;
            }
            if (runLength >= 3)
            {
                count++;
            }
            return count;
        }

        private static int CountHangul(string text)
        {
            var hangul = 0;
            foreach (var character in text ?? "")
            {
                if ((character >= '\u1100' && character <= '\u11ff')
                    || (character >= '\u3130' && character <= '\u318f')
                    || (character >= '\uac00' && character <= '\ud7af'))
                {
                    hangul++;
                }
            }
            return hangul;
        }

        private static bool LooksLikeKorean(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var hangul = CountHangul(text);
            var letters = 0;
            foreach (var character in text)
            {
                if (char.IsLetterOrDigit(character))
                {
                    letters++;
                }
            }

            return hangul >= 3
                && letters > 0
                && ((double)hangul / letters) >= 0.35;
        }
    }

    public static class TranslationProviderFactory
    {
        public static ITranslationProvider Create(string name)
        {
            switch (name)
            {
                case "DeepL API":
                    return new DeepLTranslationProvider();
                case "Microsoft Translator":
                    return new MicrosoftTranslationProvider();
                case "Google 在线翻译":
                    return new GoogleTranslationProvider();
                case "Lingva Translate":
                    return new LingvaTranslationProvider();
                case "LibreTranslate 免密钥":
                    return new LibreTranslateProvider(false);
                case "LibreTranslate API":
                    return new LibreTranslateProvider(true);
                case "MyMemory 免费翻译":
                    return new MyMemoryTranslationProvider();
                default:
                    return new ResilientTranslationProvider();
            }
        }

        public static bool IsKnown(string name)
        {
            return name == "智能在线翻译"
                || name == "DeepL API"
                || name == "Microsoft Translator"
                || name == "Google 在线翻译"
                || name == "Lingva Translate"
                || name == "LibreTranslate 免密钥"
                || name == "LibreTranslate API"
                || name == "MyMemory 免费翻译";
        }
    }

    public sealed class ResilientTranslationProvider : ITranslationProvider
    {
        private readonly GoogleTranslationProvider _primary =
            new GoogleTranslationProvider();
        private readonly MyMemoryTranslationProvider _fallback =
            new MyMemoryTranslationProvider();

        public string Name { get { return "智能在线翻译"; } }

        public async Task<TranslationResult> TranslateAsync(
            TranslationRequest request,
            AppSettings settings,
            CancellationToken cancellationToken)
        {
            using (var raceCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken))
            {
                var primaryTask = _primary.TranslateAsync(
                    request,
                    settings,
                    raceCancellation.Token);
                Exception lastError = null;
                var fallbackDelay = Task.Delay(
                    2500,
                    raceCancellation.Token);
                var first = await Task.WhenAny(
                    primaryTask,
                    fallbackDelay);
                if (first == primaryTask || primaryTask.IsCompleted)
                {
                    try
                    {
                        var primaryResult = await primaryTask;
                        raceCancellation.Cancel();
                        return primaryResult;
                    }
                    catch (OperationCanceledException)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                    catch (Exception ex)
                    {
                        lastError = ex;
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();
                var fallbackTask = _fallback.TranslateAsync(
                    request,
                    settings,
                    raceCancellation.Token);
                var remaining = new List<Task<TranslationResult>>();
                if (!primaryTask.IsCompleted)
                {
                    remaining.Add(primaryTask);
                }
                remaining.Add(fallbackTask);

                while (remaining.Count > 0)
                {
                    var completed = await Task.WhenAny(
                        remaining.ToArray());
                    remaining.Remove(completed);
                    try
                    {
                        var result = await completed;
                        raceCancellation.Cancel();
                        ObserveFailures(remaining);
                        return result;
                    }
                    catch (OperationCanceledException)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                    catch (Exception ex)
                    {
                        lastError = ex;
                    }
                }

                throw new InvalidOperationException(
                    "Google 和 MyMemory 均未能完成翻译，请检查网络或更换接口。",
                    lastError);
            }
        }

        private static void ObserveFailures(
            IEnumerable<Task<TranslationResult>> tasks)
        {
            foreach (var task in tasks)
            {
                task.ContinueWith(
                    completed =>
                    {
                        var ignored = completed.Exception;
                    },
                    CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted
                    | TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }
        }
    }

    public sealed class GoogleTranslationProvider : ITranslationProvider
    {
        private readonly JavaScriptSerializer _json = new JavaScriptSerializer();

        public string Name { get { return "Google 在线翻译"; } }

        public async Task<TranslationResult> TranslateAsync(
            TranslationRequest request,
            AppSettings settings,
            CancellationToken cancellationToken)
        {
            var watch = Stopwatch.StartNew();
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(request.Text))
            {
                throw new InvalidOperationException("没有识别到可翻译的文字。");
            }

            var sourceLanguage = string.IsNullOrWhiteSpace(
                request.SourceLanguage)
                || request.SourceLanguage.Equals(
                    "auto",
                    StringComparison.OrdinalIgnoreCase)
                ? "auto"
                : TranslationLanguages.ToGoogleCode(
                    request.SourceLanguage);
            var targetLanguage = TranslationLanguages.ToGoogleCode(
                request.TargetLanguage);
            var chunks = TranslationText.SplitContextChunks(
                TranslationText.CleanOcrText(request.Text),
                3000);
            var translated = new List<string>();
            foreach (var chunk in chunks)
            {
                cancellationToken.ThrowIfCancellationRequested();
                translated.Add(await TranslateChunkAsync(
                    chunk,
                    sourceLanguage,
                    targetLanguage,
                    cancellationToken));
            }

            watch.Stop();
            return new TranslationResult
            {
                Text = string.Join("\n", translated.ToArray()).Trim(),
                Duration = watch.Elapsed,
                Provider = Name
            };
        }

        private async Task<string> TranslateChunkAsync(
            string text,
            string sourceLanguage,
            string targetLanguage,
            CancellationToken cancellationToken)
        {
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
            var url = "https://translate.googleapis.com/translate_a/single"
                + "?client=gtx"
                + "&sl=" + Uri.EscapeDataString(sourceLanguage)
                + "&tl=" + Uri.EscapeDataString(targetLanguage)
                + "&dt=t"
                + "&q=" + Uri.EscapeDataString(text);
            var webRequest = (HttpWebRequest)WebRequest.Create(url);
            webRequest.Method = "GET";
            webRequest.Accept = "application/json";
            webRequest.UserAgent = "Mozilla/5.0 GameTranslator/1.1";
            webRequest.Timeout = 15000;
            webRequest.ReadWriteTimeout = 15000;

            using (cancellationToken.Register(webRequest.Abort))
            {
                try
                {
                    using (var response =
                        (HttpWebResponse)await webRequest.GetResponseAsync())
                    using (var stream = response.GetResponseStream())
                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        var body = await reader.ReadToEndAsync();
                        var translated = ParseTranslatedText(body);
                        if (string.IsNullOrWhiteSpace(translated))
                        {
                            throw new InvalidOperationException(
                                "在线翻译服务没有返回译文。");
                        }
                        return translated.Trim();
                    }
                }
                catch (WebException ex)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        throw new OperationCanceledException(cancellationToken);
                    }
                    throw new InvalidOperationException(
                        "高质量翻译服务暂时不可用。", ex);
                }
            }
        }

        private string ParseTranslatedText(string body)
        {
            var root = _json.DeserializeObject(body) as object[];
            if (root == null || root.Length == 0)
            {
                return "";
            }

            var segments = root[0] as object[];
            if (segments == null)
            {
                return "";
            }

            var builder = new StringBuilder();
            foreach (var value in segments)
            {
                var segment = value as object[];
                if (segment == null || segment.Length == 0)
                {
                    continue;
                }
                builder.Append(Convert.ToString(segment[0]));
            }
            return WebUtility.HtmlDecode(builder.ToString());
        }
    }

    public abstract class ChunkedTranslationProvider : ITranslationProvider
    {
        public abstract string Name { get; }

        protected virtual int MaximumChunkLength
        {
            get { return 4500; }
        }

        public async Task<TranslationResult> TranslateAsync(
            TranslationRequest request,
            AppSettings settings,
            CancellationToken cancellationToken)
        {
            var watch = Stopwatch.StartNew();
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(request.Text))
            {
                throw new InvalidOperationException("没有识别到可翻译的文字。");
            }

            ValidateSettings(settings);
            var chunks = TranslationText.SplitContextChunks(
                TranslationText.CleanOcrText(request.Text),
                MaximumChunkLength);
            var translated = new List<string>();
            foreach (var chunk in chunks)
            {
                cancellationToken.ThrowIfCancellationRequested();
                translated.Add(await TranslateChunkAsync(
                    chunk,
                    request,
                    settings,
                    cancellationToken));
            }

            watch.Stop();
            return new TranslationResult
            {
                Text = string.Join("\n", translated.ToArray()).Trim(),
                Duration = watch.Elapsed,
                Provider = Name
            };
        }

        protected abstract void ValidateSettings(AppSettings settings);

        protected abstract Task<string> TranslateChunkAsync(
            string text,
            TranslationRequest request,
            AppSettings settings,
            CancellationToken cancellationToken);
    }

    public sealed class DeepLTranslationProvider : ChunkedTranslationProvider
    {
        private readonly JavaScriptSerializer _json =
            new JavaScriptSerializer();

        public override string Name { get { return "DeepL API"; } }

        protected override void ValidateSettings(AppSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.DeepLApiKey))
            {
                throw new InvalidOperationException(
                    "DeepL API 尚未填写密钥，请在“选择接口”中配置。");
            }
        }

        protected override async Task<string> TranslateChunkAsync(
            string text,
            TranslationRequest request,
            AppSettings settings,
            CancellationToken cancellationToken)
        {
            var url = settings.DeepLUseFreeApi
                ? "https://api-free.deepl.com/v2/translate"
                : "https://api.deepl.com/v2/translate";
            var values = new Dictionary<string, object>
            {
                { "text", new[] { text } },
                {
                    "target_lang",
                    TranslationLanguages.ToDeepLTargetCode(
                        request.TargetLanguage)
                }
            };
            if (!string.IsNullOrWhiteSpace(request.SourceLanguage)
                && !request.SourceLanguage.Equals(
                    "auto",
                    StringComparison.OrdinalIgnoreCase))
            {
                values["source_lang"] =
                    TranslationLanguages.ToDeepLSourceCode(
                        request.SourceLanguage);
            }
            var body = _json.Serialize(values);
            var headers = new Dictionary<string, string>
            {
                {
                    "Authorization",
                    "DeepL-Auth-Key " + settings.DeepLApiKey.Trim()
                }
            };
            var response = await TranslationWeb.PostJsonAsync(
                url,
                body,
                headers,
                "DeepL",
                cancellationToken);
            var root = _json.DeserializeObject(response)
                as Dictionary<string, object>;
            var translations = root != null && root.ContainsKey("translations")
                ? root["translations"] as object[]
                : null;
            var first = translations != null && translations.Length > 0
                ? translations[0] as Dictionary<string, object>
                : null;
            var result = first != null && first.ContainsKey("text")
                ? Convert.ToString(first["text"])
                : "";
            if (string.IsNullOrWhiteSpace(result))
            {
                throw new InvalidOperationException("DeepL 没有返回译文。");
            }
            return result.Trim();
        }
    }

    public sealed class MicrosoftTranslationProvider
        : ChunkedTranslationProvider
    {
        private readonly JavaScriptSerializer _json =
            new JavaScriptSerializer();

        public override string Name { get { return "Microsoft Translator"; } }

        protected override void ValidateSettings(AppSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.MicrosoftTranslatorKey))
            {
                throw new InvalidOperationException(
                    "Microsoft Translator 尚未填写密钥，请在“选择接口”中配置。");
            }
            Uri endpoint;
            if (!Uri.TryCreate(
                    settings.MicrosoftTranslatorEndpoint,
                    UriKind.Absolute,
                    out endpoint)
                || endpoint.Scheme != Uri.UriSchemeHttps)
            {
                throw new InvalidOperationException(
                    "Microsoft Translator 服务地址无效，请填写 HTTPS 地址。");
            }
        }

        protected override async Task<string> TranslateChunkAsync(
            string text,
            TranslationRequest request,
            AppSettings settings,
            CancellationToken cancellationToken)
        {
            var url = BuildTranslateUrl(
                    settings.MicrosoftTranslatorEndpoint)
                + "?api-version=3.0&to="
                + Uri.EscapeDataString(
                    TranslationLanguages.ToMicrosoftCode(
                        request.TargetLanguage));
            if (!string.IsNullOrWhiteSpace(request.SourceLanguage)
                && !request.SourceLanguage.Equals(
                    "auto",
                    StringComparison.OrdinalIgnoreCase))
            {
                url += "&from=" + Uri.EscapeDataString(
                    TranslationLanguages.ToMicrosoftCode(
                        request.SourceLanguage));
            }
            var body = _json.Serialize(new object[]
            {
                new Dictionary<string, object> { { "Text", text } }
            });
            var headers = new Dictionary<string, string>
            {
                {
                    "Ocp-Apim-Subscription-Key",
                    settings.MicrosoftTranslatorKey.Trim()
                }
            };
            if (!string.IsNullOrWhiteSpace(
                settings.MicrosoftTranslatorRegion))
            {
                headers["Ocp-Apim-Subscription-Region"] =
                    settings.MicrosoftTranslatorRegion.Trim();
            }

            var response = await TranslationWeb.PostJsonAsync(
                url,
                body,
                headers,
                "Microsoft Translator",
                cancellationToken);
            var root = _json.DeserializeObject(response) as object[];
            var first = root != null && root.Length > 0
                ? root[0] as Dictionary<string, object>
                : null;
            var translations = first != null
                && first.ContainsKey("translations")
                ? first["translations"] as object[]
                : null;
            var translation = translations != null
                && translations.Length > 0
                ? translations[0] as Dictionary<string, object>
                : null;
            var result = translation != null
                && translation.ContainsKey("text")
                ? Convert.ToString(translation["text"])
                : "";
            if (string.IsNullOrWhiteSpace(result))
            {
                throw new InvalidOperationException(
                    "Microsoft Translator 没有返回译文。");
            }
            return result.Trim();
        }

        public static string BuildTranslateUrl(string endpoint)
        {
            var value = (endpoint ?? "").Trim().TrimEnd('/');
            if (value.EndsWith(
                "/translate",
                StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
            if (value.EndsWith(
                "/translator/text/v3.0",
                StringComparison.OrdinalIgnoreCase))
            {
                return value + "/translate";
            }

            Uri uri;
            if (Uri.TryCreate(value, UriKind.Absolute, out uri)
                && (uri.Host.EndsWith(
                        ".cognitiveservices.azure.com",
                        StringComparison.OrdinalIgnoreCase)
                    || uri.Host.EndsWith(
                        ".cognitiveservices.azure.cn",
                        StringComparison.OrdinalIgnoreCase)))
            {
                return value + "/translator/text/v3.0/translate";
            }
            return value + "/translate";
        }
    }

    public sealed class LingvaTranslationProvider : ChunkedTranslationProvider
    {
        private readonly JavaScriptSerializer _json =
            new JavaScriptSerializer();

        public override string Name { get { return "Lingva Translate"; } }

        protected override int MaximumChunkLength
        {
            get { return 1200; }
        }

        protected override void ValidateSettings(AppSettings settings)
        {
            ValidateEndpoint(settings.LingvaEndpoint, "Lingva");
        }

        protected override async Task<string> TranslateChunkAsync(
            string text,
            TranslationRequest request,
            AppSettings settings,
            CancellationToken cancellationToken)
        {
            var url = settings.LingvaEndpoint.Trim().TrimEnd('/')
                + "/api/v1/"
                + (string.IsNullOrWhiteSpace(request.SourceLanguage)
                    || request.SourceLanguage.Equals(
                        "auto",
                        StringComparison.OrdinalIgnoreCase)
                    ? "auto"
                    : Uri.EscapeDataString(
                        TranslationLanguages.ToGoogleCode(
                            request.SourceLanguage)))
                + "/"
                + Uri.EscapeDataString(
                    TranslationLanguages.ToGoogleCode(
                        request.TargetLanguage))
                + "/"
                + Uri.EscapeDataString(text);
            var response = await TranslationWeb.GetJsonAsync(
                url,
                "Lingva",
                cancellationToken);
            var root = _json.DeserializeObject(response)
                as Dictionary<string, object>;
            var result = root != null && root.ContainsKey("translation")
                ? Convert.ToString(root["translation"])
                : "";
            if (string.IsNullOrWhiteSpace(result))
            {
                throw new InvalidOperationException(
                    "Lingva 没有返回译文，请尝试更换公共节点。");
            }
            return WebUtility.HtmlDecode(result).Trim();
        }

        private static void ValidateEndpoint(string value, string name)
        {
            Uri endpoint;
            if (string.IsNullOrWhiteSpace(value)
                || !Uri.TryCreate(value, UriKind.Absolute, out endpoint)
                || (endpoint.Scheme != Uri.UriSchemeHttp
                    && endpoint.Scheme != Uri.UriSchemeHttps))
            {
                throw new InvalidOperationException(
                    name + " 服务地址无效，请在“选择接口”中配置。");
            }
        }
    }

    public sealed class LibreTranslateProvider : ChunkedTranslationProvider
    {
        private readonly JavaScriptSerializer _json =
            new JavaScriptSerializer();
        private readonly bool _requiresApiKey;

        public LibreTranslateProvider(bool requiresApiKey)
        {
            _requiresApiKey = requiresApiKey;
        }

        public override string Name
        {
            get
            {
                return _requiresApiKey
                    ? "LibreTranslate API"
                    : "LibreTranslate 免密钥";
            }
        }

        protected override void ValidateSettings(AppSettings settings)
        {
            Uri endpoint;
            if (string.IsNullOrWhiteSpace(settings.LibreTranslateEndpoint)
                || !Uri.TryCreate(
                    settings.LibreTranslateEndpoint,
                    UriKind.Absolute,
                    out endpoint)
                || (endpoint.Scheme != Uri.UriSchemeHttp
                    && endpoint.Scheme != Uri.UriSchemeHttps))
            {
                throw new InvalidOperationException(
                    "LibreTranslate 服务地址无效，请在“选择接口”中配置。");
            }
            if (_requiresApiKey
                && string.IsNullOrWhiteSpace(
                    settings.LibreTranslateApiKey))
            {
                throw new InvalidOperationException(
                    "LibreTranslate API 尚未填写密钥，请在“选择接口”中配置。");
            }
        }

        protected override async Task<string> TranslateChunkAsync(
            string text,
            TranslationRequest request,
            AppSettings settings,
            CancellationToken cancellationToken)
        {
            var url = settings.LibreTranslateEndpoint.Trim().TrimEnd('/');
            if (!url.EndsWith(
                "/translate",
                StringComparison.OrdinalIgnoreCase))
            {
                url += "/translate";
            }
            var values = new Dictionary<string, object>
            {
                { "q", text },
                {
                    "source",
                    string.IsNullOrWhiteSpace(request.SourceLanguage)
                        || request.SourceLanguage.Equals(
                            "auto",
                            StringComparison.OrdinalIgnoreCase)
                        ? "auto"
                        : TranslationLanguages.ToLibreCode(
                            request.SourceLanguage)
                },
                {
                    "target",
                    TranslationLanguages.ToLibreCode(
                        request.TargetLanguage)
                },
                { "format", "text" }
            };
            if (_requiresApiKey)
            {
                values["api_key"] = settings.LibreTranslateApiKey.Trim();
            }

            var response = await TranslationWeb.PostJsonAsync(
                url,
                _json.Serialize(values),
                null,
                "LibreTranslate",
                cancellationToken);
            var root = _json.DeserializeObject(response)
                as Dictionary<string, object>;
            var result = root != null && root.ContainsKey("translatedText")
                ? Convert.ToString(root["translatedText"])
                : "";
            if (string.IsNullOrWhiteSpace(result))
            {
                throw new InvalidOperationException(
                    "LibreTranslate 没有返回译文。");
            }
            return WebUtility.HtmlDecode(result).Trim();
        }
    }

    internal static class TranslationWeb
    {
        internal static async Task<string> GetJsonAsync(
            string url,
            string serviceName,
            CancellationToken cancellationToken)
        {
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.Accept = "application/json";
            request.UserAgent = "GameTranslator/1.2";
            request.Timeout = 20000;
            request.ReadWriteTimeout = 20000;
            using (cancellationToken.Register(request.Abort))
            {
                try
                {
                    using (var response =
                        (HttpWebResponse)await request.GetResponseAsync())
                    using (var stream = response.GetResponseStream())
                    using (var reader = new StreamReader(
                        stream,
                        Encoding.UTF8))
                    {
                        return await reader.ReadToEndAsync();
                    }
                }
                catch (WebException ex)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        throw new OperationCanceledException(cancellationToken);
                    }
                    var response = ex.Response as HttpWebResponse;
                    var status = response == null
                        ? ""
                        : "（HTTP " + (int)response.StatusCode + "）";
                    throw new InvalidOperationException(
                        serviceName + " 请求失败" + status
                        + "，请更换节点或检查网络。",
                        ex);
                }
            }
        }

        internal static async Task<string> PostJsonAsync(
            string url,
            string json,
            IDictionary<string, string> headers,
            string serviceName,
            CancellationToken cancellationToken)
        {
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "POST";
            request.Accept = "application/json";
            request.ContentType = "application/json; charset=UTF-8";
            request.UserAgent = "GameTranslator/1.2";
            request.Timeout = 20000;
            request.ReadWriteTimeout = 20000;
            if (headers != null)
            {
                foreach (var header in headers)
                {
                    request.Headers[header.Key] = header.Value;
                }
            }

            var payload = Encoding.UTF8.GetBytes(json);
            request.ContentLength = payload.Length;
            using (cancellationToken.Register(request.Abort))
            {
                try
                {
                    using (var requestStream =
                        await request.GetRequestStreamAsync())
                    {
                        await requestStream.WriteAsync(
                            payload,
                            0,
                            payload.Length,
                            cancellationToken);
                    }
                    using (var response =
                        (HttpWebResponse)await request.GetResponseAsync())
                    using (var stream = response.GetResponseStream())
                    using (var reader = new StreamReader(
                        stream,
                        Encoding.UTF8))
                    {
                        return await reader.ReadToEndAsync();
                    }
                }
                catch (WebException ex)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        throw new OperationCanceledException(cancellationToken);
                    }
                    var response = ex.Response as HttpWebResponse;
                    var status = response == null
                        ? ""
                        : "（HTTP " + (int)response.StatusCode + "）";
                    throw new InvalidOperationException(
                        serviceName + " 请求失败" + status
                        + "，请检查接口设置和网络。",
                        ex);
                }
            }
        }
    }

    public sealed class MyMemoryTranslationProvider : ITranslationProvider
    {
        private readonly JavaScriptSerializer _json = new JavaScriptSerializer();

        public string Name { get { return "MyMemory 免费翻译"; } }

        public async Task<TranslationResult> TranslateAsync(
            TranslationRequest request,
            AppSettings settings,
            CancellationToken cancellationToken)
        {
            var watch = Stopwatch.StartNew();
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(request.Text))
            {
                throw new InvalidOperationException("没有识别到可翻译的文字。");
            }

            var sourceLanguage = string.IsNullOrWhiteSpace(
                request.SourceLanguage)
                || request.SourceLanguage.Equals(
                    "auto",
                    StringComparison.OrdinalIgnoreCase)
                ? TranslationText.DetectSourceLanguage(request.Text)
                : TranslationLanguages.ToMyMemoryCode(
                    request.SourceLanguage);
            var targetLanguage = TranslationLanguages.ToMyMemoryCode(
                request.TargetLanguage);
            var chunks = TranslationText.SplitContextChunks(
                TranslationText.CleanOcrText(request.Text),
                440);
            var translated = new List<string>();
            foreach (var chunk in chunks)
            {
                cancellationToken.ThrowIfCancellationRequested();
                translated.Add(await TranslateChunkAsync(
                    chunk,
                    sourceLanguage,
                    targetLanguage,
                    cancellationToken));
            }

            watch.Stop();
            return new TranslationResult
            {
                Text = string.Join("\n", translated.ToArray()).Trim(),
                Duration = watch.Elapsed,
                Provider = Name
            };
        }

        private async Task<string> TranslateChunkAsync(
            string text,
            string sourceLanguage,
            string targetLanguage,
            CancellationToken cancellationToken)
        {
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
            var url = "https://api.mymemory.translated.net/get?q="
                + Uri.EscapeDataString(text)
                + "&langpair=" + Uri.EscapeDataString(
                    sourceLanguage + "|" + targetLanguage)
                + "&mt=1";
            var webRequest = (HttpWebRequest)WebRequest.Create(url);
            webRequest.Method = "GET";
            webRequest.Accept = "application/json";
            webRequest.UserAgent = "GameTranslator/1.0";
            webRequest.Timeout = 15000;
            webRequest.ReadWriteTimeout = 15000;

            using (cancellationToken.Register(webRequest.Abort))
            {
                try
                {
                    using (var response =
                        (HttpWebResponse)await webRequest.GetResponseAsync())
                    using (var stream = response.GetResponseStream())
                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        var body = await reader.ReadToEndAsync();
                        var root = _json.DeserializeObject(body)
                            as Dictionary<string, object>;
                        if (root == null || !root.ContainsKey("responseData"))
                        {
                            throw new InvalidOperationException(
                                "免费翻译服务返回了无法识别的数据。");
                        }

                        var responseData = root["responseData"]
                            as Dictionary<string, object>;
                        if (responseData == null
                            || !responseData.ContainsKey("translatedText"))
                        {
                            throw new InvalidOperationException(
                                "免费翻译服务没有返回译文。");
                        }

                        var result = Convert.ToString(
                            responseData["translatedText"]);
                        return WebUtility.HtmlDecode(result).Trim();
                    }
                }
                catch (WebException ex)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        throw new OperationCanceledException(cancellationToken);
                    }

                    var response = ex.Response as HttpWebResponse;
                    if (response == null)
                    {
                        throw new InvalidOperationException(
                            "无法连接免费翻译服务，请检查网络。", ex);
                    }

                    using (response)
                    using (var stream = response.GetResponseStream())
                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        var errorText = reader.ReadToEnd();
                        throw new InvalidOperationException(
                            "免费翻译服务请求失败 ("
                            + (int)response.StatusCode + ")：" + errorText);
                    }
                }
            }
        }

    }

    internal static class TranslationText
    {
        public static string DetectSourceLanguage(string text)
        {
            var hangul = 0;
            var kana = 0;
            var han = 0;
            var cyrillic = 0;
            foreach (var character in text)
            {
                if ((character >= '\u3040' && character <= '\u30ff')
                    || (character >= '\u31f0' && character <= '\u31ff'))
                {
                    kana++;
                }
                if ((character >= '\u1100' && character <= '\u11ff')
                    || (character >= '\u3130' && character <= '\u318f')
                    || (character >= '\uac00' && character <= '\ud7af'))
                {
                    hangul++;
                }
                if (character >= '\u4e00' && character <= '\u9fff')
                {
                    han++;
                }
                if (character >= '\u0400' && character <= '\u04ff')
                {
                    cyrillic++;
                }
            }
            if (kana > 0 && kana >= hangul)
            {
                return "ja";
            }
            if (hangul > 0)
            {
                return "ko";
            }
            if (han > 0)
            {
                return "zh-CN";
            }
            if (cyrillic > 0)
            {
                return "ru";
            }

            var normalized = NormalizeWords(text);
            var bestLanguage = "en";
            var bestScore = ScoreLanguage(
                normalized,
                new[] { "the", "and", "is", "are", "to", "of", "in", "with" });
            var candidates = new[]
            {
                new
                {
                    Code = "fr",
                    Score = ScoreLanguage(
                        normalized,
                        new[] { "le", "la", "les", "des", "une", "est", "pas", "avec" })
                        + ScoreCharacters(text, "éèêàâçù")
                },
                new
                {
                    Code = "de",
                    Score = ScoreLanguage(
                        normalized,
                        new[] { "der", "die", "das", "und", "ist", "nicht", "ein", "mit" })
                        + ScoreCharacters(text, "äöüß")
                },
                new
                {
                    Code = "es",
                    Score = ScoreLanguage(
                        normalized,
                        new[] { "el", "la", "los", "las", "que", "una", "para", "con" })
                        + ScoreCharacters(text, "áéíóúñ¿¡")
                },
                new
                {
                    Code = "pt-BR",
                    Score = ScoreLanguage(
                        normalized,
                        new[] { "o", "a", "os", "as", "que", "uma", "para", "com" })
                        + ScoreCharacters(text, "ãõçáéíóú")
                }
            };
            foreach (var candidate in candidates)
            {
                if (candidate.Score > bestScore)
                {
                    bestScore = candidate.Score;
                    bestLanguage = candidate.Code;
                }
            }
            return bestLanguage;
        }

        private static string NormalizeWords(string text)
        {
            var builder = new StringBuilder();
            foreach (var character in (text ?? "").ToLowerInvariant())
            {
                builder.Append(char.IsLetter(character) ? character : ' ');
            }
            return " " + builder.ToString() + " ";
        }

        private static int ScoreLanguage(string text, string[] words)
        {
            var score = 0;
            foreach (var word in words)
            {
                var token = " " + word + " ";
                var index = 0;
                while ((index = text.IndexOf(
                    token,
                    index,
                    StringComparison.Ordinal)) >= 0)
                {
                    score++;
                    index += token.Length;
                }
            }
            return score;
        }

        private static int ScoreCharacters(string text, string characters)
        {
            var score = 0;
            foreach (var character in (text ?? "").ToLowerInvariant())
            {
                if (characters.IndexOf(character) >= 0)
                {
                    score += 2;
                }
            }
            return score;
        }

        public static string CleanOcrText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return "";
            }

            var normalized = text
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Replace('\u00a0', ' ');
            var lines = normalized.Split(
                new[] { '\n' },
                StringSplitOptions.None);
            for (var index = 0; index < lines.Length; index++)
            {
                var line = lines[index].Trim();
                while (line.Contains("  "))
                {
                    line = line.Replace("  ", " ");
                }
                lines[index] = line;
            }
            return string.Join("\n", lines).Trim();
        }

        public static List<string> SplitContextChunks(string text, int maxBytes)
        {
            if (maxBytes < 4)
            {
                throw new ArgumentOutOfRangeException(
                    "maxBytes",
                    "分段长度至少需要容纳一个 UTF-8 字符。");
            }

            var result = new List<string>();
            var normalized = CleanOcrText(text);
            var remaining = normalized;
            while (Encoding.UTF8.GetByteCount(remaining) > maxBytes)
            {
                var byteCount = 0;
                var limit = 0;
                while (limit < remaining.Length)
                {
                    var characterLength =
                        char.IsHighSurrogate(remaining[limit])
                        && limit + 1 < remaining.Length
                        && char.IsLowSurrogate(remaining[limit + 1])
                            ? 2
                            : 1;
                    var characterBytes = Encoding.UTF8.GetByteCount(
                        remaining.Substring(limit, characterLength));
                    if (byteCount + characterBytes > maxBytes)
                    {
                        break;
                    }
                    byteCount += characterBytes;
                    limit += characterLength;
                }

                var split = FindNaturalSplit(remaining, limit);
                if (split <= 0)
                {
                    split = Math.Max(1, limit);
                }
                var chunk = remaining.Substring(0, split).Trim();
                if (chunk.Length > 0)
                {
                    result.Add(chunk);
                }
                remaining = remaining.Substring(split).Trim();
            }

            if (remaining.Length > 0)
            {
                result.Add(remaining);
            }
            if (result.Count == 0)
            {
                result.Add(text.Trim());
            }
            return result;
        }

        private static int FindNaturalSplit(string text, int limit)
        {
            var separators = new[]
            {
                '\n', '。', '！', '？', '.', '!', '?', ';', '；', ' '
            };
            for (var index = Math.Min(limit, text.Length - 1);
                index > Math.Max(0, limit / 2);
                index--)
            {
                foreach (var separator in separators)
                {
                    if (text[index] == separator)
                    {
                        return index + 1;
                    }
                }
            }
            return limit;
        }
    }
}
