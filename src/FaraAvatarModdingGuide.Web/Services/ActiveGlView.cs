namespace FaraAvatarModdingGuide.Web.Services;

/// <summary>
/// ページ内で WebGL を使っている記事内チュートリアル(Blender の <see cref="TutorialController"/> も Unity の <see cref="UnityTutorialController"/> も)の登録。
/// WebGL を使うのはページ内で 1 つだけ(gl.js の start() が前のものを壊す)なので、新しいものが始まる前に、いまのものを手放させる。
/// 登録が種類ごとに別だと、Unity と Blender が同じページにあるとき、前のものが手放されず、DOM だけが残る。
/// </summary>
internal static class ActiveGlView
{
    private static ITutorialCs? current;

    /// <summary>next が WebGL を使い始める。別のものが使っていれば、先に手放させる(<see cref="ITutorialCs.Suspend"/>)。</summary>
    public static void Claim(ITutorialCs next)
    {
        var previous = current;
        current = next;
        if (previous is not null && previous != next) previous.Suspend();
    }

    public static bool IsCurrent(ITutorialCs self) => current == self;

    public static void Release(ITutorialCs self)
    {
        if (current == self) current = null;
    }
}
