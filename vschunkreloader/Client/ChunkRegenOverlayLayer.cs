using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using VsChunkReloader;

namespace vschunkreloader.Client
{
    // tryb edycji – co robimy z chunkami
    public enum ChunkRegenEditMode
    {
        None,
        Add,
        Remove
    }

    // tryb zaznaczania – jak interpretujemy kliknięcia
    public enum ChunkRegenSelectionMode
    {
        None,
        Single,
        Box
    }

    public class ChunkRegenOverlayLayer : MapLayer
    {
        private readonly ICoreClientAPI capi;
        private readonly IWorldMapManager worldMapManager;
        private readonly ChunkRegenClientSystem clientSystem;

        private LoadedTexture debugTexture;       // czerwony
        private LoadedTexture boxStartTexture;    // fioletowy
        private int chunkSize = 32;

        // === NOWE POLA STANU ===
        private ChunkRegenEditMode editMode = ChunkRegenEditMode.None;
        private ChunkRegenSelectionMode selectionMode = ChunkRegenSelectionMode.None;

        private bool awaitingBoxEnd = false;
        private Vec2i boxStartChunk;
        private Vec2i boxCurrentChunk;

        private ChunkRegenControlsDialog controlsDialog;

        //enum SelectionMode
        //{
        //    Single,
        //    Box
        //}

        //enum EditMode
        //{
        //    Add,
        //    Remove
        //}


        public override string Title => "Chunk Reloader";
        public override EnumMapAppSide DataSide => EnumMapAppSide.Client;
        public override string LayerGroupCode => "chunkreloader";

        public ChunkRegenOverlayLayer(ICoreAPI api, IWorldMapManager mapSink)
            : base(api, mapSink)
        {
            this.capi = (ICoreClientAPI)api;
            this.worldMapManager = mapSink;

            // ModSystem klientowy
            this.clientSystem = api.ModLoader.GetModSystem<ChunkRegenClientSystem>(true);

            debugTexture = new LoadedTexture(capi, 0, 1, 1);
            int[] redPixels = { ColorUtil.ColorFromRgba(255, 0, 0, 120) };
            capi.Render.LoadOrUpdateTextureFromRgba(redPixels, false, 0, ref debugTexture);

            // Fioletowy – start boxa
            boxStartTexture = new LoadedTexture(capi, 0, 1, 1);
            int[] purplePixels = { ColorUtil.ColorFromRgba(180, 0, 255, 180) };
            capi.Render.LoadOrUpdateTextureFromRgba(purplePixels, false, 0, ref boxStartTexture);

        }

        public override void Dispose()
        {
            debugTexture?.Dispose();
            boxStartTexture?.Dispose();
            base.Dispose();
        }

        public override void OnMapOpenedClient()
        {
            // Wywoływane tylko dla WORLD MAPY, nie minimapy
            // (tak samo jak w ProspectOverlayLayer)

            if (controlsDialog == null)
            {
                controlsDialog = new ChunkRegenControlsDialog(capi, this);
            }

            if (!controlsDialog.IsOpened())
            {
                controlsDialog.TryOpen();
            }
        }

        public override void OnMapClosedClient()
        {
            if (controlsDialog != null && controlsDialog.IsOpened())
            {
                controlsDialog.TryClose();
            }
        }


        //public override void OnMapClosedClient()
        //{
        //    // zamknij okienko, jeśli mapę zamknięto
        //    if (controlsDialog != null && controlsDialog.IsOpened())
        //    {
        //        controlsDialog.TryClose();
        //    }
        //}



        public override void Render(GuiElementMap mapElem, float dt)
        {
            if (!Active) return;

            GetWorldBoundsForMap(mapElem, out Vec3d worldTopLeft, out Vec3d worldBottomRight);

            // 1. Czerwone – wszystkie zaznaczone chunki
            foreach (var c in clientSystem.selectedChunks)
            {
                DrawChunkOverlay(mapElem, worldTopLeft, worldBottomRight, c);
            }

            // 2. Fiolet – start boxa (po pierwszym kliku w trybie Box)
            if (selectionMode == ChunkRegenSelectionMode.Box && awaitingBoxEnd)
            {
                DrawStartChunkOverlay(mapElem, worldTopLeft, worldBottomRight, boxStartChunk);
            }
        }


        // ============================================================
        // MOUSE INPUT
        // ============================================================

        public override void OnMouseUpClient(MouseEvent args, GuiElementMap mapElem)
        {
            if (!Active) return;
            if (args.Button != EnumMouseButton.Left) return;

            // dopóki użytkownik nie wybierze trybów – nic się nie dzieje
            if (editMode == ChunkRegenEditMode.None || selectionMode == ChunkRegenSelectionMode.None)
                return;

            int worldX, worldZ;
            if (!TryGetWorldPosFromMouse(mapElem, args, out worldX, out worldZ))
                return;

            Vec2i chunk = new Vec2i(worldX >> 5, worldZ >> 5);

            // === BOX MODE: dwa kliknięcia ===
            if (selectionMode == ChunkRegenSelectionMode.Box)
            {
                if (!awaitingBoxEnd)
                {
                    boxStartChunk = chunk;
                    awaitingBoxEnd = true;
                }
                else
                {
                    boxCurrentChunk = chunk;
                    awaitingBoxEnd = false;
                    ApplyBoxSelection(boxStartChunk, boxCurrentChunk);
                }

                return;
            }

            // === SINGLE MODE ===
            if (selectionMode == ChunkRegenSelectionMode.Single)
            {
                if (editMode == ChunkRegenEditMode.Add)
                {
                    clientSystem.selectedChunks.Add(chunk);
                }
                else if (editMode == ChunkRegenEditMode.Remove)
                {
                    clientSystem.selectedChunks.Remove(chunk);
                }
            }
        }

        // prostokąt: dodaj wszystkie chunki z boxStartChunk..boxCurrentChunk
        private void ApplyBoxSelection(Vec2i a, Vec2i b)
        {
            int minX = Math.Min(a.X, b.X);
            int maxX = Math.Max(a.X, b.X);
            int minZ = Math.Min(a.Y, b.Y);
            int maxZ = Math.Max(a.Y, b.Y);

            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    Vec2i c = new Vec2i(x, z);

                    if (editMode == ChunkRegenEditMode.Add)
                    {
                        clientSystem.selectedChunks.Add(c);
                    }
                    else if (editMode == ChunkRegenEditMode.Remove)
                    {
                        clientSystem.selectedChunks.Remove(c);
                    }
                }
            }

            string op = editMode == ChunkRegenEditMode.Add ? "ADD" : "REMOVE";
        }

        // ============================================================
        // KONWERSJE I RYSOWANIE (BEZ ZMIAN W LOGICE)
        // ============================================================

        private bool TryGetWorldPosFromMouse(GuiElementMap mapElem, MouseEvent args, out int worldX, out int worldZ)
        {
            worldX = 0;
            worldZ = 0;

            float relX = (float)(args.X - mapElem.Bounds.absX);
            float relY = (float)(args.Y - mapElem.Bounds.absY);

            if (relX < 0 || relY < 0 || relX > mapElem.Bounds.InnerWidth || relY > mapElem.Bounds.InnerHeight)
            {
                return false;
            }

            Vec3d worldPos = new Vec3d();
            mapElem.TranslateViewPosToWorldPos(new Vec2f(relX, relY), ref worldPos);

            worldX = (int)worldPos.X;
            worldZ = (int)worldPos.Z;
            return true;
        }

        private void DrawChunkOverlay(GuiElementMap mapElem, Vec3d worldTopLeft, Vec3d worldBottomRight, Vec2i chunkCoord)
        {
            int wx1 = chunkCoord.X * chunkSize;
            int wz1 = chunkCoord.Y * chunkSize;
            int wx2 = wx1 + chunkSize;
            int wz2 = wz1 + chunkSize;

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

        private void DrawStartChunkOverlay(GuiElementMap mapElem, Vec3d worldTopLeft, Vec3d worldBottomRight, Vec2i chunkCoord)
        {
            int wx1 = chunkCoord.X * chunkSize;
            int wz1 = chunkCoord.Y * chunkSize;
            int wx2 = wx1 + chunkSize;
            int wz2 = wz1 + chunkSize;

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
                boxStartTexture.TextureId,
                (float)px, (float)py, (float)pw, (float)ph,
                95f // lekko nad czerwonym (który ma np. 90f)
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

            double nx = (worldX - worldMinX) / spanX;
            double nz = (worldZ - worldMinZ) / spanZ;

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

            mapElem.TranslateViewPosToWorldPos(
                new Vec2f(0, 0),
                ref topLeft
            );

            mapElem.TranslateViewPosToWorldPos(
                new Vec2f((float)mapElem.Bounds.InnerWidth, (float)mapElem.Bounds.InnerHeight),
                ref bottomRight
            );
        }

        public ChunkRegenEditMode CurrentEditMode => editMode;
        public ChunkRegenSelectionMode CurrentSelectionMode => selectionMode;

        public void SetEditMode(ChunkRegenEditMode mode)
        {
            editMode = mode;
            // przy zmianie trybu edycji kasujemy niedokończonego boxa
            if (selectionMode != ChunkRegenSelectionMode.Box)
            {
                awaitingBoxEnd = false;
            }
        }

        public void SetSelectionMode(ChunkRegenSelectionMode mode)
        {
            selectionMode = mode;
            // przy zmianie trybu selekcji kasujemy niedokończonego boxa
            if (selectionMode != ChunkRegenSelectionMode.Box)
            {
                awaitingBoxEnd = false;
            }
        }

        // będzie wołane z guzika "Execute"
        public void ExecuteSelection()
        {
            clientSystem.SendChunkReload();
        }

    }

}
