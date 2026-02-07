using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using VsChunkReloader;

namespace vschunkreloader.Client
{
    internal class ChunkRegenOverlayLayer : MapLayer
    {
        private readonly ICoreClientAPI capi;
        private readonly IWorldMapManager worldMapManager;
        private readonly ChunkRegenClientSystem clientSystem;
        private LoadedTexture debugTexture;
        private int chunkSize = 32;
        private bool isBoxSelecting = false;
        private Vec2i boxStartChunk;
        private Vec2i boxCurrentChunk;

        enum SelectionMode
        {
            Single,     // klik = toggle jednego chunka (to już masz)
            Box,        // klik + przeciągnięcie = prostokąt
            Brush       // „malowanie” chunków
        }

        private SelectionMode selectionMode = SelectionMode.Single;

        public override string Title => "Chunk Reloader";
        public override EnumMapAppSide DataSide => EnumMapAppSide.Client;
        public override string LayerGroupCode => "chunkreloader";

        public ChunkRegenOverlayLayer(ICoreAPI api, IWorldMapManager mapSink)
            : base(api, mapSink)
        {
            this.capi = (ICoreClientAPI)api;
            this.worldMapManager = mapSink;

            // 🔥 TAK jak w ProspectTogether: bierzemy ModSystem z api.ModLoader
            this.clientSystem = api.ModLoader.GetModSystem<ChunkRegenClientSystem>(true);

            debugTexture = new LoadedTexture(capi, 0, 1, 1);

            int[] pixels = { ColorUtil.ColorFromRgba(255, 0, 0, 120) }; // półprzezroczysty czerwony
            capi.Render.LoadOrUpdateTextureFromRgba(pixels, false, 0, ref debugTexture);
        }

        public override void Dispose()
        {
            debugTexture?.Dispose();
            base.Dispose();
        }

        public override void OnMapOpenedClient()
        {
            if (!worldMapManager.IsOpened) return;
            // na razie nic tu nie musisz robić
        }

        public override void Render(GuiElementMap mapElem, float dt)
        {
            if (!Active) return;

            // 1. policz, jakie koordy świata są w rogach mapy
            GetWorldBoundsForMap(mapElem, out Vec3d worldTopLeft, out Vec3d worldBottomRight);

            // 2. narysuj overlay dla KAŻDEGO zaznaczonego chunka
            foreach (var c in clientSystem.selectedChunks)
            {
                DrawChunkOverlay(mapElem, worldTopLeft, worldBottomRight, c);
            }
        }

        public override void OnMouseUpClient(MouseEvent args, GuiElementMap mapElem)
        {
            if (!Active) return;
            if (args.Button != EnumMouseButton.Left) return;

            // 1. zamiana pozycji kliknięcia na worldX/worldZ
            int worldX, worldZ;
            if (!TryGetWorldPosFromMouse(mapElem, args, out worldX, out worldZ))
            {
                capi.ShowChatMessage($"chuj chuj chuj");

                return; // klik poza mapą
            }

            int chunkX = worldX >> 5;
            int chunkZ = worldZ >> 5;
            var c = new Vec2i(chunkX, chunkZ);
            capi.ShowChatMessage($"mouse: {args.X},{args.Y}");

            if (clientSystem.selectedChunks.Contains(c))
            {
                clientSystem.selectedChunks.Remove(c);
                capi.ShowChatMessage($"[ChunkRegen] Odznaczono chunk ({chunkX},{chunkZ}). Zaznaczonych: {clientSystem.selectedChunks.Count}");
            }
            else
            {
                clientSystem.selectedChunks.Add(c);
                capi.ShowChatMessage($"[ChunkRegen] Zaznaczono chunk ({chunkX},{chunkZ}). Zaznaczonych: {clientSystem.selectedChunks.Count}");
            }
        }

        private bool TryGetWorldPosFromMouse(GuiElementMap mapElem, MouseEvent args, out int worldX, out int worldZ)
        {
            worldX = 0;
            worldZ = 0;

            // 1. Pozycja myszy względem mapy (viewPos)
            // MouseEvent.X/Y są w koordach ekranu, Bounds.absX/absY to pozycja mapy na ekranie
            float relX = (float)(args.X - mapElem.Bounds.absX);
            float relY = (float)(args.Y - mapElem.Bounds.absY);

            // 2. Odrzucamy kliknięcia poza obszarem mapy
            if (relX < 0 || relY < 0 || relX > mapElem.Bounds.InnerWidth || relY > mapElem.Bounds.InnerHeight)
            {
                return false;
            }

            // 3. Konwersja viewPos -> worldPos
            Vec3d worldPos = new Vec3d();
            mapElem.TranslateViewPosToWorldPos(new Vec2f(relX, relY), ref worldPos);

            worldX = (int)worldPos.X;
            worldZ = (int)worldPos.Z;
            return true;
        }

        private void DrawChunkOverlay(GuiElementMap mapElem, Vec3d worldTopLeft, Vec3d worldBottomRight, Vec2i chunkCoord
)
        {
            int wx1 = chunkCoord.X * chunkSize;
            int wz1 = chunkCoord.Y * chunkSize;
            int wx2 = wx1 + chunkSize;
            int wz2 = wz1 + chunkSize;

            // przeliczamy dwa rogi chunka na piksele na mapie
            if (!TryWorldToMapPixel(mapElem, worldTopLeft, worldBottomRight, wx1, wz1, out double x1, out double y1))
            {
                return;
            }
            if (!TryWorldToMapPixel(mapElem, worldTopLeft, worldBottomRight, wx2, wz2, out double x2, out double y2))
            {
                return;
            }

            double px = x1;
            double py = y1;
            double pw = x2 - x1;
            double ph = y2 - y1;

            // proste zabezpieczenie przed „ujemną szerokością” gdy osie się odwrócą
            if (pw < 0)
            {
                px += pw;
                pw = -pw;
            }
            if (ph < 0)
            {
                py += ph;
                ph = -ph;
            }

            capi.Render.Render2DTexture(
                debugTexture.TextureId,
                (float)px, (float)py, (float)pw, (float)ph,
                90f
            );
        }


        private bool TryWorldToMapPixel(GuiElementMap mapElem, Vec3d topLeft, Vec3d bottomRight, float worldX, float worldZ, out double mapX, out double mapY)
        {
            mapX = mapY = 0;

            double worldMinX = topLeft.X;
            double worldMaxX = bottomRight.X;
            double worldMinZ = topLeft.Z;
            double worldMaxZ = bottomRight.Z;

            double spanX = worldMaxX - worldMinX;
            double spanZ = worldMaxZ - worldMinZ;

            if (spanX == 0 || spanZ == 0)
            {
                return false;
            }

            // pozycja w [0..1] w obrębie widoku mapy
            double nx = (worldX - worldMinX) / spanX;
            double nz = (worldZ - worldMinZ) / spanZ;

            // można odfiltrować dalekie rzeczy (poza ekranem)
            if (nx < -0.2 || nx > 1.2 || nz < -0.2 || nz > 1.2)
            {
                return false;
            }

            mapX = mapElem.Bounds.absX + nx * mapElem.Bounds.InnerWidth;
            mapY = mapElem.Bounds.absY + nz * mapElem.Bounds.InnerHeight;

            return true;
        }


        private void GetWorldBoundsForMap(GuiElementMap mapElem, out Vec3d topLeft, out Vec3d bottomRight)
        {
            topLeft = new Vec3d();
            bottomRight = new Vec3d();

            // (0,0) – lewy górny róg mapy (w lokalnych view coords)
            mapElem.TranslateViewPosToWorldPos(
                new Vec2f(0, 0),
                ref topLeft
            );

            // (InnerWidth, InnerHeight) – prawy dolny róg mapy
            mapElem.TranslateViewPosToWorldPos(
                new Vec2f((float)mapElem.Bounds.InnerWidth, (float)mapElem.Bounds.InnerHeight),
                ref bottomRight
            );
        }


    }
}
