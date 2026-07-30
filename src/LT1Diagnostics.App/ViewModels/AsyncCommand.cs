using System.Windows.Input;

namespace LT1Diagnostics.App.ViewModels;

public sealed class AsyncCommand(Func<Task> execute, Func<bool>? canExecute = null) : ICommand
{
    private bool _executing;

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => !_executing && (canExecute?.Invoke() ?? true);

    public async void Execute(object? parameter) => await ExecuteAsync().ConfigureAwait(true);

    public async Task ExecuteAsync()
    {
        if (!CanExecute(null))
        {
            return;
        }

        _executing = true;
        RaiseCanExecuteChanged();
        try
        {
            await execute().ConfigureAwait(true);
        }
        finally
        {
            _executing = false;
            RaiseCanExecuteChanged();
        }
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
