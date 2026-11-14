using System.Collections.Generic;

namespace JobGame
{
    public sealed class InterviewQuestion
    {
        public int Index;
        public SkillTag Topic;
        public string Prompt;             // siap tampil
    }

    public sealed class AnswerFeedback
    {
        public int QuestionIndex;
        public AnswerQuality Quality;
        public string Text;               // siap tampil
    }

    public sealed class InterviewResult
    {
        public bool Passed;
        public int StrongCount, OkayCount, WeakCount;
        public string Summary;            // siap tampil
    }

    public interface IInterviewSession
    {
        string JobTitle { get; }
        string Company { get; }

        // Jumlah pertanyaan 1-3. UI wajib melakukan loop sampai Questions.Count.
        IReadOnlyList<InterviewQuestion> Questions { get; }
        int CurrentIndex { get; }                          // 0..Questions.Count
        IReadOnlyList<CardRef> AvailableCards { get; }     // kartu di CV yang dikirim
        bool IsFinished { get; }
        InterviewResult Result { get; }                    // null sampai IsFinished

        // Menjawab pertanyaan saat ini. Mengembalikan null (tanpa efek) jika
        // IsFinished atau kartu tidak ada di AvailableCards.
        AnswerFeedback Answer(CardRef card);
    }
}
