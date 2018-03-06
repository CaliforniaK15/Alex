﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using Alex.ResourcePackLib.Json.Models;
using ICSharpCode.SharpZipLib.Zip;
using log4net;
using Newtonsoft.Json;

namespace Alex.ResourcePackLib
{
	public class BedrockResourcePack : IDisposable
	{
		private static readonly ILog Log = LogManager.GetLogger(typeof(BedrockResourcePack));

		public IReadOnlyDictionary<string, EntityModel> EntityModels { get; private set; }
		public IReadOnlyDictionary<string, Bitmap> Textures { get; private set; }
		public IReadOnlyDictionary<string, string> TextureJsons { get; private set; }

		private ZipFile _archive;
		public BedrockResourcePack(ZipFile archive)
		{
			_archive = archive;
			
			Load();
		}

		public BedrockResourcePack(byte[] resourceData) : this(new ZipFile(new MemoryStream(resourceData))
		{
			IsStreamOwner = true
		})
		{

		}

		public bool TryGetTexture(string name, out Bitmap texture)
		{
			return Textures.TryGetValue(NormalisePath(name), out texture);
		}

		public bool TryGetTextureJson(string name, out string textureJson)
		{
			return TextureJsons.TryGetValue(NormalisePath(name), out textureJson);
		}

		private string NormalisePath(string path)
		{
			return path.Replace('\\', '/').ToLowerInvariant();
		}

		private void Load()
		{
			foreach (ZipEntry entry in _archive)
			{
				if (entry.IsDirectory)
				{
					CheckDirectory(entry);
					continue;
				}

				if (entry.IsFile)
				{
					CheckFile(entry);
				}
			}
		}

		private bool EntitysLoaded { get; set; } = false;
		private void CheckFile(ZipEntry entry)
		{
			if (entry.Name.EndsWith("mobs.json") && !EntitysLoaded)
			{
				LoadMobs(entry);
			}
			else if (entry.Name.EndsWith("textures_list.json"))
			{
				LoadTexture(entry);
			}
		}

		private void CheckDirectory(ZipEntry entry)
		{
			
		}

		private void LoadTexture(ZipEntry entry)
		{
			var stream = new StreamReader(_archive.GetInputStream(entry));
			var json = stream.ReadToEnd();

			Dictionary<string, Bitmap> textures = new Dictionary<string, Bitmap>();
			Dictionary<string, string> textureJsons = new Dictionary<string, string>();

			string[] definitions = JsonConvert.DeserializeObject<string[]>(json);
			foreach (string def in definitions)
			{
				if (textures.ContainsKey(def))
					continue;
				
				var e = _archive.GetEntry(def + ".png");
				if (e != null && e.IsFile)
				{
					Bitmap bmp = new Bitmap(_archive.GetInputStream(e));
					textures.Add(NormalisePath(def), bmp);
				}

				e = _archive.GetEntry(def + ".json");
				if (e != null && e.IsFile)
				{
					using(var eStream = _archive.GetInputStream(e))
					using (var sr = new StreamReader(eStream))
					{
						var textureJson = sr.ReadToEnd();
						textureJsons.Add(NormalisePath(def), textureJson);
					}
				}
			}

			Textures = textures;
			TextureJsons = textureJsons;
			Log.Info($"Loaded {textures.Count} textures and {textureJsons.Count} textureJsons");
		}

		private void LoadMobs(ZipEntry entry)
		{
			var stream = new StreamReader(_archive.GetInputStream(entry));
			var json = stream.ReadToEnd();

			Dictionary<string, EntityModel> entries = JsonConvert.DeserializeObject<Dictionary<string, EntityModel>>(json);
			EntityModels = entries;

			Log.Info($"Imported {entries.Count} entity models");
		}

		public void Dispose()
		{
			_archive?.Close();
		}
	}
}