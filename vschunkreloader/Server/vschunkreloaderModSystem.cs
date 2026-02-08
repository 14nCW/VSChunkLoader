using ProtoBuf;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using vschunkreloader.Client;

namespace VsChunkReloader
{
    [ProtoContract]
    public class ChunkRegenRequestPacket
    {
        [ProtoMember(1)]
        public List<Vec2i> Coords { get; set; }

        [ProtoMember(2)]
        public bool DeleteRegion { get; set; }
    }

    public class VsChunkReloaderModSystem : ModSystem
    {
        private ICoreServerAPI sapi;

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            sapi = api;

            //RegisterCommands();
            RegisterNetwork();
        }

        //private void RegisterCommands()
        //{
        //    var parsers = sapi.ChatCommands.Parsers;

        //    sapi.ChatCommands
        //        .GetOrCreate("cregen")
        //        .WithDescription("Narzędzia do regeneracji chunków (mod)")
        //        .RequiresPrivilege(Privilege.controlserver)

        //        // /cregen here
        //        .BeginSubCommand("here")
        //            .WithDescription("Regeneruje chunk, na którym stoisz")
        //            .HandleWith(OnCmdRegenHere)
        //        .EndSubCommand()

        //        // /cregen area <radius>
        //        .BeginSubCommand("area")
        //            .WithDescription("Regeneruje kwadratowy obszar chunków wokół gracza")
        //            .WithArgs(parsers.IntRange("radius", 0, 10))
        //            .HandleWith(OnCmdRegenArea)
        //        .EndSubCommand();
        //}

        private void RegisterNetwork()
        {
            sapi.Network
                .RegisterChannel("chunkreloader")
                .RegisterMessageType<ChunkRegenRequestPacket>()
                .SetMessageHandler<ChunkRegenRequestPacket>(OnChunkRegenRequest);
        }

        private void OnChunkRegenRequest(IServerPlayer fromPlayer, ChunkRegenRequestPacket packet)
        {
            if (packet?.Coords == null || packet.Coords.Count == 0)
            {
                return;
            }

            SimpleRegenChunks(packet.Coords, fromPlayer, packet.DeleteRegion);
        }

        // /cregen here
        private TextCommandResult OnCmdRegenHere(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            if (player == null)
            {
                return TextCommandResult.Error("Tę komendę można użyć tylko jako gracz.");
            }

            var pos = player.Entity.Pos.AsBlockPos;

            // Chunk coords – >> 5 zamiast / 32, żeby działało też dla ujemnych
            int chunkX = pos.X >> 5;
            int chunkZ = pos.Z >> 5;

            var coords = new List<Vec2i> { new Vec2i(chunkX, chunkZ) };

            SimpleRegenChunks(coords, player, deleteRegion: false);

            return TextCommandResult.Success($"Regeneruję chunk ({chunkX}, {chunkZ}).");
        }

        // /cregen area <radius>
        private TextCommandResult OnCmdRegenArea(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            if (player == null)
            {
                return TextCommandResult.Error("Tę komendę można użyć tylko jako gracz.");
            }

            int radius = (int)args[0];

            var pos = player.Entity.Pos.AsBlockPos;
            int centerChunkX = pos.X >> 5;
            int centerChunkZ = pos.Z >> 5;

            var coords = new List<Vec2i>();

            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dz = -radius; dz <= radius; dz++)
                {
                    coords.Add(new Vec2i(centerChunkX + dx, centerChunkZ + dz));
                }
            }

            SimpleRegenChunks(coords, player, deleteRegion: false);

            int diam = 2 * radius + 1;
            return TextCommandResult.Success(
                $"Regeneruję obszar {diam}x{diam} chunków wokół Ciebie (środek: {centerChunkX},{centerChunkZ})."
            );
        }

        /// <summary>
        /// Główna logika: usuń kolumny chunków, (opcjonalnie) regiony, wygeneruj na nowo, wyślij do gracza.
        /// </summary>
        private void SimpleRegenChunks(List<Vec2i> coords, IServerPlayer player, bool deleteRegion)
        {
            if (coords == null || coords.Count == 0) return;

            var wm = sapi.WorldManager;
            int chunkSize = wm.ChunkSize;                          // 32
            int regionChunkSize = wm.RegionSize / chunkSize;       // np. 8
            int chunksY = wm.MapSizeY / chunkSize;

            HashSet<Vec2i> regions = new HashSet<Vec2i>();

            // 1) Usuń kolumny chunków
            foreach (var c in coords)
            {
                wm.DeleteChunkColumn(c.X, c.Y);

                if (deleteRegion)
                {
                    int regX = c.X / regionChunkSize;
                    int regZ = c.Y / regionChunkSize;
                    regions.Add(new Vec2i(regX, regZ));
                }
            }

            if (deleteRegion)
            {
                foreach (var r in regions)
                {
                    wm.DeleteMapRegion(r.X, r.Y);
                }
            }

            // 2) Zmuszamy serwer, żeby ponownie wysłał chunki graczowi
            player.CurrentChunkSentRadius = 0;

            // 3) Załaduj ponownie i po wygenerowaniu wyślij dane do klienta
            foreach (var coord in coords)
            {
                var cLocal = coord; // lokalna kopia dla lambdy

                var opts = new ChunkLoadOptions
                {
                    ChunkGenParams = null,
                    OnLoaded = () =>
                    {
                        sapi.Logger.Notification($"[VsChunkReloader] Chunk ({cLocal.X}, {cLocal.Y}) wygenerowany, wysyłam do klienta.");

                        for (int cy = 0; cy < chunksY; cy++)
                        {
                            wm.BroadcastChunk(cLocal.X, cy, cLocal.Y, true);
                        }
                    }
                };

                wm.LoadChunkColumnPriority(cLocal.X, cLocal.Y, opts);
            }
        }
    }

    public class ChunkRegenClientSystem : ModSystem
    {
        private ICoreClientAPI capi;
        internal IClientNetworkChannel netChannel;

        public HashSet<Vec2i> selectedChunks = new HashSet<Vec2i>();

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
            capi = api;

            netChannel = capi.Network
                .RegisterChannel("chunkreloader")
                .RegisterMessageType<ChunkRegenRequestPacket>();

            var mapManager = api.ModLoader.GetModSystem<WorldMapManager>();

            // ✅ prawidłowe wywołanie z dwoma argumentami: layerCode + position
            mapManager.RegisterMapLayer<ChunkRegenOverlayLayer>("chunkreloader", 0.5);
        }

        private bool OnHotkeyRegenCurrent(KeyCombination comb)
        {
            var player = capi.World.Player;
            if (player == null) return true;

            var pos = player.Entity.Pos.AsBlockPos;
            int chunkX = pos.X >> 5;
            int chunkZ = pos.Z >> 5;

            var packet = new ChunkRegenRequestPacket
            {
                Coords = new List<Vec2i> { new Vec2i(chunkX, chunkZ) },
                DeleteRegion = false
            };

            netChannel.SendPacket(packet);

            return true;
        }

        private bool OnHotkeyToggleChunk(KeyCombination comb)
        {
            var player = capi.World.Player;
            if (player == null) return true;

            var pos = player.Entity.Pos.AsBlockPos;
            int chunkX = pos.X >> 5;
            int chunkZ = pos.Z >> 5;

            var c = new Vec2i(chunkX, chunkZ);

            if (selectedChunks.Contains(c))
            {
                selectedChunks.Remove(c);
            }
            else
            {
                selectedChunks.Add(c);
            }

            return true;
        }

        public bool SendChunkReload()
        {
            if (selectedChunks.Count == 0)
            {
                return true;
            }

            var list = new List<Vec2i>(selectedChunks);

            var packet = new ChunkRegenRequestPacket
            {
                Coords = list,
                DeleteRegion = false
            };

            netChannel.SendPacket(packet);
            capi.ShowChatMessage($"[ChunkRegen] Sent {list.Count} chunks to regenerate.");

            // Opcjonalnie wyczyść zaznaczenie po wysłaniu
            selectedChunks.Clear();

            return true;
        }
    }

}

