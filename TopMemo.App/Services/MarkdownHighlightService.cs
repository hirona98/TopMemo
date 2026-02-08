using System.Text.RegularExpressions;
using System.Windows;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Rendering;
using MediaBrush = System.Windows.Media.Brush;
using MediaColor = System.Windows.Media.Color;
using MediaSolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace TopMemo.App.Services;

/// <summary>
/// Markdown 色付けをエディタへ適用するサービスです。
/// </summary>
public sealed class MarkdownHighlightService
{
    private readonly MarkdownColorizer _markdownColorizer = new();

    /// <summary>
    /// 色付けを適用します。
    /// </summary>
    /// <param name="textEditor">対象エディタ。</param>
    public void Apply(TextEditor textEditor)
    {
        // 既存の色付け設定をクリアして再適用します。
        textEditor.TextArea.TextView.LineTransformers.Clear();

        // 組み込み Markdown があれば優先して使います。
        var markdownDefinition = HighlightingManager.Instance.GetDefinition("Markdown");
        if (markdownDefinition is not null)
        {
            textEditor.SyntaxHighlighting = markdownDefinition;
            return;
        }

        // 組み込み定義が無い場合は簡易色付けを適用します。
        textEditor.SyntaxHighlighting = null;
        textEditor.TextArea.TextView.LineTransformers.Add(_markdownColorizer);
    }
}

/// <summary>
/// Markdown の簡易色付けを行う変換クラスです。
/// </summary>
public sealed partial class MarkdownColorizer : DocumentColorizingTransformer
{
    private static readonly MediaBrush HeadingBrush = new MediaSolidColorBrush(MediaColor.FromRgb(23, 84, 179));
    private static readonly MediaBrush ListBrush = new MediaSolidColorBrush(MediaColor.FromRgb(18, 117, 66));
    private static readonly MediaBrush CodeBrush = new MediaSolidColorBrush(MediaColor.FromRgb(132, 56, 15));
    private static readonly MediaBrush EmphasisBrush = new MediaSolidColorBrush(MediaColor.FromRgb(146, 26, 130));
    private static readonly MediaBrush LinkBrush = new MediaSolidColorBrush(MediaColor.FromRgb(0, 102, 204));

    /// <summary>
    /// 行単位の色付け処理を行います。
    /// </summary>
    /// <param name="line">対象行。</param>
    protected override void ColorizeLine(DocumentLine line)
    {
        // 行文字列を取得します。
        var lineText = CurrentContext.Document.GetText(line);
        if (string.IsNullOrEmpty(lineText))
        {
            return;
        }

        // 行頭ルールを先に適用します。
        if (lineText.StartsWith("#", StringComparison.Ordinal))
        {
            ApplyColor(line, 0, lineText.Length, HeadingBrush);
        }
        else if (ListMarkerRegex().IsMatch(lineText))
        {
            ApplyColor(line, 0, Math.Min(lineText.Length, 2), ListBrush);
        }
        else if (lineText.TrimStart().StartsWith("```", StringComparison.Ordinal))
        {
            ApplyColor(line, 0, lineText.Length, CodeBrush);
        }

        // インライン要素を順に着色します。
        ApplyMatches(line, lineText, InlineCodeRegex(), CodeBrush);
        ApplyMatches(line, lineText, BoldRegex(), EmphasisBrush);
        ApplyMatches(line, lineText, LinkRegex(), LinkBrush, underline: true);
    }

    /// <summary>
    /// 正規表現マッチ部分に色を適用します。
    /// </summary>
    /// <param name="line">対象行。</param>
    /// <param name="lineText">行文字列。</param>
    /// <param name="regex">正規表現。</param>
    /// <param name="brush">前景色。</param>
    /// <param name="weight">フォントウェイト。</param>
    /// <param name="underline">下線有無。</param>
    private void ApplyMatches(
        DocumentLine line,
        string lineText,
        Regex regex,
        MediaBrush brush,
        bool underline = false)
    {
        // 該当するすべてのマッチへ色を適用します。
        foreach (Match match in regex.Matches(lineText))
        {
            if (!match.Success || match.Length <= 0)
            {
                continue;
            }

            ApplyColor(line, match.Index, match.Length, brush, underline);
        }
    }

    /// <summary>
    /// 指定範囲へ色とフォント情報を適用します。
    /// </summary>
    /// <param name="line">対象行。</param>
    /// <param name="index">行内開始位置。</param>
    /// <param name="length">長さ。</param>
    /// <param name="brush">前景色。</param>
    /// <param name="weight">ウェイト。</param>
    /// <param name="underline">下線有無。</param>
    private void ApplyColor(
        DocumentLine line,
        int index,
        int length,
        MediaBrush brush,
        bool underline = false)
    {
        // 行オフセットをドキュメントオフセットへ変換します。
        var start = line.Offset + index;
        var end = start + length;

        // 描画属性を指定範囲に適用します。
        ChangeLinePart(start, end, element =>
        {
            element.TextRunProperties.SetForegroundBrush(brush);
            if (underline)
            {
                element.TextRunProperties.SetTextDecorations(TextDecorations.Underline);
            }
        });
    }

    /// <summary>
    /// 箇条書きマーカーを判定する正規表現です。
    /// </summary>
    /// <returns>正規表現。</returns>
    [GeneratedRegex(@"^\s*[-*+]\s", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex ListMarkerRegex();

    /// <summary>
    /// インラインコードを判定する正規表現です。
    /// </summary>
    /// <returns>正規表現。</returns>
    [GeneratedRegex(@"`[^`]+`", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex InlineCodeRegex();

    /// <summary>
    /// 太字を判定する正規表現です。
    /// </summary>
    /// <returns>正規表現。</returns>
    [GeneratedRegex(@"\*\*[^*]+\*\*", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex BoldRegex();

    /// <summary>
    /// Markdown リンクを判定する正規表現です。
    /// </summary>
    /// <returns>正規表現。</returns>
    [GeneratedRegex(@"\[[^\]]+\]\(([^)\s]+)\)", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex LinkRegex();
}
