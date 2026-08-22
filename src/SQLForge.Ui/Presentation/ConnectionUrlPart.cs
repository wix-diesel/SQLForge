namespace SQLForge.Ui.Presentation;

/// <summary>接続 URL を色分けして出すための断片。</summary>
public sealed record ConnectionUrlPart(string Text, ConnectionUrlPartKind Kind)
{
    public bool IsScheme => Kind == ConnectionUrlPartKind.Scheme;

    public bool IsDatabase => Kind == ConnectionUrlPartKind.Database;

    public bool IsParameterName => Kind == ConnectionUrlPartKind.ParameterName;
}

public enum ConnectionUrlPartKind
{
    /// <summary>postgresql:// の部分。</summary>
    Scheme,

    /// <summary>ユーザー・ホスト・区切りなど。</summary>
    Plain,

    /// <summary>データベース名。</summary>
    Database,

    /// <summary>クエリパラメータの名前。</summary>
    ParameterName
}
