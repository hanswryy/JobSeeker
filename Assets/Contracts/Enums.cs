namespace JobGame
{
    public static class ContractInfo { public const string Version = "1.0"; }

    public enum SkillTag { Git, Komunikasi, Python, SQL, JavaScript, Docker, RestApi, Excel }
    public enum ActionType { Apply, UpgradeSkill, UpgradeExperience, Rest }
    public enum ResourceKind { Energy, Mental, Runway }

    public enum GamePhase { Planning, AwaitingInterview, AwaitingOfferDecision, Ended }

    public enum ActionError
    {
        None, WrongPhase, NoSlotsLeft, UnknownId, AlreadyApplied, InvalidCv, NotOffered, RunEnded
    }

    public enum MarketKind { Normal, CareerFair, HiringFreeze }

    public enum ApplicationStage
    {
        Screening, InterviewReady, FinalReview, OfferReady, Rejected, OfferAccepted, OfferDeclined
    }
    public enum RejectStage { None, Screening, Interview, Final }
    public enum RejectReason { None, MissingMustHave, ExperienceTooLow }

    public enum CardKind { Skill, Experience }
    public enum AnswerQuality { Weak, Okay, Strong }
    public enum RunOutcome { Won, LostRunway, LostMental, LostTimeUp }
    public enum ChainKind { Skill, Experience, Apply, Interview, Offer, Failure }

    public static class SkillTagExtensions
    {
        public static string DisplayName(this SkillTag tag)
        {
            return tag == SkillTag.RestApi ? "REST API" : tag.ToString();
        }
    }
}
