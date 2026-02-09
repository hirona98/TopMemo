using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using TopMemo.App.Infrastructure;
using TopMemo.App.ViewModels;
using WinRect = TopMemo.App.Infrastructure.Rect;

namespace TopMemo.App;

/// <summary>
/// TopMemo のメインエディタウィンドウです。
/// </summary>
public partial class MainWindow : Window
{
    private bool _suppressEditorTextChanged;
    private bool _suppressTabSelectionChanged;
    private bool _allowClose;
    private MemoTabViewModel? _contextMenuTargetTab;
    private readonly ContextMenu _tabContextMenu = new();

    /// <summary>
    /// 初期化します。
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();

        // タブ右クリックメニューを構築します。
        BuildTabContextMenu();

        // 閉じる操作を非表示へ変換するため Closing を購読します。
        Closing += MainWindow_Closing;
        SourceInitialized += MainWindow_SourceInitialized;
    }

    /// <summary>
    /// タブ追加要求イベントです。
    /// </summary>
    public event Action? AddTabRequested;

    /// <summary>
    /// タブ改名要求イベントです。
    /// </summary>
    public event Action<MemoTabViewModel>? RenameTabRequested;

    /// <summary>
    /// タブ削除要求イベントです。
    /// </summary>
    public event Action<MemoTabViewModel>? DeleteTabRequested;

    /// <summary>
    /// タブ切替イベントです。
    /// </summary>
    public event Action<MemoTabViewModel?>? SelectedTabChanged;

    /// <summary>
    /// エディタ本文変更イベントです。
    /// </summary>
    public event Action<MemoTabViewModel, string>? EditorTextChanged;

    /// <summary>
    /// エディタ初期化イベントです。
    /// </summary>
    public event Action<TextEditor>? EditorLoaded;

    /// <summary>
    /// 閉じる操作時の非表示要求イベントです。
    /// </summary>
    public event Action? HideRequested;

    /// <summary>
    /// リンククリック要求イベントです。
    /// </summary>
    public event Func<TextDocument, int, bool>? LinkOpenRequested;

    /// <summary>
    /// 開くダイアログ表示要求イベントです。
    /// </summary>
    public event Action? OpenFileDialogRequested;

    /// <summary>
    /// 現在選択中のタブを取得します。
    /// </summary>
    public MemoTabViewModel? SelectedTab => MemoTabControl.SelectedItem as MemoTabViewModel;

    /// <summary>
    /// エディタ表示中かを返します。
    /// </summary>
    public bool IsEditorVisible => IsVisible;

    /// <summary>
    /// タブ一覧をバインドします。
    /// </summary>
    /// <param name="tabs">タブ一覧。</param>
    public void BindTabs(ObservableCollection<MemoTabViewModel> tabs)
    {
        // ItemsSource へタブ一覧を設定します。
        MemoTabControl.ItemsSource = tabs;
    }

    /// <summary>
    /// 画面下部のファイルパス表示を更新します。
    /// </summary>
    /// <param name="memoPath">表示対象パス。</param>
    public void SetCurrentMemoPath(string memoPath)
    {
        // パス表示へ最新値を反映します。
        CurrentPathTextBox.Text = memoPath;
        CurrentPathTextBox.ToolTip = memoPath;
    }

    /// <summary>
    /// 指定タブを選択します。
    /// </summary>
    /// <param name="tab">選択対象。</param>
    public void SelectTab(MemoTabViewModel tab)
    {
        // 選択イベントを抑止して切り替えます。
        _suppressTabSelectionChanged = true;
        MemoTabControl.SelectedItem = tab;
        _suppressTabSelectionChanged = false;
    }

    /// <summary>
    /// エディタを表示してフォーカスします。
    /// </summary>
    /// <param name="x">X 座標。</param>
    /// <param name="y">Y 座標。</param>
    /// <param name="width">幅。</param>
    /// <param name="height">高さ。</param>
    /// <param name="topMost">最前面設定。</param>
    public void ShowEditor(double x, double y, double width, double height, bool topMost)
    {
        // 座標とサイズを反映します。
        Left = x;
        Top = y;
        Width = width;
        Height = height;
        Topmost = topMost;

        // ウィンドウを表示してアクティブ化します。
        if (!IsVisible)
        {
            Show();
        }
        Activate();

        // 現在タブ内のエディタへフォーカスします。
        var selectedEditor = TryGetSelectedEditor();
        if (selectedEditor is not null)
        {
            selectedEditor.Focus();
            return;
        }

        // エディタ未生成時はタブへフォーカスします。
        MemoTabControl.Focus();
    }

    /// <summary>
    /// エディタを非表示にします。
    /// </summary>
    public void HideEditor()
    {
        // 表示中のみ非表示化します。
        if (IsVisible)
        {
            Hide();
        }
    }

    /// <summary>
    /// 現在のウィンドウ配置を返します。
    /// </summary>
    /// <returns>配置情報。</returns>
    public (double X, double Y, double Width, double Height) GetWindowPlacement()
    {
        // 現在の配置値を返します。
        return (Left, Top, Width, Height);
    }

    /// <summary>
    /// ウィンドウ矩形を画面ピクセルで取得します。
    /// </summary>
    /// <returns>矩形。未取得時は null。</returns>
    internal WinRect? TryGetWindowRect()
    {
        // ハンドルが無効なら取得できません。
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == nint.Zero)
        {
            return null;
        }

        // Win32 API で矩形を取得します。
        return NativeMethods.GetWindowRect(handle, out var rect) ? rect : null;
    }

    /// <summary>
    /// アプリ終了のために閉じることを許可します。
    /// </summary>
    public void CloseForExit()
    {
        // Closing キャンセルを解除して閉じます。
        _allowClose = true;
        Close();
    }

    /// <summary>
    /// ウィンドウソース初期化処理です。
    /// </summary>
    /// <param name="sender">送信元。</param>
    /// <param name="eventArgs">イベント引数。</param>
    private void MainWindow_SourceInitialized(object? sender, EventArgs eventArgs)
    {
        // Alt+Tab 非表示のため WS_EX_TOOLWINDOW を付与します。
        var handle = new WindowInteropHelper(this).Handle;
        var exStyle = NativeMethods.GetWindowLongPtr(handle);
        NativeMethods.SetWindowLongPtr(handle, exStyle | NativeMethods.WsExToolWindow);
    }

    /// <summary>
    /// 追加ボタンクリック処理です。
    /// </summary>
    /// <param name="sender">送信元。</param>
    /// <param name="eventArgs">イベント引数。</param>
    private void AddTabButton_Click(object sender, RoutedEventArgs eventArgs)
    {
        // 新規タブ追加を要求します。
        AddTabRequested?.Invoke();
    }

    /// <summary>
    /// 開くボタンクリック処理です。
    /// </summary>
    /// <param name="sender">送信元。</param>
    /// <param name="eventArgs">イベント引数。</param>
    private void OpenPathButton_Click(object sender, RoutedEventArgs eventArgs)
    {
        // 開くダイアログの表示を要求します。
        OpenFileDialogRequested?.Invoke();
    }

    /// <summary>
    /// タブ選択変更イベント処理です。
    /// </summary>
    /// <param name="sender">送信元。</param>
    /// <param name="eventArgs">イベント引数。</param>
    private void MemoTabControl_SelectionChanged(object sender, SelectionChangedEventArgs eventArgs)
    {
        // 内部選択変更中は通知しません。
        if (_suppressTabSelectionChanged)
        {
            return;
        }

        // 選択タブの本文をエディタへ反映します。
        SyncSelectedTabEditorText();

        // 新しい選択タブを通知します。
        SelectedTabChanged?.Invoke(SelectedTab);
    }

    /// <summary>
    /// タブヘッダのマウスホイール処理です。
    /// </summary>
    /// <param name="sender">送信元。</param>
    /// <param name="eventArgs">イベント引数。</param>
    private void MemoTabControl_PreviewMouseWheel(object sender, MouseWheelEventArgs eventArgs)
    {
        // タブ数が 2 未満なら切り替え不要です。
        if (MemoTabControl.Items.Count < 2 || SelectedTab is null)
        {
            return;
        }

        // 現在インデックスを起点に次タブを計算します。
        var currentIndex = MemoTabControl.SelectedIndex;
        var nextIndex = eventArgs.Delta > 0 ? currentIndex - 1 : currentIndex + 1;
        if (nextIndex < 0)
        {
            nextIndex = MemoTabControl.Items.Count - 1;
        }
        else if (nextIndex >= MemoTabControl.Items.Count)
        {
            nextIndex = 0;
        }

        // 選択変更してホイールイベントを消費します。
        MemoTabControl.SelectedIndex = nextIndex;
        eventArgs.Handled = true;
    }

    /// <summary>
    /// タブ右クリック時に対象タブを選択します。
    /// </summary>
    /// <param name="sender">送信元。</param>
    /// <param name="eventArgs">イベント引数。</param>
    private void TabItem_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs eventArgs)
    {
        // 対象タブを選択しコンテキストメニューを開きます。
        if (sender is TabItem tabItem && tabItem.DataContext is MemoTabViewModel tab)
        {
            tabItem.IsSelected = true;
            _contextMenuTargetTab = tab;
            _tabContextMenu.PlacementTarget = tabItem;
            _tabContextMenu.IsOpen = true;
            eventArgs.Handled = true;
        }
    }

    /// <summary>
    /// タブ右クリックメニューを組み立てます。
    /// </summary>
    private void BuildTabContextMenu()
    {
        // 「名前変更」メニューを作成します。
        var renameItem = new MenuItem { Header = "名前変更" };
        renameItem.Click += (_, _) =>
        {
            if (_contextMenuTargetTab is not null)
            {
                RenameTabRequested?.Invoke(_contextMenuTargetTab);
            }
        };

        // 「削除」メニューを作成します。
        var deleteItem = new MenuItem { Header = "削除" };
        deleteItem.Click += (_, _) =>
        {
            if (_contextMenuTargetTab is not null)
            {
                DeleteTabRequested?.Invoke(_contextMenuTargetTab);
            }
        };

        // メニューへ項目を追加します。
        _tabContextMenu.Items.Add(renameItem);
        _tabContextMenu.Items.Add(deleteItem);
    }

    /// <summary>
    /// タブ内エディタロード時の処理です。
    /// </summary>
    /// <param name="sender">送信元。</param>
    /// <param name="eventArgs">イベント引数。</param>
    private void MemoEditor_Loaded(object sender, RoutedEventArgs eventArgs)
    {
        // 対象がテキストエディタでない場合は処理しません。
        if (sender is not TextEditor editor)
        {
            return;
        }

        // タブ情報が無い場合は処理しません。
        if (editor.Tag is not MemoTabViewModel tab)
        {
            return;
        }

        // 多重購読を避けるため再購読します。
        editor.TextChanged -= MemoEditor_TextChanged;
        editor.TextChanged += MemoEditor_TextChanged;

        // タブ本文を初期反映します。
        SetEditorText(editor, tab.Content);

        // 初期化済みエディタを外部へ通知します。
        EditorLoaded?.Invoke(editor);
    }

    /// <summary>
    /// タブ内エディタアンロード時の処理です。
    /// </summary>
    /// <param name="sender">送信元。</param>
    /// <param name="eventArgs">イベント引数。</param>
    private void MemoEditor_Unloaded(object sender, RoutedEventArgs eventArgs)
    {
        // 対象がテキストエディタの場合のみ購読を解除します。
        if (sender is TextEditor editor)
        {
            editor.TextChanged -= MemoEditor_TextChanged;
        }
    }

    /// <summary>
    /// タブ内エディタ本文変更イベント処理です。
    /// </summary>
    /// <param name="sender">送信元。</param>
    /// <param name="eventArgs">イベント引数。</param>
    private void MemoEditor_TextChanged(object? sender, EventArgs eventArgs)
    {
        // 対象がテキストエディタでない場合は処理しません。
        if (sender is not TextEditor editor)
        {
            return;
        }

        // 対象タブを解決できない場合は通知できません。
        if (editor.Tag is not MemoTabViewModel tab)
        {
            return;
        }

        // 内部反映中は通知しません。
        if (_suppressEditorTextChanged)
        {
            return;
        }

        // 対象タブと本文を外部へ通知します。
        EditorTextChanged?.Invoke(tab, editor.Text);
    }

    /// <summary>
    /// エディタのマウス左ボタンクリック処理です。
    /// </summary>
    /// <param name="sender">送信元。</param>
    /// <param name="eventArgs">イベント引数。</param>
    private void MemoEditor_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs eventArgs)
    {
        // 対象がテキストエディタでない場合は処理しません。
        if (sender is not TextEditor editor)
        {
            return;
        }

        // ドキュメント未初期化時は処理しません。
        if (editor.Document is null)
        {
            return;
        }

        // クリック位置からドキュメントオフセットを計算します。
        var position = editor.GetPositionFromPoint(eventArgs.GetPosition(editor));
        if (position is null)
        {
            return;
        }

        // クリック位置のリンク遷移を要求します。
        var offset = editor.Document.GetOffset(position.Value.Location);
        var opened = LinkOpenRequested?.Invoke(editor.Document, offset) ?? false;
        if (opened)
        {
            eventArgs.Handled = true;
        }
    }

    /// <summary>
    /// 現在選択中タブのエディタを取得します。
    /// </summary>
    /// <returns>エディタ。未生成時は null。</returns>
    private TextEditor? TryGetSelectedEditor()
    {
        // 選択タブが無い場合は取得できません。
        if (SelectedTab is null)
        {
            return null;
        }

        // 可視ツリーから選択タブに紐づくエディタを検索します。
        return FindEditorByTab(MemoTabControl, SelectedTab);
    }

    /// <summary>
    /// 選択タブ本文をエディタへ同期します。
    /// </summary>
    private void SyncSelectedTabEditorText()
    {
        // まず現在の可視ツリーで同期を試行します。
        var immediateEditor = TryGetSelectedEditor();
        if (immediateEditor is not null && immediateEditor.Tag is MemoTabViewModel immediateTab)
        {
            SetEditorText(immediateEditor, immediateTab.Content);
            return;
        }

        // 未生成時はレイアウト確定後に再試行します。
        Dispatcher.BeginInvoke(() =>
        {
            var delayedEditor = TryGetSelectedEditor();
            if (delayedEditor is null || delayedEditor.Tag is not MemoTabViewModel delayedTab)
            {
                return;
            }

            SetEditorText(delayedEditor, delayedTab.Content);
        }, DispatcherPriority.Loaded);
    }

    /// <summary>
    /// エディタ本文を通知抑止付きで設定します。
    /// </summary>
    /// <param name="editor">対象エディタ。</param>
    /// <param name="text">設定本文。</param>
    private void SetEditorText(TextEditor editor, string text)
    {
        // 同じ本文なら更新しません。
        if (editor.Text == text)
        {
            return;
        }

        // 外部通知を抑止して本文を反映します。
        _suppressEditorTextChanged = true;
        editor.Text = text;
        _suppressEditorTextChanged = false;
    }

    /// <summary>
    /// 指定タブに紐づくエディタを可視ツリーから探索します。
    /// </summary>
    /// <param name="root">探索開始ノード。</param>
    /// <param name="tab">対象タブ。</param>
    /// <returns>一致するエディタ。見つからない場合は null。</returns>
    private static TextEditor? FindEditorByTab(DependencyObject root, MemoTabViewModel tab)
    {
        // 子要素を順に探索します。
        var childCount = VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < childCount; index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);

            // タブに紐づくエディタを検出したら返します。
            if (child is TextEditor editor &&
                editor.Tag is MemoTabViewModel ownerTab &&
                ownerTab.Id == tab.Id)
            {
                return editor;
            }

            // 子孫ノードを再帰探索します。
            var nested = FindEditorByTab(child, tab);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    /// <summary>
    /// ウィンドウ閉じる操作時の処理です。
    /// </summary>
    /// <param name="sender">送信元。</param>
    /// <param name="eventArgs">イベント引数。</param>
    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs eventArgs)
    {
        // 終了許可が無い場合は閉じずに非表示要求を出します。
        if (_allowClose)
        {
            return;
        }

        eventArgs.Cancel = true;
        HideRequested?.Invoke();
    }
}
