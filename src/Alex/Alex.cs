﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using Alex.API;
using Alex.API.Data.Servers;
using Alex.API.Events;
using Alex.API.Graphics.Typography;
using Alex.API.Gui;
using Alex.API.Input;
using Alex.API.Input.Listeners;
using Alex.API.Network;
using Alex.API.Resources;
using Alex.API.Services;
using Alex.API.Utils;
using Alex.API.World;
using Alex.Blocks.Minecraft;
using Alex.Blocks.State;
using Alex.Entities;
using Alex.GameStates;
using Alex.Gamestates.Debug;
using Alex.GameStates.Gui.MainMenu;
using Alex.GameStates.Playing;
using Alex.Gui;
using Alex.Gui.Dialogs.Containers;
using Alex.Items;
using Alex.Networking.Bedrock;
using Alex.Networking.Java.Packets;
using Alex.Plugins;
using Alex.Services;
using Alex.Utils;
using Alex.Worlds;
using Alex.Worlds.Bedrock;
using Alex.Worlds.Java;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MiNET.Net;
using MiNET.Utils;
using Newtonsoft.Json;
using NLog;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using GuiDebugHelper = Alex.Gui.GuiDebugHelper;
using Image = SixLabors.ImageSharp.Image;
using Point = Microsoft.Xna.Framework.Point;
using Skin = Alex.API.Utils.Skin;
using TextInputEventArgs = Microsoft.Xna.Framework.TextInputEventArgs;

namespace Alex
{
	public partial class Alex : Microsoft.Xna.Framework.Game
	{
		private static readonly Logger Log = LogManager.GetCurrentClassLogger(typeof(Alex));

		public static string DotnetRuntime { get; } =
			$"{System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}";

		public const string Version = "1.0 DEV";

		public static bool IsMultiplayer { get; set; } = false;

		public static IFont Font;
		public static IFont DebugFont;

		private SpriteBatch _spriteBatch;

		public static Alex Instance { get; private set; }
		public GameStateManager GameStateManager { get; private set; }

		public ResourceManager Resources { get; private set; }

		public InputManager InputManager { get; private set; }
		public GuiRenderer GuiRenderer { get; private set; }
		public GuiManager GuiManager { get; private set; }
		public GuiDebugHelper GuiDebugHelper { get; private set; }

		public GraphicsDeviceManager DeviceManager { get; }

		//public ProfileManager ProfileManager { get; private set; }
		
		internal ConcurrentQueue<Action> UIThreadQueue { get; }

		//internal StorageSystem Storage { get; private set; }

		private LaunchSettings LaunchSettings { get; }
		//public ChromiumWebBrowser CefWindow { get; private set; }
		public PluginManager PluginManager { get; }
        public FpsMonitor FpsMonitor { get; }
        //private IPlayerProfileService ProfileService { get; set; }
        
        public new IServiceProvider Services { get; set; }
        
        public DedicatedThreadPool ThreadPool { get; private set; }
        private DedicatedThreadPool NetworkThreadPool { get; set; } = null;
        
        public StorageSystem Storage { get; private set; }
        
        public Alex(LaunchSettings launchSettings)
		{
			Instance = this;
			LaunchSettings = launchSettings;

			DeviceManager = new GraphicsDeviceManager(this)
			{
				PreferMultiSampling = false,
				SynchronizeWithVerticalRetrace = false,
				GraphicsProfile = GraphicsProfile.Reach,
			};

			DeviceManager.PreparingDeviceSettings += (sender, args) =>
				{
					args.GraphicsDeviceInformation.PresentationParameters.DepthStencilFormat = DepthFormat.Depth24Stencil8;
					DeviceManager.PreferMultiSampling = true;
				};
			
			Content.RootDirectory = "assets";

			IsFixedTimeStep = false;
           // graphics.ToggleFullScreen();
			
			this.Window.AllowUserResizing = true;
			this.Window.ClientSizeChanged += (sender, args) =>
			{
				if (DeviceManager.PreferredBackBufferWidth != Window.ClientBounds.Width ||
				    DeviceManager.PreferredBackBufferHeight != Window.ClientBounds.Height)
				{
					if (DeviceManager.IsFullScreen)
					{
						DeviceManager.PreferredBackBufferWidth = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width;
						DeviceManager.PreferredBackBufferHeight = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height;
					}
					else
					{
						DeviceManager.PreferredBackBufferWidth = Window.ClientBounds.Width;
						DeviceManager.PreferredBackBufferHeight = Window.ClientBounds.Height;
					}

					DeviceManager.ApplyChanges();

					//CefWindow.Size = new System.Drawing.Size(Window.ClientBounds.Width, Window.ClientBounds.Height);
				}
			};
			

			JsonConvert.DefaultSettings = () => new JsonSerializerSettings()
			{
				Converters = new List<JsonConverter>()
				{
					new Texture2DJsonConverter(GraphicsDevice)
				},
				Formatting = Formatting.Indented
			};
			
			IServiceCollection serviceCollection = new ServiceCollection();
			ConfigureServices(serviceCollection);

			Services = serviceCollection.BuildServiceProvider();
			
			UIThreadQueue = new ConcurrentQueue<Action>();

            PluginManager = new PluginManager(Services);
            FpsMonitor = new FpsMonitor();

            Resources = Services.GetRequiredService<ResourceManager>();

            ThreadPool = new DedicatedThreadPool(new DedicatedThreadPoolSettings(Environment.ProcessorCount,
	            ThreadType.Background, "Dedicated ThreadPool"));
            
           PacketFactory.CustomPacketFactory = new AlexPacketFactory();
           
           KeyboardInputListener.InstanceCreated += KeyboardInputCreated;
           MouseInputListener.InstanceCreated += MouseInputCreated;
		}

        private void MouseInputCreated(object? sender, MouseInputListener e)
        {
	        var bindings = KeyBinds.DefaultMouseBindings;

	        if (Storage.TryReadJson($"controls_mouse", out Dictionary<InputCommand, MouseButton> loadedMouseBindings))
	        {
		        bindings = loadedMouseBindings;
	        }

	        foreach (var binding in bindings)
	        {
		        e.RegisterMap(binding.Key, binding.Value);
	        }
        }

        private void KeyboardInputCreated(object sender, KeyboardInputListener e)
        {
	        var bindings = KeyBinds.DefaultBindings;

	        if (Storage.TryReadJson($"controls", out Dictionary<InputCommand, Keys> loadedBindings))
	        {
		        bindings = loadedBindings;
	        }

	        foreach (var binding in bindings)
	        {
		        e.RegisterMap(binding.Key, binding.Value);
	        }
        }

        public static EventHandler<TextInputEventArgs> OnCharacterInput;

		private void Window_TextInput(object sender, TextInputEventArgs e)
		{
			OnCharacterInput?.Invoke(this, e);
		}

		protected override void Initialize()
		{
			Window.Title = "Alex - " + Version;

			// InitCamera();
			this.Window.TextInput += Window_TextInput;
			
			var currentAdapter = GraphicsAdapter.Adapters.FirstOrDefault(x => x == GraphicsDevice.Adapter);
			if (currentAdapter != null)
			{
				if (currentAdapter.IsProfileSupported(GraphicsProfile.HiDef))
				{
					DeviceManager.GraphicsProfile = GraphicsProfile.HiDef;
				}
			}
			
			GraphicsDevice.PresentationParameters.MultiSampleCount = 8;
			
			DeviceManager.ApplyChanges();
			
			base.Initialize();
		}

		protected override void LoadContent()
		{
			var options = Services.GetService<IOptionsProvider>();
			options.Load();
			
			var fontStream = Assembly.GetEntryAssembly().GetManifestResourceStream("Alex.Resources.DebugFont.xnb");
			
			DebugFont = (WrappedSpriteFont) Content.Load<SpriteFont>(fontStream.ReadAllBytes());
			
			_spriteBatch = new SpriteBatch(GraphicsDevice);
			InputManager = new InputManager(this);

			GuiRenderer = new GuiRenderer();
			//GuiRenderer.Init(GraphicsDevice);
			
			GuiManager = new GuiManager(this, Services, InputManager, GuiRenderer, options);
			GuiManager.Init(GraphicsDevice, Services);

			options.AlexOptions.VideoOptions.UseVsync.Bind((value, newValue) => { SetVSync(newValue); });
			if (options.AlexOptions.VideoOptions.UseVsync.Value)
			{
				SetVSync(true);
			}
			
			options.AlexOptions.VideoOptions.Fullscreen.Bind((value, newValue) => { SetFullscreen(newValue); });
			if (options.AlexOptions.VideoOptions.Fullscreen.Value)
			{
				SetFullscreen(true);
			}

			options.AlexOptions.VideoOptions.LimitFramerate.Bind((value, newValue) =>
				{
					SetFrameRateLimiter(newValue, options.AlexOptions.VideoOptions.MaxFramerate.Value);
				});

			options.AlexOptions.VideoOptions.MaxFramerate.Bind((value, newValue) =>
				{
					SetFrameRateLimiter(options.AlexOptions.VideoOptions.LimitFramerate.Value, newValue);
				});

			if (options.AlexOptions.VideoOptions.LimitFramerate.Value)
			{
				SetFrameRateLimiter(true, options.AlexOptions.VideoOptions.MaxFramerate.Value);
			}

			options.AlexOptions.VideoOptions.Antialiasing.Bind((value, newValue) =>
			{
				SetAntiAliasing(newValue > 0, newValue);
			});

			options.AlexOptions.MiscelaneousOptions.Language.Bind((value, newValue) =>
			{
				GuiRenderer.SetLanguage(newValue);
			});
			GuiRenderer.SetLanguage(options.AlexOptions.MiscelaneousOptions.Language);

			SetAntiAliasing(options.AlexOptions.VideoOptions.Antialiasing > 0,
				options.AlexOptions.VideoOptions.Antialiasing.Value);
			
			GuiDebugHelper = new GuiDebugHelper(GuiManager);

			OnCharacterInput += GuiManager.FocusManager.OnTextInput;

			GameStateManager = new GameStateManager(GraphicsDevice, _spriteBatch, GuiManager);

			var splash = new SplashScreen();
			GameStateManager.AddState("splash", splash);
			GameStateManager.SetActiveState("splash");

			WindowSize = this.Window.ClientBounds.Size;
			//	Log.Info($"Initializing Alex...");
			ThreadPool.QueueUserWorkItem(() =>
			{
				try
				{
					InitializeGame(splash);
				}
				catch (Exception ex)
				{
					Log.Error(ex, $"Could not initialize! {ex}");
				}
			});
		}

		private void SetAntiAliasing(bool enabled, int count)
		{
			UIThreadQueue.Enqueue(() =>
			{
				DeviceManager.PreferMultiSampling = enabled;
				GraphicsDevice.PresentationParameters.MultiSampleCount = count;
				
				DeviceManager.ApplyChanges();
			});
		}

		private void SetFrameRateLimiter(bool enabled, int frameRateLimit)
		{
			UIThreadQueue.Enqueue(() =>
			{
				base.IsFixedTimeStep = enabled;
				base.TargetElapsedTime = TimeSpan.FromSeconds(1d /  frameRateLimit);
			});
		}
		
		private void SetVSync(bool enabled)
		{
			UIThreadQueue.Enqueue(() =>
			{
				base.IsFixedTimeStep = enabled;
				DeviceManager.SynchronizeWithVerticalRetrace = enabled;
				DeviceManager.ApplyChanges();
			});
		}

		private Point WindowSize { get; set; }
		private void SetFullscreen(bool enabled)
		{
			UIThreadQueue.Enqueue(() =>
			{
				if (this.DeviceManager.IsFullScreen != enabled)
				{
					if (enabled)
					{
						WindowSize = Window.ClientBounds.Size;
					}
					else
					{
						DeviceManager.PreferredBackBufferWidth = WindowSize.X;
						DeviceManager.PreferredBackBufferHeight =WindowSize.Y;
						this.DeviceManager.ApplyChanges();
					}
					
					this.DeviceManager.IsFullScreen = enabled;
					this.DeviceManager.ApplyChanges();
				}
			});
		}
		
		private void ConfigureServices(IServiceCollection services)
		{
			Storage = new StorageSystem(LaunchSettings.WorkDir);

			services.AddSingleton<Alex>(this);
			services.AddSingleton<IStorageSystem>(Storage);
			services.AddSingleton<IOptionsProvider, OptionsProvider>();
			services.AddSingleton<ProfileManager>();

			services.AddSingleton<IListStorageProvider<SavedServerEntry>, SavedServerDataProvider>();
			
			services.AddSingleton<XBLMSAService>();
			
			services.AddSingleton<IServerQueryProvider>(new ServerQueryProvider(this));
			services.AddSingleton<IPlayerProfileService, PlayerProfileService>();

			services.AddSingleton<IRegistryManager, RegistryManager>();
            services.AddSingleton<AlexIpcService>();

            services.AddSingleton<IEventDispatcher, EventDispatcher>();
            services.AddSingleton<ResourceManager>();
            services.AddSingleton<GuiManager>((o) => this.GuiManager)
;            //Storage = storage;
		}

		protected override void UnloadContent()
		{
			//ProfileManager.SaveProfiles();
			
			Services.GetService<IOptionsProvider>().Save();
			Services.GetService<AlexIpcService>().Stop();

			GuiDebugHelper.Dispose();

            PluginManager.UnloadAll();
        }

		protected override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			
			InputManager.Update(gameTime);

			GuiManager.Update(gameTime);
			GameStateManager.Update(gameTime);
			GuiDebugHelper.Update(gameTime);

			if (!UIThreadQueue.IsEmpty && UIThreadQueue.TryDequeue(out Action a))
			{
				try
				{
					a.Invoke();
				}
				catch (Exception ex)
				{
					Log.Warn($"Exception on UIThreadQueue: {ex.ToString()}");
				}
			}
		}

		protected override void Draw(GameTime gameTime)
		{
            FpsMonitor.Update();
            GraphicsDevice.RasterizerState = RasterizerState.CullClockwise;
            
            GameStateManager.Draw(gameTime);
			GuiManager.Draw(gameTime);
			
			base.Draw(gameTime);
		}

		private void InitializeGame(IProgressReceiver progressReceiver)
		{
			progressReceiver.UpdateProgress(0, "Initializing...");
			API.Extensions.Init(GraphicsDevice);
			MCPacketFactory.Load();

			//ConfigureServices();

			var eventDispatcher = Services.GetRequiredService<IEventDispatcher>() as EventDispatcher;
			foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies())
				eventDispatcher.LoadFrom(assembly);
			
			var options = Services.GetService<IOptionsProvider>();

			string pluginDirectoryPaths = Path.GetDirectoryName(new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath);

            var pluginDir = options.AlexOptions.ResourceOptions.PluginDirectory;
            if (!string.IsNullOrWhiteSpace(pluginDir))
            {
                pluginDirectoryPaths = pluginDir;
            }
            else
            {
	            if (!string.IsNullOrWhiteSpace(LaunchSettings.WorkDir) && Directory.Exists(LaunchSettings.WorkDir))
	            {
		            pluginDirectoryPaths = LaunchSettings.WorkDir;
	            }
            }

            foreach (string dirPath in pluginDirectoryPaths.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string directory = dirPath;
                if (!Path.IsPathRooted(directory))
                {
                    directory = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), dirPath);
                }

                PluginManager.DiscoverPlugins(directory);
            }


            var profileManager = Services.GetService<ProfileManager>();

            //	Log.Info($"Loading resources...");
			if (!Resources.CheckResources(GraphicsDevice, progressReceiver,
				OnResourcePackPreLoadCompleted))
			{
                Console.WriteLine("Press enter to exit...");
			    Console.ReadLine();
				Exit();
				return;
			}
			
			profileManager.LoadProfiles(progressReceiver);
			
			//GuiRenderer.LoadResourcePack(Resources.ResourcePack, null);
			AnvilWorldProvider.LoadBlockConverter();

			PluginManager.EnablePlugins();
			
			if (LaunchSettings.ModelDebugging)
			{
				GameStateManager.SetActiveState<ModelDebugState>();
			}
			else
			{
				GameStateManager.SetActiveState<TitleState>("title");
				var player = new Player(GraphicsDevice, InputManager, null, null, new Skin(),  null, PlayerIndex.One);
				player.Inventory.IsPeInventory = true;
				/*Random rnd = new Random();
				for (int i = 0; i < player.Inventory.SlotCount; i++)
				{
					player.Inventory[i] = new ItemBlock(BlockFactory.AllBlockstates.ElementAt(rnd.Next() % BlockFactory.AllBlockstates.Count).Value)
					{
						Count = rnd.Next(1, 64)
					};
				}*/
				//GuiManager.ShowDialog(new GuiPlayerInventoryDialog(player, player.Inventory));
			}

			GameStateManager.RemoveState("splash");

		}

		private void OnResourcePackPreLoadCompleted(Image<Rgba32> fontBitmap, List<char> bitmapCharacters)
		{
			UIThreadQueue.Enqueue(() =>
			{
				var scalar = fontBitmap.Width / 128;
				Font = new BitmapFont(GraphicsDevice, fontBitmap, 16, bitmapCharacters);

				GuiManager.ApplyFont(Font);
			});
		}

		public void ConnectToServer(IPEndPoint serverEndPoint, PlayerProfile profile, bool bedrock = false, string hostname = null)
		{
			var oldNetworkPool = NetworkThreadPool;
			
			var optionsProvider =  Services.GetService<IOptionsProvider>();
			
			NetworkThreadPool = new DedicatedThreadPool(new DedicatedThreadPoolSettings(optionsProvider.AlexOptions.NetworkOptions.NetworkThreads.Value, ThreadType.Background, "Network ThreadPool"));

			try
			{
				var eventDispatcher = Services.GetRequiredService<IEventDispatcher>() as EventDispatcher;
				eventDispatcher?.Reset();
				
				WorldProvider provider;
				INetworkProvider networkProvider;
				IsMultiplayer = true;
				if (bedrock)
				{
					provider = new BedrockWorldProvider(this, serverEndPoint,
						profile, NetworkThreadPool, out networkProvider);
				}
				else
				{
					provider = new JavaWorldProvider(this, serverEndPoint, profile,
						out networkProvider)
					{
						Hostname = hostname
					};
				}

				LoadWorld(provider, networkProvider);
			}
			catch (Exception ex)
			{
				Log.Error(ex, "FCL");
			}
			
			oldNetworkPool?.Dispose();
		}

		public void LoadWorld(WorldProvider worldProvider, INetworkProvider networkProvider)
		{
			PlayingState playState = new PlayingState(this, GraphicsDevice, worldProvider, networkProvider);
			
			LoadingWorldState loadingScreen = new LoadingWorldState();
			GameStateManager.AddState("loading", loadingScreen);
			GameStateManager.SetActiveState("loading");

			worldProvider.Load(loadingScreen.UpdateProgress).ContinueWith(task =>
			{
				GameStateManager.RemoveState("play");
				GameStateManager.AddState("play", playState);
				
				if (networkProvider.IsConnected)
				{
					GameStateManager.SetActiveState("play");
				}
				else
				{
					GameStateManager.RemoveState("play");
					worldProvider.Dispose();
				}

				GameStateManager.RemoveState("loading");
			});
		}
	}

    public interface IProgressReceiver
	{
		void UpdateProgress(int percentage, string statusMessage);
		void UpdateProgress(int percentage, string statusMessage, string sub);
    }
}