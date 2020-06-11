﻿using System;
using System.IO;
using System.Net;
using System.Threading;
using Alex.API.Input;
using log4net;
using NLog;
using LogManager = NLog.LogManager;

namespace Alex
{
	/// <summary>
	/// The main class.
	/// </summary>
	public static class Program
	{
		private static readonly Logger Log = LogManager.GetCurrentClassLogger(typeof(Program));

		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		static void Main(string[] args)
		{
			LaunchSettings launchSettings = ParseArguments(args);

			if (!Directory.Exists(launchSettings.WorkDir))
			{
				Directory.CreateDirectory(launchSettings.WorkDir);
			}

			ConfigureNLog(launchSettings.WorkDir);

            if (launchSettings.Server == null && launchSettings.ConnectOnLaunch)
			{
				launchSettings.ConnectOnLaunch = false;
				Log.Warn($"No server specified, ignoring connect argument.");
			}

            if (!Clipboard.IsClipboardAvailable())
            {
	            Log.Warn($"No suitable Clipboard implementation, clipboard will not be available! If you are on linux, install 'XClip' using 'apt install XClip'");
            }

			//Cef.Initialize(new Settings());

			Log.Info($"Starting...");

			using (var game = new Alex(launchSettings))
			{
				game.Run();
			}
		}

		private static void ConfigureNLog(string baseDir)
		{
			string loggerConfigFile = Path.Combine(baseDir, "NLog.config");
			if (!File.Exists(loggerConfigFile))
			{
				File.WriteAllText(loggerConfigFile, Resources.NLogConfig);
			}

			string logsDir = Path.Combine(baseDir, "logs");
			if (!Directory.Exists(logsDir))
			{
				Directory.CreateDirectory(logsDir);
			}

			NLog.LogManager.Configuration = new NLog.Config.XmlLoggingConfiguration(loggerConfigFile, true);
			LogManager.Configuration.Variables["basedir"] = baseDir;

			NLogAppender.Initialize();
        }

		private static LaunchSettings ParseArguments(string[] args)
		{
			LaunchSettings launchSettings    = new LaunchSettings();
			bool           nextIsServer      = false;
			bool           nextIsuuid        = false;
			bool           nextIsaccessToken = false;
			bool           nextIsUsername    = false;
			bool nextIsWorkDir = false;

			foreach (var arg in args)
			{
				if (nextIsServer)
				{
					nextIsServer = false;
					var s = arg.Split(':');
					if (IPAddress.TryParse(s[0], out IPAddress val))
					{
						if (ushort.TryParse(s[1], out ushort reee))
						{
							launchSettings.Server = new IPEndPoint(val, reee);
						}
					}
				}

				if (nextIsaccessToken)
				{
					nextIsaccessToken = false;
					launchSettings.AccesToken = arg;
					continue;
				}

				if (nextIsuuid)
				{
					nextIsuuid = false;
					launchSettings.UUID = arg;
					continue;
				}

				if (nextIsUsername)
				{
					nextIsUsername = false;
					launchSettings.Username = arg;
					continue;
				}

				if (nextIsWorkDir)
				{
					nextIsWorkDir = false;
					launchSettings.WorkDir = arg;
					continue;
				}

				if (arg == "--server")
				{
					nextIsServer = true;
				}

				if (arg == "--bedrock")
				{
					launchSettings.ConnectToBedrock = true;
				}

				if (arg == "--accessToken")
				{
					nextIsaccessToken = true;
				}

				if (arg == "--uuid")
				{
					nextIsuuid = true;
				}

				if (arg == "--username")
				{
					nextIsUsername = true;
				}

				if (arg == "--direct")
				{
					launchSettings.ConnectOnLaunch = true;
				}

				if (arg == "--console")
				{
					launchSettings.ShowConsole = true;
				}

				if (arg == "--workDir")
				{
					nextIsWorkDir = true;
				}

				if (arg == "--debug")
				{
					launchSettings.ModelDebugging = true;
				}
			}

			return launchSettings;
		}
		
	}
}
