using System.ComponentModel;
using System.Diagnostics;
using System.Windows;

namespace Admin_Panel
{
	public partial class MainWindow : Window, INotifyPropertyChanged
	{
		private readonly PipeClient pipeClient = new PipeClient();

		private int _displayedID = -1;
		public int DisplayedID
		{
			get => _displayedID;
			set
			{
				if (_displayedID != value)
				{
					_displayedID = value;
					OnPropertyChanged(nameof(DisplayedID));
				}
			}
		}

		private string _displayedName;
		public string DisplayedName
		{
			get => _displayedName;
			set
			{
				if (_displayedName != value)
				{
					_displayedName = value;
					OnPropertyChanged(nameof(DisplayedName));
				}
			}
		}


		public MainWindow()
		{
			InitializeComponent();
			DataContext = this;

			pipeClient = new PipeClient();
			pipeClient.MessageReceived += Pipe_MessageReceived; // <-- wire it here
			_ = StartPipeClientAsync();


		}

		private async Task StartPipeClientAsync()
		{
			pipeClient.MessageReceived += msg =>
			{
				Dispatcher.Invoke(() =>
				{
					if (int.TryParse(msg, out int id))
						DisplayedID = id;
				});
			};
			await pipeClient.StartAsync();
		}

		public event PropertyChangedEventHandler PropertyChanged;
		private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

		private void Pipe_MessageReceived(string msg)
		{
			Dispatcher.Invoke(() =>
			{
				// Expecting "ID,Name"
				var parts = msg.Split(',');
				if (parts.Length >= 2)
				{
					if (int.TryParse(parts[0], out int id))
					{
						DisplayedID = id;
						DisplayedName = parts[1]; // Add this property to MainWindow
					}
				}
				Debug.WriteLine($"Received: {msg}");
			});
		}



		private async void BtnUpdate_Click(object sender, RoutedEventArgs e)
		{
			
		}

		private void BtnCreate_Click(object sender, RoutedEventArgs e)
		{

		}

		private void BtnDelete_Click(object sender, RoutedEventArgs e)
		{

		}
	}
}
