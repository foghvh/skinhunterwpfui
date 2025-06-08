using System.Threading.Tasks;
using System.Threading;
using System.IO;
using System.IO.Pipes;
using System.Text;

namespace skinhunter.Services
{
    public class PipeClientService
    {
        private const string RequestTokenCommand = "GET_TOKEN";
        private const string TokenPrefix = "TOKEN:";
        private const string ErrorPrefix = "ERROR:";
        private const string AckCommand = "TOKEN_RECEIVED_ACK";
        private const string EndOfMessage = "\n"; // Asegúrate que esto es lo que espera el servidor para ReadLineAsync

        public async Task<string?> RequestTokenFromServerAsync(string pipeName, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(pipeName))
            {
                FileLoggerService.Log("[PipeClientService][skinhunter] Pipe name is null or empty.");
                return null;
            }

            try
            {
                FileLoggerService.Log($"[PipeClientService][skinhunter] Attempting to connect to pipe: {pipeName}");
                await using var clientStream = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

                await clientStream.ConnectAsync(10000, cancellationToken);
                if (cancellationToken.IsCancellationRequested)
                {
                    FileLoggerService.Log("[PipeClientService][skinhunter] Connection attempt cancelled during ConnectAsync.");
                    return null;
                }
                FileLoggerService.Log("[PipeClientService][skinhunter] Connected to pipe server.");

                // Usar leaveOpen: false para que los writers/readers se dispongan con el stream
                await using var writer = new StreamWriter(clientStream, Encoding.UTF8, bufferSize: 1024, leaveOpen: true) { AutoFlush = false }; // AutoFlush false, haremos flush manual
                using var reader = new StreamReader(clientStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);

                FileLoggerService.Log($"[PipeClientService][skinhunter] About to send command: {RequestTokenCommand}");
                await writer.WriteAsync($"{RequestTokenCommand}{EndOfMessage}"); // WriteAsync escribe al buffer interno
                await writer.FlushAsync(cancellationToken); // Flush explícito para enviar los datos por el pipe
                FileLoggerService.Log($"[PipeClientService][skinhunter] Sent command and flushed: {RequestTokenCommand}");

                FileLoggerService.Log($"[PipeClientService][skinhunter] Waiting for server response...");
                string? response = await reader.ReadLineAsync(cancellationToken); // ReadLineAsync espera hasta un \n o \r\n
                FileLoggerService.Log($"[PipeClientService][skinhunter] Received from server: {response}");

                if (cancellationToken.IsCancellationRequested)
                {
                    FileLoggerService.Log("[PipeClientService][skinhunter] Operation cancelled after receiving server response (or during wait).");
                    return null;
                }

                if (response != null && response.StartsWith(TokenPrefix))
                {
                    string token = response.Substring(TokenPrefix.Length); // .TrimEnd('\n') no es necesario con ReadLineAsync
                    FileLoggerService.Log($"[PipeClientService][skinhunter] Token received: {token.Substring(0, Math.Min(token.Length, 20))}... Sending ACK.");
                    await writer.WriteAsync($"{AckCommand}{EndOfMessage}");
                    await writer.FlushAsync(cancellationToken); // Flush explícito para el ACK
                    FileLoggerService.Log($"[PipeClientService][skinhunter] ACK sent and flushed.");
                    return token;
                }
                else if (response != null && response.StartsWith(ErrorPrefix))
                {
                    FileLoggerService.Log($"[PipeClientService][skinhunter] Server responded with error: {response}");
                    return null;
                }
                else
                {
                    FileLoggerService.Log($"[PipeClientService][skinhunter] Invalid response from server: {response}");
                    return null;
                }
            }
            catch (OperationCanceledException)
            {
                FileLoggerService.Log("[PipeClientService][skinhunter] Operation cancelled during pipe communication.");
                return null;
            }
            catch (TimeoutException)
            {
                FileLoggerService.Log("[PipeClientService][skinhunter] Timeout connecting to pipe server.");
                return null;
            }
            catch (IOException ex)
            {
                FileLoggerService.Log($"[PipeClientService][skinhunter] IOException: {ex.Message}. Server might have closed the pipe or pipe is broken.");
                return null;
            }
            catch (Exception ex)
            {
                FileLoggerService.Log($"[PipeClientService][skinhunter] Error requesting token: {ex.Message}");
                return null;
            }
        }
    }
}
