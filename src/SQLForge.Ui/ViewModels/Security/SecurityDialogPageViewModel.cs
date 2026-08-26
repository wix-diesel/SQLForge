namespace SQLForge.Ui.ViewModels.Security;

/// <summary>
/// プロパティ ダイアログのページ 1 枚。SSMS の左側にある「ページの選択」にあたる。
///
/// ページの中身はそれぞれのビューモデルで、「全般」だけはダイアログ自身が中身になる
/// （名前や種類の欄はダイアログのプロパティそのものだから）。
/// </summary>
/// <param name="Title">タブに出す名前。</param>
/// <param name="Content">そのページのビューモデル。</param>
public sealed record SecurityDialogPageViewModel(string Title, object Content);
