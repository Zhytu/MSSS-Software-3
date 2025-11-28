using System;
using System.IO;
using System.IO.Pipes;
using System.Threading.Tasks;

namespace Admin_Panel
{
    public class PipeSender
    {
        private NamedPipeClientStream client;
        private StreamWriter writer;

        public PipeSender()
        {
            client = new NamedPipeClientStream(".", "MainAppPipe", PipeDirection.Out, PipeOptions.Asynchronous);
        }

        public async Task ConnectAsync()
        {
            await client.ConnectAsync(); // Wait until connected
            writer = new StreamWriter(client) { AutoFlush = true };
        }

        public async Task SendMessageAsync(string id, string name, string mode)
        {
            if (client == null || !client.IsConnected)
                throw new InvalidOperationException("Pipe is not connected yet.");

            string message = $"{id},{name},{mode}";
            await writer.WriteLineAsync(message); // write without disposing
        }

        public void Close()
        {
            writer?.Dispose();
            client?.Dispose();
        }
    }
}
