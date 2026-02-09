using System.IO;
using System.Text;
using TopMemo.App.ViewModels;

namespace TopMemo.App.Services;

/// <summary>
/// タブ名入力から保存ファイル名を扱うサービスです。
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
    /// 競合しないファイル名を作成します。
    /// </summary>
    /// <param name="input">入力名。</param>
    /// <param name="existingTabs">既存タブ一覧。</param>
    /// <returns>一意な .md ファイル名。</returns>
    public string BuildUniqueFileName(string input, IEnumerable<MemoTabViewModel> existingTabs)
    {
        // 入力を .md ファイル名へ正規化します。
        var normalized = NormalizeFileName(input);
        var baseName = Path.GetFileNameWithoutExtension(normalized);

        // 重複しない候補を順に探索します。
        var index = 1;
        while (true)
        {
            var candidate = index == 1 ? $"{baseName}.md" : $"{baseName} ({index}).md";
            var isUsedByTabs = existingTabs.Any(tab => string.Equals(Path.GetFileName(tab.FileName), candidate, StringComparison.OrdinalIgnoreCase));

            // タブ利用中でなくファイルも未存在なら確定します。
            if (!isUsedByTabs && !_jsonStorageService.MemoExists(candidate))
            {
                return candidate;
            }

            index++;
        }
    }

    /// <summary>
    /// 入力文字列を正規化して .md ファイル名へ変換します。
    /// </summary>
    /// <param name="input">入力文字列。</param>
    /// <returns>正規化済みファイル名。</returns>
    public static string NormalizeFileName(string input)
    {
        // 入力が空なら既定ファイル名を返します。
        if (string.IsNullOrWhiteSpace(input))
        {
            return "memo.md";
        }

        // 入力をファイル名本体として受け取り、禁止文字を '_' へ置換します。
        var invalidChars = Path.GetInvalidFileNameChars();
        var rawName = Path.GetFileNameWithoutExtension(input.Trim());
        var builder = new StringBuilder(rawName);
        for (var i = 0; i < builder.Length; i++)
        {
            if (invalidChars.Contains(builder[i]))
            {
                builder[i] = '_';
            }
        }

        // 空白のみになるケースを既定値へ丸めます。
        var normalizedBase = builder.ToString().Trim();
        if (string.IsNullOrWhiteSpace(normalizedBase))
        {
            normalizedBase = "memo";
        }

        // 最終的に .md 拡張子を付与します。
        return $"{normalizedBase}.md";
    }

    /// <summary>
    /// 指定ファイル名が既存タブと重複しているか判定します。
    /// </summary>
    /// <param name="fileName">判定対象ファイル名。</param>
    /// <param name="existingTabs">既存タブ一覧。</param>
    /// <param name="ignoreTabId">判定除外するタブ ID。</param>
    /// <returns>重複する場合は true。</returns>
    public static bool IsDuplicatedFileName(string fileName, IEnumerable<MemoTabViewModel> existingTabs, string? ignoreTabId = null)
    {
        // 同一タブを除外して重複名を判定します。
        return existingTabs.Any(tab =>
            !string.Equals(tab.Id, ignoreTabId, StringComparison.Ordinal) &&
            string.Equals(Path.GetFileName(tab.FileName), fileName, StringComparison.OrdinalIgnoreCase));
    }
}
