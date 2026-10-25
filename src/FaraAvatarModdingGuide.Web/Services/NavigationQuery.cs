using Microsoft.AspNetCore.Components;

namespace FaraAvatarModdingGuide.Web.Services;

/// <summary>アドレスのクエリ文字列を読む(部分一致ではなく、名前の完全一致)。</summary>
internal static class NavigationQuery
{
    public static string? Get(this NavigationManager nav, string name)
    {
        var query = new Uri(nav.Uri).Query.TrimStart('?');
        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = pair.Split('=', 2);
            if (kv[0] == name) return kv.Length == 2 ? Uri.UnescapeDataString(kv[1]) : "";
        }
        return null;
    }

    /// <summary>C# 版で動かすか(Phase 3b。既定は C# 版。?engine=js だけが今までの Three.js 版)。</summary>
    public static bool IsCsEngine(this NavigationManager nav) => nav.Get("engine") != "js";
}
