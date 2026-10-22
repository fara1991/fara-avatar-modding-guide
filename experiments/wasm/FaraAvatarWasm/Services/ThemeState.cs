using Microsoft.JSInterop;

namespace FaraAvatarWasm.Services;

/// <summary>ライト／ダーク。保存先は本体と同じ localStorage のキー（avatar-modding-guide.theme）。既定はダーク（本体と同じ）。</summary>
public sealed class ThemeState(IJSRuntime js)
{
    public bool IsDark { get; private set; } = true;

    public async Task InitializeAsync()
    {
        var saved = await js.InvokeAsync<string?>("faraTheme.get");
        IsDark = saved != "light";
    }

    public async Task ToggleAsync()
    {
        IsDark = !IsDark;
        await js.InvokeVoidAsync("faraTheme.set", IsDark ? "dark" : "light");
        Changed?.Invoke();
    }

    public event Action? Changed;
}
