using System.Diagnostics;
using Xunit;

namespace FaraAvatarModdingGuide.Practice.Tests;

/// <summary>画面からは起こせない入力(範囲外のマテリアルの番号など)で、固まらず・壊れず・何も変えない(レビュー L3)。</summary>
public class UnityRobustnessTests
{
    private static (UnityEditorCore Core, UnityNode Body) Make()
    {
        var core = new UnityEditorCore(new UnityOptions { Instances = ["Avatar"] });
        var body = core.FindByName("Avatar/Body")!;
        core.Select(body.Id);
        return (core, body);
    }

    [Theory]
    [InlineData(1_000_000_000)]
    [InlineData(int.MaxValue)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    [InlineData(3)]   // 枚数(2)より 1 つ先は、穴ができるので無視する
    public void Out_of_range_material_slots_are_ignored_quickly(int slot)
    {
        var (core, body) = Make();
        var before = body.Smr!.Materials!.ToList();
        var depth = core.UndoDepth;
        var clock = Stopwatch.StartNew();
        core.DropAsset("Hoodie_Red", "slot", null, slot, 0, 0);
        core.AssignMaterial(body.Id, slot, "Hoodie_Red");
        Assert.True(clock.ElapsedMilliseconds < 500, "固まった: " + clock.ElapsedMilliseconds + " ms");
        Assert.Equal(before, body.Smr.Materials!);
        Assert.Equal(depth, core.UndoDepth);
    }

    [Fact]
    public void Slot_equal_to_count_appends_and_slots_inside_replace()
    {
        var (core, body) = Make();
        core.AssignMaterial(body.Id, 2, "Hoodie_Red");
        Assert.Equal(3, body.Smr!.Materials!.Count);
        Assert.Equal("Hoodie_Red", body.Smr.Materials[2]);
        core.DropAsset("Hoodie_lilToon", "slot", null, 0, 0, 0);
        Assert.Equal("Hoodie_lilToon", body.Smr.Materials[0]);
        Assert.Equal(2, core.UndoDepth);
    }
}
