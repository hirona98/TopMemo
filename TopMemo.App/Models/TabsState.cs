namespace TopMemo.App.Models;

/// <summary>
/// タブ永続化データです。
/// </summary>
public sealed class TabsState
{
    /// <summary>
    /// アクティブタブ ID を取得または設定します。
    /// </summary>
    public string ActiveTabId { get; set; } = "tab-1";

    /// <summary>
    /// タブ定義一覧を取得または設定します。
    /// </summary>
    public List<TabDefinition> Tabs { get; set; } = [];

    /// <summary>
    /// 既定のタブ状態を生成します。
    /// </summary>
    /// <returns>既定の <see cref="TabsState"/>。</returns>
    public static TabsState CreateDefault()
    {
        // 既定の 1 タブ構成を返します。
        return new TabsState
        {
            ActiveTabId = "tab-1",
            Tabs =
            [
                new TabDefinition
                {
                    Id = "tab-1",
                    Title = "memo",
                    FileName = "memo.md"
                }
            ]
        };
    }
}

/// <summary>
/// タブ1件分の定義です。
/// </summary>
public sealed class TabDefinition
{
    /// <summary>
    /// タブ ID を取得または設定します。
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// タブ表示名を取得または設定します。
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 保存ファイル名を取得または設定します。
    /// </summary>
    public string FileName { get; set; } = string.Empty;
}

