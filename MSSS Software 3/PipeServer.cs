using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading.Tasks;

namespace MSSS_Software_3
{
	public class PipeServer
	{
		private readonly NamedPipeServerStream server;
		private StreamWriter writer;

		public PipeServer(string pipeName = "MainAppPipe")
		{
			server = new NamedPipeServerStream(pipeName, PipeDirection.Out, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
		}

		// Wait for a client to connect once at startup
		public async Task WaitForClientAsync()
		{
			await server.WaitForConnectionAsync();
			writer = new StreamWriter(server) { AutoFlush = true };
		}

		// Send messages through the connected pipe
		public async Task SendAsync(string message)
		{
			if (server.IsConnected && writer != null)
			{
				await writer.WriteLineAsync(message);
			}
		}
	}
}
