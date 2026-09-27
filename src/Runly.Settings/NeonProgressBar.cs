using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace Runly.Settings;

/// <summary>Linear progress bar of the update and install panels. The work only sets <see cref="Target"/>
/// and <see cref="Ceiling"/>; the bar approaches the target by 8% of the gap each frame and, once there,
/// creeps toward the ceiling by 0.6% of the gap, so a long step never looks frozen. It never goes back.</summary>
internal sealed class NeonProgressBar : Control
{
    private const int TrackAlpha = TeknesyumTokens.Tone10Alpha;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 16 };
    private readonly bool _reducedMotion = ReducedMotion();
    private double _shown;
    private double _target;
    private double _ceiling;
    private float _scan;

    public NeonProgressBar()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Height = Metrics.Px(TeknesyumTokens.Space2);
        _timer.Tick += (_, _) => Step();
    }

    public event EventHandler? DisplayChanged;

    public double Displayed => _shown;

    public bool Running { get; set; } = true;

    public double Target
    {
        get => _target;
        set
        {
            _target = Math.Clamp(value, _target, 100);
            _ceiling = Math.Max(_ceiling, _target);
            Kick();
        }
    }

    public double Ceiling
    {
        get => _ceiling;
        set
        {
            _ceiling = Math.Clamp(value, _target, 100);
            Kick();
        }
    }

    public void Reset()
    {
        _shown = _target = _ceiling = 0;
        Invalidate();
        DisplayChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Complete()
    {
        _target = _ceiling = 100;
        Kick();
    }

    private void Kick()
    {
        if (_reducedMotion || !IsHandleCreated)
        {
            _shown = Math.Max(_shown, _target);
            Invalidate();
            DisplayChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        _timer.Start();
    }

    private void Step()
    {
        var before = _shown;
        if (_shown < _target)
        {
            _shown = Math.Min(_target, _shown + Math.Max(0.2, (_target - _shown) * 0.08));
        }
        else if (_shown < _ceiling)
        {
            _shown = Math.Min(_ceiling, _shown + ((_ceiling - _shown) * 0.006));
        }

        _scan = (_scan + 0.012f) % 1f;
        if (_shown >= 100 || (!Running && _shown >= _target))
        {
            _timer.Stop();
        }

        Invalidate();
        if (Math.Floor(before) != Math.Floor(_shown))
        {
            DisplayChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var track = new Rectangle(0, 0, Width - 1, Height - 1);
        if (track.Width <= 0 || track.Height <= 0)
        {
            return;
        }

        var radius = Math.Min(NeonTheme.CornerRadius, track.Height / 2);
        using (var path = NeonTheme.RoundedRect(track, radius))
        using (var fill = new SolidBrush(Color.FromArgb(TrackAlpha, Palette.Renk1)))
        {
            g.FillPath(fill, path);
        }

        var width = (int)Math.Round(track.Width * Math.Clamp(_shown, 0, 100) / 100);
        if (width <= 0)
        {
            return;
        }

        var done = new Rectangle(0, 0, Math.Max(width, track.Height), track.Height);
        using (var path = NeonTheme.RoundedRect(done, radius))
        using (var gradient = new LinearGradientBrush(track, Palette.Renk1, Palette.Renk2, LinearGradientMode.Horizontal))
        {
            g.FillPath(gradient, path);
            if (Running && !_reducedMotion && _shown < 100)
            {
                var light = Math.Max(Metrics.Px(TeknesyumTokens.Space5), done.Width / 5);
                var left = (int)(_scan * (done.Width + light)) - light;
                var band = new Rectangle(left, 0, light, done.Height);
                using var shine = new LinearGradientBrush(new Rectangle(left - 1, 0, light + 2, done.Height),
                    Color.FromArgb(0, Palette.TextStrong), Color.FromArgb(0, Palette.TextStrong), LinearGradientMode.Horizontal)
                {
                    InterpolationColors = new ColorBlend
                    {
                        Colors = [Color.FromArgb(0, Palette.TextStrong), Color.FromArgb(TrackAlpha * 3, Palette.TextStrong), Color.FromArgb(0, Palette.TextStrong)],
                        Positions = [0f, 0.5f, 1f],
                    },
                };
                g.SetClip(path);
                g.FillRectangle(shine, band);
                g.ResetClip();
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
        }

        base.Dispose(disposing);
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(uint action, uint param, [MarshalAs(UnmanagedType.Bool)] out bool value, uint winIni);

    private static bool ReducedMotion()
    {
        const uint GetClientAreaAnimation = 0x1042;
        return SystemParametersInfo(GetClientAreaAnimation, 0, out var enabled, 0) && !enabled;
    }
}
