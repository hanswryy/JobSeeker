using System.Collections.Generic;

namespace JobGame
{
    // Bot hanya boleh memakai IGameSession, sama seperti UI. Dimiliki A (task A7).
    public interface IStrategyBot
    {
        string Name { get; }
        void PlayRun(IGameSession session);                // main sampai Phase == Ended
    }

    public sealed class BatchReport
    {
        public string BotName;
        public int Runs, Wins;
        public float WinRate, AvgWeeksPlayed, AvgScoreWhenWon;
        public Dictionary<ActionType, int> ActionCounts = new Dictionary<ActionType, int>();
    }

    public interface IBatchRunner
    {
        BatchReport Run(IStrategyBot bot, IGameFactory factory, int runs, int baseSeed);
    }
}
