using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace FieldCure.Tools.AudioBenchmark;

/// <summary>
/// Computes Word Error Rate and Character Error Rate using Levenshtein
/// distance on normalized text.
/// </summary>
/// <remarks>
/// <para>Normalization rules applied to both reference and hypothesis before
/// comparison: Unicode NFC, lowercase, strip punctuation (keeping letters,
/// digits, and whitespace), collapse whitespace. Korean text is compared at
/// character level (CER) which is more meaningful than word-level for
/// non-space-delimited scripts.</para>
/// <para>WER/CER values exceeding 1.0 are possible when the hypothesis is
/// significantly longer than the reference (e.g. hallucinated repetition).
/// Raw ratios are returned uncapped — that is more informative for
/// diagnostics than a clamped value.</para>
/// </remarks>
internal static partial class TranscriptionAccuracy
{
    /// <summary>
    /// Computes Word Error Rate. Returns 0.0 for an empty reference (caller
    /// should treat that case as undefined and prefer skipping the metric).
    /// </summary>
    public static double ComputeWer(string reference, string hypothesis)
    {
        var refTokens = Tokenize(Normalize(reference));
        var hypTokens = Tokenize(Normalize(hypothesis));
        if (refTokens.Length == 0) return 0.0;
        var distance = LevenshteinDistance(refTokens, hypTokens);
        return (double)distance / refTokens.Length;
    }

    /// <summary>
    /// Computes Character Error Rate. Operates on individual characters of
    /// the normalized text — whitespace is excluded so spacing differences
    /// don't dominate Korean CER.
    /// </summary>
    public static double ComputeCer(string reference, string hypothesis)
    {
        var refChars = StripWhitespace(Normalize(reference));
        var hypChars = StripWhitespace(Normalize(hypothesis));
        if (refChars.Length == 0) return 0.0;
        var distance = LevenshteinDistance(refChars, hypChars);
        return (double)distance / refChars.Length;
    }

    /// <summary>
    /// Returns the number of whitespace-delimited tokens in the normalized text.
    /// </summary>
    public static int CountWords(string text)
        => Tokenize(Normalize(text)).Length;

    /// <summary>
    /// Applies the shared normalization pipeline used by both WER and CER.
    /// </summary>
    private static string Normalize(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        // NFC: combine compatible decomposed forms so "한" and "ㅎ+ㅏ+ㄴ" compare equal.
        var nfc = text.Normalize(NormalizationForm.FormC);

        // Lowercase under invariant culture so Turkish dotted-i etc. don't surprise.
        var lower = nfc.ToLowerInvariant();

        // Strip everything that isn't a letter, digit, or whitespace. This drops
        // ASCII punctuation, Hangul punctuation, fullwidth punctuation, and emoji.
        // Letters/digits include Hangul, Latin, Cyrillic, etc.
        var sb = new StringBuilder(lower.Length);
        foreach (var ch in lower)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category is UnicodeCategory.LowercaseLetter
                or UnicodeCategory.UppercaseLetter
                or UnicodeCategory.OtherLetter
                or UnicodeCategory.ModifierLetter
                or UnicodeCategory.TitlecaseLetter
                or UnicodeCategory.DecimalDigitNumber
                or UnicodeCategory.LetterNumber
                or UnicodeCategory.OtherNumber
                or UnicodeCategory.SpaceSeparator)
            {
                sb.Append(ch);
            }
            else if (ch == '\t' || ch == '\n' || ch == '\r')
            {
                sb.Append(' ');
            }
        }

        // Collapse runs of whitespace to single spaces and trim.
        return WhitespaceRun().Replace(sb.ToString(), " ").Trim();
    }

    private static string[] Tokenize(string normalized)
        => string.IsNullOrEmpty(normalized)
            ? Array.Empty<string>()
            : normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    private static char[] StripWhitespace(string normalized)
    {
        if (string.IsNullOrEmpty(normalized)) return Array.Empty<char>();
        var sb = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
            if (ch != ' ') sb.Append(ch);
        var result = new char[sb.Length];
        sb.CopyTo(0, result, 0, sb.Length);
        return result;
    }

    /// <summary>
    /// Standard Levenshtein DP using two rolling rows. O(m*n) time,
    /// O(min(m,n)) memory. Operates on any IReadOnlyList of equatable items
    /// (string for WER tokens, char for CER characters).
    /// </summary>
    private static int LevenshteinDistance<T>(IReadOnlyList<T> source, IReadOnlyList<T> target)
        where T : IEquatable<T>
    {
        if (source.Count == 0) return target.Count;
        if (target.Count == 0) return source.Count;

        // Always iterate the longer sequence outside, shorter inside, so the
        // rolling rows are sized by the shorter one.
        IReadOnlyList<T> outer = source, inner = target;
        if (target.Count < source.Count)
        {
            outer = target;
            inner = source;
        }

        var prev = new int[inner.Count + 1];
        var curr = new int[inner.Count + 1];
        for (var j = 0; j <= inner.Count; j++) prev[j] = j;

        for (var i = 1; i <= outer.Count; i++)
        {
            curr[0] = i;
            for (var j = 1; j <= inner.Count; j++)
            {
                var cost = outer[i - 1].Equals(inner[j - 1]) ? 0 : 1;
                curr[j] = Math.Min(
                    Math.Min(curr[j - 1] + 1, prev[j] + 1),
                    prev[j - 1] + cost);
            }
            (prev, curr) = (curr, prev);
        }

        return prev[inner.Count];
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRun();
}
