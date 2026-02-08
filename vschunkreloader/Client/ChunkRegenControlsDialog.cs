using Vintagestory.API.Client;

namespace vschunkreloader.Client
{
    internal class ChunkRegenControlsDialog : GuiDialog
    {
        private readonly ICoreClientAPI capi;
        private readonly ChunkRegenOverlayLayer overlay;
        private GuiElementDynamicText statusText;



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
            ElementBounds statusBounds = ElementBounds.Fixed(10, 120, 170, 20);
            ElementBounds dialogBounds = ElementBounds.Fixed(50, 80, 210, 170);

            // Tło na całe okienko
            ElementBounds bgBounds = ElementBounds.Fill;

            // Wnętrze z paddingiem
            ElementBounds innerBounds = ElementBounds.Fill
                .WithFixedPadding(GuiStyle.ElementToDialogPadding)
                .FixedGrow(0, 0);
            innerBounds.BothSizing = ElementSizing.Fixed;

            SingleComposer = capi.Gui
                .CreateCompo("Chunk Reloader", dialogBounds)
                .AddShadedDialogBG(bgBounds, true)              // pełne tło
                .AddDialogTitleBar("Chunk Reloader", OnCloseClicked)
                .BeginChildElements(innerBounds);

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
                .AddSmallButton("Execute", OnExecuteClicked, execBounds)
                .AddDynamicText("Mode: (disabled)", CairoFont.WhiteDetailText(), statusBounds, "modeStatus");

            SingleComposer.EndChildElements().Compose();

            // zapamiętujemy referencję do labela
            statusText = SingleComposer.GetDynamicText("modeStatus");
            UpdateStatusText();
        }

        private void UpdateStatusText()
        {
            if (statusText == null) return;

            string edit = overlay.CurrentEditMode.ToString();        // None/Add/Remove
            string sel = overlay.CurrentSelectionMode.ToString();   // None/Single/Box

            string text;

            if (overlay.CurrentEditMode == ChunkRegenEditMode.None ||
                overlay.CurrentSelectionMode == ChunkRegenSelectionMode.None)
            {
                text = "Mode: (disabled)";
            }
            else
            {
                text = $"Mode: {edit} + {sel}";
            }

            statusText.SetNewText(text);
        }


        private void OnCloseClicked()
        {
            TryClose();
        }

        private bool OnAddClicked()
        {
            overlay.SetEditMode(
                overlay.CurrentEditMode == ChunkRegenEditMode.Add
                    ? ChunkRegenEditMode.None
                    : ChunkRegenEditMode.Add
            );

            UpdateStatusText();
            return true;
        }

        private bool OnRemoveClicked()
        {
            overlay.SetEditMode(
                overlay.CurrentEditMode == ChunkRegenEditMode.Remove
                    ? ChunkRegenEditMode.None
                    : ChunkRegenEditMode.Remove
            );

            UpdateStatusText();
            return true;
        }

        private bool OnSingleClicked()
        {
            overlay.SetSelectionMode(
                overlay.CurrentSelectionMode == ChunkRegenSelectionMode.Single
                    ? ChunkRegenSelectionMode.None
                    : ChunkRegenSelectionMode.Single
            );

            UpdateStatusText();
            return true;
        }

        private bool OnBoxClicked()
        {
            overlay.SetSelectionMode(
                overlay.CurrentSelectionMode == ChunkRegenSelectionMode.Box
                    ? ChunkRegenSelectionMode.None
                    : ChunkRegenSelectionMode.Box
            );

            UpdateStatusText();
            return true;
        }


        private bool OnExecuteClicked()
        {
            overlay.ExecuteSelection();
            return true;
        }
    }
}
