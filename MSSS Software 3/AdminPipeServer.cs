using System;
using System.IO;
using System.IO.Pipes;
using System.Threading.Tasks;

namespace MSSS_Software_3
{
	public class AdminPipeServer
	{
		public event Action<string> MessageReceived;

		public async Task StartAsync(string pipeName = "AdminToMainPipe")
		{
			while (true)
			{
				using (var server = new NamedPipeServerStream(pipeName, PipeDirection.In, 1,
															  PipeTransmissionMode.Byte, PipeOptions.Asynchronous))
				{
					await server.WaitForConnectionAsync();
					using (var reader = new StreamReader(server))
					{
						string line;
						while ((line = await reader.ReadLineAsync()) != null)
						{
							MessageReceived?.Invoke(line);
						}
					}
				}
				// loop back and wait for new connection
			}
		}
	}
}
