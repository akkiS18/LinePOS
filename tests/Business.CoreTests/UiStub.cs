// Database integration tests use the real production SQL; only UI dispatch is absent on Linux.
namespace System.Windows {
    public class Application { public static Application? Current => null; public Dispatcher Dispatcher => new(); }
    public class Dispatcher { public bool CheckAccess() => true; public void BeginInvoke(Action action) => action(); public void Invoke(Action action) => action(); }
}

// KeyboardLayoutHelper is WPF/Win32-bound; console tests only need the Ru->En text mapping hook used by search.
namespace PosElectro.Desktop.Services {
    public static class KeyboardLayoutHelper { public static string ConvertRuToEn(string? input) => input ?? string.Empty; }
}
