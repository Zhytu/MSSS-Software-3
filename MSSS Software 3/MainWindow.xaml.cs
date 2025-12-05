using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace MSSS_Software_3
{
	public partial class MainWindow : Window
	{
		// Named-pipe server used to send messages to the Admin panel
		private readonly PipeServer pipeServer = new PipeServer();

		// Server that listens for messages coming from Admin_Panel
		private readonly AdminPipeServer adminPipeServer = new AdminPipeServer();

		// Routed commands exposed by the main window
		public static readonly RoutedCommand Find = new RoutedCommand();
		public static readonly RoutedCommand OpenAdmin = new RoutedCommand();

		// Constructor: initialize UI, load data, start pipe servers, and wire-up views and commands
		public MainWindow()
		{
			InitializeComponent();
			LoadCsvIntoMasterFile("staff.csv");

			// Start main app pipe server to accept one client connection asynchronously
			_ = StartPipeServerAsync();

			// Listen for messages from Admin_Panel and start the admin pipe server
			adminPipeServer.MessageReceived += Pipe_MessageReceivedFromAdmin;
			_ = adminPipeServer.StartAsync();

			// Populate list views from the in-memory master file
			FilteredView.ItemsSource = StaffData.MasterFile
				.Select(kvp => new { ID = kvp.Key, Name = kvp.Value })
				.ToList();
			UnfilteredView.ItemsSource = StaffData.MasterFile
				.Select(kvp => new { ID = kvp.Key, Name = kvp.Value })
				.ToList();

			// Bind keyboard shortcuts and commands
			CommandBindings.Add(new CommandBinding(Find, Find_Executed));
			CommandBindings.Add(new CommandBinding(OpenAdmin, OpenAdmin_Executed));
			InputBindings.Add(new KeyBinding(OpenAdmin, Key.A, ModifierKeys.Alt));
		}

		// Command handler to open the Admin panel (passes selected or default staff entry)
		private void OpenAdmin_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			int staffId;
			string staffName;
			bool enableCreate = false;

			if (FilteredView.SelectedItem != null)
			{
				dynamic selected = FilteredView.SelectedItem;
				staffId = selected.ID;
				staffName = selected.Name;

				// If selection or filter indicates a create candidate, prepare create mode
				if (staffId == 77 || FilterTextBox.Text == "77")
				{
					staffId = 77;
					staffName = "";
					enableCreate = true;
				}
			}
			else
			{
				// No selection: default to create mode
				staffId = 77;
				staffName = "";
				enableCreate = true;
			}

			// Instantiate Admin window with create flag and show it modally
			var adminWindow = new Admin_Panel.MainWindow(staffId, staffName, enableCreate)
			{
				Owner = this,
				WindowStartupLocation = WindowStartupLocation.CenterOwner
			};

			ShowStatusMessage("Command Executed: ALT + A", 2);
			adminWindow.ShowDialog();
		}

		// Command handler to activate the Find textbox
		private void Find_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			FilterTextBox.Clear();
			FilterTextBox.Focus();
			ShowStatusMessage("Command Executed: ALT + F", 2);
		}

		// Show a temporary status message and fade it out after the given duration
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

		// Start the pipe server and wait for a client connection once
		private async Task StartPipeServerAsync()
		{
			await pipeServer.WaitForClientAsync();
		}

		// Handle selection changes in the filtered view: update labels and notify Admin via pipe
		private async void FilteredView_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (FilteredView.SelectedItem == null) return;

			dynamic selected = FilteredView.SelectedItem;

			lbID.Content = selected.ID;
			lbName.Content = selected.Name;
			lbDOB.Content = GenerateRandomDOB();
			lbDOH.Content = GenerateRandomDOH();
			lbPN.Content = "0" + selected.ID;

			// Send selected ID and Name to connected pipe client
			string message = $"{selected.ID},{selected.Name}";
			await pipeServer.SendAsync(message);
		}

		// Generate a random Date of Birth string within a plausible range
		private string GenerateRandomDOB()
		{
			var rand = new Random();
			DateTime start = new DateTime(1950, 1, 1);
			DateTime end = new DateTime(2010, 12, 31);
			int range = (end - start).Days;
			return start.AddDays(rand.Next(range)).ToString("dd/MM/yyyy");
		}

		// Generate a random Date of Hire string within a recent range
		private string GenerateRandomDOH()
		{
			var rand = new Random();
			DateTime start = new DateTime(2021, 1, 1);
			DateTime end = new DateTime(2025, 12, 31);
			int range = (end - start).Days;
			return start.AddDays(rand.Next(range)).ToString("dd/MM/yyyy");
		}

		// Filter the master file based on textbox input and update the filtered view
		private void FilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			string filter = FilterTextBox.Text.Trim();
			var filtered = string.IsNullOrEmpty(filter)
				? StaffData.MasterFile
				: StaffData.MasterFile
					.Where(kvp => kvp.Key.ToString().StartsWith(filter) || kvp.Value.StartsWith(filter, StringComparison.OrdinalIgnoreCase))
					.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

			FilteredView.ItemsSource = filtered
				.Select(kvp => new { ID = kvp.Key, Name = kvp.Value })
				.ToList();
		}

		// Load staff entries from a CSV into the in-memory master file
		private void LoadCsvIntoMasterFile(string filePath)
		{
			try
			{
				var lines = File.ReadAllLines(filePath);
				foreach (var line in lines)
				{
					var parts = line.Split(',');
					if (parts.Length < 2) continue;

					if (int.TryParse(parts[0], out int id))
						StaffData.MasterFile[id] = parts[1].Trim();
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show("Error loading CSV: " + ex.Message);
			}
		}

		// Handle messages received from the Admin panel and apply create/update/delete/save actions
		private void Pipe_MessageReceivedFromAdmin(string msg)
		{
			Dispatcher.Invoke(() =>
			{
				var parts = msg.Split('|');
				if (parts.Length == 0)
					return;

				string mode = parts[0];

				switch (mode)
				{
					case "C":
						if (parts.Length >= 3 && int.TryParse(parts[1], out int createId))
						{
							string createName = parts[2];

							// If provided ID is invalid, offer to generate a new valid one
							if (createId < 770000000)
							{
								MessageBoxResult result = MessageBox.Show(
									$"ERROR: {createId} is invalid. Ensure ID starts with 77 and is 8 characters. Generate new ID?",
									"ERROR",
									MessageBoxButton.YesNo,
									MessageBoxImage.Question
								);

								if (result == MessageBoxResult.Yes)
								{
									var rand = new Random();
									int randomSuffix = rand.Next(0, 1000000);
									createId = 770000000 + randomSuffix;

									// Ensure uniqueness
									while (StaffData.MasterFile.ContainsKey(createId))
									{
										randomSuffix = rand.Next(0, 1000000);
										createId = 770000000 + randomSuffix;
									}
								}
							}

							if (StaffData.MasterFile.ContainsKey(createId))
							{
								MessageBox.Show($"ERROR: Cannot create. ID {createId} already exists.");
							}
							else
							{
								StaffData.MasterFile[createId] = createName;
								MessageBox.Show($"INFO: Created new staff ID {createId}.");
							}
						}
						break;

					case "U":
						if (parts.Length >= 4 && int.TryParse(parts[1], out int oldId))
						{
							string newName = parts[3];

							if (!StaffData.MasterFile.ContainsKey(oldId))
							{
								MessageBox.Show($"ERROR: Cannot update. ID {oldId} does not exist.");
							}
							else
							{
								StaffData.MasterFile[oldId] = newName;
								MessageBox.Show($"INFO: Updated name for ID {oldId}.");
							}
						}
						break;

					case "D":
						if (parts.Length >= 2 && int.TryParse(parts[1], out int deleteId))
						{
							if (!StaffData.MasterFile.ContainsKey(deleteId))
							{
								MessageBox.Show($"ERROR: Cannot delete. ID {deleteId} does not exist.");
							}
							else
							{
								StaffData.MasterFile.Remove(deleteId);
								MessageBox.Show($"INFO: Deleted staff ID {deleteId}.");
							}
						}
						break;

					case "S":
						SaveStaffDataToCsv("staff.csv");
						ShowStatusMessage("Changes saved to CSV.", 2);
						break;

					default:
						// Ignore unrecognized commands
						break;
				}

				RefreshListViews();
			});
		}

		// Persist the master file back to a CSV file
		private void SaveStaffDataToCsv(string filePath)
		{
			try
			{
				using (var writer = new StreamWriter(filePath))
				{
					foreach (var kvp in StaffData.MasterFile)
					{
						writer.WriteLine($"{kvp.Key},{kvp.Value}");
					}
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Error saving CSV: {ex.Message}");
			}
		}

		// Refresh both list views; keep filtered view in sync with current filter text
		private void RefreshListViews()
		{
			UnfilteredView.ItemsSource = StaffData.MasterFile
				.Select(kvp => new { ID = kvp.Key, Name = kvp.Value })
				.ToList();

			FilterTextBox_TextChanged(null, null);
		}
	}
}
