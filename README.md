# VRCLogAnalyzer
VRChatのログを解析して、訪れたワールドやその場にいたユーザーのデータベースを作成するWFPアプリです。

現在、ベータ版で動作確認してくれる方を募っています。

![動作画面](docs/img/MainWindow.png "メイン画面")


## 動作環境

- OS: Windows 10 （64bit）
- SteamからVRChatを起動（Oculusからの起動は未確認）
- VRChatの起動オプションに「--enable-sdk-log-levels」を追加
  - ユーザー情報とワールドの詳細をログ出力させるためのオプション追加
  - すでにほかのオプションを指定している場合は、半角スペースで区切って追加する。

![VRChatプロパティを開く](docs/img/vrcproperty.png "VRChatプロパティ")
![オプションに --enable-sdk-log-levels を追加](docs/img/enable-sdk-log-levels.png "VRChatオプション")

対応環境は、今後拡大する可能性はありますが、複数環境への対応大変なので、自分も使っている一般的なVRChat環境に限定して動作確認しています。

## 動作説明

- VRChatのデフォルトのログパスからログファイルを取得し、データベースに格納します。
- データベースファイルは、現在MyDocument直下に置かれています（ベータ完了後、長期的な保存に適したフォルダに以降予定）
- データベースはSQLite3を利用しており、SQLite3のクライアントから直接確認することも可能です。


## 操作方法

### データベースの更新

- アプリ起動後、「設定」メニューから「データの更新」を選択すると、ログからデータベースにデータが取り込まれます。
- データベースの更新は、コマンドラインから「VRCLogAnalyzer.exe /analyze」とオプションをつけて実行することもできます。VRChatのログは、数日でローテーションして消えてしまうので、これをタスクスケジューラ等から1日1回実行するような使い方を想定しています。

### データの表示

- 現在、ユーザーとの交流ログの表示機能だけがあります。日付で絞り込んで検索することができます。（誰とも会っていないワールドは表示対象に含まれません）
- ワールドだけの履歴も保存してるので、ワールド履歴だけの機能を今後追加予定です。
- ユーザー名等、別の情報での検索も必要に応じて追加予定です。



