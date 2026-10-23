using Microsoft.AspNetCore.Components;

namespace FaraAvatarModdingGuide.Web.Search;

/// <summary>検索結果の種類の表示（searchcore.js の TYPES）と、強調の描画。</summary>
public static class SearchTypes
{
    /// <summary>種類の名前と、バッジの class（Bootstrap のクラス）。</summary>
    public static (string Label, string Css) Of(string type) => type switch
    {
        "article" => ("記事", "bg-primary"),
        "glossary" => ("用語", "bg-warning text-dark"),
        "howto" => ("何て名前？", "bg-danger"),
        "tip" => ("TIPS", "bg-info text-dark"),
        "qa" => ("Q&A", "bg-light text-dark"),
        "category" => ("カテゴリ", "bg-secondary"),
        "page" => ("ページ", "bg-secondary"),
        "shortcut" => ("ショートカット", "bg-success"),
        "addon" => ("アドオン", "bg-secondary"),
        "link" => ("リンク", "bg-primary-subtle text-primary-emphasis"),
        _ => (type, "bg-secondary"),
    };

    /// <summary>強調の区間を、&lt;mark&gt; つきで描く(文字は Blazor がエスケープする)。</summary>
    public static RenderFragment Marked(IReadOnlyList<Segment> segments) => builder =>
    {
        var seq = 0;
        foreach (var segment in segments)
        {
            if (segment.Match)
            {
                builder.OpenElement(seq++, "mark");
                builder.AddContent(seq++, segment.Text);
                builder.CloseElement();
            }
            else
            {
                builder.AddContent(seq++, segment.Text);
            }
        }
    };
}
