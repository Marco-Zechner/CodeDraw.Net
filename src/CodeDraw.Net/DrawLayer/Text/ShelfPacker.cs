namespace MarcoZechner.CodeDrawDotNet.DrawLayer.Text;

internal sealed class ShelfPacker(int w, int h)
{
    private int _curX, _curY, _shelfH;

    public bool TryAlloc(int w1, int h1, out int x, out int y)
    {
        x = y = 0;
        if (w1 > w || h1 > h) return false;

        if (_curX + w1 > w)
        {
            _curX = 0;
            _curY += _shelfH;
            _shelfH = 0;
        }

        if (_curY + h1 > h) return false;

        x = _curX;
        y = _curY;

        _curX += w1;
        _shelfH = Math.Max(_shelfH, h1);
        return true;
    }
}