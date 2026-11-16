using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using JobGame;

namespace JobGame.UI
{
    public sealed class JobPostingListPresenter : MonoBehaviour
    {
        const int MaxVisiblePostings = 3;

        [SerializeField] SessionHudPresenter sessionPresenter;
        [SerializeField] GameObject[] jobBoxes = new GameObject[MaxVisiblePostings];
        [SerializeField] Transform availableJobsRoot;
        [SerializeField] GameObject jobDescriptionPanel;

        [Header("Job Description")]
        [SerializeField] TMP_Text jobTitleValue;
        [SerializeField] TMP_Text companyNameValue;
        [SerializeField] TMP_Text skillTagsValue;
        [SerializeField] TMP_Text mustToHaveValue;
        [SerializeField] TMP_Text niceToHaveValue;
        [SerializeField] TMP_Text minExperienceValue;
        [SerializeField] TMP_Text salaryTierValue;
        [SerializeField] TMP_Text interviewDifficultyValue;
        [SerializeField] TMP_Text finalLagWeeksValue;
        [SerializeField] TMP_Text twistValue;

        IGameSession _session;
        IGameSession _lastBoundSession;
        JobPosting _selectedPosting;
        PanelNavigator _panelNavigator;
        GameObject _cvMakerPanel;
        CvMakerCategoryPresenter _cvMakerPresenter;
        Button _applyButton;
        readonly TMP_Text[] _jobTitleLabels = new TMP_Text[MaxVisiblePostings];
        readonly Button[] _jobButtons = new Button[MaxVisiblePostings];
        readonly UnityAction[] _jobButtonActions = new UnityAction[MaxVisiblePostings];
        readonly JobPosting[] _visiblePostings = new JobPosting[MaxVisiblePostings];

        public JobPosting SelectedPosting
        {
            get
            {
                return _selectedPosting != null && !_selectedPosting.Applied
                    ? _selectedPosting
                    : null;
            }
        }

        public IGameSession BoundSession { get { return _session ?? _lastBoundSession; } }

        void Awake()
        {
            if (jobDescriptionPanel == null)
                Debug.LogError("Job Description Panel belum diisi.", this);
            else
            {
                jobDescriptionPanel.SetActive(false);
                _applyButton = FindApplyButton(jobDescriptionPanel);
                if (_applyButton == null)
                    Debug.LogError("Button bernama Apply Button tidak ditemukan di Job Description.", this);
            }

            _panelNavigator = FindSceneComponent<PanelNavigator>();
            if (_panelNavigator == null)
                Debug.LogError("PanelNavigator tidak ditemukan di scene.", this);

            Transform cvMakerTransform = FindSceneTransform("CV Maker");
            if (cvMakerTransform == null)
                Debug.LogError("Panel CV Maker tidak ditemukan di scene.", this);
            else
                _cvMakerPanel = cvMakerTransform.gameObject;
            _cvMakerPresenter = FindSceneComponent<CvMakerCategoryPresenter>();

            if (jobBoxes == null || jobBoxes.Length != MaxVisiblePostings)
            {
                Debug.LogError(
                    "JobPostingListPresenter membutuhkan array berisi tepat 3 kotak job.",
                    this);
                return;
            }

            ResolveMissingJobBoxes();

            for (int i = 0; i < jobBoxes.Length; i++)
            {
                if (jobBoxes[i] == null)
                {
                    Debug.LogError(
                        "Kotak job ke-" + (i + 1) +
                        " tidak ditemukan. Assign kotak tersebut atau Available Job pada Available Jobs Root.",
                        this);
                    continue;
                }

                _jobTitleLabels[i] = jobBoxes[i].GetComponentInChildren<TMP_Text>(true);
                if (_jobTitleLabels[i] == null)
                    Debug.LogError("UI Panel job ke-" + (i + 1) + " tidak memiliki TextMeshPro child.", jobBoxes[i]);

                _jobButtons[i] = jobBoxes[i].GetComponent<Button>();
                if (_jobButtons[i] == null)
                    _jobButtons[i] = jobBoxes[i].AddComponent<Button>();

                if (_jobButtons[i].targetGraphic == null)
                {
                    Graphic targetGraphic = jobBoxes[i].GetComponent<Graphic>();
                    if (targetGraphic == null)
                        targetGraphic = jobBoxes[i].GetComponentInChildren<Graphic>(true);

                    if (targetGraphic == null)
                    {
                        Debug.LogError(
                            "UI Panel job ke-" + (i + 1) + " tidak memiliki Graphic untuk menerima klik.",
                            jobBoxes[i]);
                        _jobButtons[i].interactable = false;
                    }
                    else
                    {
                        targetGraphic.raycastTarget = true;
                        _jobButtons[i].targetGraphic = targetGraphic;
                    }
                }

                int slot = i;
                _jobButtonActions[i] = delegate { OnJobSelected(slot); };
            }

            ValidateDescriptionFields();
        }

        void ResolveMissingJobBoxes()
        {
            bool hasMissingBox = false;
            for (int i = 0; i < jobBoxes.Length; i++)
                hasMissingBox |= jobBoxes[i] == null;

            if (!hasMissingBox)
                return;

            if (availableJobsRoot == null)
                availableJobsRoot = FindSceneTransform("Available Job");

            if (availableJobsRoot == null)
            {
                Debug.LogError(
                    "Available Job tidak ditemukan. Assign Available Job pada field Available Jobs Root.",
                    this);
                return;
            }

            int nextSlot = 0;
            foreach (Transform child in availableJobsRoot)
            {
                if (child.GetComponent<Image>() == null ||
                    child.GetComponentInChildren<TMP_Text>(true) == null)
                    continue;

                bool alreadyAssigned = false;
                for (int i = 0; i < jobBoxes.Length; i++)
                    if (jobBoxes[i] == child.gameObject)
                        alreadyAssigned = true;

                if (alreadyAssigned)
                    continue;

                while (nextSlot < jobBoxes.Length && jobBoxes[nextSlot] != null)
                    nextSlot++;

                if (nextSlot >= jobBoxes.Length)
                    break;

                jobBoxes[nextSlot] = child.gameObject;
                nextSlot++;
            }
        }

        static Transform FindSceneTransform(string objectName)
        {
            Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate.name == objectName && candidate.gameObject.scene.IsValid())
                    return candidate;
            }

            return null;
        }

        static T FindSceneComponent<T>() where T : Component
        {
            T[] components = Resources.FindObjectsOfTypeAll<T>();
            for (int i = 0; i < components.Length; i++)
                if (components[i].gameObject.scene.IsValid())
                    return components[i];

            return null;
        }

        static Button FindApplyButton(GameObject panel)
        {
            Button[] buttons = panel.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
                if (buttons[i].gameObject.name == "Apply Button")
                    return buttons[i];

            return null;
        }

        void OnEnable()
        {
            if (_applyButton != null)
                _applyButton.onClick.AddListener(OnApplyClicked);

            for (int i = 0; i < _jobButtons.Length; i++)
                if (_jobButtons[i] != null)
                    _jobButtons[i].onClick.AddListener(_jobButtonActions[i]);

            if (sessionPresenter == null)
            {
                Debug.LogError("JobPostingListPresenter membutuhkan SessionHudPresenter.", this);
                Refresh();
                return;
            }

            sessionPresenter.SessionBound += Bind;
            Bind(sessionPresenter.Session);
        }

        void OnDisable()
        {
            if (_applyButton != null)
                _applyButton.onClick.RemoveListener(OnApplyClicked);

            for (int i = 0; i < _jobButtons.Length; i++)
                if (_jobButtons[i] != null && _jobButtonActions[i] != null)
                    _jobButtons[i].onClick.RemoveListener(_jobButtonActions[i]);

            if (sessionPresenter != null)
                sessionPresenter.SessionBound -= Bind;

            if (jobDescriptionPanel != null)
                jobDescriptionPanel.SetActive(false);

            Bind(null);
        }

        void Bind(IGameSession session)
        {
            Unsubscribe(_session);
            if (session != null && !ReferenceEquals(session, _lastBoundSession))
            {
                _selectedPosting = null;
                _lastBoundSession = session;
            }

            _session = session;
            if (_session == null)
                _selectedPosting = null;
            Subscribe(_session);
            Refresh();
        }

        void Subscribe(IGameSession session)
        {
            if (session == null || !isActiveAndEnabled)
                return;

            session.PostingsChanged += Refresh;
            session.PhaseChanged += OnPhaseChanged;
        }

        void Unsubscribe(IGameSession session)
        {
            if (session == null)
                return;

            session.PostingsChanged -= Refresh;
            session.PhaseChanged -= OnPhaseChanged;
        }

        void OnPhaseChanged(GamePhase phase)
        {
            if (phase != GamePhase.Planning)
            {
                _selectedPosting = null;
                if (jobDescriptionPanel != null)
                    jobDescriptionPanel.SetActive(false);
            }

            Refresh();
        }

        void Refresh()
        {
            for (int i = 0; i < _jobTitleLabels.Length; i++)
            {
                _visiblePostings[i] = null;
                if (_jobTitleLabels[i] != null)
                    _jobTitleLabels[i].text = string.Empty;
            }

            if (_session == null)
            {
                if (jobDescriptionPanel != null)
                    jobDescriptionPanel.SetActive(false);
                return;
            }

            if (_session.Phase != GamePhase.Planning)
            {
                _selectedPosting = null;
                return;
            }

            IReadOnlyList<JobPosting> postings = _session.Postings;
            if (postings == null)
            {
                Debug.LogError("IGameSession.Postings tidak boleh null.", this);
                return;
            }

            if (_selectedPosting != null)
            {
                bool selectedPostingAvailable = false;
                for (int i = 0; i < postings.Count; i++)
                {
                    JobPosting posting = postings[i];
                    if (posting != null &&
                        string.Equals(posting.Id, _selectedPosting.Id, System.StringComparison.Ordinal) &&
                        !posting.Applied)
                    {
                        selectedPostingAvailable = true;
                        _selectedPosting = posting;
                        break;
                    }
                }

                if (!selectedPostingAvailable)
                {
                    _selectedPosting = null;
                    if (jobDescriptionPanel != null)
                        jobDescriptionPanel.SetActive(false);
                }
            }

            int labelIndex = 0;
            int labelCount = MaxVisiblePostings;
            for (int i = 0; i < postings.Count && labelIndex < labelCount; i++)
            {
                JobPosting posting = postings[i];
                if (posting == null || posting.Applied || posting.Template == null)
                    continue;

                TMP_Text label = _jobTitleLabels[labelIndex];
                if (label != null)
                    label.text = posting.Template.Title ?? string.Empty;
                _visiblePostings[labelIndex] = posting;
                labelIndex++;
            }
        }

        void OnJobSelected(int slot)
        {
            if (_session == null || _session.Phase != GamePhase.Planning)
                return;

            if (slot < 0 || slot >= _visiblePostings.Length || _visiblePostings[slot] == null)
                return;

            if (jobDescriptionPanel == null)
            {
                Debug.LogError("Job Description Panel belum diisi.", this);
                return;
            }

            JobTemplate job = _visiblePostings[slot].Template;
            if (job == null)
            {
                Debug.LogError("Posting yang dipilih tidak memiliki JobTemplate.", this);
                return;
            }

            _selectedPosting = _visiblePostings[slot];
            SetLabeledText(jobTitleValue, "Job Title", job.Title);
            SetLabeledText(companyNameValue, "Company Name", job.Company);
            SetLabeledText(
                skillTagsValue,
                "Skill Tags",
                FormatCombinedSkills(job.MustHave, job.NiceToHave));
            SetLabeledText(mustToHaveValue, "Must To Have", FormatSkills(job.MustHave));
            SetLabeledText(niceToHaveValue, "Nice To Have", FormatSkills(job.NiceToHave));
            SetLabeledText(minExperienceValue, "Min Experience", job.MinExperience.ToString());
            SetLabeledText(salaryTierValue, "Salary Tier", "Tier " + job.SalaryTier);
            SetLabeledText(
                interviewDifficultyValue,
                "Interview Difficulty",
                job.InterviewDifficulty + "/3");
            SetLabeledText(finalLagWeeksValue, "Final Lag Weeks", job.FinalLagWeeks + " minggu");
            SetLabeledText(
                twistValue,
                "Twist",
                string.IsNullOrWhiteSpace(job.Twist) ? "Tidak ada" : job.Twist);
            jobDescriptionPanel.SetActive(true);
        }

        void OnApplyClicked()
        {
            if (_panelNavigator == null || _cvMakerPanel == null)
            {
                Debug.LogError("PanelNavigator atau panel CV Maker belum tersedia.", this);
                return;
            }

            if (jobDescriptionPanel != null)
                jobDescriptionPanel.SetActive(false);
            if (_cvMakerPresenter != null)
                _cvMakerPresenter.ResetDraftForNewPosting();
            _panelNavigator.ShowOnly(_cvMakerPanel);
        }

        void ValidateDescriptionFields()
        {
            ValidateField(jobTitleValue, "Job Title Value");
            ValidateField(companyNameValue, "Company Name Value");
            ValidateField(skillTagsValue, "Skill Tags");
            ValidateField(mustToHaveValue, "Must To Have");
            ValidateField(niceToHaveValue, "Nice To Have");
            ValidateField(minExperienceValue, "Min Experience");
            ValidateField(salaryTierValue, "Salary Tier");
            ValidateField(interviewDifficultyValue, "Interview Difficulty");
            ValidateField(finalLagWeeksValue, "Final Lag Weeks");
            ValidateField(twistValue, "Twist");
        }

        void ValidateField(TMP_Text field, string fieldName)
        {
            if (field == null)
                Debug.LogError("Referensi TextMeshPro untuk " + fieldName + " belum diisi.", this);
        }

        static string FormatSkills(SkillTag[] skills)
        {
            List<string> names = new List<string>();
            AddSkillNames(names, skills);
            return names.Count == 0 ? "Tidak ada" : string.Join(", ", names.ToArray());
        }

        static string FormatCombinedSkills(SkillTag[] mustHave, SkillTag[] niceToHave)
        {
            List<string> names = new List<string>();
            AddSkillNames(names, mustHave);
            AddSkillNames(names, niceToHave);
            return names.Count == 0 ? "Tidak ada" : string.Join(", ", names.ToArray());
        }

        static void AddSkillNames(List<string> names, SkillTag[] skills)
        {
            if (skills == null)
                return;

            for (int i = 0; i < skills.Length; i++)
            {
                string name = skills[i].DisplayName();
                if (!names.Contains(name))
                    names.Add(name);
            }
        }

        static void SetLabeledText(TMP_Text label, string fieldName, string value)
        {
            if (label != null)
                label.text = fieldName + ": " + (value ?? string.Empty);
        }
    }
}
