using System.Runtime.InteropServices.JavaScript;
using FaraAvatarModdingGuide.RenderLab;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

Gl.Mark("lab:main");
var builder = WebAssemblyHostBuilder.CreateDefault(args);
var host = builder.Build();
await JSHost.ImportAsync("gl", "../js/gl.js");
await JSHost.ImportAsync("gl-lab", "../js/gl-lab.js");
Gl.Mark("lab:imported");
Lab.Run();
Gl.Mark("lab:run-returned");
await host.RunAsync();
