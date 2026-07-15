using System.IO.Pipes;
using System.Text;

namespace DwmBorderRemover.Services;

internal sealed class IpcServer : IDisposable
{
    private const string PipeName = "DwmBorderRemover.Ipc.v1";
    private readonly CancellationTokenSource _cancellation = new();
    private readonly SynchronizationContext _synchronizationContext;
    private readonly Action<string> _handler;
    private readonly Task _listenerTask;

    internal IpcServer(SynchronizationContext synchronizationContext, Action<string> handler)
    {
        _synchronizationContext = synchronizationContext;
        _handler = handler;
        _listenerTask = Task.Run(ListenLoopAsync);
    }

    internal static bool TrySend(string command, int timeoutMilliseconds = 1500)
    {
        try
        {
            using NamedPipeClientStream client = new(
                ".",
                PipeName,
                PipeDirection.Out,
                PipeOptions.None);

            client.Connect(timeoutMilliseconds);
            using StreamWriter writer = new(client, new UTF8Encoding(false), leaveOpen: false)
            {
                AutoFlush = true
            };
            writer.WriteLine(command);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task ListenLoopAsync()
    {
        while (!_cancellation.IsCancellationRequested)
        {
            try
            {
                await using NamedPipeServerStream server = new(
                    PipeName,
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

                await server.WaitForConnectionAsync(_cancellation.Token).ConfigureAwait(false);
                using StreamReader reader = new(server, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
                string? command = await reader.ReadLineAsync(_cancellation.Token).ConfigureAwait(false);

                if (!string.IsNullOrWhiteSpace(command))
                {
                    _synchronizationContext.Post(_ => _handler(command.Trim()), null);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception exception)
            {
                AppLog.Write("IPC listener error: " + exception.Message);
                await Task.Delay(250, _cancellation.Token).ConfigureAwait(false);
            }
        }
    }

    public void Dispose()
    {
        _cancellation.Cancel();
        try
        {
            _listenerTask.Wait(1000);
        }
        catch
        {
            // Shutdown should not be blocked by a pipe listener.
        }

        _cancellation.Dispose();
    }
}
