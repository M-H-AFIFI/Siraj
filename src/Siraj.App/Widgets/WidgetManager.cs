using Siraj.App.Services;
using Siraj.Core.Settings;

namespace Siraj.App.Widgets;

internal sealed class WidgetManager
{
    private readonly List<WidgetWindow> _windows = [];

    public event Action? Changed;

    public List<Windows.Graphics.RectInt32> Rects(WidgetWindow except) =>
        _windows.Where(w => w != except && w.AppWindow.IsVisible).Select(w => w.Placement).ToList();

    public IReadOnlyList<WidgetConfig> Configs => App.Settings.Current.Widgets;

    public void Restore()
    {
        foreach (var config in App.Settings.Current.Widgets) Open(config);
    }

    private void Open(WidgetConfig config)
    {
        try
        {
            var window = new WidgetWindow(config);
            window.Closed += (_, _) => _windows.Remove(window);
            _windows.Add(window);
            window.Show();
        }
        catch (Exception e)
        {
            Log.Error($"opening widget {config.Face}", e);
        }
    }

    public void Add(string face, string background)
    {
        var config = new WidgetConfig { Face = face, Background = background };
        App.Settings.Update(s => s with { Widgets = [.. s.Widgets, config] });
        Open(config);
        Changed?.Invoke();
    }

    public void Update(WidgetConfig config)
    {
        App.Settings.Update(s => s with { Widgets = s.Widgets.Select(w => w.Id == config.Id ? config : w).ToList() });
        _windows.FirstOrDefault(w => w.Id == config.Id)?.Apply(config);
        Changed?.Invoke();
    }

    public void SavePosition(string id, int x, int y) =>
        App.Settings.Update(s => s with { Widgets = s.Widgets.Select(w => w.Id == id ? w with { X = x, Y = y } : w).ToList() });

    public void Remove(string id)
    {
        App.Settings.Update(s => s with { Widgets = s.Widgets.Where(w => w.Id != id).ToList() });
        var window = _windows.FirstOrDefault(w => w.Id == id);
        window?.Close();
        Changed?.Invoke();
    }

    public void Refresh()
    {
        foreach (var window in _windows) window.Refresh();
    }

    public void Peek()
    {
        foreach (var window in _windows) window.Peek(TimeSpan.FromSeconds(6));
    }

    public void CloseAll()
    {
        foreach (var window in _windows.ToList()) window.Close();
    }
}
