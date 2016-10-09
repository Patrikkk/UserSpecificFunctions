﻿using System;
using System.Threading.Tasks;
using System.IO;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;
using UserSpecificFunctions.Extensions;

namespace UserSpecificFunctions
{
	[ApiVersion(1, 25)]
	public class UserSpecificFunctions : TerrariaPlugin
	{
		public override string Name { get { return "User Specific Functions"; } }
		public override string Author { get { return "Professor X"; } }
		public override string Description { get { return ""; } }
		public override Version Version { get { return new Version(1, 4, 8, 0); } }

		public Config USFConfig = new Config();
		public Database USFDatabase = new Database();

		public static UserSpecificFunctions Instance;
		public UserSpecificFunctions(Main game) : base(game)
		{
			Instance = this;
		}

		#region Initialize/Dispose
		public override void Initialize()
		{
			ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
			ServerApi.Hooks.ServerChat.Register(this, OnChat);

			PlayerHooks.PlayerPermission += OnPlayerPermission;
			GeneralHooks.ReloadEvent += OnReload;
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
				ServerApi.Hooks.ServerChat.Deregister(this, OnChat);

				PlayerHooks.PlayerPermission -= OnPlayerPermission;
				GeneralHooks.ReloadEvent -= OnReload;
			}
			base.Dispose(disposing);
		}
		#endregion

		#region Hooks
		/// <summary>
		/// Internal hook, fired when the server is set up.
		/// </summary>
		/// <param name="args">The <see cref="EventArgs"/> object.</param>
		private void OnInitialize(EventArgs args)
		{
			LoadConfig();
			USFDatabase.DBConnect();

			Commands.ChatCommands.RemoveAll(c => c.HasAlias("help"));

			Commands.ChatCommands.Add(new Command(USFCommands.Help, "help") { HelpText = "Lists commands or gives help on them." });
			Commands.ChatCommands.Add(new Command(USFCommands.USFMain, "us"));
			Commands.ChatCommands.Add(new Command(Permissions.setPermissions, USFCommands.USFPermission, "permission"));
		}

		/// <summary>
		/// Internal hook, fired when a chat message is sent.
		/// </summary>
		/// <param name="args">The <see cref="ServerChatEventArgs"/> object.</param>
		private void OnChat(ServerChatEventArgs args)
		{
			// Return if the packet was already handled by another plugin.
			if (args.Handled)
			{
				return;
			}

			// Ensure the player is not null and has modified data.
			TSPlayer tsplr = TShock.Players[args.Who];
			if (tsplr == null || tsplr.GetPlayerInfo() == null)
			{
				return;
			}

			// Check if the player has the permission to speak and has not been muted.
			if (!tsplr.HasPermission(TShockAPI.Permissions.canchat) || tsplr.mute)
			{
				return;
			}

			if (!args.Text.StartsWith(TShock.Config.CommandSpecifier) && !args.Text.StartsWith(TShock.Config.CommandSilentSpecifier))
			{
				string prefix = tsplr.GetPlayerInfo().Prefix?.ToString() ?? tsplr.Group.Prefix;
				string suffix = tsplr.GetPlayerInfo().Suffix?.ToString() ?? tsplr.Group.Suffix;
				Color chatColor = tsplr.GetPlayerInfo().ChatColor?.ToColor() ?? tsplr.Group.ChatColor.ToColor();

				if (!TShock.Config.EnableChatAboveHeads)
				{
					string message = string.Format(TShock.Config.ChatFormat, tsplr.Group.Name, prefix, tsplr.Name, suffix, args.Text);
					TSPlayer.All.SendMessage(message, chatColor);
					TSPlayer.Server.SendMessage(message, chatColor);
					TShock.Log.Info("Broadcast: {0}", message);

					args.Handled = true;
				}
				else
				{
					Player player = Main.player[args.Who];
					string name = player.name;
					player.name = string.Format(TShock.Config.ChatAboveHeadsFormat, tsplr.Group.Name, prefix, tsplr.Name, suffix);
					NetMessage.SendData((int)PacketTypes.PlayerInfo, -1, -1, player.name, args.Who, 0, 0, 0, 0);
					player.name = name;
					var text = args.Text;
					NetMessage.SendData((int)PacketTypes.ChatText, -1, args.Who, text, args.Who, chatColor.R, chatColor.G, chatColor.B);
					NetMessage.SendData((int)PacketTypes.PlayerInfo, -1, -1, name, args.Who, 0, 0, 0, 0);

					string message = string.Format("<{0}> {1}", string.Format(TShock.Config.ChatAboveHeadsFormat, tsplr.Group.Name, prefix, tsplr.Name, suffix), text);
					tsplr.SendMessage(message, chatColor);
					TSPlayer.Server.SendMessage(message, chatColor);
					TShock.Log.Info("Broadcast: {0}", message);

					args.Handled = true;
				}
			}
			else
			{
				// Check if the player entered a command.
				if (!string.IsNullOrWhiteSpace(args.Text.Substring(1)))
				{
					try
					{
						args.Handled = tsplr.ExecuteCommand(args.Text);
					}
					catch (Exception ex)
					{
						TShock.Log.ConsoleError("An exception occured executing a command.");
						TShock.Log.Error(ex.ToString());
					}
				}
			}
		}

		/// <summary>
		/// Internal hook, fired whenever <see cref="TSPlayer.HasPermission(string)"/> is invoked.
		/// </summary>
		/// <param name="args">The <see cref="PlayerPermissionEventArgs"/> object.</param>
		private void OnPlayerPermission(PlayerPermissionEventArgs args)
		{
			// Return if the event was already handled by another plugin.
			if (args.Handled)
			{
				return;
			}

			// Ensure the player is not null and has special permissions.
			if (args.Player == null || args.Player.GetPlayerInfo() == null)
			{
				return;
			}

			// Handle the event.
			args.Handled = args.Player.GetPlayerInfo().HasPermission(args.Permission);
		}

		/// <summary>
		/// Internal hook, fired whenever a player executes /reload.
		/// </summary>
		/// <param name="args">The <see cref="ReloadEventArgs"/> object.</param>
		private void OnReload(ReloadEventArgs args)
		{
			LoadConfig();
			Task.Run(() => USFDatabase.LoadPlayerData());
		}
		#endregion

		#region LoadConfig
		/// <summary>
		/// Internal method, reloads the configuration file.
		/// </summary>
		internal void LoadConfig()
		{
			string configPath = Path.Combine(TShock.SavePath, "UserSpecificFunctions.json");
			USFConfig = Config.TryRead(configPath);
		}
		#endregion
	}
}
