using System.Collections;

namespace FaraAvatarModdingGuide.Practice;

/// <summary>
/// 追加した順に並ぶ集合(JavaScript の Set と同じ: すでにある要素を足しても順番は変わらず、消してから足すと最後に付く)。
/// 編集モードの選択は、この順番で結果が変わる(マージで残る頂点、面を張る順など)ので、HashSet は使わない(HashSet は消したあと順番が崩れる)。
/// 要素の比較は既定のもの(クラスは参照、EdgeKey は値)。
/// </summary>
public sealed class OrderedSet<T> : IEnumerable<T> where T : notnull
{
    private readonly OrderedDictionary<T, bool> items = new();

    public OrderedSet() { }

    public OrderedSet(IEnumerable<T> source)
    {
        foreach (var x in source) items.TryAdd(x, true);
    }

    public int Count => items.Count;

    public bool Contains(T x) => items.ContainsKey(x);

    /// <summary>足す(すでにあれば何もしない)。</summary>
    public void Add(T x) => items.TryAdd(x, true);

    public bool Remove(T x) => items.Remove(x);

    public void Clear() => items.Clear();

    /// <summary>順番どおりの写し(JavaScript の [...set])。繰り返しの途中で集合を変えてよいように、コピーを返す。</summary>
    public List<T> ToList() => [.. items.Keys];

    public IEnumerator<T> GetEnumerator() => ToList().GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
