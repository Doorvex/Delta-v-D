using System.Linq;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Station;
using Content.Shared.Atmos;
using Content.Shared.Station;
using Robust.Shared.Audio;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server.StationEvents.Events
{
    internal sealed class GasLeak : StationEvent
    {
        [Dependency] private IRobustRandom _robustRandom = default!;
        [Dependency] private IEntityManager _entityManager = default!;

        public override string Name => "GasLeak";

        public override string? StartAnnouncement =>
            "Attention crew, there is a gas leak on the station. We advise you to avoid the area and wear suit internals in the meantime.";

        // Sourced from https://github.com/vgstation-coders/vgstation13/blob/2c5a491446ab824a8fbbf39bcf656b590e0228df/sound/misc/bloblarm.ogg
        public override string? StartAudio => "/Audio/Announcements/bloblarm.ogg";

        protected override string? EndAnnouncement => "The source of the gas leak has been fixed. Please be cautious around areas with gas remaining.";

        private static readonly Gas[] LeakableGases = {
            Gas.Plasma,
            Gas.Tritium,
        };

        public override int EarliestStart => 10;

        public override int MinimumPlayers => 5;

        /// <summary>
        ///     Give people time to get their internals on.
        /// </summary>
        protected override float StartAfter => 20f;

        /// <summary>
        ///     Don't know how long the event will be until we calculate the leak amount.
        /// </summary>
        protected override float EndAfter { get; set; } = float.MaxValue;

        /// <summary>
        ///     Running cooldown of how much time until another leak.
        /// </summary>
        private float _timeUntilLeak;

        /// <summary>
        ///     How long between more gas being added to the tile.
        /// </summary>
        private const float LeakCooldown = 1.0f;

        // Event variables

        private StationId _targetStation;

        private IEntity? _targetGrid;

        private Vector2i _targetTile;

        private EntityCoordinates _targetCoords;

        private bool _foundTile;

        private Gas _leakGas;

        private float _molesPerSecond;

        private const int MinimumMolesPerSecond = 20;

        /// <summary>
        ///     Don't want to make it too fast to give people time to flee.
        /// </summary>
        private const int MaximumMolesPerSecond = 50;

        private const int MinimumGas = 250;

        private const int MaximumGas = 1000;

        private const float SparkChance = 0.05f;

        public override void Startup()
        {
            base.Startup();

            // Essentially we'll pick out a target amount of gas to leak, then a rate to leak it at, then work out the duration from there.
            if (TryFindRandomTile(out _targetTile, out _targetStation, out _targetGrid, out _targetCoords))
            {
                _foundTile = true;

                _leakGas = _robustRandom.Pick(LeakableGases);
                // Was 50-50 on using normal distribution.
                var totalGas = (float) _robustRandom.Next(MinimumGas, MaximumGas);
                _molesPerSecond = _robustRandom.Next(MinimumMolesPerSecond, MaximumMolesPerSecond);
                 EndAfter = totalGas / _molesPerSecond + StartAfter;
                 Logger.InfoS("stationevents", $"Leaking {totalGas} of {_leakGas} over {EndAfter - StartAfter} seconds at {_targetTile}");
            }

            // Look technically if you wanted to guarantee a leak you'd do this in announcement but having the announcement
            // there just to fuck with people even if there is no valid tile is funny.
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            if (!Started || !Running) return;

            _timeUntilLeak -= frameTime;

            if (_timeUntilLeak > 0f) return;
            _timeUntilLeak += LeakCooldown;

            var atmosphereSystem = EntitySystem.Get<AtmosphereSystem>();

            if (!_foundTile ||
                _targetGrid == null ||
                (!IoCManager.Resolve<IEntityManager>().EntityExists(_targetGrid) ? EntityLifeStage.Deleted : IoCManager.Resolve<IEntityManager>().GetComponent<MetaDataComponent>(_targetGrid).EntityLifeStage) >= EntityLifeStage.Deleted ||
                !atmosphereSystem.IsSimulatedGrid(IoCManager.Resolve<IEntityManager>().GetComponent<TransformComponent>(_targetGrid).GridID))
            {
                Running = false;
                return;
            }

            var environment = atmosphereSystem.GetTileMixture(IoCManager.Resolve<IEntityManager>().GetComponent<TransformComponent>(_targetGrid).GridID, _targetTile, true);

            environment?.AdjustMoles(_leakGas, LeakCooldown * _molesPerSecond);
        }

        public override void Shutdown()
        {
            base.Shutdown();

            Spark();

            _foundTile = false;
            _targetGrid = null;
            _targetTile = default;
            _targetCoords = default;
            _leakGas = Gas.Oxygen;
            EndAfter = float.MaxValue;
        }

        private void Spark()
        {
            var atmosphereSystem = EntitySystem.Get<AtmosphereSystem>();
            if (_robustRandom.NextFloat() <= SparkChance)
            {
                if (!_foundTile ||
                    _targetGrid == null ||
                    (!IoCManager.Resolve<IEntityManager>().EntityExists(_targetGrid) ? EntityLifeStage.Deleted : IoCManager.Resolve<IEntityManager>().GetComponent<MetaDataComponent>(_targetGrid).EntityLifeStage) >= EntityLifeStage.Deleted ||
                    !atmosphereSystem.IsSimulatedGrid(IoCManager.Resolve<IEntityManager>().GetComponent<TransformComponent>(_targetGrid).GridID))
                {
                    return;
                }

                // Don't want it to be so obnoxious as to instantly murder anyone in the area but enough that
                // it COULD start potentially start a bigger fire.
                atmosphereSystem.HotspotExpose(IoCManager.Resolve<IEntityManager>().GetComponent<TransformComponent>(_targetGrid).GridID, _targetTile, 700f, 50f, true);
                SoundSystem.Play(Filter.Pvs(_targetCoords), "/Audio/Effects/sparks4.ogg", _targetCoords);
            }
        }
    }
}
