namespace SQLForge.Domain.Connections;

/// <summary>
/// 開いた接続 1 本で何ができるか。DBMS ごとに実装の進み具合が違うので、
/// 「まだ書いていない操作」を画面へ出さずに済ませるための申告になる。
///
/// できないことを例外で知らせると、利用者はメニューを押してから断られる。
/// ドライバーが自分で申告し、画面はそれを見て枝やメニューを出し分ける。
/// </summary>
/// <param name="SupportsSecurity">
/// ログイン・ユーザー・ロール・スキーマ・権限を読み書きできるか。
/// false のときツリーに「セキュリティ」の枝を出さない。
/// </param>
/// <param name="SupportsTableEditing">
/// 編集グリッド（先頭 N 行を編集）を使えるか。
/// false のときツリーの右クリックにそのメニューを出さない。
/// </param>
public sealed record SessionCapabilities(bool SupportsSecurity, bool SupportsTableEditing)
{
    /// <summary>この版で用意した操作をすべて備えたドライバー（SQL Server）。</summary>
    public static SessionCapabilities Full { get; } =
        new(SupportsSecurity: true, SupportsTableEditing: true);

    /// <summary>カタログを読むだけのドライバー。接続してツリーを辿るところまではできる。</summary>
    public static SessionCapabilities CatalogOnly { get; } =
        new(SupportsSecurity: false, SupportsTableEditing: false);
}
