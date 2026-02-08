using System.Diagnostics;
using System.Text.RegularExpressions;
using ICSharpCode.AvalonEdit.Document;

namespace TopMemo.App.Services;

/// <summary>
/// エディタクリック位置からリンク遷移を行うサービスです。
/// </summary>
public sealed partial class LinkNavigationService
{
    /// <summary>
    /// 指定オフセット位置のリンクを開きます。
    /// </summary>
    /// <param name="document">ドキュメント。</param>
    /// <param name="offset">クリック位置オフセット。</param>
    /// <returns>リンク遷移できた場合は true。</returns>
    public bool TryOpenLink(TextDocument document, int offset)
    {
        // オフセット範囲外は無効です。
        if (offset < 0 || offset >= document.TextLength)
        {
            return false;
        }

        // 対象行を取り出します。
        var line = document.GetLineByOffset(offset);
        var lineText = document.GetText(line.Offset, line.Length);
        var column = offset - line.Offset;

        // Markdown リンク形式を優先して判定します。
        var markdownUrl = TryResolveUrlFromRegex(lineText, column, MarkdownLinkRegex(), groupIndex: 1);
        if (!string.IsNullOrWhiteSpace(markdownUrl))
        {
            return OpenUrl(markdownUrl);
        }

        // 裸 URL 形式を判定します。
        var plainUrl = TryResolveUrlFromRegex(lineText, column, PlainUrlRegex(), groupIndex: 0);
        if (!string.IsNullOrWhiteSpace(plainUrl))
        {
            return OpenUrl(plainUrl);
        }

        return false;
    }

    /// <summary>
    /// マッチ位置に該当する URL を抽出します。
    /// </summary>
    /// <param name="lineText">行文字列。</param>
    /// <param name="column">列位置。</param>
    /// <param name="regex">判定用正規表現。</param>
    /// <param name="groupIndex">URL 抽出グループ。</param>
    /// <returns>URL。見つからない場合は null。</returns>
    private static string? TryResolveUrlFromRegex(string lineText, int column, Regex regex, int groupIndex)
    {
        // 行内のすべてのマッチを探索します。
        foreach (Match match in regex.Matches(lineText))
        {
            if (!match.Success || match.Length <= 0)
            {
                continue;
            }

            // クリック位置を含むマッチのみ採用します。
            var start = match.Index;
            var end = start + match.Length;
            if (column < start || column > end)
            {
                continue;
            }

            // URL グループを返します。
            return groupIndex == 0 ? match.Value : match.Groups[groupIndex].Value;
        }

        return null;
    }

    /// <summary>
    /// URL を既定ブラウザで開きます。
    /// </summary>
    /// <param name="url">URL。</param>
    /// <returns>起動に成功した場合は true。</returns>
    private static bool OpenUrl(string url)
    {
        try
        {
            // OS 既定アプリで URL を開きます。
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
            return true;
        }
        catch
        {
            // 起動失敗時は false を返します。
            return false;
        }
    }

    /// <summary>
    /// Markdown リンク判定正規表現です。
    /// </summary>
    /// <returns>正規表現。</returns>
    [GeneratedRegex(@"\[[^\]]+\]\(([^)\s]+)\)", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex MarkdownLinkRegex();

    /// <summary>
    /// 裸 URL 判定正規表現です。
    /// </summary>
    /// <returns>正規表現。</returns>
    [GeneratedRegex(@"https?://[^\s)]+", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex PlainUrlRegex();
}

