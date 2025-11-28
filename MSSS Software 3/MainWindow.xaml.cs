using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace MSSS_Software_3
{
	public partial class MainWindow : Window
	{
		private readonly PipeServer pipeServer = new PipeServer();

		public MainWindow()
		{
			InitializeComponent();
			LoadCsvIntoMasterFile("staff.csv");

			// Start pipe server after UI is ready
			_ = StartPipeServerAsync();

			// Populate ListViews
			FilteredView.ItemsSource = StaffData.MasterFile
				.Select(kvp => new { ID = kvp.Key, Name = kvp.Value })
				.ToList();
			// Assign the dictionary directly
			UnfilteredView.ItemsSource = StaffData.MasterFile;
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
				// Expecting "ID,Name,Mode"
				var parts = msg.Split(',');
				if (parts.Length >= 3)
				{
					if (int.TryParse(parts[0], out int id))
					{
						// Update MasterFile dictionary
						if (parts[2] == "U") // Update
							StaffData.MasterFile[id] = parts[1];
						else if (parts[2] == "C") // Create
							StaffData.MasterFile[id] = parts[1];

						// Refresh list views
						RefreshListViews();
					}
				}
			});
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
