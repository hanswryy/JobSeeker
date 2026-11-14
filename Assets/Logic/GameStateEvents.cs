using System;

namespace JobGame.Logic
{
    public sealed partial class GameState
    {
        public event Action<int> WeekStarted;
        public event Action<int> SlotsChanged;
        public event Action<ResourceKind, float, float> ResourceChanged;
        public event Action<MarketEvent> MarketChanged;
        public event Action PostingsChanged;
        public event Action HoldingsChanged;
        public event Action<ApplicationRecord> ApplicationUpdated;
        public event Action<GamePhase> PhaseChanged;
        public event Action<IInterviewSession> InterviewStarted;
        public event Action<Offer> OfferReceived;
        public event Action<RunSummary> RunEnded;
    }
}
