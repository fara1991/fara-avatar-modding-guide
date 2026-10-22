using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components;
using FaraSearchIsland;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// 静的な HTML の <fara-search> を、この島（検索）に置き換える。ページごとに 1 回だけ読み込まれる
builder.RootComponents.RegisterCustomElement<SearchIsland>("fara-search");

// 検索インデックス（/search-index.json）は、サイトと同じオリジンから取る
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

await builder.Build().RunAsync();
