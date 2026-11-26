namespace Siraj.Core.Sky;

public static class SkyPalette
{
    private const float SkyAmount = 0.50f;
    private const float CardAmount = 0.55f;
    private const float ArcAmount = 0.30f;

    public static SkyColors Tint(SkyColors sky, Argb accent)
    {
        if (accent.A == 0) return sky;
        var accentHsl = Hsl.Of(accent);
        if (accentHsl.S < 0.12f) return sky;
        return sky with
        {
            Sky = sky.Sky.Select(c => Towards(c, accentHsl, SkyAmount)).ToArray(),
            Arc = sky.Arc.Select(c => Towards(c, accentHsl, ArcAmount)).ToArray(),
            CardBorderTop = Towards(sky.CardBorderTop, accentHsl, CardAmount),
            CardBorderBottom = Towards(sky.CardBorderBottom, accentHsl, CardAmount),
            FooterBg = Towards(sky.FooterBg, accentHsl, CardAmount),
            CardBg = Towards(sky.CardBg, accentHsl, CardAmount),
            CloudDark = Towards(sky.CloudDark, accentHsl, SkyAmount * 0.5f),
        };
    }

    internal static Argb Towards(Argb color, Hsl accent, float amount)
    {
        var hsl = Hsl.Of(color);
        float dark = 1f - hsl.L;
        float weight = amount * Coolness(hsl) * (0.15f + 0.85f * dark * dark);
        if (weight <= 0.001f) return color;
        var target = new Hsl(
            accent.H,
            MathF.Min(accent.S, MathF.Max(hsl.S, 0.35f)),
            hsl.L).ToArgb(color.A);
        return Argb.Lerp(color, target, Math.Clamp(weight, 0f, 1f));
    }

    internal static float Coolness(Hsl hsl)
    {
        if (hsl.S < 0.10f) return 1f;
        float d = MathF.Abs(hsl.H - 230f);
        if (d > 180f) d = 360f - d;
        if (d <= 60f) return 1f;
        if (d >= 120f) return 0f;
        return 1f - (d - 60f) / 60f;
    }

    internal readonly record struct Hsl(float H, float S, float L)
    {
        public Argb ToArgb(byte alpha)
        {
            float c = (1f - MathF.Abs(2f * L - 1f)) * S;
            float hp = (H / 60f) % 6f;
            float x = c * (1f - MathF.Abs(hp % 2f - 1f));
            var (r1, g1, b1) = hp switch
            {
                < 1f => (c, x, 0f),
                < 2f => (x, c, 0f),
                < 3f => (0f, c, x),
                < 4f => (0f, x, c),
                < 5f => (x, 0f, c),
                _ => (c, 0f, x),
            };
            float m = L - c / 2f;
            return new Argb(
                alpha,
                (byte)Math.Clamp(MathF.Round((r1 + m) * 255f), 0, 255),
                (byte)Math.Clamp(MathF.Round((g1 + m) * 255f), 0, 255),
                (byte)Math.Clamp(MathF.Round((b1 + m) * 255f), 0, 255));
        }

        public static Hsl Of(Argb color)
        {
            float r = color.R / 255f;
            float g = color.G / 255f;
            float b = color.B / 255f;
            float maxC = MathF.Max(r, MathF.Max(g, b));
            float minC = MathF.Min(r, MathF.Min(g, b));
            float l = (maxC + minC) / 2f;
            float delta = maxC - minC;
            if (delta < 1e-5f) return new Hsl(0f, 0f, l);
            float s = delta / (1f - MathF.Abs(2f * l - 1f));
            float h = maxC == r
                ? 60f * (((g - b) / delta) % 6f)
                : maxC == g
                    ? 60f * (((b - r) / delta) + 2f)
                    : 60f * (((r - g) / delta) + 4f);
            return new Hsl(h < 0f ? h + 360f : h, Math.Clamp(s, 0f, 1f), l);
        }
    }
}
