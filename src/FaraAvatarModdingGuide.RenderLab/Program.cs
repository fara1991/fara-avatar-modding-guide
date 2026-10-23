using System.Runtime.InteropServices.JavaScript;
using FaraAvatarModdingGuide.RenderLab;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
var host = builder.Build();
await JSHost.ImportAsync("gl", "../js/gl.js");
Lab.Run();
await host.RunAsync();
