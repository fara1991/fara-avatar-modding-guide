using System.Runtime.Versioning;

// このアプリはブラウザ（WebAssembly）でだけ動く。[JSImport]・JSHost など browser 専用の API を呼ぶので、そう宣言する（CA1416）
[assembly: SupportedOSPlatform("browser")]
