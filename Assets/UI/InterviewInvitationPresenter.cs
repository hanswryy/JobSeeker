using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JobGame.UI
{
    public sealed class InterviewInvitationPresenter : MonoBehaviour
    {
        [Header("Session and Navigation")]
        [SerializeField] SessionHudPresenter sessionPresenter;
        [SerializeField] PanelNavigator panelNavigator;
        [SerializeField] GameObject mailsPanel;
        [SerializeField] GameObject interviewPanel;

        [Header("Invitation Email")]
        [SerializeField] TMP_Text senderNameText;
        [SerializeField] TMP_Text receiverNameText;
        [SerializeField] TMP_Text subjectText;
        [SerializeField] TMP_Text messageContentText;
        [SerializeField] TMP_Text[] inboxJobTitleTexts;
        [SerializeField] Button acceptButton;
        [SerializeField] Button declineButton;

        IGameSession _session;
        IInterviewSession _pendingInterview;
        Offer _pendingOffer;
        bool _decisionMade;

        void OnEnable()
        {
            if (acceptButton != null)
                acceptButton.onClick.AddListener(OnAcceptClicked);
            if (declineButton != null)
                declineButton.onClick.AddListener(OnDeclineClicked);

            if (!ValidateSetup())
                return;

            sessionPresenter.SessionBound += Bind;
            Bind(sessionPresenter.Session);
        }

        void OnDisable()
        {
            if (acceptButton != null)
                acceptButton.onClick.RemoveListener(OnAcceptClicked);
            if (declineButton != null)
                declineButton.onClick.RemoveListener(OnDeclineClicked);

            if (sessionPresenter != null)
                sessionPresenter.SessionBound -= Bind;
            Unbind(_session);
            _session = null;
        }

        public void Bind(IGameSession session)
        {
            Unbind(_session);
            _session = session;
            _pendingInterview = null;
            _pendingOffer = null;
            _decisionMade = false;

            if (_session == null)
            {
                RefreshInbox();
                SetDecisionButtons(false);
                return;
            }

            _session.InterviewStarted += OnInterviewStarted;
            _session.OfferReceived += OnOfferReceived;
            _session.ApplicationUpdated += OnApplicationUpdated;
            _session.PhaseChanged += OnPhaseChanged;
            RefreshInbox();

            if (_session.Phase == GamePhase.AwaitingInterview &&
                _session.ActiveInterview != null)
                OpenInvitation(_session.ActiveInterview);
            else if (_session.Phase == GamePhase.AwaitingOfferDecision &&
                _session.PendingOffer != null)
                OpenOffer(_session.PendingOffer);
            else
                SetDecisionButtons(false);
        }

        bool ValidateSetup()
        {
            if (sessionPresenter != null &&
                panelNavigator != null &&
                mailsPanel != null &&
                interviewPanel != null &&
                senderNameText != null &&
                receiverNameText != null &&
                subjectText != null &&
                messageContentText != null &&
                inboxJobTitleTexts != null &&
                inboxJobTitleTexts.Length > 0 &&
                acceptButton != null &&
                declineButton != null)
            {
                if (object.ReferenceEquals(subjectText, messageContentText))
                    Debug.LogWarning(
                        "Subject dan Message Content menunjuk ke TMP yang sama; email akan digabung pada satu field.",
                        this);
                return true;
            }

            Debug.LogError(
                "InterviewInvitationPresenter: assign SessionHudPresenter, PanelNavigator, " +
                "panel Mails/Xoom, field email, daftar inbox, serta tombol Accept/Decline di Inspector.",
                this);
            return false;
        }

        void Unbind(IGameSession session)
        {
            if (session == null)
                return;

            session.InterviewStarted -= OnInterviewStarted;
            session.OfferReceived -= OnOfferReceived;
            session.ApplicationUpdated -= OnApplicationUpdated;
            session.PhaseChanged -= OnPhaseChanged;
        }

        void OnInterviewStarted(IInterviewSession interview)
        {
            OpenInvitation(interview);
        }

        void OnOfferReceived(Offer offer)
        {
            OpenOffer(offer);
        }

        void OnApplicationUpdated(ApplicationRecord application)
        {
            RefreshInbox();
        }

        void OnPhaseChanged(GamePhase phase)
        {
            if (phase == GamePhase.Ended)
            {
                ClearPendingDecision();
                SetDecisionButtons(false);
                panelNavigator.ShowDesktop();
                return;
            }

            if (phase == GamePhase.Planning && _session != null &&
                _session.ActiveInterview == null && _session.PendingOffer == null &&
                (_pendingInterview != null || _pendingOffer != null))
            {
                ClearPendingDecision();
                RefreshInbox();
                SetDecisionButtons(false);
                panelNavigator.ShowDesktop();
            }
        }

        void ClearPendingDecision()
        {
            _pendingInterview = null;
            _pendingOffer = null;
            _decisionMade = false;
        }

        void OpenInvitation(IInterviewSession interview)
        {
            if (interview == null)
            {
                Debug.LogError("InterviewStarted mengirim sesi interview kosong.", this);
                return;
            }

            _pendingInterview = interview;
            _pendingOffer = null;
            _decisionMade = false;
            senderNameText.text = "Tim Rekrutmen - " + interview.Company;
            receiverNameText.text = "Kepada: Kandidat";
            SetEmailText(
                "Undangan Interview: " + interview.JobTitle,
                "Halo,\n\n" +
                "Kami ingin mengundang Anda untuk mengikuti interview untuk posisi " +
                interview.JobTitle + " di " + interview.Company + ".\n\n" +
                "Pilih Accept untuk melanjutkan ke tahap interview, atau Decline untuk menolak undangan.");

            RefreshInbox();
            SetDecisionButtons(true);
            panelNavigator.ShowOnly(mailsPanel);
        }

        void OpenOffer(Offer offer)
        {
            if (offer == null)
            {
                Debug.LogError("OfferReceived mengirim data offer kosong.", this);
                return;
            }

            _pendingInterview = null;
            _pendingOffer = offer;
            _decisionMade = false;
            senderNameText.text = "Tim Rekrutmen - " + offer.Company;
            receiverNameText.text = "Kepada: Kandidat";
            SetEmailText(
                "Penawaran Kerja: " + offer.JobTitle,
                "Halo,\n\n" +
                "Kami menawarkan posisi " + offer.JobTitle + " di " + offer.Company + ".\n" +
                "Tingkat gaji: " + offer.SalaryTier + ".\n\n" +
                "Pilih Accept untuk mengakhiri permainan dengan menerima pekerjaan, atau " +
                "Decline untuk melanjutkan permainan.");

            RefreshInbox();
            SetDecisionButtons(true);
            panelNavigator.ShowOnly(mailsPanel);
        }

        void RefreshInbox()
        {
            if (inboxJobTitleTexts == null)
                return;

            List<ApplicationRecord> invitations = new List<ApplicationRecord>();
            if (_session != null)
            {
                foreach (ApplicationRecord application in _session.Applications)
                    if (application.Stage == ApplicationStage.InterviewReady ||
                        application.Stage == ApplicationStage.OfferReady)
                        invitations.Add(application);
            }

            if (_pendingOffer != null)
            {
                for (int i = 0; i < invitations.Count; i++)
                {
                    ApplicationRecord application = invitations[i];
                    if (application.Id == _pendingOffer.ApplicationId)
                    {
                        invitations.RemoveAt(i);
                        invitations.Insert(0, application);
                        break;
                    }
                }
            }
            else if (_pendingInterview != null)
            {
                for (int i = 0; i < invitations.Count; i++)
                {
                    ApplicationRecord application = invitations[i];
                    if (application.JobTitle == _pendingInterview.JobTitle &&
                        application.Company == _pendingInterview.Company)
                    {
                        invitations.RemoveAt(i);
                        invitations.Insert(0, application);
                        break;
                    }
                }
            }

            for (int i = 0; i < inboxJobTitleTexts.Length; i++)
            {
                TMP_Text label = inboxJobTitleTexts[i];
                if (label == null)
                    continue;

                if (i >= invitations.Count)
                {
                    label.text = string.Empty;
                    continue;
                }

                ApplicationRecord application = invitations[i];
                string stage = application.Stage == ApplicationStage.OfferReady
                    ? "Offer"
                    : "Interview";
                label.text = stage + ": " + application.Company + " - " + application.JobTitle;
            }
        }

        void OnAcceptClicked()
        {
            if (!CanDecide())
                return;

            SetDecisionButtons(false);
            if (_pendingInterview != null)
            {
                _decisionMade = true;
                panelNavigator.ShowOnly(interviewPanel);
                return;
            }

            _decisionMade = true;
            ActionResult result = _session.AcceptOffer();
            if (!result.Ok)
            {
                _decisionMade = false;
                Debug.LogWarning(
                    "Menerima offer gagal: " + result.Error + " - " + result.Message,
                    this);
                SetDecisionButtons(true);
            }
        }

        void OnDeclineClicked()
        {
            if (!CanDecide())
                return;

            SetDecisionButtons(false);
            _decisionMade = true;
            ActionResult result = _pendingInterview != null
                ? _session.DeclineInterview()
                : _session.DeclineOffer();
            if (!result.Ok)
            {
                _decisionMade = false;
                Debug.LogWarning(
                    "Menolak undangan gagal: " + result.Error + " - " + result.Message,
                    this);
                SetDecisionButtons(true);
                return;
            }
        }

        void SetEmailText(string subject, string message)
        {
            if (object.ReferenceEquals(subjectText, messageContentText))
            {
                messageContentText.text = subject + "\n\n" + message;
                return;
            }

            subjectText.text = subject;
            messageContentText.text = message;
        }

        bool CanDecide()
        {
            if (_session != null && !_decisionMade)
            {
                if (_pendingInterview != null &&
                    _session.Phase == GamePhase.AwaitingInterview &&
                    object.ReferenceEquals(_session.ActiveInterview, _pendingInterview))
                    return true;

                if (_pendingOffer != null &&
                    _session.Phase == GamePhase.AwaitingOfferDecision &&
                    object.ReferenceEquals(_session.PendingOffer, _pendingOffer))
                    return true;
            }

            Debug.LogWarning("Undangan atau offer tidak lagi aktif.", this);
            return false;
        }

        void SetDecisionButtons(bool canDecide)
        {
            if (acceptButton != null)
                acceptButton.interactable = canDecide;
            if (declineButton != null)
                declineButton.interactable = canDecide;
        }
    }
}
