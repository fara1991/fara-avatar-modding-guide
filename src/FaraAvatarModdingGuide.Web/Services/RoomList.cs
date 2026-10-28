using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FaraAvatarModdingGuide.Web.Services;

/// <summary>練習の部屋の一覧の 1 件(GET /api/rooms の rooms)。公開の情報だけ(部屋の名前・人数・鍵のあり無し・作成の時刻)。</summary>
public sealed record RoomListItem(string Id, string Name, int Count, int Max, bool Locked, double CreatedAt);

/// <summary>GET /api/rooms の応答。</summary>
public sealed record RoomListResponse(List<RoomListItem> Rooms, int Limit);

/// <summary>一覧の取得(画面は、この口だけを読む。テストでは偽物に差し替える)。</summary>
public interface IRoomDirectory
{
    Task<IReadOnlyList<RoomListItem>> ListAsync(CancellationToken cancellationToken);
}

/// <summary>GET /api/rooms(Worker の RoomDirectory)を読む。キャッシュさせない(Worker が no-store を返す)。</summary>
public sealed class HttpRoomDirectory(HttpClient http) : IRoomDirectory
{
    public async Task<IReadOnlyList<RoomListItem>> ListAsync(CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync("api/rooms", cancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync(RoomListJson.Default.RoomListResponse, cancellationToken);
        return body?.Rooms ?? [];
    }
}

/// <summary>一覧の JSON(camelCase)。ILLink で型が消されないように、ソース生成で作る。</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(RoomListResponse))]
internal partial class RoomListJson : JsonSerializerContext;
