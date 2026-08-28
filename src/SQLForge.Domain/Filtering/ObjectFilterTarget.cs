namespace SQLForge.Domain.Filtering;

/// <summary>
/// 絞り込みに掛ける 1 件ぶんの値。ツリーのノードそのものではなく、
/// 条件に使うところだけを抜き出して渡す（ドメインは UI のノードを知らない）。
/// </summary>
/// <param name="Name">オブジェクトの名前。</param>
/// <param name="CreatedAt">作成された日時。エンジンから読めないものは null。</param>
public sealed record ObjectFilterTarget(string Name, DateTime? CreatedAt = null);
