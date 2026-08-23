using SQLForge.Domain.Connections;

namespace SQLForge.Application.Connections;

/// <summary>
/// 実装の入っていないドライバーを選んだときの文言。接続テストと接続で同じ言い方をする。
/// 「つなげる先」は台帳から作るので、ドライバーを足せば文言も自動で追随する。
/// </summary>
public static class UnsupportedDriverMessage
{
    public static string For(DatabaseDriver driver, IReadOnlyList<DatabaseDriver> supportedDrivers)
    {
        ArgumentNullException.ThrowIfNull(driver);
        ArgumentNullException.ThrowIfNull(supportedDrivers);

        var head = $"{driver.DisplayName} 用のドライバーはまだ入っていません。";

        return supportedDrivers.Count == 0
            ? head + "つなげるドライバーが 1 つも入っていません。"
            : head + $"今つなげるのは {string.Join(" / ", supportedDrivers.Select(d => d.DisplayName))} です。";
    }
}
