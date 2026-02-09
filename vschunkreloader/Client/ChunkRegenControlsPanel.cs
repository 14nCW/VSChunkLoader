using Vintagestory.API.Client;
using Vintagestory.GameContent;

namespace vschunkreloader.Client
{
    public class ChunkRegenControlsPanel
    {
        private readonly ICoreClientAPI capi;
        private readonly ChunkRegenOverlayLayer overlay;
        private GuiElementDynamicText statusText;


        public ChunkRegenControlsPanel(ICoreClientAPI capi, ChunkRegenOverlayLayer overlay)
        {
            this.capi = capi;
            this.overlay = overlay;
        }

        public void Compose(string key, GuiDialogWorldMap mapDlg, GuiComposer _)
        {
            ElementBounds dialogBounds = ElementBounds.Fixed(50, 80, 290, 210);

            ElementBounds bgBounds = ElementBounds.Fill;

            ElementBounds innerBounds = ElementBounds.Fill
                .WithFixedPadding(GuiStyle.ElementToDialogPadding)
                .FixedGrow(0, 0);
            innerBounds.BothSizing = ElementSizing.Fixed;


            ElementBounds clearAll = ElementBounds.Fixed(0, 20, 120, 25);
            ElementBounds clearButtons = ElementBounds.Fixed(130, 20, 120, 25);
            ElementBounds statusBounds = ElementBounds.Fixed(0, 50, 170, 20);

            ElementBounds addBounds = ElementBounds.Fixed(40, 80, 80, 25);
            ElementBounds removeBounds = ElementBounds.Fixed(40 + 90, 80, 80, 25);
            ElementBounds singleBounds = ElementBounds.Fixed(40, 110, 80, 25);
            ElementBounds boxBounds = ElementBounds.Fixed(40 + 90, 110, 80, 25);
            ElementBounds execBounds = ElementBounds.Fixed(40, 155, 170, 25);

            GuiComposer composer = capi.Gui
                .CreateCompo("Chunk Reloader Settings", dialogBounds)
                .AddShadedDialogBG(bgBounds, true)
                .AddDialogTitleBar("Chunk Reloader", () =>
                {
                    mapDlg.Composers[key].Enabled = false;
                })
                .BeginChildElements(innerBounds)
                    .AddSmallButton("Clear All", OnClearAllClicked, clearAll)
                    .AddSmallButton("Clear Buttons", OnClearButtonsClicked, clearButtons)
                    .AddSmallButton("Add", OnAddClicked, addBounds)
                    .AddSmallButton("Remove", OnRemoveClicked, removeBounds)
                    .AddSmallButton("Single", OnSingleClicked, singleBounds)
                    .AddSmallButton("Box", OnBoxClicked, boxBounds)
                    .AddSmallButton("Execute", OnExecuteClicked, execBounds)
                    .AddDynamicText("Mode: (disabled)", CairoFont.WhiteDetailText(), statusBounds, "modeStatus")
                .EndChildElements()
                .Compose(true);

            statusText = composer.GetDynamicText("modeStatus");
            UpdateStatusText();

            mapDlg.Composers[key] = composer;
        }

        private void UpdateStatusText()
        {
            if (statusText == null) return;

            string edit = overlay.CurrentEditMode.ToString();
            string sel = overlay.CurrentSelectionMode.ToString();

            string text;

            if (overlay.CurrentEditMode == ChunkRegenEditMode.None &&
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

        private bool OnClearButtonsClicked()
        {
            overlay.ClearButtons();
            UpdateStatusText();
            return true;
        }

        private bool OnClearAllClicked()
        {
            OnClearButtonsClicked();
            overlay.ClearAll();
            return true;
        }
    }
}
