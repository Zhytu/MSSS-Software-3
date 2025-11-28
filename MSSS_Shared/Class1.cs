using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace MSSS_Shared
{
	public static class DataStore
	{
		public static Dictionary<int, string> MasterFile = new Dictionary<int, string>();

		private static KeyValuePair<int, string>? _selectedStaff;
		public static KeyValuePair<int, string>? SelectedStaff
		{
			get => _selectedStaff;
			set
			{
				_selectedStaff = value;
				StaffJsonHelper.SaveSelectedStaff(_selectedStaff);
			}
		}
	}

	public static class StaffJsonHelper
	{
		// Both apps write/read from the same known location
		private static readonly string FilePath = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
			"SelectedStaff.json"
		);

		public static void SaveSelectedStaff(KeyValuePair<int, string>? staff)
		{
			if (staff.HasValue)
			{
				var json = JsonSerializer.Serialize(new { ID = staff.Value.Key, Name = staff.Value.Value });
				File.WriteAllText(FilePath, json);
			}
			else
			{
				if (File.Exists(FilePath)) File.Delete(FilePath);
			}
		}

		public static (int ID, string Name)? LoadSelectedStaff()
		{
			if (!File.Exists(FilePath)) return null;

			var json = File.ReadAllText(FilePath);
			var obj = JsonSerializer.Deserialize<Dictionary<string, string>>(json);

			if (obj != null && obj.ContainsKey("ID") && obj.ContainsKey("Name") &&
				int.TryParse(obj["ID"], out int id))
			{
				return (id, obj["Name"]);
			}
			return null;
		}
	}
}
