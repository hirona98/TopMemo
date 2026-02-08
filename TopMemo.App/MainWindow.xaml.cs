using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
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

        // エディタ変更イベントを購読します。
        MemoEditor.TextChanged += MemoEditor_TextChanged;

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
    public event Action<string>? EditorTextChanged;

    /// <summary>
    /// 閉じる操作時の非表示要求イベントです。
    /// </summary>
    public event Action? HideRequested;

    /// <summary>
    /// リンククリック要求イベントです。
    /// </summary>
    public event Func<int, bool>? LinkOpenRequested;

    /// <summary>
    /// エディタドキュメントを取得します。
    /// </summary>
    public TextDocument Document => MemoEditor.Document;

    /// <summary>
    /// 現在選択中のタブを取得します。
    /// </summary>
    public MemoTabViewModel? SelectedTab => MemoTabControl.SelectedItem as MemoTabViewModel;

    /// <summary>
    /// エディタ表示中かを返します。
    /// </summary>
    public bool IsEditorVisible => IsVisible;

    /// <summary>
    /// AvalonEdit 本体を取得します。
    /// </summary>
    public ICSharpCode.AvalonEdit.TextEditor Editor => MemoEditor;

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
    /// エディタ本文をイベント抑止付きで設定します。
    /// </summary>
    /// <param name="text">本文。</param>
    public void SetEditorText(string text)
    {
        // 反映中は TextChanged 通知を抑止します。
        _suppressEditorTextChanged = true;
        MemoEditor.Text = text;
        _suppressEditorTextChanged = false;
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
        MemoEditor.Focus();
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
    /// エディタ本文変更イベント処理です。
    /// </summary>
    /// <param name="sender">送信元。</param>
    /// <param name="eventArgs">イベント引数。</param>
    private void MemoEditor_TextChanged(object? sender, EventArgs eventArgs)
    {
        // 内部反映中は通知しません。
        if (_suppressEditorTextChanged)
        {
            return;
        }

        // 外部へ本文変更を通知します。
        EditorTextChanged?.Invoke(MemoEditor.Text);
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
    /// エディタのマウス左ボタンクリック処理です。
    /// </summary>
    /// <param name="sender">送信元。</param>
    /// <param name="eventArgs">イベント引数。</param>
    private void MemoEditor_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs eventArgs)
    {
        // ドキュメント未初期化時は処理しません。
        if (MemoEditor.Document is null)
        {
            return;
        }

        // クリック位置からドキュメントオフセットを計算します。
        var position = MemoEditor.GetPositionFromPoint(eventArgs.GetPosition(MemoEditor));
        if (position is null)
        {
            return;
        }

        // クリック位置のリンク遷移を要求します。
        var offset = MemoEditor.Document.GetOffset(position.Value.Location);
        var opened = LinkOpenRequested?.Invoke(offset) ?? false;
        if (opened)
        {
            eventArgs.Handled = true;
        }
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
