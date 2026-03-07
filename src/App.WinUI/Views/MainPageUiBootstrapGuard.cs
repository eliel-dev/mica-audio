namespace App.WinUI.Views;

internal sealed class MainPageUiBootstrapGuard
{
    private int depth;

    internal bool IsActive => Volatile.Read(ref depth) > 0;

    internal IDisposable Enter()
    {
        Interlocked.Increment(ref depth);
        return new Scope(this);
    }

    private void Exit()
    {
        Interlocked.Decrement(ref depth);
    }

    private sealed class Scope : IDisposable
    {
        private MainPageUiBootstrapGuard? owner;

        internal Scope(MainPageUiBootstrapGuard owner)
        {
            this.owner = owner;
        }

        public void Dispose()
        {
            var current = Interlocked.Exchange(ref owner, null);
            current?.Exit();
        }
    }
}
