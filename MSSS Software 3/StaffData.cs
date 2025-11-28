using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MSSS_Software_3
{
	// General GUI: holds all staff data
	public static class StaffData
	{
		// TKey: Staff ID, TValue: Staff Name
		public static Dictionary<int, string> MasterFile { get; private set; } = new Dictionary<int, string>();

		
	}

}
