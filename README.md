# TopMemo

TopMemo は Windows 11 向けの常駐型ホバーメモ帳です。  
ホットゾーンにマウスが侵入すると即表示し、領域外に出ると即非表示になります。

## 技術スタック

- C#
- .NET 8
- WPF
- AvalonEdit

## 主な機能

- マルチモニタ対応（仮想座標）
- 単一インスタンス起動（2重起動時は既存インスタンスを前面化）
- 最前面固定、タスクバー非表示、Alt+Tab 非表示
- システムトレイ常駐
- システムトレイ右クリックで `表示/非表示` `自動起動` `終了` メニュー表示
- トレイメニュー `自動起動` のチェック付きトグル
- Markdown のシンタックスハイライト（プレビューなし）
- リンククリックで外部遷移
- タブ対応
- 初期タブ 1 件
- `+` ボタンでタブ追加
- タブ右クリックでタブ名変更（保存ファイル名へ反映）
- タブ右クリックでタブ削除（最後の 1 タブは削除不可）
- タブ上ホイールでタブ切り替え
- マウス左下ホットゾーン（設定可能）で `Win+Tab` を送出
- dirty タブを `画面非表示時 / タブ切替時 / 終了時` に保存
- `logs/app.log` は 100KB 上限で `app.log.1` へローテーション

## 保存ポリシー

- 設定・データはすべてアプリフォルダ内で完結
- 自動起動はスタートアップフォルダ（`.lnk`）を優先
- スタートアップフォルダ登録失敗時のみレジストリ（`HKCU\\...\\Run`）を使用

想定ファイル構成:

- `settings.json`
- `tabs.json`
- `memos/<tabFileName>.md`
- `logs/app.log`（任意）

## 設計書

詳細設計は `docs/hover-notepad-design.md` を参照してください。

## ビルド

WSL 環境では PowerShell 経由でビルドします。

```powershell
powershell.exe -NoProfile -Command "cd 'D:\AliceEncoder\TopMemo'; dotnet.exe build .\TopMemo.sln -c Debug"
```
