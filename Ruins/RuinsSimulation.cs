using System;
using System.Collections.Generic;
using System.Linq;
using Game.Content.Features.Belts;
using Game.Core.Simulation;
using JetBrains.Annotations;

namespace ArcticRuins.Ruins
{
    public class RuinsSimulation([NotNull] RuinsSimulationState state)
        : Simulation<RuinsSimulationState>(state)
    { }
}