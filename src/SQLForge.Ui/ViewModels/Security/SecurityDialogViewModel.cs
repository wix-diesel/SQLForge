using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SQLForge.Application.Security;

namespace SQLForge.Ui.ViewModels.Security;

/// <summary>
/// セキュリティ関係のプロパティ ダイアログで共通の足回り。
/// 「保存を投げて、欄ごとの理由か失敗の理由を出し、通ったら閉じる」という形は
/// ロール・スキーマ・ユーザー・ログインのどれでも変わらないので、ここにまとめる。
/// </summary>
public abstract partial class SecurityDialogViewModel : ObservableObject
{
    private SecurityValidationResult _validation = SecurityValidationResult.Valid;

    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _isBusy;

    /// <summary>閉じることを伝える。true なら保存済みで、呼び出し側は一覧を読み直す。</summary>
    public event EventHandler<bool>? CloseRequested;

    public bool HasError => ErrorMessage is not null;

    /// <summary>欄ごとの理由。赤枠にするかどうかの判断に使う。</summary>
    protected string? ErrorFor(string field) => _validation[field];

    [RelayCommand]
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;

        try
        {
            var result = await SaveCoreAsync(cancellationToken).ConfigureAwait(true);

            SetValidation(result);

            if (result.IsValid)
            {
                CloseRequested?.Invoke(this, true);
            }
        }
        catch (OperationCanceledException)
        {
            ClearError();
        }
        catch (Exception exception)
        {
            // 権限不足や、同じ名前のものが既にある場合はここへ来る。開いたままにして理由を出す。
            SetError(exception.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(this, false);

    /// <summary>実際に保存する。派生クラスが埋める。</summary>
    protected abstract Task<SecurityValidationResult> SaveCoreAsync(CancellationToken cancellationToken);

    /// <summary>赤枠の判断に使うプロパティを、まとめて出し直す。派生クラスが埋める。</summary>
    protected virtual void OnErrorChanged() => OnPropertyChanged(nameof(HasError));

    protected void SetError(string message)
    {
        _validation = SecurityValidationResult.Valid;
        ErrorMessage = message;
        OnErrorChanged();
    }

    private void SetValidation(SecurityValidationResult validation)
    {
        _validation = validation;
        ErrorMessage = validation.FirstError;
        OnErrorChanged();
    }

    private void ClearError()
    {
        _validation = SecurityValidationResult.Valid;
        ErrorMessage = null;
        OnErrorChanged();
    }
}
