using System.Text.Json;
using System.Text.Json.Serialization;

namespace FaraSearchIsland;

/// <summary>
/// 検索インデックス（JSON）の読み書きを、ソースジェネレーターで作る。
/// リフレクションだと、トリミングした publish で型の情報が落ちて失敗するため（試作で確認）。
/// </summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(List<SearchEntry>))]
internal partial class SearchJsonContext : JsonSerializerContext
{
}
