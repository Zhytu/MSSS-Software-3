using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace Admin_Panel
{
	public partial class MainWindow : Window, INotifyPropertyChanged
	{
		private readonly PipeClient pipeClient = new PipeClient();

		private readonly PipeSenderToMain pipeSenderToMain = new PipeSenderToMain();

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


		public MainWindow()
		{
			InitializeComponent();
			DataContext = this;

			pipeClient = new PipeClient();
			pipeClient.MessageReceived += Pipe_MessageReceived; // <-- wire it here
			_ = StartPipeClientAsync();

			CommandBindings.Add(new CommandBinding(Quit, Quit_Executed));
			InputBindings.Add(new KeyBinding(Quit, Key.A, ModifierKeys.Alt));

		}

		private void Quit_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			this.Close();
		}

		// Overloaded constructor to accept initial staff ID and Name
		// In overloaded constructor
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
				Data1.IsReadOnly = false; // allow entering new ID
				Data1.IsEnabled = true;
			}
			else
			{
				BtnUpdate.IsEnabled = true;
				BtnDelete.IsEnabled = true;
				Data1.IsReadOnly = true; // lock ID for existing entries
				Data1.IsEnabled = false;
			}

			pipeClient = new PipeClient();
			pipeClient.MessageReceived += Pipe_MessageReceived;
			_ = StartPipeClientAsync();

			CommandBindings.Add(new CommandBinding(Quit, Quit_Executed));
			InputBindings.Add(new KeyBinding(Quit, Key.L, ModifierKeys.Alt));
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

		private int _newID;
		public int NewID
		{
			get => _newID;
			set { _newID = value; OnPropertyChanged(nameof(NewID)); }
		}


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

			string msg = $"U|{oldId}|{oldId}|{newName}"; // oldId == newId
			await pipeSenderToMain.SendAsync(msg);

			ShowStatusMessage($"Updated Staff ID {DisplayedID}.", 2);
		}

		protected override async void OnClosing(System.ComponentModel.CancelEventArgs e)
		{
			await SendSaveCommandAsync(); // notify main app to save CSV
			base.OnClosing(e);
		}



		// Send Save command to Main App
		private async Task SendSaveCommandAsync()
		{
			string msg = "S|"; // "S" = Save
			await pipeSenderToMain.SendAsync(msg);
		}


		private async void BtnCreate_Click(object sender, RoutedEventArgs e)
		{
			if (!BtnCreate.IsEnabled) return;

			if (!int.TryParse(Data1.Text, out int newId) || newId < 770000000)
			{
				//ShowStatusMessage("Invalid ID. Must start with 77 and must be 9 characters.", 3);
				//return;
			}

			string newName = Data2.Text?.Trim();
			if (string.IsNullOrWhiteSpace(newName))
			{
				ShowStatusMessage("Staff Name cannot be empty.", 3);
				return;
			}

			string msg = $"C|{newId}|{newName}";
			await pipeSenderToMain.SendAsync(msg);

			// Clear for next entry
			DisplayedID = 77;
			DisplayedName = "";
		}

		private void ShowStatusMessage(string message, double durationSeconds)
		{
			// Stop any existing animation
			myStatusBarText.BeginAnimation(OpacityProperty, null);

			// Set message and make fully visible
			myStatusBarText.Text = message;
			myStatusBarText.Opacity = 1;

			// Fade out after durationSeconds
			var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(500))
			{
				BeginTime = TimeSpan.FromSeconds(durationSeconds)
			};
			myStatusBarText.BeginAnimation(OpacityProperty, fadeOut);
		}


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
