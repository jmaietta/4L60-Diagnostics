using System.Windows.Input;

namespace LT1Diagnostics.App.ViewModels;

public sealed class RelayCommand(Action execute, Func<bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged;

    public event EventHandler<Exception>? Faulted;

    public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;

    public void Execute(object? parameter)
    {
        try
        {
            execute();
        }
        catch (Exception exception) when (Faulted is not null && exception is not OperationCanceledException)
        {
            Faulted.Invoke(this, exception);
        }
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
