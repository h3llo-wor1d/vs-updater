using Newtonsoft.Json;
using System.IO;
using System.IO.Compression;
using static System.Net.Mime.MediaTypeNames;

class Program
{
	public static Dictionary<string, string> createJson(string i)
	{
		var o = new Dictionary<string, string>();
		string[] lines = i.Split('\n')
				 .Select(line => line.Trim().Replace(",", ""))
				 .ToArray();
		foreach (string line in lines)
		{
			if (line != "{" && line != "}")
			{
				string[] kv = line.Split(":");
				o[kv[0]] = kv[1].Replace("\"", "").Trim();
			}
		}
		return o;
	}

	public static string makeOptionChange(string n, string o)
	{
		var oLines = createJson(o.Split("document.styleOptions = ")[1]);
		var ot = oLines;
		var nLines = createJson(n.Split("document.styleOptions = ")[1]);

		foreach (var kvp in nLines)
		{
			if (!oLines.ContainsKey(kvp.Key))
			{
				ot[kvp.Key] = kvp.Value;
				System.Diagnostics.Debug.WriteLine(kvp.Key);
			}
		}

		if (!ot.SequenceEqual(oLines))
		{
			string file = $"document.styleOptions = {JsonConvert.SerializeObject(oLines, Formatting.Indented)}";
			return file;
		}
		else
		{
			return "false";
		}
	}

	public static void syncNew(List<string> files, ZipArchive z)
	{
		foreach (var item in files)
		{
			var io = item.Split("./")[1];
			if (io.Split("/").Length > 1)
			{
				var checkNew = io.Split("/");
				checkNew = checkNew.Take(checkNew.Count() - 1).ToArray();
				string newOut = string.Join("/", checkNew);
				if (!Directory.Exists(string.Join("/", checkNew)))
				    Directory.CreateDirectory(newOut);
			}

			string o = "VStream-Widgets-Collection-main/"+item.Split("./")[1];

			foreach (ZipArchiveEntry entry in z.Entries)
			{
				Console.WriteLine(entry.FullName);
				if (entry.FullName == o)
				{
					entry.ExtractToFile(item);
				}
		    }
		}
	}

	public static void runSync(List<string> zContent, List<string> currentFiles, ZipArchive z, List<string> newFiles)
	{

		foreach (var item in zContent)
		{
			
			string relativePath = item.Replace("VStream-Widgets-Collection-main/", "");

			// Create new directories if they do not exist already
			if (relativePath.Split("/").Length > 1)
			{
				var checkNew = relativePath.Split("/");
				checkNew = checkNew.Take(checkNew.Count() - 1).ToArray();
				string newOut = string.Join("/", checkNew);
				if (!Directory.Exists(string.Join("/", checkNew)))
					Directory.CreateDirectory(newOut);
			}

			string newContent = new StreamReader(z.GetEntry(item).Open()).ReadToEnd();
			try
			{
				string oldContent = File.ReadAllText(relativePath);

				if (oldContent.StartsWith(" "))
				{
					oldContent = oldContent.Substring(1);
				}

				if (relativePath.Split("/")[1] == "options.js")
				{
					string changedContent = makeOptionChange(newContent, oldContent);
					if (changedContent != "false")
					{
						File.WriteAllText(relativePath, changedContent);
					}
					continue;
				}
			} catch { };
			File.WriteAllText(relativePath, newContent);
			newFiles.Remove("./"+relativePath);
		}

		syncNew(newFiles, z);
	}

	public static async Task Main()
	{
		var url = "https://github.com/h3llo-wor1d/VStream-Widgets-Collection/archive/refs/heads/main.zip";

		using (HttpClient client = new HttpClient())
		{
			using (var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
			{
				response.EnsureSuccessStatusCode();
				using (Stream contentStream = await response.Content.ReadAsStreamAsync())
				{

					using (ZipArchive archive = new ZipArchive(contentStream, ZipArchiveMode.Read))
					{
						// Need to compare some files that are changed here eventually, also this lacks .exe which needs to be fixed augh
						var z = archive;
						var zContents = archive.Entries
							.Where(entry => entry.Name.Contains(".") &&
											!entry.Name.EndsWith(".lnk") &&
											!entry.Name.EndsWith(".png") &&
											!entry.Name.EndsWith(".mp3") &&
											!entry.Name.EndsWith(".ttf") &&
											!entry.Name.EndsWith(".otf") &&
											!entry.Name.EndsWith(".py") &&
											!entry.Name.EndsWith(".exe"))
							.Select(entry => entry.FullName.Replace("\\", "/"))
							.ToList();

						var allFiles = Directory.GetFiles(".", "*.*", SearchOption.AllDirectories)
						.Where(file => !file.EndsWith(".dll"))
						.Select(file => file.Replace("\\", "/")).ToList();

						var newContents = archive.Entries
							.Where(entry => entry.Name.Contains("."))
							.Select(entry => "./"+entry.FullName.Replace("\\", "/").Replace("VStream-Widgets-Collection-main/", ""))
							.Where(entry => allFiles.IndexOf(entry) == -1 && !entry.EndsWith("options.js"))
							.ToList();

						var currentFiles = Directory.GetFiles(".", "*.*", SearchOption.AllDirectories)
							.Where(file => file.Contains(".") &&
										   !file.EndsWith(".lnk") &&
										   !file.EndsWith(".png") &&
										   !file.EndsWith(".mp3") &&
										   !file.EndsWith(".ttf") &&
										   !file.EndsWith(".otf") &&
										   !file.EndsWith(".py") &&
										   !file.EndsWith(".exe"))
							.Select(file => file.Replace("\\", "/"))
							.ToList();

						runSync(zContents, currentFiles, z, newContents);		
					}
				}
			}
		}
	}
}