
public class UnionFind
{
    private readonly int[] _ids;

    public UnionFind(int size)
    {
        _ids = new int[size];
        for (int i = 0; i < size; ++i) _ids[i] = i;
    }

    public void Union(int x, int y)
    {
        int xParent = Find(x);
        int yParent = Find(y);
        _ids[xParent] = yParent;
    }

    public int At(int x)
    {
        return Find(x);
    }

    // TODO: optimize this with path compression
    private int Find(int x)
    {
        while (_ids[x] != x)
        {
            x = _ids[x];
        }

        return x;
    }
}
