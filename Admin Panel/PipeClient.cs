using System;
using System.IO;
using System.IO.Pipes;
using System.Threading.Tasks;

namespace Admin_Panel
{
	public class PipeClient
	{
		public event Action<string> MessageReceived;

		private NamedPipeClientStream client;

		public async Task StartAsync(string pipeName = "MainAppPipe")
		{
			client = new NamedPipeClientStream(".", pipeName, PipeDirection.In, PipeOptions.Asynchronous);
			await client.ConnectAsync();

			var reader = new StreamReader(client);
			while (true)
			{
				string msg = await reader.ReadLineAsync();
				if (msg == null) break;
				MessageReceived?.Invoke(msg);
			}
		}
	}
}
