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

            RegisterNetwork();
        }

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

        public bool SendChunkReload()
        {
            if (selectedChunks.Count == 0)
            {
                capi.ShowChatMessage("[ChunkRegen] Brak zaznaczonych chunków.");
                return true;
            }

            if (netChannel == null || !netChannel.Connected)
            {
                capi.ShowChatMessage("[ChunkRegen] Brak połączenia z serwerem (kanał nieaktywny).");
                return true;
            }

            var list = new List<Vec2i>(selectedChunks);

            var packet = new ChunkRegenRequestPacket
            {
                Coords = list,
                DeleteRegion = true   // zawsze czyścimy mapę dla regionu
            };

            netChannel.SendPacket(packet);
            capi.ShowChatMessage($"[ChunkRegen] Sent {list.Count} chunks to regenerate.");

            // opcjonalnie: czyścić zaznaczenie po wysłaniu
            selectedChunks.Clear();

            return true;
        }
    }
}

