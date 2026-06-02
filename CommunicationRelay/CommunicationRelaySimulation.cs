using System;
using System.Collections.Generic;
using System.Linq;
using Game.Content.Features.Belts;
using Game.Core.Simulation;
using JetBrains.Annotations;

namespace ArcticRuins.CommunicationRelay
{
    public class CommunicationRelaySimulation([NotNull] CommunicationRelaySimulationState state)
        : Simulation<CommunicationRelaySimulationState>(state)
    {
    }
}