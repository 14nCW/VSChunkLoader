using Vintagestory.API.Client;

namespace vschunkreloader.Client
{
    internal class ChunkRegenControlsDialog : GuiDialog
    {
        private readonly ICoreClientAPI capi;
        private readonly ChunkRegenOverlayLayer overlay;

        public override string ToggleKeyCombinationCode => "chunkreloadercontrols";

        public ChunkRegenControlsDialog(ICoreClientAPI capi, ChunkRegenOverlayLayer overlay)
            : base(capi)
        {
            this.capi = capi;
            this.overlay = overlay;

            Compose();
        }

        private void Compose()
        {
            // małe okienko, pozycję możesz potem poprawić
            ElementBounds dialogBounds = ElementBounds.Fixed(50, 80, 190, 140);

            ElementBounds bgBounds = ElementBounds.Fill
                .WithFixedPadding(GuiStyle.ElementToDialogPadding)
                .FixedGrow(0, 0);

            bgBounds.BothSizing = ElementSizing.Fixed;

            SingleComposer = capi.Gui
                .CreateCompo("chunkreloader-controls", dialogBounds)
                .AddDialogTitleBar("Chunk Reloader", OnCloseClicked)
                .BeginChildElements(bgBounds);

            ElementBounds addBounds = ElementBounds.Fixed(10, 25, 80, 25);
            ElementBounds removeBounds = ElementBounds.Fixed(100, 25, 80, 25);
            ElementBounds singleBounds = ElementBounds.Fixed(10, 55, 80, 25);
            ElementBounds boxBounds = ElementBounds.Fixed(100, 55, 80, 25);
            ElementBounds execBounds = ElementBounds.Fixed(10, 90, 170, 25);

            SingleComposer
                .AddSmallButton("Add", OnAddClicked, addBounds)
                .AddSmallButton("Remove", OnRemoveClicked, removeBounds)
                .AddSmallButton("Single", OnSingleClicked, singleBounds)
                .AddSmallButton("Box", OnBoxClicked, boxBounds)
                .AddSmallButton("Execute", OnExecuteClicked, execBounds);

            SingleComposer.EndChildElements().Compose();
        }

        private void OnCloseClicked()
        {
            TryClose();
        }

        private bool OnAddClicked()
        {
            // toggle Add / None
            overlay.SetEditMode(
                overlay.CurrentEditMode == ChunkRegenEditMode.Add
                    ? ChunkRegenEditMode.None
                    : ChunkRegenEditMode.Add
            );

            capi.ShowChatMessage($"[ChunkRegen] EditMode = {overlay.CurrentEditMode}");
            return true;
        }

        private bool OnRemoveClicked()
        {
            overlay.SetEditMode(
                overlay.CurrentEditMode == ChunkRegenEditMode.Remove
                    ? ChunkRegenEditMode.None
                    : ChunkRegenEditMode.Remove
            );

            capi.ShowChatMessage($"[ChunkRegen] EditMode = {overlay.CurrentEditMode}");
            return true;
        }

        private bool OnSingleClicked()
        {
            overlay.SetSelectionMode(
                overlay.CurrentSelectionMode == ChunkRegenSelectionMode.Single
                    ? ChunkRegenSelectionMode.None
                    : ChunkRegenSelectionMode.Single
            );

            capi.ShowChatMessage($"[ChunkRegen] SelectionMode = {overlay.CurrentSelectionMode}");
            return true;
        }

        private bool OnBoxClicked()
        {
            overlay.SetSelectionMode(
                overlay.CurrentSelectionMode == ChunkRegenSelectionMode.Box
                    ? ChunkRegenSelectionMode.None
                    : ChunkRegenSelectionMode.Box
            );

            capi.ShowChatMessage($"[ChunkRegen] SelectionMode = {overlay.CurrentSelectionMode}");
            return true;
        }

        private bool OnExecuteClicked()
        {
            overlay.ExecuteSelection();
            return true;
        }
    }
}
