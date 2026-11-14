using UnityEngine;

namespace JobGame.Examples
{
    // Contoh loop interview: UI tidak mengasumsikan jumlah pertanyaan selalu tiga.
    public sealed class ExampleInterviewPresenter : MonoBehaviour
    {
        IGameSession _session;
        IInterviewSession _interview;

        public void Bind(IGameSession session)
        {
            if (_session != null)
                _session.InterviewStarted -= OnInterviewStarted;

            _session = session;
            if (_session != null)
            {
                _session.InterviewStarted += OnInterviewStarted;
                if (_session.ActiveInterview != null)
                    OnInterviewStarted(_session.ActiveInterview);
            }
        }

        void OnDestroy()
        {
            if (_session != null)
                _session.InterviewStarted -= OnInterviewStarted;
        }

        void OnInterviewStarted(IInterviewSession interview)
        {
            _interview = interview;
            Debug.Log("Interview dimulai: " + interview.JobTitle, this);
            RefreshQuestion();
        }

        public void OnCardClicked(CardRef card)
        {
            if (_interview == null || _interview.IsFinished)
                return;

            AnswerFeedback feedback = _interview.Answer(card);
            if (feedback == null)
                return;

            Debug.Log("Feedback: " + feedback.Quality + " - " + feedback.Text, this);
            RefreshQuestion();
        }

        public void OnCloseInterviewClicked()
        {
            if (_session == null || _interview == null || !_interview.IsFinished)
                return;

            ActionResult result = _session.CloseInterview();
            if (!result.Ok)
                Debug.LogWarning(result.Error + ": " + result.Message, this);
        }

        void RefreshQuestion()
        {
            if (_interview == null)
                return;

            if (_interview.IsFinished)
            {
                Debug.Log("Interview selesai: " + _interview.Result.Summary, this);
                return;
            }

            // CurrentIndex digunakan untuk mengambil pertanyaan aktif.
            // UI menampilkan semua kartu dari AvailableCards sebagai pilihan.
            InterviewQuestion question = _interview.Questions[_interview.CurrentIndex];
            Debug.Log("Pertanyaan: " + question.Prompt, this);
        }
    }
}
