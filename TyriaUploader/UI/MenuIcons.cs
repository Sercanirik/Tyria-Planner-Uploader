using System.Drawing.Drawing2D;

namespace TyriaUploader.UI;

internal static class MenuIcons
{
    private const int Size = 16;

    public static Bitmap Check(Color color)    => Render((g, pen) => g.DrawLines(pen, new[] { new PointF(3.5f, 8.5f), new PointF(6.5f, 11.5f), new PointF(12.5f, 4.5f) }), color);
    public static Bitmap Dot(Color color)      => Render(g => g.FillEllipse(new SolidBrush(color), 6, 6, 4, 4), color);
    public static Bitmap Cross(Color color)    => Render((g, pen) =>
    {
        g.DrawLine(pen, 4f, 4f, 12f, 12f);
        g.DrawLine(pen, 12f, 4f, 4f, 12f);
    }, color);
    public static Bitmap Pause(Color color)    => Render(g =>
    {
        using var brush = new SolidBrush(color);
        g.FillRectangle(brush, 4, 3, 3, 10);
        g.FillRectangle(brush, 9, 3, 3, 10);
    }, color);
    public static Bitmap Refresh(Color color)  => Render((g, pen) =>
    {

        var rect = new RectangleF(2.5f, 2.5f, 11f, 11f);
        g.DrawArc(pen, rect, 30, 270);
        var arrow = new[] { new PointF(13f, 4.5f), new PointF(13f, 1.5f), new PointF(10f, 4.5f) };
        g.DrawLines(pen, arrow);
    }, color);
    public static Bitmap List(Color color)     => Render((g, pen) =>
    {
        g.DrawLine(pen, 3f, 4.5f,  13f, 4.5f);
        g.DrawLine(pen, 3f, 8f,    13f, 8f);
        g.DrawLine(pen, 3f, 11.5f, 13f, 11.5f);
    }, color);
    public static Bitmap Gear(Color color)     => Render((g, pen) =>
    {

        g.DrawEllipse(pen, 5f, 5f, 6f, 6f);
        for (int i = 0; i < 6; i++)
        {
            double angle = i * Math.PI / 3.0;
            float cx = 8f + (float)Math.Cos(angle) * 7.2f;
            float cy = 8f + (float)Math.Sin(angle) * 7.2f;
            float ix = 8f + (float)Math.Cos(angle) * 5.4f;
            float iy = 8f + (float)Math.Sin(angle) * 5.4f;
            g.DrawLine(pen, ix, iy, cx, cy);
        }
        using var brush = new SolidBrush(color);
        g.FillEllipse(brush, 7f, 7f, 2f, 2f);
    }, color);
    public static Bitmap Power(Color color)    => Render((g, pen) =>
    {

        g.DrawArc(pen, new RectangleF(3f, 3f, 10f, 10f), 60, 240);
        g.DrawLine(pen, 8f, 2.5f, 8f, 7f);
    }, color);

    private static Bitmap Render(Action<Graphics> body, Color _)
    {
        var bmp = new Bitmap(Size, Size);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        body(g);
        return bmp;
    }

    private static Bitmap Render(Action<Graphics, Pen> body, Color color)
    {
        var bmp = new Bitmap(Size, Size);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        using var pen = new Pen(color, 1.6f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round,
        };
        body(g, pen);
        return bmp;
    }
}
