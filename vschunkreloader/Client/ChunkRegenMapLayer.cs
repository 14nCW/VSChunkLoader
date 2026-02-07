namespace ChunkRegenMod
{
    //public class ChunkRegenMapLayer : IWorldMapLayer
    //{
    //    private readonly ICoreClientAPI capi;
    //    private readonly ChunkRegenClientSystem clientSystem;

    //    // Zaznaczone chunki w koordach chunkowych
    //    private readonly HashSet<Vec2i> selectedChunks = new HashSet<Vec2i>();

    //    public string Title => "Chunk Regen";
    //    public string LayerGroupCode => "debug"; // albo "player", "other" - gdzie ma się wyświetlać
    //    public EnumWorldMapCenterMode CenterMode => EnumWorldMapCenterMode.Player;

    //    public ChunkRegenMapLayer(ICoreClientAPI capi, ChunkRegenClientSystem clientSystem)
    //    {
    //        this.capi = capi;
    //        this.clientSystem = clientSystem;
    //    }

    //    public void OnRender(GuiElementMap map, float dt)
    //    {
    //        // 1. Przeliczenie wycinka mapy na world coords
    //        // 2. Dla każdej komórki zaznaczonego chunka narysowanie prostokąta

    //        foreach (var c in selectedChunks)
    //        {
    //            // chunk -> world (blok)
    //            int chunkSize = capi.World.BlockAccessor.ChunkSize;
    //            int wx = c.X * chunkSize;
    //            int wz = c.Y * chunkSize;

    //            // world -> coords na mapie
    //            double mx1, my1, mx2, my2;
    //            map.TranslateWorldPosToViewPos(wx, wz, out mx1, out my1);
    //            map.TranslateWorldPosToViewPos(wx + chunkSize, wz + chunkSize, out mx2, out my2);

    //            capi.Render.Render2DRectangle(
    //                (float)mx1, (float)my1,
    //                (float)(mx2 - mx1), (float)(my2 - my1),
    //                0.3f, 0.6f, 1f, 0.3f   // kolor + alfa (możesz dobrać)
    //            );
    //        }
    //    }

    //    public void OnMouseDown(MapMouseEvent args)
    //    {
    //        if (args.Button == EnumMouseButton.Left)
    //        {
    //            ToggleChunkSelectionAt(args);
    //            args.Handled = true;
    //        }
    //    }

    //    private void ToggleChunkSelectionAt(MapMouseEvent args)
    //    {
    //        // klik → współrzędne świata
    //        int wx, wz;
    //        capi.World.Map.TranslateViewPosToWorldPos(args.X, args.Y, out wx, out wz);

    //        int chunkSize = capi.World.BlockAccessor.ChunkSize;
    //        int cx = wx >> 5;
    //        int cz = wz >> 5;

    //        var c = new Vec2i(cx, cz);

    //        if (!selectedChunks.Add(c))
    //        {
    //            selectedChunks.Remove(c);
    //        }
    //    }

    //    public void SendSelectionToServer(bool deleteRegion)
    //    {
    //        if (selectedChunks.Count == 0) return;

    //        var list = new List<Vec2i>(selectedChunks);

    //        clientSystem.netChannel.SendPacket(new ChunkRegenRequestPacket
    //        {
    //            Coords = list,
    //            DeleteRegion = deleteRegion
    //        });

    //        capi.ShowChatMessage($"Wysłano {list.Count} chunk(ów) do regeneracji.");
    //    }

    //    // Resztę metod IWorldMapLayer możesz zaimplementować pustych / minimalnych:
    //    public void OnTick(float dt) { }
    //    public void OnMouseUp(MapMouseEvent args) { }
    //    public void OnMouseMove(MapMouseEvent args) { }
    //    public void OnKeyDown(MapKeyEvent args) { }
    //    public void OnKeyUp(MapKeyEvent args) { }
    //    public void Dispose() { /* jeśli kiedyś dodasz tekstury/meshe, tu zrobisz Dispose */ }
    //}
}
