using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SQLForge.Application.Abstractions;
using SQLForge.Application.Catalog;
using SQLForge.Application.Security;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Security;

namespace SQLForge.Ui.ViewModels.Security;

/// <summary>
/// ログインのプロパティ ダイアログ「ユーザー マッピング」のページ。
/// SSMS と同じく、サーバー上のデータベースをすべて並べ、チェックの付いたものへ
/// ユーザーを作る（外せばそのユーザーを消す）。
/// </summary>
public sealed partial class LoginUserMappingsViewModel : ObservableObject
{
    private readonly IDatabaseSession _session;
    private readonly ListDatabasesUseCase _databases;
    private readonly ListDatabaseRolesUseCase _roles;
    private readonly ListLoginUserMappingsUseCase _mappings;
    private readonly string _loginName;
    private readonly bool _isNewLogin;

    [ObservableProperty] private LoginUserMappingRowViewModel? _selectedRow;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private bool _isLoading;

    /// <param name="loginName">対応づけるログイン。新規作成では、これから作る名前。</param>
    /// <param name="isNewLogin">新しいログインか。まだ居ないログインの対応づけは読めない。</param>
    public LoginUserMappingsViewModel(
        IDatabaseSession session,
        string loginName,
        bool isNewLogin,
        ListDatabasesUseCase databases,
        ListDatabaseRolesUseCase roles,
        ListLoginUserMappingsUseCase mappings)
    {
        _session = session;
        _loginName = loginName;
        _isNewLogin = isNewLogin;
        _databases = databases;
        _roles = roles;
        _mappings = mappings;
    }

    public bool HasError => Error is not null;

    /// <summary>データベースごとの行。</summary>
    public ObservableCollection<LoginUserMappingRowViewModel> Rows { get; } = [];

    /// <summary>開いたときにサーバーから読んだ姿。保存のときの突き合わせに使う。</summary>
    public IReadOnlyList<LoginUserMapping> Original { get; private set; } = [];

    /// <summary>データベースと今の対応づけを読み、行を組み立てる。開いた直後に 1 度だけ呼ぶ。</summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;

        try
        {
            Original = _isNewLogin
                ? []
                : await _mappings
                    .ExecuteAsync(_session, new ServerLoginName(_loginName), cancellationToken)
                    .ConfigureAwait(true);

            var databases = await _databases.ExecuteAsync(_session, cancellationToken).ConfigureAwait(true);

            Fill(databases);
        }
        catch (OperationCanceledException)
        {
            // 画面を閉じた。何もしない。
        }
        catch (Exception exception)
        {
            // 権限が足りないと、ほかのデータベースの中までは見えない。理由だけ出す。
            Error = exception.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>今のページの姿。保存はダイアログ側がまとめて行う。</summary>
    public IReadOnlyList<LoginUserMappingDraft> ToDrafts() => Rows.Select(row => row.ToDraft()).ToList();

    private void Fill(IReadOnlyList<DatabaseDescriptor> databases)
    {
        Rows.Clear();

        foreach (var database in databases.Where(database => database.IsAccessible))
        {
            var mapping = Original.FirstOrDefault(mapping =>
                string.Equals(mapping.Database.Value, database.Name.Value, StringComparison.OrdinalIgnoreCase));

            Rows.Add(new LoginUserMappingRowViewModel(
                database.Name.Value,
                _loginName,
                mapping is null ? null : LoginUserMappingDraft.FromMapping(mapping)));
        }

        SelectedRow = Rows.FirstOrDefault(row => row.IsMapped) ?? Rows.FirstOrDefault();
    }

    partial void OnErrorChanged(string? value) => OnPropertyChanged(nameof(HasError));

    /// <summary>
    /// 選び直したら、その行のロールを読む。読み込みは投げっぱなしにする
    /// （<see cref="LoadRolesAsync"/> が例外を受け止める）。
    /// </summary>
    partial void OnSelectedRowChanged(LoginUserMappingRowViewModel? value)
    {
        if (value is not null)
        {
            _ = LoadRolesAsync(value);
        }
    }

    private async Task LoadRolesAsync(LoginUserMappingRowViewModel row)
    {
        try
        {
            await row.EnsureRolesAsync(async token =>
                {
                    var roles = await _roles
                        .ExecuteAsync(_session, new DatabaseName(row.Database), token)
                        .ConfigureAwait(true);

                    return roles.Select(role => role.Name.Value).ToList();
                })
                .ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // 画面を閉じた。何もしない。
        }
        catch (Exception exception)
        {
            // ロールが読めなくても、対応づけそのものは作れる。理由だけ出す。
            Error = exception.Message;
        }
    }
}
