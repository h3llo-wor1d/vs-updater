using Newtonsoft.Json;
using System.IO.Compression;

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

	public static void runSync(List<string> zContent, List<string> currentFiles, ZipArchive z)
	{
		foreach (var item in zContent)
		{
			string relativePath = item.Replace("VStream-Widgets-Collection-main/", "");
			string oFile;
			if (currentFiles.IndexOf(relativePath) != -1)
			{
				string newContent = new StreamReader(z.GetEntry(item).Open()).ReadToEnd();
				string oldContent = File.ReadAllText(relativePath);

				if (oldContent.StartsWith(" "))
				{
					oldContent = oldContent.Substring(1);
				}

				if (newContent != oldContent)
				{
					if (item.EndsWith("options.js"))
					{
						string changedContent = makeOptionChange(newContent, oldContent);
						if (changedContent != "false")
						{
							File.WriteAllText(relativePath, changedContent);
							continue;
						}
					}
				}
				else
				{
					continue;
				}
			} else
			{
				oFile = new StreamReader(z.GetEntry(item).Open()).ReadToEnd();
				File.WriteAllText(relativePath, oFile);
			}
		}
	}

	public static async Task Main()
	{
		var conAndZ = new List<List<string>>();
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
											!entry.Name.EndsWith(".py"))
							.Select(entry => entry.FullName.Replace("\\", "/"))
							.ToList();

						var currentFiles = Directory.GetFiles(".", "*.*", SearchOption.AllDirectories)
							.Where(file => file.Contains(".") &&
										   !file.EndsWith(".lnk") &&
										   !file.EndsWith(".png") &&
										   !file.EndsWith(".mp3") &&
										   !file.EndsWith(".ttf") &&
										   !file.EndsWith(".otf") &&
										   !file.EndsWith(".py"))
							.Select(file => file.Replace("\\", "/"))
							.ToList();

						runSync(zContents, currentFiles, z);		
					}
				}
			}
		}
	}
}