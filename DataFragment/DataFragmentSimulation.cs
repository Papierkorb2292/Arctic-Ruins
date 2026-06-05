using System;
using System.Collections.Generic;
using System.Linq;
using Game.Content.Features.Belts;
using Game.Core.Simulation;
using JetBrains.Annotations;

namespace ArcticRuins.DataFragment
{
    public class DataFragmentSimulation([NotNull] DataFragmentSimulationState state)
        : Simulation<DataFragmentSimulationState>(state)
    {
    }
}