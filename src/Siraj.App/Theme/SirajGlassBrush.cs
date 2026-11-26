using Microsoft.Graphics.Canvas.Effects;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Siraj.App.Theme;

public sealed class SirajGlassBrush : XamlCompositionBrushBase
{
    private readonly record struct RoleSpec(Color Tint, float Blur, Color Fallback);

    private static readonly Dictionary<string, RoleSpec> RoleSpecs = new(StringComparer.Ordinal);
    private static readonly List<WeakReference<SirajGlassBrush>> Instances = [];

    private CompositionEffectBrush? _effectBrush;
    private CompositionColorBrush? _solidBrush;
    private CompositionBackdropBrush? _backdrop;
    private bool _connected;
    private bool _usingEffect;
    private string _role = "Card";

    public string Role
    {
        get => _role;
        set
        {
            _role = string.IsNullOrEmpty(value) ? "Card" : value;
            ApplyStoredRole();
        }
    }

    public Color TintColor { get; private set; } = Color.FromArgb(140, 255, 255, 255);
    public float BlurAmount { get; private set; } = 10f;

    public SirajGlassBrush()
    {
        FallbackColor = Color.FromArgb(200, 255, 255, 255);
        Instances.Add(new WeakReference<SirajGlassBrush>(this));
        ApplyStoredRole();
    }

    public SirajGlassBrush(string role, Color tintColor, float blurAmount, Color fallback)
    {
        _role = string.IsNullOrEmpty(role) ? "Card" : role;
        TintColor = tintColor;
        BlurAmount = Math.Max(0f, blurAmount);
        FallbackColor = fallback;
        Instances.Add(new WeakReference<SirajGlassBrush>(this));
    }

    public SirajGlassBrush Clone()
    {
        if (RoleSpecs.TryGetValue(_role, out var spec))
            return new SirajGlassBrush(_role, spec.Tint, spec.Blur, spec.Fallback);
        return new SirajGlassBrush(_role, TintColor, BlurAmount, FallbackColor);
    }

    public static void UpdateRole(string role, Color tintColor, float blurAmount, Color fallback)
    {
        RoleSpecs[role] = new RoleSpec(tintColor, Math.Max(0f, blurAmount), fallback);
        Instances.RemoveAll(w => !w.TryGetTarget(out _));
        foreach (var w in Instances)
        {
            if (w.TryGetTarget(out var brush) && string.Equals(brush._role, role, StringComparison.Ordinal))
                brush.Update(tintColor, blurAmount, fallback);
        }
    }

    private void ApplyStoredRole()
    {
        if (RoleSpecs.TryGetValue(_role, out var spec))
            Update(spec.Tint, spec.Blur, spec.Fallback);
    }

    public void Update(Color tintColor, float blurAmount, Color fallback)
    {
        TintColor = tintColor;
        BlurAmount = Math.Max(0f, blurAmount);
        FallbackColor = fallback;
        if (!_connected) return;

        bool wantEffect = BlurAmount > 0.5f;
        if (wantEffect != _usingEffect)
        {
            RebuildBrush();
            return;
        }

        if (_usingEffect && _effectBrush != null)
        {
            _effectBrush.Properties.InsertScalar("Blur.BlurAmount", BlurAmount);
            _effectBrush.Properties.InsertColor("Tint.Color", TintColor);
        }
        else if (_solidBrush != null)
        {
            _solidBrush.Color = TintColor;
        }
    }

    protected override void OnConnected()
    {
        base.OnConnected();
        _connected = true;
        // Always pull latest role spec before creating the Composition brush so deferred XAML
        // ThemeResources immediately reflect Dark/AMOLED mode on their very first frame.
        if (RoleSpecs.TryGetValue(_role, out var spec))
        {
            TintColor = spec.Tint;
            BlurAmount = spec.Blur;
            FallbackColor = spec.Fallback;
        }
        if (CompositionBrush == null) RebuildBrush();
    }

    protected override void OnDisconnected()
    {
        _connected = false;
        DisposeBrushes();
        base.OnDisconnected();
    }

    private void RebuildBrush()
    {
        DisposeBrushes();
        var compositor = CompositionTarget.GetCompositorForCurrentThread();
        if (BlurAmount > 0.5f)
        {
            try
            {
                var blur = new GaussianBlurEffect
                {
                    Name = "Blur",
                    BlurAmount = BlurAmount,
                    BorderMode = EffectBorderMode.Hard,
                    Optimization = EffectOptimization.Balanced,
                    Source = new CompositionEffectSourceParameter("Backdrop"),
                };
                var tint = new ColorSourceEffect
                {
                    Name = "Tint",
                    Color = TintColor,
                };
                var composite = new CompositeEffect
                {
                    Mode = Microsoft.Graphics.Canvas.CanvasComposite.SourceOver,
                    Sources = { blur, tint },
                };
                using var factory = compositor.CreateEffectFactory(composite, new[] { "Blur.BlurAmount", "Tint.Color" });
                _backdrop = compositor.CreateBackdropBrush();
                _effectBrush = factory.CreateBrush();
                _effectBrush.SetSourceParameter("Backdrop", _backdrop);
                _usingEffect = true;
                CompositionBrush = _effectBrush;
                return;
            }
            catch
            {
                DisposeBrushes();
            }
        }

        _solidBrush = compositor.CreateColorBrush(TintColor);
        _usingEffect = false;
        CompositionBrush = _solidBrush;
    }

    private void DisposeBrushes()
    {
        CompositionBrush = null;
        _effectBrush?.Dispose();
        _effectBrush = null;
        _backdrop?.Dispose();
        _backdrop = null;
        _solidBrush?.Dispose();
        _solidBrush = null;
    }
}
