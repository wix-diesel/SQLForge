using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SQLForge.Domain.Connections;

namespace SQLForge.Ui.ViewModels;

/// <summary>左ペインに並ぶ 1 項目。見出し行と接続行を同じ一覧に混ぜるための共通面。</summary>
public interface IConnectionListEntry
{
    /// <summary>見出し行は選択できない。</summary>
    bool IsSelectable { get; }
}

/// <summary>環境タグの見出し行（本番 / ステージング / ローカル）。</summary>
public sealed class ConnectionGroupHeaderViewModel(EnvironmentTag environment) : IConnectionListEntry
{
    public string Title { get; } = environment.DisplayName;

    public bool IsSelectable => false;
}

/// <summary>
/// 保存済み接続 1 件の行。右クリックから書き出しと削除ができる。
/// どちらも一覧そのものを動かすので、受け持ちは左ペイン（<paramref name="owner"/>）に預ける。
/// </summary>
public sealed partial class SavedConnectionItemViewModel(ConnectionProfile profile, SavedConnectionsViewModel owner)
    : ObservableObject, IConnectionListEntry
{
    private readonly SavedConnectionsViewModel _owner = owner;

    public ConnectionProfile Profile { get; } = profile;

    public string Name => Profile.Name;

    /// <summary>例: postgres · 10.2.0.14:5432</summary>
    public string Summary => Profile.Target.Summary;

    public bool IsSelectable => true;

    public bool IsCritical => Profile.Environment.Severity == EnvironmentSeverity.Critical;

    public bool IsElevated => Profile.Environment.Severity == EnvironmentSeverity.Elevated;

    public bool IsNormal => Profile.Environment.Severity == EnvironmentSeverity.Normal;

    public bool IsNeutral => Profile.Environment.Severity == EnvironmentSeverity.Neutral;

    /// <summary>読み取り専用で開かれる接続。行末に盾を出す。</summary>
    public bool IsReadOnly => Profile.IsReadOnly;

    /// <summary>右クリックの「書き出し…」。</summary>
    [RelayCommand]
    private Task ExportAsync(CancellationToken cancellationToken) => _owner.ExportAsync(Profile, cancellationToken);

    /// <summary>右クリックの「削除」。確認は行き先（左ペイン）の受け持ち。</summary>
    [RelayCommand]
    private Task DeleteAsync(CancellationToken cancellationToken) => _owner.DeleteAsync(Profile, cancellationToken);
}
