using SQLForge.Domain.Connections;

namespace SQLForge.Application.Connections;

/// <summary>環境タグごとにまとめた保存済み接続。左ペインの見出し 1 つ分。</summary>
public sealed record SavedConnectionGroup(EnvironmentTag Environment, IReadOnlyList<ConnectionProfile> Profiles);
