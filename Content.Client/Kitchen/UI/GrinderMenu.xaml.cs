using System.Collections.Generic;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Kitchen.Components;
using Robust.Client.AutoGenerated;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Client.Kitchen.UI
{
    [GenerateTypedNameReferences]
    public partial class GrinderMenu : SS14Window
    {
        private readonly IEntityManager _entityManager;
        private readonly IPrototypeManager _prototypeManager ;
        private readonly ReagentGrinderBoundUserInterface _owner;

        private readonly Dictionary<int, EntityUid> _chamberVisualContents = new();

        public GrinderMenu(ReagentGrinderBoundUserInterface owner, IEntityManager entityManager, IPrototypeManager prototypeManager)
        {
            RobustXamlLoader.Load(this);
            _entityManager = entityManager;
            _prototypeManager = prototypeManager;
            _owner = owner;
            GrindButton.OnPressed += owner.StartGrinding;
            JuiceButton.OnPressed += owner.StartJuicing;
            ChamberContentBox.EjectButton.OnPressed += owner.EjectAll;
            BeakerContentBox.EjectButton.OnPressed += owner.EjectBeaker;
            ChamberContentBox.BoxContents.OnItemSelected += OnChamberBoxContentsItemSelected;
            BeakerContentBox.BoxContents.SelectMode = ItemList.ItemListSelectMode.None;
        }

        private void OnChamberBoxContentsItemSelected(ItemList.ItemListSelectedEventArgs args)
        {
            _owner.EjectChamberContent(_chamberVisualContents[args.ItemIndex]);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            _chamberVisualContents.Clear();
            GrindButton.OnPressed -= _owner.StartGrinding;
            JuiceButton.OnPressed -= _owner.StartJuicing;
            ChamberContentBox.EjectButton.OnPressed -= _owner.EjectAll;
            BeakerContentBox.EjectButton.OnPressed -= _owner.EjectBeaker;
            ChamberContentBox.BoxContents.OnItemSelected -= OnChamberBoxContentsItemSelected;
        }

        public void UpdateState(ReagentGrinderInterfaceState state)
        {
            BeakerContentBox.EjectButton.Disabled = !state.HasBeakerIn;
            ChamberContentBox.EjectButton.Disabled = state.ChamberContents.Length <= 0;
            GrindButton.Disabled = !state.CanGrind || !state.Powered;
            JuiceButton.Disabled = !state.CanJuice || !state.Powered;
            RefreshContentsDisplay(state.ReagentQuantities, state.ChamberContents, state.HasBeakerIn);
        }

        public void HandleMessage(BoundUserInterfaceMessage message)
        {
            switch (message)
            {
                case SharedReagentGrinderComponent.ReagentGrinderWorkStartedMessage workStarted:
                    GrindButton.Disabled = true;
                    GrindButton.Modulate = workStarted.GrinderProgram == SharedReagentGrinderComponent.GrinderProgram.Grind ? Color.Green : Color.White;
                    JuiceButton.Disabled = true;
                    JuiceButton.Modulate = workStarted.GrinderProgram == SharedReagentGrinderComponent.GrinderProgram.Juice ? Color.Green : Color.White;
                    BeakerContentBox.EjectButton.Disabled = true;
                    ChamberContentBox.EjectButton.Disabled = true;
                    break;
                case SharedReagentGrinderComponent.ReagentGrinderWorkCompleteMessage doneMessage:
                    GrindButton.Disabled = false;
                    JuiceButton.Disabled = false;
                    GrindButton.Modulate = Color.White;
                    JuiceButton.Modulate = Color.White;
                    BeakerContentBox.EjectButton.Disabled = false;
                    ChamberContentBox.EjectButton.Disabled = false;
                    break;
            }
        }

        private void RefreshContentsDisplay(IList<Solution.ReagentQuantity>? reagents, IReadOnlyList<EntityUid> containedSolids, bool isBeakerAttached)
        {
            //Refresh chamber contents
            _chamberVisualContents.Clear();

            ChamberContentBox.BoxContents.Clear();
            foreach (var uid in containedSolids)
            {
                if (!_entityManager.TryGetEntity(uid, out var entity))
                {
                    return;
                }
                var texture = IoCManager.Resolve<IEntityManager>().GetComponent<SpriteComponent>(entity).Icon?.Default;

                var solidItem = ChamberContentBox.BoxContents.AddItem(IoCManager.Resolve<IEntityManager>().GetComponent<MetaDataComponent>(entity).EntityName, texture);
                var solidIndex = ChamberContentBox.BoxContents.IndexOf(solidItem);
                _chamberVisualContents.Add(solidIndex, uid);
            }

            //Refresh beaker contents
            BeakerContentBox.BoxContents.Clear();
            //if no beaker is attached use this guard to prevent hitting a null reference.
            if (!isBeakerAttached || reagents == null)
            {
                return;
            }

            //Looks like we have a beaker attached.
            if (reagents.Count <= 0)
            {
                BeakerContentBox.BoxContents.AddItem(Loc.GetString("grinder-menu-beaker-content-box-is-empty"));
            }
            else
            {
                foreach (var reagent in reagents)
                {
                    var reagentName = _prototypeManager.TryIndex(reagent.ReagentId, out ReagentPrototype? proto) ? Loc.GetString($"{reagent.Quantity} {proto.Name}") : "???";
                    BeakerContentBox.BoxContents.AddItem(reagentName);
                }
            }
        }
    }
}
