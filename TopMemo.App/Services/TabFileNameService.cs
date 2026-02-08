using System.IO;
using System.Text;
using TopMemo.App.ViewModels;

namespace TopMemo.App.Services;

/// <summary>
/// タブ名から保存ファイル名を生成するサービスです。
/// </summary>
public sealed class TabFileNameService
{
    private readonly JsonStorageService _jsonStorageService;

    /// <summary>
    /// 初期化します。
    /// </summary>
    /// <param name="jsonStorageService">保存サービス。</param>
    public TabFileNameService(JsonStorageService jsonStorageService)
    {
        // 依存関係を保持します。
        _jsonStorageService = jsonStorageService;
    }

    /// <summary>
    /// タイトルから競合しないファイル名を作成します。
    /// </summary>
    /// <param name="title">タブタイトル。</param>
    /// <param name="existingTabs">既存タブ一覧。</param>
    /// <param name="ignoreTabId">除外するタブ ID。</param>
    /// <returns>一意な .md ファイル名。</returns>
    public string BuildUniqueFileName(string title, IEnumerable<MemoTabViewModel> existingTabs, string? ignoreTabId = null)
    {
        // タイトルをファイル名へ正規化します。
        var baseName = SanitizeTitle(title);
        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = "memo";
        }

        // 重複しない候補を順に探索します。
        var index = 1;
        while (true)
        {
            var candidate = index == 1 ? $"{baseName}.md" : $"{baseName} ({index}).md";
            var isUsedByTabs = existingTabs.Any(tab =>
                !string.Equals(tab.Id, ignoreTabId, StringComparison.Ordinal) &&
                string.Equals(tab.FileName, candidate, StringComparison.OrdinalIgnoreCase));

            // タブ利用中でなくファイルも未存在なら確定します。
            if (!isUsedByTabs && !_jsonStorageService.MemoExists(candidate))
            {
                return candidate;
            }

            index++;
        }
    }

    /// <summary>
    /// タイトルを安全なファイル名文字列へ変換します。
    /// </summary>
    /// <param name="title">タブタイトル。</param>
    /// <returns>正規化済み名称。</returns>
    public static string SanitizeTitle(string title)
    {
        // 入力が空なら既定値を返します。
        if (string.IsNullOrWhiteSpace(title))
        {
            return "memo";
        }

        // 禁止文字を '_' へ置換します。
        var invalidChars = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(title.Trim());
        for (var i = 0; i < builder.Length; i++)
        {
            if (invalidChars.Contains(builder[i]))
            {
                builder[i] = '_';
            }
        }

        // 空白のみになるケースを既定値へ丸めます。
        var sanitized = builder.ToString().Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "memo" : sanitized;
    }
}

