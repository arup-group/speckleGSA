using SpeckleGSAInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Interop.Gsa_10_0;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Reflection;

namespace SpeckleGSAProxy
{
	//Aim: to provide a thread-safe interface to the GSA process
	public class GSAInterfacer : IGSAInterfacer
	{
		public IGSAIndexer Indexer { get; set; }

		private Dictionary<string, object> PreviousGSAGetCache = new Dictionary<string, object>();
		private Dictionary<string, object> GSAGetCache = new Dictionary<string, object>();

		private Dictionary<string, object> PreviousGSASetCache = new Dictionary<string, object>();
		private Dictionary<string, object> GSASetCache = new Dictionary<string, object>();

		private Dictionary<string, string> SidCache = new Dictionary<string, string>();

		private const string SID_TAG = "speckle_app_id";

		private string PreviousGSAResultInit = "";

		private ComAuto GSAObject;

		public string FilePath;

		#region Communication
		public void InitializeReceiver()
		{
			FullClearCache();
			//this.GSAObject = new ComAuto();
		}

		public void PreReceiving()
		{
			ClearCache();
		}

		public void PostReceiving()
		{
			BlankDepreciatedGWASetCommands();
		}

		public void InitializeSender()
		{
			FullClearCache();
			//this.GSAObject = new ComAuto();
		}

		public void PreSending()
		{
			ClearCache();
		}

		public void PostSending()
		{

		}
		#endregion


		public void BlankDepreciatedGWASetCommands()
		{
			List<string> prevSets = PreviousGSASetCache.Keys.Where(l => l.StartsWith("SET")).ToList();

			for (int i = 0; i < prevSets.Count(); i++)
			{
				string[] split = Regex.Replace(prevSets[i], ":{" + SID_TAG + ":.*}", "").ListSplit("\t");
				prevSets[i] = split[1] + "\t" + split[2] + "\t";
			}

			prevSets = prevSets.Where(l => !GSASetCache.Keys.Any(x => Regex.Replace(x, ":{" + SID_TAG + ":.*}", "").Contains(l))).ToList();

			for (int i = 0; i < prevSets.Count(); i++)
			{
				string p = prevSets[i];

				string[] split = p.ListSplit("\t");

				if (split[1].IsDigits())
				{
					// Uses SET
					if (!Indexer.InBaseline(split[0], Convert.ToInt32(split[1])))
						RunGWACommand("BLANK\t" + split[0] + "\t" + split[1], false);
				}
				else if (split[0].IsDigits())
				{

					// Uses SET_AT
					if (!Indexer.InBaseline(split[1], Convert.ToInt32(split[0])))
					{
						RunGWACommand("DELETE\t" + split[1] + "\t" + split[0], false);
						int idxShifter = Convert.ToInt32(split[0]) + 1;
						bool flag = false;
						while (!flag)
						{
							flag = true;

							prevSets = prevSets
									.Select(line => line.Replace(
											idxShifter.ToString() + "\t" + split[1] + "\t",
											(idxShifter - 1).ToString() + "\t" + split[1] + "\t")).ToList();

							string target = "\t" + idxShifter.ToString() + "\t" + split[1] + "\t";
							string rep = "\t" + (idxShifter - 1).ToString() + "\t" + split[1] + "\t";

							string prevCacheKey = PreviousGSASetCache.Keys
									.FirstOrDefault(x => x.Contains(target));
							if (prevCacheKey != null)
							{
								PreviousGSASetCache[prevCacheKey.Replace(target, rep)] = PreviousGSASetCache[prevCacheKey];
								PreviousGSASetCache.Remove(prevCacheKey);
								flag = false;
							}

							string currCacheKey = GSASetCache.Keys
									.FirstOrDefault(x => x.Contains(target));
							if (currCacheKey != null)
							{
								GSASetCache[currCacheKey.Replace(target, rep)] = GSASetCache[currCacheKey];
								GSASetCache.Remove(currCacheKey);
								flag = false;
							}

							idxShifter++;
						}
					}
				}
				else
				{
					//Some commands - like "LOAD_GRAVITY.2" and "LOAD_2D_THERMAL.2" have no indices at all in their GWA commands
					//TODO
				}
			}
		}

		public void ClearCache()
		{
			PreviousGSAGetCache = new Dictionary<string, object>(GSAGetCache);
			GSAGetCache.Clear();
			PreviousGSASetCache = new Dictionary<string, object>(GSASetCache);
			GSASetCache.Clear();
			SidCache.Clear();
		}

		public int[] ConvertGSAList(string list, GSAEntity type)
		{
			if (list == null) return new int[0];

			string[] pieces = list.ListSplit(" ");
			pieces = pieces.Where(s => !string.IsNullOrEmpty(s)).ToArray();

			List<int> items = new List<int>();
			for (int i = 0; i < pieces.Length; i++)
			{
				if (pieces[i].IsDigits())
					items.Add(Convert.ToInt32(pieces[i]));
				else if (pieces[i].Contains('"'))
				{
					items.AddRange(ConvertNamedGSAList(pieces[i], type));
				}
				else if (pieces[i] == "to")
				{
					int lowerRange = Convert.ToInt32(pieces[i - 1]);
					int upperRange = Convert.ToInt32(pieces[i + 1]);

					for (int j = lowerRange + 1; j <= upperRange; j++)
						items.Add(j);

					i++;
				}
				else
				{
					try
					{
						int[] itemTemp;
						GSAObject.EntitiesInList(pieces[i], (GsaEntity)type, out itemTemp);

						if (itemTemp == null)
							GSAObject.EntitiesInList("\"" + list + "\"", (GsaEntity)type, out itemTemp);

						if (itemTemp == null)
							continue;

						items.AddRange((int[])itemTemp);
					}
					catch
					{ }
				}
			}

			return items.ToArray();
		}

		public int[] ConvertNamedGSAList(string list, GSAEntity type)
		{
			list = list.Trim(new char[] { '"', ' ' });

			try
			{
				string res = GetGWARecords("GET\tLIST\t" + list).FirstOrDefault();

				string[] pieces = res.Split(new char[] { '\t' });

				return ConvertGSAList(pieces[pieces.Length - 1], type);
			}
			catch
			{
				try
				{
					GSAObject.EntitiesInList("\"" + list + "\"", (GsaEntity)type, out int[] itemTemp);
					if (itemTemp == null)
						return new int[0];
					else
						return (int[])itemTemp;
				}
				catch { return new int[0]; }
			}
		}

		public void DeleteSpeckleObjects()
		{
			BlankDepreciatedGWASetCommands();
			ClearCache();
			BlankDepreciatedGWASetCommands();
		}

		public void FullClearCache()
		{
			PreviousGSAGetCache.Clear();
			GSAGetCache.Clear();
			PreviousGSASetCache.Clear();
			GSASetCache.Clear();
			SidCache.Clear();
		}

		public string[] GetDeletedGWARecords(string command)
		{
			if (!command.Contains("GET"))
				throw new Exception("GetDeletedGWAGetCommands() only takes in GET commands");

			object result = RunGWACommand(command);

			if (PreviousGSAGetCache.ContainsKey(command))
			{
				if ((result as string) == (PreviousGSAGetCache[command] as string))
					return new string[0];

				string[] newPieces = ((string)result).Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries).Select((s, idx) => idx.ToString() + ":" + s).ToArray();
				string[] prevPieces = ((string)PreviousGSAGetCache[command]).Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries).Select((s, idx) => idx.ToString() + ":" + s).ToArray();

				string[] ret = prevPieces.Where(p => !newPieces.Contains(p)).ToArray();

				return ret;
			}
			else
				return new string[0];
		}

		public (int, double, string) GetGridPlaneData(int gridPlaneRef)
		{
			string res = GetGWARecords("GET\tGRID_PLANE.4\t" + gridPlaneRef.ToString()).FirstOrDefault();
			string[] pieces = res.ListSplit("\t");

			return (Convert.ToInt32(pieces[4]), Convert.ToDouble(pieces[5]), res);
		}

		public (int, string) GetGridPlaneRef(int gridSurfaceRef)
		{
			string res = GetGWARecords("GET\tGRID_SURFACE.1\t" + gridSurfaceRef.ToString()).FirstOrDefault();
			string[] pieces = res.ListSplit("\t");

			return (Convert.ToInt32(pieces[3]), res);
		}

		public Dictionary<string, object> GetGSAResult(int id, int resHeader, int flags, List<string> keys, string loadCase, string axis = "local", int num1DPoints = 2)
		{
			try
			{
				GsaResults[] res;
				int num;

				// Special case for assemblies
				if (Enum.IsDefined(typeof(ResHeader), resHeader) || resHeader == 18002000)
				{
					var initKey = "ARR" + flags.ToString() + axis + loadCase + resHeader.ToString() + num1DPoints.ToString();
					if (PreviousGSAResultInit != initKey)
					{
						GSAObject.Output_Init_Arr(flags, axis, loadCase, (ResHeader)resHeader, num1DPoints);
						PreviousGSAResultInit = initKey;
					}

					try
					{
						GSAObject.Output_Extract_Arr(id, out var outputExtractResults, out num);
						res = (GsaResults[])outputExtractResults;
					}
					catch
					{
						// Try reinit if fail
						GSAObject.Output_Init_Arr(flags, axis, loadCase, (ResHeader)resHeader, num1DPoints);
						GSAObject.Output_Extract_Arr(id, out var outputExtractResults, out num);
						res = (GsaResults[])outputExtractResults;
					}
				}
				else
				{
					var initKey = "SINGLE" + flags.ToString() + axis + loadCase + resHeader.ToString() + num1DPoints.ToString();
					if (PreviousGSAResultInit != initKey)
					{
						GSAObject.Output_Init(flags, axis, loadCase, resHeader, num1DPoints);
						PreviousGSAResultInit = initKey;
					}
					int numPos = GSAObject.Output_NumElemPos(id);

					res = new GsaResults[numPos];

					try
					{
						for (int i = 0; i < numPos; i++)
							res[i] = new GsaResults() { dynaResults = new double[] { (double)GSAObject.Output_Extract(id, i) } };
					}
					catch
					{
						// Try reinit if fail
						GSAObject.Output_Init(flags, axis, loadCase, resHeader, num1DPoints);
						for (int i = 0; i < numPos; i++)
							res[i] = new GsaResults() { dynaResults = new double[] { (double)GSAObject.Output_Extract(id, i) } };
					}
				}

				int counter = 0;
				Dictionary<string, object> ret = new Dictionary<string, object>();

				foreach (string key in keys)
				{
					ret[key] = res.Select(x => (double)x.dynaResults.GetValue(counter)).ToList();
					counter++;
				}

				return ret;
			}
			catch
			{
				Dictionary<string, object> ret = new Dictionary<string, object>();

				foreach (string key in keys)
					ret[key] = new List<double>() { 0 };

				return ret;
			}
		}

		public (double, string) GetGSATotal2DElementOffset(int propIndex, double insertionPointOffset)
		{
			double materialInsertionPointOffset = 0;
			double zMaterialOffset = 0;

			string res = GetGWARecords("GET\tPROP_2D\t" + propIndex.ToString()).FirstOrDefault();

			if (res == null || res == "")
				return (insertionPointOffset, null);

			string[] pieces = res.ListSplit("\t");

			zMaterialOffset = -Convert.ToDouble(pieces[12]);
			return (insertionPointOffset + zMaterialOffset + materialInsertionPointOffset, res);
		}

		public string[] GetGWARecords(string command)
		{
			if (!command.StartsWith("GET"))
				throw new Exception("GetGWAGetCommands() only takes in GET commands");

			object result = RunGWACommand(command);
			string[] newPieces = ((string)result).Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries).Select((s, idx) => idx.ToString() + ":" + s).ToArray();
			return newPieces;
		}

		public (string, string) GetPolylineDesc(int polylineRef)
		{
			string res = GetGWARecords("GET\tPOLYLINE.1\t" + polylineRef.ToString()).FirstOrDefault();
			string[] pieces = res.ListSplit("\t");

			return (pieces[6], res);
		}

		public string GetSID(string keyword, int id)
		{
			if (!SidCache.ContainsKey(keyword + "\t" + id.ToString()))
			{
				try
				{
					// Look in GET cache first
					if (GSAGetCache.ContainsKey("GET\t" + keyword + "\t" + id.ToString()))
					{
						var command = (string)GSAGetCache["GET\t" + keyword + "\t" + id.ToString()];
						var match = Regex.Match(command, "(?<={" + SID_TAG + ":).*?(?=})");
						if (!string.IsNullOrEmpty(match.Value))
							SidCache[keyword + "\t" + id.ToString()] = match.Value;
						else
							SidCache[keyword + "\t" + id.ToString()] = "gsa/" + keyword + "_" + id.ToString();
					}
					else
					{
						SidCache[keyword + "\t" + id.ToString()] = GSAObject.GetSidTagValue(keyword, id, SID_TAG);
						if (string.IsNullOrEmpty(SidCache[keyword + "\t" + id.ToString()]))
							SidCache[keyword + "\t" + id.ToString()] = "gsa/" + keyword + "_" + id.ToString();
					}
				}
				catch
				{
					SidCache[keyword + "\t" + id.ToString()] = "gsa/" + keyword + "_" + id.ToString();
				}
			}

			return SidCache[keyword + "\t" + id.ToString()];
		}

		public int NodeAt(string keyword, string typeName, double x, double y, double z, double coincidentNodeAllowance, string applicationId = null)
		{
			int idx = GSAObject.Gen_NodeAt(x, y, z, coincidentNodeAllowance);

			if (applicationId != null)
				Indexer.ReserveIndicesAndMap(keyword, typeName, new List<int>() { idx }, new List<string>() { applicationId });
			else
				Indexer.ReserveIndices(keyword, new List<int>() { idx });

			// Add artificial cache
			string cacheKey = "SET\t" + keyword + "\t" + idx.ToString() + "\t";
			if (!GSASetCache.ContainsKey(cacheKey))
				GSASetCache[cacheKey] = 0;

			return idx;
		}

		public object RunGWACommand(string command, bool cache = true)
		{
			if (cache)
			{
				if (command.StartsWith("GET"))
				{
					if (!GSAGetCache.ContainsKey(command))
					{
						// Let's speed things up a bit
						var commandPieces = command.Split(new char[] { '\t' });
						var newCommand = "GET_ALL\t" + commandPieces[1];

						GSAGetCache[newCommand] = GSAObject.GwaCommand(newCommand);

						var allRecords = ((string)GSAGetCache[newCommand]).Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);

						foreach (string rec in allRecords)
						{
							var recPieces = rec.Split(new char[] { '\t' });
							GSAGetCache["GET\t" + commandPieces[1] + "\t" + recPieces[1]] = rec;
						}
					}

					return GSAGetCache.ContainsKey(command) ? GSAGetCache[command] : "";
				}

				if (command.StartsWith("SET"))
				{
					if (PreviousGSASetCache.ContainsKey(command))
						GSASetCache[command] = PreviousGSASetCache[command];

					if (!GSASetCache.ContainsKey(command))
						GSASetCache[command] = GSAObject.GwaCommand(command);

					return GSASetCache[command];
				}
			}

			return GSAObject.GwaCommand(command);
		}

		/// <summary>
		/// Checks if the load case exists in the GSA file
		/// </summary>
		/// <param name="loadCase">GSA load case description</param>
		/// <returns>True if load case exists</returns>
		public bool CaseExist(string loadCase)
		{
			try
			{
				string[] pieces = loadCase.Split(new char[] { 'p' }, StringSplitOptions.RemoveEmptyEntries);

				if (pieces.Length == 1)
					return GSAObject.CaseExist(loadCase[0].ToString(), Convert.ToInt32(loadCase.Substring(1))) == 1;
				else if (pieces.Length == 2)
					return GSAObject.CaseExist(loadCase[0].ToString(), Convert.ToInt32(pieces[0].Substring(1))) == 1;
				else
					return false;
			}
			catch { return false; }
		}


		#region File Operations
		/// <summary>
		/// Creates a new GSA file. Email address and server address is needed for logging purposes.
		/// </summary>
		/// <param name="emailAddress">User email address</param>
		/// <param name="serverAddress">Speckle server address</param>
		public void NewFile(string emailAddress, string serverAddress, bool showWindow = true)
		{
			if (GSAObject != null)
			{
				try
				{
					GSAObject.Close();
				}
				catch { }
				GSAObject = null;
			}

			GSAObject = new ComAuto();

			GSAObject.LogFeatureUsage("api::specklegsa::" +
					FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location)
							.ProductVersion + "::GSA " + GSAObject.VersionString()
							.Split(new char[] { '\n' })[0]
							.Split(new char[] { '\t' }, StringSplitOptions.RemoveEmptyEntries)[1]);

			GSAObject.NewFile();
			GSAObject.SetLocale(Locale.LOC_EN_GB);
			GSAObject.DisplayGsaWindow(showWindow);

			//GetSpeckleClients(emailAddress, serverAddress);

			//Status.AddMessage("Created new file.");
		}

		/// <summary>
		/// Opens an existing GSA file. Email address and server address is needed for logging purposes.
		/// </summary>
		/// <param name="path">Absolute path to GSA file</param>
		/// <param name="emailAddress">User email address</param>
		/// <param name="serverAddress">Speckle server address</param>
		public void OpenFile(string path, string emailAddress, string serverAddress, bool showWindow = true)
		{

			if (GSAObject != null)
			{
				try
				{
					GSAObject.Close();
				}
				catch { }
				GSAObject = null;
			}

			GSAObject = new ComAuto();

			GSAObject.LogFeatureUsage("api::specklegsa::" +
				FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location)
					.ProductVersion + "::GSA " + GSAObject.VersionString()
					.Split(new char[] { '\n' })[0]
					.Split(new char[] { '\t' }, StringSplitOptions.RemoveEmptyEntries)[1]);

			GSAObject.Open(path);
			FilePath = path;
			GSAObject.SetLocale(Locale.LOC_EN_GB);
			GSAObject.DisplayGsaWindow(showWindow);

			//GetSpeckleClients(emailAddress, serverAddress);

			//Status.AddMessage("Opened new file.");
		}

		public int SaveAs(string filePath)
		{
			return GSAObject.SaveAs(filePath);
		}

		/// <summary>
		/// Close GSA file.
		/// </summary>
		public void Close()
		{
			try
			{
				GSAObject.Close();
			}
			catch { }

		}
		#endregion

		#region Speckle Client
				/// <summary>
		/// Writes sender and receiver streams associated with the account.
		/// </summary>
		/// <param name="emailAddress">User email address</param>
		/// <param name="serverAddress">Speckle server address</param>
		public void SetSID(string sidRecord)
		{
			GSAObject.GwaCommand("SET\tSID\t" + sidRecord);
		}
		#endregion

		#region Document Properties
		/// <summary>
		/// Extract the title of the GSA model.
		/// </summary>
		/// <returns>GSA model title</returns>
		public string GetTitle()
		{
			string res = (string)GSAObject.GwaCommand("GET\tTITLE");

			string[] pieces = res.ListSplit("\t");

			return pieces.Length > 1 ? pieces[1] : "My GSA Model";
		}

		public string[] GetTolerances()
		{
			return ((string)GSAObject.GwaCommand("GET\tTOL")).ListSplit("\t");
		}

		/// <summary>
		/// Updates the GSA unit stored in SpeckleGSA.
		/// </summary>
		public string GetUnits()
		{
			return ((string)GSAObject.GwaCommand("GET\tUNIT_DATA.1\tLENGTH")).ListSplit("\t")[2];
		}
		#endregion

		#region Views
		/// <summary>
		/// Update GSA viewer. This should be called at the end of changes.
		/// </summary>
		public void UpdateViews()
		{
			GSAObject.UpdateViews();
		}

		/// <summary>
		/// Update GSA case and task links. This should be called at the end of changes.
		/// </summary>
		public void UpdateCasesAndTasks()
		{
			GSAObject.ReindexCasesAndTasks();
		}

		public string GetSID()
		{
			return (string)GSAObject.GwaCommand("GET\tSID");
		}
		#endregion

		public int HighestIndex(string keyword)
		{
			return (int)GSAObject.GwaCommand("HIGHEST\t" + keyword);
		}
	}
}
