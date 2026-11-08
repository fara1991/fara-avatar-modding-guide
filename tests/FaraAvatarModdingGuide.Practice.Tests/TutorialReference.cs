using System.Text.Json;

namespace FaraAvatarModdingGuide.Practice.Tests;

/// <summary>
/// 現行の scenarios.js（Blender の 3D ビューのシナリオ 9 本）を Node で本物のまま動かした結果の凍結した基準（tests/js/golden/tutorial.json.gz。作ったのは
/// tests/js/tutorial-reference.mjs。commit 8262089）。手順の定義・再生（replayTo）後のビューの状態・案内（矢印・点線）・イベントの一覧への done / fail / hint の答え。
/// </summary>
internal static class TutorialReference
{
    public static JsonElement Output { get; } = JsGolden.Read("tutorial");
}
