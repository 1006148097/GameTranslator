using System;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GameTranslator;

internal static class OcrCandidateSmoke
{
    private static int Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.Error.WriteLine("Usage: OcrCandidateSmoke <image-path>");
            return 2;
        }

        Console.OutputEncoding = new UTF8Encoding(false);
        var type = typeof(WindowsOcrEngine);
        var find = type.GetMethod(
            "FindTesseract",
            BindingFlags.Static | BindingFlags.NonPublic);
        var prepare = type.GetMethod(
            "CreateTesseractInput",
            BindingFlags.Static | BindingFlags.NonPublic);
        var run = type.GetMethod(
            "RunTesseractAsync",
            BindingFlags.Static | BindingFlags.NonPublic);
        var normalize = type.GetMethod(
            "NormalizeOcrLines",
            BindingFlags.Static | BindingFlags.NonPublic);
        var score = type.GetMethod(
            "ScoreKoreanCandidate",
            BindingFlags.Static | BindingFlags.NonPublic);

        var executable = Convert.ToString(find.Invoke(null, null));
        var prepared = Convert.ToString(
            prepare.Invoke(null, new object[] { args[0] }));
        foreach (var mode in new[] { 3, 4, 6, 7, 11 })
        {
            var task = (Task<string>)run.Invoke(
                null,
                new object[]
                {
                    executable,
                    prepared,
                    mode,
                    CancellationToken.None
                });
            var raw = task.GetAwaiter().GetResult();
            var text = Convert.ToString(
                normalize.Invoke(null, new object[] { raw }));
            var value = Convert.ToInt32(
                score.Invoke(null, new object[] { text }));
            Console.WriteLine("PSM=" + mode + " SCORE=" + value);
            Console.WriteLine(text);
            Console.WriteLine("---");
        }
        return 0;
    }
}
