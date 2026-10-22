namespace FaraSearchIsland;

/// <summary>
/// 検索インデックスの 1 件（本体の Data/SearchIndex.cs と同じ項目。JSON は camelCase）。
/// 位置引数の record だと、トリミング後の publish で System.Text.Json がコンストラクタを見つけられず失敗したため（試作で確認）、プロパティで受ける形にしている。
/// </summary>
public sealed class SearchEntry
{
    public string Type { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Subtitle { get; set; }
    public string Url { get; set; } = "";
    public string Text { get; set; } = "";
}
