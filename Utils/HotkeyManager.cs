using System.Diagnostics;
using System.Runtime.InteropServices;

public class HotKeyManager : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private readonly Dictionary<int, Action> _registeredHotKeys = new();
    private bool _disposed;
    private readonly Form _form;
    private int _currentId = 1;

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public HotKeyManager(Form form)
    {
        _form = form ?? throw new ArgumentNullException(nameof(form));
        // Ensure form handle is created
        if (!_form.IsHandleCreated)
        {
            _form.HandleCreated += (s, e) => InitializeHotKeys();
        }
        else
        {
            InitializeHotKeys();
        }
    }

    private void InitializeHotKeys()
    {
        // Re-register any existing hotkeys after handle creation
        var existingHotKeys = _registeredHotKeys.ToList();
        _registeredHotKeys.Clear();
        foreach (var hotKey in existingHotKeys)
        {
            RegisterHotKey(Keys.None, hotKey.Value); // You'll need to store the actual keys used
        }
    }

    public bool RegisterHotKey(Keys key, Action callback, bool ctrl = false, bool alt = false, bool shift = false)
    {
        if (_disposed) return false;

        try
        {
            uint modifiers = 0;
            if (ctrl) modifiers |= 0x0002;
            if (alt) modifiers |= 0x0001;
            if (shift) modifiers |= 0x0004;

            int id = _currentId++;
            if (!_form.IsHandleCreated || RegisterHotKey(_form.Handle, id, modifiers, (uint)key))
            {
                _registeredHotKeys[id] = callback;
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error registering hotkey: {ex.Message}");
            return false;
        }
    }

    public bool HandleHotKey(Message m)
    {
        if (_disposed) return false;

        try
        {
            if (m.Msg == WM_HOTKEY && _registeredHotKeys.TryGetValue(m.WParam.ToInt32(), out var callback))
            {
                callback?.Invoke();
                return true;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error handling hotkey: {ex.Message}");
        }
        return false;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing && _form.IsHandleCreated)
            {
                foreach (int id in _registeredHotKeys.Keys)
                {
                    try
                    {
                        UnregisterHotKey(_form.Handle, id);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error unregistering hotkey: {ex.Message}");
                    }
                }
                _registeredHotKeys.Clear();
            }
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}