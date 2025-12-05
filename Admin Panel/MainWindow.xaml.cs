using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace Admin_Panel
{
	public partial class MainWindow : Window, INotifyPropertyChanged
	{
		// Client for receiving messages from the main application via named pipe
		private readonly PipeClient pipeClient = new PipeClient();

		// Sender for sending commands/messages to the main application
		private readonly PipeSenderToMain pipeSenderToMain = new PipeSenderToMain();

		// Application-level Quit command
		public static readonly RoutedCommand Quit = new RoutedCommand();

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

		// Default constructor: set up UI, pipe client and command bindings
		public MainWindow()
		{
			InitializeComponent();
			DataContext = this;

			pipeClient.MessageReceived += Pipe_MessageReceived;
			_ = StartPipeClientAsync();

			CommandBindings.Add(new CommandBinding(Quit, Quit_Executed));
			InputBindings.Add(new KeyBinding(Quit, Key.A, ModifierKeys.Alt));
		}

		private void Quit_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			this.Close();
		}

		// Overload: initialize window with a specific staff entry and optional create mode
		public MainWindow(int staffId, string staffName, bool enableCreate = false)
		{
			InitializeComponent();
			DataContext = this;

			DisplayedID = staffId;
			DisplayedName = staffName;

			BtnCreate.IsEnabled = enableCreate;

			if (enableCreate)
			{
				BtnUpdate.IsEnabled = false;
				BtnDelete.IsEnabled = false;
				Data1.IsReadOnly = false;
				Data1.IsEnabled = true;
			}
			else
			{
				BtnUpdate.IsEnabled = true;
				BtnDelete.IsEnabled = true;
				Data1.IsReadOnly = true;
				Data1.IsEnabled = false;
			}

			pipeClient.MessageReceived += Pipe_MessageReceived;
			_ = StartPipeClientAsync();

			CommandBindings.Add(new CommandBinding(Quit, Quit_Executed));
			InputBindings.Add(new KeyBinding(Quit, Key.L, ModifierKeys.Alt));
		}

		// Start the pipe client and attach a lightweight handler to update DisplayedID when an integer message is received
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

		// Handle incoming pipe messages that contain "ID,Name"
		private void Pipe_MessageReceived(string msg)
		{
			Dispatcher.Invoke(() =>
			{
				var parts = msg.Split(',');
				if (parts.Length >= 2)
				{
					if (int.TryParse(parts[0], out int id))
					{
						DisplayedID = id;
						DisplayedName = parts[1];
					}
				}
				Debug.WriteLine($"Received: {msg}");
			});
		}

		private int _newID;
		public int NewID
		{
			get => _newID;
			set { _newID = value; OnPropertyChanged(nameof(NewID)); }
		}

		// Update an existing staff record: validate and send update command to main app
		private async void BtnUpdate_Click(object sender, RoutedEventArgs e)
		{
			if (DisplayedID <= 0) return;

			string oldId = DisplayedID.ToString();
			string newName = Data2.Text?.Trim();
			if (string.IsNullOrWhiteSpace(newName))
			{
				ShowStatusMessage("Staff Name cannot be empty.", 3);
				return;
			}

			string msg = $"U|{oldId}|{oldId}|{newName}";
			await pipeSenderToMain.SendAsync(msg);

			ShowStatusMessage($"Updated Staff ID {DisplayedID}.", 2);
		}

		// Ensure main application saves data before this window closes
		protected override async void OnClosing(System.ComponentModel.CancelEventArgs e)
		{
			await SendSaveCommandAsync();
			base.OnClosing(e);
		}

		// Send Save command to main application
		private async Task SendSaveCommandAsync()
		{
			string msg = "S|";
			await pipeSenderToMain.SendAsync(msg);
		}

		// Create a new staff entry: validate and send create command to main app
		private async void BtnCreate_Click(object sender, RoutedEventArgs e)
		{
			if (!BtnCreate.IsEnabled) return;

			if (!int.TryParse(Data1.Text, out int newId) || newId < 770000000)
			{
				// Intentionally allow through but validation placeholder retained
			}

			string newName = Data2.Text?.Trim();
			if (string.IsNullOrWhiteSpace(newName))
			{
				ShowStatusMessage("Staff Name cannot be empty.", 3);
				return;
			}

			string msg = $"C|{newId}|{newName}";
			await pipeSenderToMain.SendAsync(msg);

			DisplayedID = 77;
			DisplayedName = "";
		}

		// Show a transient status message in the status bar with fade-out animation
		private void ShowStatusMessage(string message, double durationSeconds)
		{
			myStatusBarText.BeginAnimation(OpacityProperty, null);

			myStatusBarText.Text = message;
			myStatusBarText.Opacity = 1;

			var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(500))
			{
				BeginTime = TimeSpan.FromSeconds(durationSeconds)
			};
			myStatusBarText.BeginAnimation(OpacityProperty, fadeOut);
		}

		// Delete current staff entry and notify main app
		private async void BtnDelete_Click(object sender, RoutedEventArgs e)
		{
			if (DisplayedID <= 0) return;

			string msg = $"D|{DisplayedID}";
			await pipeSenderToMain.SendAsync(msg);

			DisplayedID = 0;
			DisplayedName = "";
			Data1.IsReadOnly = true;
		}
	}
}
