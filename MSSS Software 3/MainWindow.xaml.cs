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
		private readonly PipeServer pipeServer = new PipeServer();

		private readonly AdminPipeServer adminPipeServer = new AdminPipeServer();

		// define a RoutedCommand for Find
		public static readonly RoutedCommand Find = new RoutedCommand();
		public static readonly RoutedCommand OpenAdmin = new RoutedCommand();



		public MainWindow()
		{
			InitializeComponent();
			LoadCsvIntoMasterFile("staff.csv");

			_ = StartPipeServerAsync(); // existing MainAppPipe (to Admin)

			// start listening for messages from Admin_Panel
			adminPipeServer.MessageReceived += Pipe_MessageReceivedFromAdmin;
			_ = adminPipeServer.StartAsync();

			FilteredView.ItemsSource = StaffData.MasterFile
				.Select(kvp => new { ID = kvp.Key, Name = kvp.Value })
				.ToList();
			UnfilteredView.ItemsSource = StaffData.MasterFile
				.Select(kvp => new { ID = kvp.Key, Name = kvp.Value })
				.ToList();  // ✅ Consistent anonymous type

			// bind keyboard shortcuts
			CommandBindings.Add(new CommandBinding(Find, Find_Executed));
			CommandBindings.Add(new CommandBinding(OpenAdmin, OpenAdmin_Executed));
			InputBindings.Add(new KeyBinding(OpenAdmin, Key.A, ModifierKeys.Alt));	

		}
		private void OpenAdmin_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			int staffId;
			string staffName;
			bool enableCreate = false; // default: disabled

			if (FilteredView.SelectedItem != null)
			{
				dynamic selected = FilteredView.SelectedItem;
				staffId = selected.ID;
				staffName = selected.Name;

				// Special case for Create if Staff ID 77 or filter indicates new user
				if (staffId == 77 || FilterTextBox.Text == "77")
				{
					staffId = 77;       // reset ID for creation
					staffName = "";    // reset name for creation
					enableCreate = true; // enable the Create button
				}
			}
			else
			{
				// No selection → create new
				staffId = 77;
				staffName = "";
				enableCreate = true; // enable Create button
			}

			// Pass enableCreate flag to Admin window constructor
			var adminWindow = new Admin_Panel.MainWindow(staffId, staffName, enableCreate)
			{
				Owner = this, // modal
				WindowStartupLocation = WindowStartupLocation.CenterOwner
			};

			ShowStatusMessage("Command Executed: ALT + A", 2);
			adminWindow.ShowDialog();
		}

		private void Find_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			FilterTextBox.Clear();
			FilterTextBox.Focus();
			ShowStatusMessage("Command Executed: ALT + F", 2);
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


		private async Task StartPipeServerAsync()
		{
			await pipeServer.WaitForClientAsync();
		}

		private async void FilteredView_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (FilteredView.SelectedItem == null) return;

			dynamic selected = FilteredView.SelectedItem;

			lbID.Content = selected.ID;
			lbName.Content = selected.Name;
			lbDOB.Content = GenerateRandomDOB();
			lbDOH.Content = GenerateRandomDOH();
			lbPN.Content = "0" + selected.ID;

			// Send both ID and Name through the pipe
			string message = $"{selected.ID},{selected.Name}";
			await pipeServer.SendAsync(message);
		}

		private string GenerateRandomDOB()
		{
			var rand = new Random();
			DateTime start = new DateTime(1950, 1, 1);
			DateTime end = new DateTime(2010, 12, 31);
			int range = (end - start).Days;
			return start.AddDays(rand.Next(range)).ToString("dd/MM/yyyy");
		}

		private string GenerateRandomDOH()
		{
			var rand = new Random();
			DateTime start = new DateTime(2021, 1, 1);
			DateTime end = new DateTime(2025, 12, 31);
			int range = (end - start).Days;
			return start.AddDays(rand.Next(range)).ToString("dd/MM/yyyy");
		}

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



		private void Pipe_MessageReceivedFromAdmin(string msg)
		{
			Dispatcher.Invoke(() =>
			{
				// Expected formats from Admin panel:
				//  C|newId|newName   → Create new entry
				//  U|oldId|ignored|newName  → Update name only
				//  D|oldId           → Delete entry

				var parts = msg.Split('|');
				if (parts.Length == 0)
					return;

				string mode = parts[0];

				switch (mode)
				{
					case "C": // Create new staff
						if (parts.Length >= 3 && int.TryParse(parts[1], out int createId))
						{
							string createName = parts[2];

							if (createId < 770000000)
							{
								MessageBoxResult result = MessageBox.Show(
									$"ERROR: {createId} is invalid. Ensure ID starts with 77 and is 8 characters. Generate new ID?",   // message
									"ERROR",               // title
									MessageBoxButton.YesNo,       // buttons
									MessageBoxImage.Question      // optional icon
								);

								if (result == MessageBoxResult.Yes)
								{
									// RANDOMLY generate a valid new ID starting with 77
									var rand = new Random();
									int randomSuffix = rand.Next(0, 1000000); // 6 digits
									createId = 770000000 + randomSuffix;

									// ChecK for uniqueness
									while (StaffData.MasterFile.ContainsKey(createId))
									{
										randomSuffix = rand.Next(0, 1000000);
										createId = 770000000 + randomSuffix;
									}

								}
								else
								{
									// User clicked No
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

					case "U": // Update existing staff name only
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

					case "D": // Delete staff
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

					case "S": // Save command from Admin panel
						SaveStaffDataToCsv("staff.csv");
						ShowStatusMessage("Changes saved to CSV.", 2);
						break;


					default:
						// Unknown command - ignore
						break;
				}

				// Refresh both ListViews
				RefreshListViews();
			});
		}

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



		private void RefreshListViews()
		{
			UnfilteredView.ItemsSource = StaffData.MasterFile
				.Select(kvp => new { ID = kvp.Key, Name = kvp.Value })
				.ToList();

			// Optional: refresh filtered view if needed
			FilterTextBox_TextChanged(null, null);
		}

	}
}
