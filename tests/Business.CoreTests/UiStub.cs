// Database integration tests use the real production SQL; only UI dispatch is absent on Linux.
namespace System.Windows {
    public class Application { public static Application? Current => null; public Dispatcher Dispatcher => new(); }
    public class Dispatcher { public bool CheckAccess() => true; public void BeginInvoke(Action action) => action(); public void Invoke(Action action) => action(); }
}
