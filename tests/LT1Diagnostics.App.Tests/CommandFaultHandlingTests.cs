using LT1Diagnostics.App.ViewModels;

namespace LT1Diagnostics.App.Tests;

public sealed class CommandFaultHandlingTests
{
    [Fact]
    public void RelayCommandRaisesFaultedInsteadOfThrowing()
    {
        var expected = new InvalidOperationException("boom");
        var command = new RelayCommand(() => throw expected);
        Exception? observed = null;
        command.Faulted += (_, exception) => observed = exception;

        command.Execute(null);

        Assert.Same(expected, observed);
    }

    [Fact]
    public void RelayCommandStillThrowsWhenNothingHandlesTheFault()
    {
        var command = new RelayCommand(() => throw new InvalidOperationException("boom"));

        Assert.Throws<InvalidOperationException>(() => command.Execute(null));
    }

    [Fact]
    public async Task AsyncCommandRaisesFaultedInsteadOfThrowing()
    {
        var expected = new InvalidOperationException("boom");
        var command = new AsyncCommand(() => Task.FromException(expected));
        Exception? observed = null;
        command.Faulted += (_, exception) => observed = exception;

        await command.ExecuteAsync();

        Assert.Same(expected, observed);
        Assert.True(command.CanExecute(null));
    }

    [Fact]
    public async Task AsyncCommandStillThrowsWhenNothingHandlesTheFault()
    {
        var command = new AsyncCommand(() => Task.FromException(new InvalidOperationException("boom")));

        await Assert.ThrowsAsync<InvalidOperationException>(() => command.ExecuteAsync());
    }

    [Fact]
    public async Task AsyncCommandDoesNotReportCancellationAsFault()
    {
        var command = new AsyncCommand(() => Task.FromException(new OperationCanceledException()));
        Exception? observed = null;
        command.Faulted += (_, exception) => observed = exception;

        await Assert.ThrowsAsync<OperationCanceledException>(() => command.ExecuteAsync());

        Assert.Null(observed);
    }
}
