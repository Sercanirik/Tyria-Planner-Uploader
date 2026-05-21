using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TyriaUploader.UI;

internal static class Theme
{
    public static readonly Color Background   = ColorTranslator.FromHtml("#0f0e17");
    public static readonly Color Surface      = ColorTranslator.FromHtml("#1a1927");
    public static readonly Color SurfaceAlt   = ColorTranslator.FromHtml("#221f31");
    public static readonly Color SurfaceHi    = ColorTranslator.FromHtml("#2a2740");
    public static readonly Color Border       = ColorTranslator.FromHtml("#2e2c42");
    public static readonly Color BorderHi     = ColorTranslator.FromHtml("#3a3754");
    public static readonly Color TextPrimary  = ColorTranslator.FromHtml("#efe6d3");
    public static readonly Color TextMuted    = ColorTranslator.FromHtml("#a7a0c0");
    public static readonly Color TextFaint    = ColorTranslator.FromHtml("#6f6890");
    public static readonly Color Accent       = ColorTranslator.FromHtml("#e8b059");
    public static readonly Color AccentHover  = ColorTranslator.FromHtml("#f0c073");
    public static readonly Color AccentDim    = ColorTranslator.FromHtml("#7a5c2e");
    public static readonly Color AccentText   = ColorTranslator.FromHtml("#1a1410");
    public static readonly Color Success      = ColorTranslator.FromHtml("#5ec89e");
    public static readonly Color Danger       = ColorTranslator.FromHtml("#e87878");

    public static readonly Font BodyFont     = new("Segoe UI", 9.5f, FontStyle.Regular);
    public static readonly Font BodyBoldFont = new("Segoe UI Semibold", 9.5f, FontStyle.Bold);
    public static readonly Font HeaderFont   = new("Segoe UI Semibold", 10.5f, FontStyle.Bold);
    public static readonly Font TitleFont    = new("Segoe UI Semibold", 18f, FontStyle.Bold);
    public static readonly Font SubtitleFont = new("Segoe UI", 9f, FontStyle.Regular);
    public static readonly Font LabelFont    = new("Segoe UI", 8.5f, FontStyle.Regular);

    public static void ApplyForm(Form form)
    {
        form.BackColor = Background;
        form.ForeColor = TextPrimary;
        form.Font = BodyFont;

        if (form.IsHandleCreated) Win32.UseDarkTitleBar(form.Handle);
        else form.HandleCreated += (s, _) => { if (s is Form f) Win32.UseDarkTitleBar(f.Handle); };
    }

    public static Label Heading(string text) => new()
    {
        Text = text,
        Font = HeaderFont,
        ForeColor = Accent,
        BackColor = Color.Transparent,
        AutoSize = true,
    };

    public static Label Body(string text, bool muted = false) => new()
    {
        Text = text,
        Font = BodyFont,
        ForeColor = muted ? TextMuted : TextPrimary,
        BackColor = Color.Transparent,
        AutoSize = true,
    };

    public static Label FieldLabel(string text) => new()
    {
        Text = text.ToUpperInvariant(),
        Font = LabelFont,
        ForeColor = TextFaint,
        BackColor = Color.Transparent,
        AutoSize = true,
    };

    public static FieldHost TextField()
    {
        return new FieldHost();
    }

    public static RoundedButton PrimaryButton(string text)
    {
        var b = new RoundedButton
        {
            Text = text,
            Font = BodyBoldFont,
            ForeColor = AccentText,
            NormalBackColor = Accent,
            HoverBackColor = AccentHover,
            PressBackColor = AccentDim,
            OutlineColor = Color.Transparent,
            MinimumSize = new Size(100, 34),
            Padding = new Padding(14, 0, 14, 0),
            Cursor = Cursors.Hand,
            Radius = 8,
        };
        return b;
    }

    public static RoundedButton SecondaryButton(string text)
    {
        var b = new RoundedButton
        {
            Text = text,
            Font = BodyFont,
            ForeColor = TextPrimary,
            NormalBackColor = SurfaceAlt,
            HoverBackColor = SurfaceHi,
            PressBackColor = Border,
            OutlineColor = Border,
            MinimumSize = new Size(100, 34),
            Padding = new Padding(14, 0, 14, 0),
            Cursor = Cursors.Hand,
            Radius = 8,
        };
        return b;
    }

    public static RoundedButton GhostButton(string text)
    {
        var b = new RoundedButton
        {
            Text = text,
            Font = BodyFont,
            ForeColor = TextMuted,
            NormalBackColor = Color.Transparent,
            HoverBackColor = SurfaceAlt,
            PressBackColor = Border,
            OutlineColor = Border,
            MinimumSize = new Size(80, 30),
            Padding = new Padding(12, 0, 12, 0),
            Cursor = Cursors.Hand,
            Radius = 8,
        };
        return b;
    }

    public static ThemedCheckBox Toggle(string text, bool initial = false)
    {
        return new ThemedCheckBox
        {
            Text = text,
            Checked = initial,
            ForeColor = TextPrimary,
            Font = BodyFont,
            Cursor = Cursors.Hand,
            AutoSize = true,
        };
    }

    public static Bitmap LoadLogo()
    {
        // Full crest logo used inside the settings window header. icon.png
        // includes the shield surround for a "premium" look in-app.
        return LoadEmbeddedPng("TyriaUploader.Resources.icon.png");
    }

    public static Icon LoadAppIcon()
    {
        // Tray / taskbar / title-bar icon · the crest's shield reads as
        // mush at 16-24px so we ship a lion-only variant (tray.png) for
        // these small surfaces. Falls back to the full crest if the
        // shield-less asset is somehow missing.
        Bitmap bmp;
        try { bmp = LoadEmbeddedPng("TyriaUploader.Resources.tray.png"); }
        catch { bmp = LoadLogo(); }
        using (bmp)
        using (var square = new Bitmap(256, 256))
        {
            using (var g = Graphics.FromImage(square))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                int side = Math.Min(bmp.Width, bmp.Height);
                int sx = (bmp.Width - side) / 2;
                int sy = (bmp.Height - side) / 2;
                g.DrawImage(bmp, new Rectangle(0, 0, 256, 256), new Rectangle(sx, sy, side, side), GraphicsUnit.Pixel);
            }
            var hicon = square.GetHicon();
            return Icon.FromHandle(hicon);
        }
    }

    private static Bitmap LoadEmbeddedPng(string resourceName)
    {
        var asm = Assembly.GetExecutingAssembly();
        using var stream = asm.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource not found: {resourceName}");
        return new Bitmap(stream);
    }

    public static LinkLabel Link(string text)
    {
        return new LinkLabel
        {
            Text = text,
            BackColor = Color.Transparent,
            LinkColor = Accent,
            ActiveLinkColor = AccentHover,
            VisitedLinkColor = Accent,
            Font = BodyFont,
            AutoSize = true,
        };
    }

    internal static Color ResolveOpaqueParentBg(Control? start)
    {
        var c = start;
        while (c != null && c.BackColor == Color.Transparent) c = c.Parent;
        return c?.BackColor ?? Background;
    }

    public static GraphicsPath RoundedRect(Rectangle r, int radius)
    {
        var path = new GraphicsPath();
        if (radius <= 0)
        {
            path.AddRectangle(r);
            path.CloseFigure();
            return path;
        }
        int d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    public sealed class Card : Panel
    {
        public int Radius { get; set; } = 10;

        public Card()
        {

            BackColor = Surface;
            ForeColor = TextPrimary;
            Padding = new Padding(20, 18, 20, 18);
            SetStyle(ControlStyles.UserPaint
                | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {

            e.Graphics.Clear(ResolveOpaqueParentBg(Parent));
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = RoundedRect(rect, Radius);
            using (var brush = new SolidBrush(Surface))
                e.Graphics.FillPath(brush, path);
            using (var pen = new Pen(Border, 1))
                e.Graphics.DrawPath(pen, path);
        }
    }

    public sealed class StatusDot : Control
    {
        private Color _dotColor = TextMuted;
        public Color DotColor
        {
            get => _dotColor;
            set { _dotColor = value; Invalidate(); }
        }

        public StatusDot()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.UserPaint, true);
            BackColor = Color.Transparent;
            Size = new Size(14, 14);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            using (var halo = new SolidBrush(Color.FromArgb(60, _dotColor)))
                e.Graphics.FillEllipse(halo, 0, 0, Width, Height);
            using var brush = new SolidBrush(_dotColor);
            e.Graphics.FillEllipse(brush, 3, 3, Width - 6, Height - 6);
        }
    }

    public sealed class AccentStripe : Control
    {
        public AccentStripe()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.UserPaint
                | ControlStyles.ResizeRedraw
                | ControlStyles.SupportsTransparentBackColor, true);
            Height = 2;
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {

            using var brush = new LinearGradientBrush(
                ClientRectangle,
                Color.FromArgb(0, Accent),
                Color.FromArgb(0, Accent),
                LinearGradientMode.Horizontal);
            var blend = new ColorBlend
            {
                Colors = new[]
                {
                    Color.FromArgb(0, Accent),
                    Accent,
                    Accent,
                    Color.FromArgb(0, Accent),
                },
                Positions = new[] { 0f, 0.2f, 0.8f, 1f },
            };
            brush.InterpolationColors = blend;
            e.Graphics.FillRectangle(brush, ClientRectangle);
        }
    }

    public sealed class RoundedButton : Button
    {
        public int Radius { get; set; } = 8;
        public Color NormalBackColor { get; set; } = SurfaceAlt;
        public Color HoverBackColor  { get; set; } = SurfaceHi;
        public Color PressBackColor  { get; set; } = Border;
        public Color OutlineColor    { get; set; } = Color.Transparent;

        private bool _hover;
        private bool _pressed;

        public RoundedButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            FlatAppearance.MouseOverBackColor = Color.Transparent;
            FlatAppearance.MouseDownBackColor = Color.Transparent;
            UseVisualStyleBackColor = false;
            SetStyle(ControlStyles.UserPaint
                | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw
                | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs mevent) { _pressed = true; Invalidate(); base.OnMouseDown(mevent); }
        protected override void OnMouseUp(MouseEventArgs mevent)   { _pressed = false; Invalidate(); base.OnMouseUp(mevent); }
        protected override void OnEnabledChanged(EventArgs e)      { Invalidate(); base.OnEnabledChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var parentBg = ResolveOpaqueParentBg(Parent);

            Color bg;
            Color fg;
            if (!Enabled)
            {
                bg = Color.FromArgb(80, NormalBackColor.R, NormalBackColor.G, NormalBackColor.B);
                fg = Color.FromArgb(140, ForeColor.R, ForeColor.G, ForeColor.B);
            }
            else
            {
                bg = _pressed ? PressBackColor : _hover ? HoverBackColor : NormalBackColor;
                fg = ForeColor;
            }

            e.Graphics.Clear(parentBg);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = RoundedRect(rect, Radius);
            if (bg.A > 0)
            {
                using var brush = new SolidBrush(bg);
                e.Graphics.FillPath(brush, path);
            }
            if (OutlineColor.A > 0)
            {
                using var pen = new Pen(Enabled ? OutlineColor : Color.FromArgb(100, OutlineColor), 1);
                e.Graphics.DrawPath(pen, path);
            }

            var textRect = ClientRectangle;
            textRect.Inflate(-Padding.Horizontal / 2, 0);
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, fg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
    }

    public sealed class FieldHost : Panel
    {
        public TextBox TextBox { get; }
        public int Radius { get; set; } = 8;

        private bool _focused;

        public FieldHost()
        {
            BackColor = Color.Transparent;
            Padding = new Padding(12, 9, 12, 9);
            Height = 36;
            SetStyle(ControlStyles.UserPaint
                | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw
                | ControlStyles.SupportsTransparentBackColor, true);

            TextBox = new TextBox
            {
                BorderStyle = BorderStyle.None,
                BackColor = SurfaceAlt,
                ForeColor = TextPrimary,
                Font = BodyFont,
                Dock = DockStyle.Fill,
            };
            TextBox.GotFocus += (_, _) => { _focused = true; Invalidate(); };
            TextBox.LostFocus += (_, _) => { _focused = false; Invalidate(); };
            Controls.Add(TextBox);
        }

        public string TextValue
        {
            get => TextBox.Text;
            set => TextBox.Text = value;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            TextBox.Focus();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var parentBg = ResolveOpaqueParentBg(Parent);
            e.Graphics.Clear(parentBg);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = RoundedRect(rect, Radius);
            using (var brush = new SolidBrush(SurfaceAlt))
                e.Graphics.FillPath(brush, path);
            using (var pen = new Pen(_focused ? Accent : Border, 1))
                e.Graphics.DrawPath(pen, path);
        }
    }

    public sealed class ThemedCheckBox : CheckBox
    {
        private const int BoxSize = 16;
        private const int BoxTextGap = 8;
        private bool _hover;

        public ThemedCheckBox()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            FlatAppearance.MouseOverBackColor = Color.Transparent;
            FlatAppearance.MouseDownBackColor = Color.Transparent;
            FlatAppearance.CheckedBackColor = Color.Transparent;
            UseVisualStyleBackColor = false;
            SetStyle(ControlStyles.UserPaint
                | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw
                | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            AutoSize = true;
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            var textSize = TextRenderer.MeasureText(Text ?? string.Empty, Font);
            return new Size(BoxSize + BoxTextGap + textSize.Width + 2, Math.Max(BoxSize + 4, textSize.Height + 4));
        }

        protected override void OnMouseEnter(EventArgs eventargs) { _hover = true;  Invalidate(); base.OnMouseEnter(eventargs); }
        protected override void OnMouseLeave(EventArgs eventargs) { _hover = false; Invalidate(); base.OnMouseLeave(eventargs); }
        protected override void OnCheckedChanged(EventArgs e)     { Invalidate();   base.OnCheckedChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(ResolveOpaqueParentBg(Parent));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            var boxRect = new Rectangle(0, (Height - BoxSize) / 2, BoxSize, BoxSize);
            using var boxPath = RoundedRect(boxRect, 3);

            Color fill;
            Color border;
            if (Checked)
            {
                fill = _hover ? AccentHover : Accent;
                border = fill;
            }
            else
            {
                fill = SurfaceAlt;
                border = _hover ? Accent : Border;
            }

            using (var brush = new SolidBrush(fill))
                g.FillPath(brush, boxPath);
            using (var pen = new Pen(border, 1))
                g.DrawPath(pen, boxPath);

            if (Checked)
            {

                using var pen = new Pen(AccentText, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                int x = boxRect.X, y = boxRect.Y;
                g.DrawLines(pen, new[]
                {
                    new PointF(x + 3.5f,  y + 8.5f),
                    new PointF(x + 6.5f,  y + 11.5f),
                    new PointF(x + 12.5f, y + 4.5f),
                });
            }

            var textRect = new Rectangle(BoxSize + BoxTextGap, 0, Width - BoxSize - BoxTextGap, Height);
            var fg = Enabled ? ForeColor : Color.FromArgb(140, ForeColor);
            TextRenderer.DrawText(g, Text, Font, textRect, fg,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
    }

    public sealed class DarkMenuColors : ProfessionalColorTable
    {
        public override Color MenuItemSelected => SurfaceAlt;
        public override Color MenuItemSelectedGradientBegin => SurfaceAlt;
        public override Color MenuItemSelectedGradientEnd => SurfaceAlt;
        public override Color MenuItemBorder => Border;
        public override Color MenuBorder => Border;
        public override Color ToolStripDropDownBackground => Surface;
        public override Color ImageMarginGradientBegin => Surface;
        public override Color ImageMarginGradientMiddle => Surface;
        public override Color ImageMarginGradientEnd => Surface;
        public override Color SeparatorDark => Border;
        public override Color SeparatorLight => Border;
        public override Color MenuItemPressedGradientBegin => Border;
        public override Color MenuItemPressedGradientEnd => Border;
    }

    internal static class Win32
    {
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_20H1 = 20;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD  = 19;
        private const int DWMWA_BORDER_COLOR  = 34;
        private const int DWMWA_CAPTION_COLOR = 35;
        private const int DWMWA_TEXT_COLOR    = 36;

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        public static void UseDarkTitleBar(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return;

            int useDark = 1;

            int hr = DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_20H1, ref useDark, sizeof(int));
            if (hr != 0)
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, ref useDark, sizeof(int));

            int caption = ToColorRef(Background);
            int border  = ToColorRef(Background);
            int textCol = ToColorRef(TextPrimary);
            DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref caption, sizeof(int));
            DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR,  ref border,  sizeof(int));
            DwmSetWindowAttribute(hwnd, DWMWA_TEXT_COLOR,    ref textCol, sizeof(int));
        }

        private static int ToColorRef(Color c) => c.R | (c.G << 8) | (c.B << 16);
    }
}
