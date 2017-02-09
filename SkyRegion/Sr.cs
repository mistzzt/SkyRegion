using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;

namespace SkyRegion
{
	[ApiVersion(2, 0)]
	public class Sr : TerrariaPlugin
	{
		public override string Name => Assembly.GetExecutingAssembly().GetName().Name;

		public override string Author => "MistZZT";

		public override Version Version => Assembly.GetExecutingAssembly().GetName().Version;

		public override string Description => "天上的区域";

		public Sr(Main game) : base(game) { }

		internal List<int> InRegion = new List<int>();

		internal SkyRegionManager Srm;

		public override void Initialize()
		{
			ServerApi.Hooks.GameUpdate.Register(this, OnUpdate);
			ServerApi.Hooks.GamePostInitialize.Register(this, OnPostInit, -1000);
			ServerApi.Hooks.ServerLeave.Register(this, OnLeave);
			ServerApi.Hooks.GameInitialize.Register(this, OnInit);
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				ServerApi.Hooks.GameUpdate.Deregister(this, OnUpdate);
				ServerApi.Hooks.GamePostInitialize.Deregister(this, OnPostInit);
				ServerApi.Hooks.ServerLeave.Deregister(this, OnLeave);
				ServerApi.Hooks.GameInitialize.Deregister(this, OnInit);
			}
			base.Dispose(disposing);
		}

		private void OnInit(EventArgs args)
		{
			Commands.ChatCommands.Add(new Command("sr.manage", SrCmd, "sr"));
		}

		private void OnLeave(LeaveEventArgs args)
		{
			if (InRegion.Contains(args.Who))
				InRegion.Remove(args.Who);
		}

		private void OnPostInit(EventArgs args)
		{
			Srm = new SkyRegionManager(TShock.DB);
			Srm.LoadRegions();
		}

		private void OnUpdate(EventArgs args)
		{
			foreach (var player in TShock.Players.Where(p => p?.Active == true))
			{
				if (player.CurrentRegion != null && Srm.SrRegions.Any(p => p.ID == player.CurrentRegion.ID))
				{
					if (!InRegion.Contains(player.Index))
					{
						InRegion.Add(player.Index);
						var ws = Main.worldSurface;
						var rl = Main.rockLayer;
						Main.worldSurface = player.CurrentRegion.Area.Bottom;
						Main.rockLayer = player.CurrentRegion.Area.Bottom + 10;
						player.SendData(PacketTypes.WorldInfo);
						Main.worldSurface = ws;
						Main.rockLayer = rl;
					}
				}
				else if (InRegion.Contains(player.Index))
				{
					InRegion.Remove(player.Index);
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
