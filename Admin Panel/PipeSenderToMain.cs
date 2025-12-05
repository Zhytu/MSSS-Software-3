using System.IO;
using System.IO.Pipes;
using System.Threading.Tasks;

namespace Admin_Panel
{
	public class PipeSenderToMain
	{
		public async Task SendAsync(string message, string pipeName = "AdminToMainPipe")
		{
			using (var client = new NamedPipeClientStream(".", pipeName, PipeDirection.Out, PipeOptions.Asynchronous))
			{
				await client.ConnectAsync();
				using (var writer = new StreamWriter(client) { AutoFlush = true })
				{
					await writer.WriteLineAsync(message);
				}
			}
		}
	}
}
