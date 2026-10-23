using System.Text.Json;
using System.Text.Json.Serialization;

namespace FaraAvatarModdingGuide.Models;

/// <summary>
/// 3 つの JSON（content.json・search.json・site.json）を読み書きするための、ソース生成の型情報。
/// WebAssembly のトリミング（リフレクションを使わない読み込み）に必要。Build が書くときも同じ型情報を使うので、読み書きの形が食い違わない。
/// </summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(SiteContent))]
[JsonSerializable(typeof(SiteSettings))]
[JsonSerializable(typeof(IReadOnlyList<SearchRecord>))]
[JsonSerializable(typeof(IReadOnlyList<LegacySearchEntry>))]
public sealed partial class SiteJsonContext : JsonSerializerContext
{
}
