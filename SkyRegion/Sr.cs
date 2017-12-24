﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using OTAPI;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.DB;

namespace SkyRegion
{
	[ApiVersion(2, 0)]
	public class Sr : TerrariaPlugin
	{
	    private const int DefaultIndex = -1;

		private const string SrRegionKey = "sr.cur.region";

		public override string Name => Assembly.GetExecutingAssembly().GetName().Name;

		public override string Author => "MistZZT";

		public override Version Version => Assembly.GetExecutingAssembly().GetName().Version;

		public override string Description => "天上的区域";

		public Sr(Main game) : base(game) { }

		internal List<int> InRegion = new List<int>();

		internal SkyRegionManager Srm;

		private double _worldSurface;

		private double _rockLayer;

		public override void Initialize()
		{
			ServerApi.Hooks.GameUpdate.Register(this, OnUpdate);
			ServerApi.Hooks.GamePostInitialize.Register(this, OnPostInit, -1000);
			ServerApi.Hooks.ServerLeave.Register(this, OnLeave);
			ServerApi.Hooks.GameInitialize.Register(this, OnInit);
			ServerApi.Hooks.NetGreetPlayer.Register(this, OnGreet);
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				ServerApi.Hooks.GameUpdate.Deregister(this, OnUpdate);
				ServerApi.Hooks.GamePostInitialize.Deregister(this, OnPostInit);
				ServerApi.Hooks.ServerLeave.Deregister(this, OnLeave);
				ServerApi.Hooks.GameInitialize.Deregister(this, OnInit);
				ServerApi.Hooks.NetGreetPlayer.Deregister(this, OnGreet);
			}
			base.Dispose(disposing);
		}

		private static void OnGreet(GreetPlayerEventArgs args)
		{
			var player = TShock.Players.ElementAtOrDefault(args.Who);

			player?.SetData(SrRegionKey, DefaultIndex);
		}

		private void OnInit(EventArgs args)
		{
			Commands.ChatCommands.Add(new Command("sr.manage", SrCmd, "sr"));
		}

		private void OnLeave(LeaveEventArgs args)
		{
		    if (InRegion.Contains(args.Who))
		    {
		        InRegion.Remove(args.Who);
		    }
		}

		private void OnPostInit(EventArgs args)
		{
			Srm = new SkyRegionManager(TShock.DB);
			Srm.LoadRegions();

			_worldSurface = Main.worldSurface;
			_rockLayer = Main.rockLayer;

			Philosophyz.Hooks.SendDataHooks.PreSendData += PreSd;
			Philosophyz.Hooks.SendDataHooks.PostSendData += PostSd;
		}

		private HookResult PostSd(int remoteClient, int index)
		{
			if (remoteClient == -1) // 跳过所有全体
				return HookResult.Cancel;

			if (!InRegion.Contains(index))
				return HookResult.Continue;

			Main.worldSurface = _worldSurface;
			Main.rockLayer = _rockLayer;
			return HookResult.Continue;
		}

		private HookResult PreSd(int remoteClient, int index)
		{
			if (!InRegion.Contains(index))
				return HookResult.Continue;

			if (remoteClient == -1) // 全体信息不发送给区域内玩家（发送以后会无效）
				return HookResult.Cancel;

			_worldSurface = Main.worldSurface;
			_rockLayer = Main.rockLayer;
			var bottom = TShock.Regions.GetRegionByID(TShock.Players[index].GetData<int>(SrRegionKey)).Area.Bottom;
			Main.worldSurface = bottom;
			Main.rockLayer = bottom + 10;
			return HookResult.Continue;
		}

		private void OnUpdate(EventArgs args)
		{
			foreach (var player in TShock.Players.Where(p => p?.Active == true))
			{
				Region region = null;
				if (Srm.SrRegions.Any(p => (region = TShock.Regions.GetRegionByID(p.ID))?.InArea(player.TileX, player.TileY) == true))
				{
					if (!InRegion.Contains(player.Index))
					{
						InRegion.Add(player.Index);
						player.SetData(SrRegionKey, region.ID);

						var ws = Main.worldSurface;
						var rl = Main.rockLayer;
						Main.worldSurface = region.Area.Bottom;
						Main.rockLayer = region.Area.Bottom + 10;
						player.SendData(PacketTypes.WorldInfo);
						Main.worldSurface = ws;
						Main.rockLayer = rl;
					}
				}
				else if (InRegion.Contains(player.Index))
				{
					InRegion.Remove(player.Index);
					player.SetData(SrRegionKey, DefaultIndex);
					player.SendData(PacketTypes.WorldInfo);
				}
			}
		}

		private void SrCmd(CommandArgs args)
		{
			if (args.Parameters.Count > 1)
			{
				var region = TShock.Regions.GetRegionByName(string.Join(" ", args.Parameters.Skip(1)));
				if (!string.IsNullOrWhiteSpace(region?.Name))
				{
					if (string.Equals(args.Parameters[0], "add", StringComparison.OrdinalIgnoreCase))
					{
						Srm.Add(region);
						args.Player.SendInfoMessage("添加区域完毕.");
						return;
					}
					if (string.Equals(args.Parameters[0], "del", StringComparison.OrdinalIgnoreCase))
					{
						Srm.Remove(region);
						args.Player.SendInfoMessage("移除区域完毕.");
						return;
					}
				}
				else
				{
					args.Player.SendErrorMessage("区域名 {0} 无效!", string.Join(" ", args.Parameters.Skip(1)));
					return;
				}
			}

			args.Player.SendErrorMessage("语法无效! 正确语法: /sr <add/del> <区域名>");
		}
	}
}
