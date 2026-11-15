using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using JobGame;

namespace JobGame.UI
{
    public sealed class CvMakerCategoryPresenter : MonoBehaviour
    {
        const int VisibleItemCount = 6;

        [Header("Session")]
        [SerializeField] SessionHudPresenter sessionPresenter;

        [Header("Category Tabs")]
        [SerializeField] Button skillsTabButton;
        [SerializeField] Button experiencesTabButton;

        [Header("CV Submission")]
        [SerializeField] JobPostingListPresenter jobPostingPresenter;
        [SerializeField] Button applyButton;
        [SerializeField] TMP_Text applyStatusText;
        [SerializeField] PanelNavigator panelNavigator;

        [Header("Inventory Cards (ordered by slot)")]
        [SerializeField] CvMakerDragSource[] inventoryCards = new CvMakerDragSource[VisibleItemCount];

        [Header("CV Drop Areas")]
        [SerializeField] CvMakerDropZone skillsDropZone;
        [SerializeField] CvMakerDropZone experiencesDropZone;

        [Header("Drag Preview")]
        [SerializeField] Canvas dragCanvas;

        readonly GameObject[] _itemBoxes = new GameObject[VisibleItemCount];
        readonly TMP_Text[] _itemLabels = new TMP_Text[VisibleItemCount];
        readonly SkillTag[] _displayedSkills = new SkillTag[VisibleItemCount];
        readonly bool[] _hasDisplayedSkills = new bool[VisibleItemCount];
        readonly ExperienceCard[] _displayedExperiences = new ExperienceCard[VisibleItemCount];
        readonly List<Transform> _skillSelectionSlots = new List<Transform>();
        readonly List<TMP_Text> _skillSelectionLabels = new List<TMP_Text>();
        readonly List<string> _skillSelectionPlaceholders = new List<string>();
        readonly List<Transform> _experienceSelectionSlots = new List<Transform>();
        readonly List<TMP_Text> _experienceSelectionLabels = new List<TMP_Text>();
        readonly List<string> _experienceSelectionPlaceholders = new List<string>();

        IGameSession _session;
        IGameSession _lastBoundSession;
        CvDraft _draft = new CvDraft();
        Category _activeCategory = Category.Skills;
        RectTransform _canvasRect;
        GameObject _dragGhost;
        RectTransform _dragGhostRect;
        bool _isConfigured;
        bool _hasDraggedCard;
        bool _clearDragPayloadInLateUpdate;
        Category _draggedCategory;
        SkillTag _draggedSkill;
        ExperienceCard _draggedExperience;
        int _configuredSkillSlotCount;
        int _configuredExperienceSlotCount;
        int _skillSelectionCapacity;
        int _experienceSelectionCapacity;

        enum Category
        {
            Skills,
            Experiences
        }

        public CvDraft Draft { get { return _draft; } }

        void Awake()
        {
            if (jobPostingPresenter == null)
                Debug.LogError("Assign JobPostingListPresenter pada Inspector.", this);
            if (applyButton == null)
                Debug.LogError("Assign tombol Apply Button dari CV Maker pada Inspector.", this);
            if (panelNavigator == null)
                Debug.LogError("Assign PanelNavigator pada Inspector untuk kembali ke Desktop.", this);

            if (!ValidateSetup())
                return;

            _canvasRect = dragCanvas.transform as RectTransform;
            if (_canvasRect == null)
            {
                Debug.LogError("Drag Canvas harus memiliki RectTransform.", dragCanvas);
                return;
            }

            if (!ResolveInventoryCards())
                return;

            skillsDropZone.Bind(this, false);
            experiencesDropZone.Bind(this, true);

            if (!ResolveSelectionSlots(
                    skillsDropZone,
                    _skillSelectionSlots,
                    _skillSelectionLabels,
                    _skillSelectionPlaceholders,
                    "[SKILL ") ||
                !ResolveSelectionSlots(
                    experiencesDropZone,
                    _experienceSelectionSlots,
                    _experienceSelectionLabels,
                    _experienceSelectionPlaceholders,
                    "[EXPERIENCES "))
                return;

            _skillSelectionCapacity = _skillSelectionSlots.Count;
            _experienceSelectionCapacity = _experienceSelectionSlots.Count;
            MakeDropZoneRaycastable(skillsDropZone);
            MakeDropZoneRaycastable(experiencesDropZone);

            _isConfigured = true;
            ClearLabels();
            RefreshSelection();
        }

        void OnEnable()
        {
            if (!_isConfigured)
                return;

            skillsTabButton.onClick.AddListener(ShowSkills);
            experiencesTabButton.onClick.AddListener(ShowExperiences);
            if (applyButton != null)
                applyButton.onClick.AddListener(OnApplyClicked);

            sessionPresenter.SessionBound += Bind;
            Bind(sessionPresenter.Session);
        }

        void OnDisable()
        {
            if (skillsTabButton != null)
                skillsTabButton.onClick.RemoveListener(ShowSkills);
            if (experiencesTabButton != null)
                experiencesTabButton.onClick.RemoveListener(ShowExperiences);
            if (applyButton != null)
                applyButton.onClick.RemoveListener(OnApplyClicked);

            if (sessionPresenter != null)
                sessionPresenter.SessionBound -= Bind;

            if (_session != null)
                _session.HoldingsChanged -= OnHoldingsChanged;
            _session = null;

            ClearLabels();
            ClearDragState();
        }

        void LateUpdate()
        {
            if (!_clearDragPayloadInLateUpdate)
                return;

            _clearDragPayloadInLateUpdate = false;
            ClearDragPayload();
        }

        bool ValidateSetup()
        {
            bool valid = true;

            if (sessionPresenter == null)
            {
                Debug.LogError("Assign SessionHudPresenter pada Inspector.", this);
                valid = false;
            }
            if (skillsTabButton == null || experiencesTabButton == null)
            {
                Debug.LogError("Assign tombol Skills Tab dan Experiences Tab pada Inspector.", this);
                valid = false;
            }
            if (inventoryCards == null || inventoryCards.Length != VisibleItemCount)
            {
                Debug.LogError("Inventory Cards harus berisi tepat 6 komponen CvMakerDragSource.", this);
                valid = false;
            }
            else
            {
                for (int i = 0; i < inventoryCards.Length; i++)
                {
                    if (inventoryCards[i] == null)
                    {
                        Debug.LogError("Assign CvMakerDragSource untuk inventory slot ke-" + (i + 1) + ".", this);
                        valid = false;
                    }
                }
            }
            if (skillsDropZone == null || experiencesDropZone == null)
            {
                Debug.LogError("Assign CvMakerDropZone untuk area Skills dan Experiences.", this);
                valid = false;
            }
            else if (skillsDropZone == experiencesDropZone)
            {
                Debug.LogError("Area drop Skills dan Experiences harus berbeda.", this);
                valid = false;
            }
            if (dragCanvas == null)
            {
                Debug.LogError("Assign Canvas untuk Drag Preview pada Inspector.", this);
                valid = false;
            }

            return valid;
        }

        bool ResolveInventoryCards()
        {
            bool valid = true;
            for (int i = 0; i < inventoryCards.Length; i++)
            {
                CvMakerDragSource source = inventoryCards[i];
                _itemBoxes[i] = source.gameObject;
                _itemLabels[i] = source.GetComponentInChildren<TMP_Text>(true);

                if (_itemLabels[i] == null)
                {
                    Debug.LogError(
                        "Inventory card ke-" + (i + 1) + " tidak memiliki TextMeshPro child.",
                        source);
                    valid = false;
                    continue;
                }

                _itemLabels[i].raycastTarget = true;
                Graphic graphic = source.GetComponent<Graphic>();
                if (graphic != null)
                    graphic.raycastTarget = true;

                source.Bind(this, i);
            }

            return valid;
        }

        bool ResolveSelectionSlots(
            CvMakerDropZone zone,
            List<Transform> slots,
            List<TMP_Text> labels,
            List<string> placeholders,
            string placeholderPrefix)
        {
            Transform area = zone.transform;
            bool valid = true;

            for (int i = 0; i < area.childCount; i++)
            {
                Transform slot = area.GetChild(i);
                TMP_Text label = slot.GetComponentInChildren<TMP_Text>(true);
                if (label == null)
                {
                    Debug.LogError("Slot " + slot.name + " tidak memiliki TextMeshPro child.", slot);
                    valid = false;
                    continue;
                }

                slots.Add(slot);
                labels.Add(label);
                placeholders.Add(string.IsNullOrEmpty(label.text)
                    ? placeholderPrefix + slots.Count + "]"
                    : label.text);
            }

            if (slots.Count == 0)
            {
                Debug.LogError("Area drop " + area.name + " harus memiliki minimal satu slot.", area);
                valid = false;
            }

            return valid;
        }

        void Bind(IGameSession session)
        {
            if (_session != null)
                _session.HoldingsChanged -= OnHoldingsChanged;

            if (session != null && !ReferenceEquals(session, _lastBoundSession))
            {
                _draft = new CvDraft();
                _lastBoundSession = session;
            }

            _session = session;
            if (_session != null && isActiveAndEnabled)
                _session.HoldingsChanged += OnHoldingsChanged;

            if (_session != null)
                EnsureSelectionCapacity();

            Refresh();
        }

        void ShowSkills()
        {
            _activeCategory = Category.Skills;
            Refresh();
        }

        void ShowExperiences()
        {
            _activeCategory = Category.Experiences;
            Refresh();
        }

        void OnApplyClicked()
        {
            if (_session == null)
            {
                ReportApplyResult("Sesi belum dimulai.", false);
                return;
            }

            if (jobPostingPresenter == null)
            {
                ReportApplyResult("JobPostingListPresenter belum di-assign.", false);
                return;
            }

            if (!ReferenceEquals(jobPostingPresenter.BoundSession, _session))
            {
                ReportApplyResult("Job yang dipilih bukan dari sesi aktif. Pilih ulang job dari Job Finder.", false);
                return;
            }

            JobPosting posting = jobPostingPresenter.SelectedPosting;
            if (posting == null)
            {
                ReportApplyResult("Pilih job dari Job Finder sebelum mengirim CV.", false);
                return;
            }

            if (string.IsNullOrEmpty(posting.Id) || posting.Template == null)
            {
                ReportApplyResult("Posting yang dipilih tidak memiliki ID atau JobTemplate yang valid.", false);
                return;
            }

            CvDraft submittedDraft = new CvDraft
            {
                Skills = new List<SkillTag>(_draft.Skills),
                Experiences = new List<ExperienceCard>(_draft.Experiences)
            };
            MatchResult match = _session.PreviewMatch(posting, submittedDraft);
            ActionResult result = _session.TryApply(posting.Id, submittedDraft);

            string message = result.Ok
                ? "Lamar " + posting.Id + " berhasil."
                : "Lamar " + posting.Id + " gagal: " + result.Error + " - " + result.Message;

            if (result.Ok && match != null)
            {
                message += "\nPreview CV: score " + match.Score + ".";
                if (match.IsRejected)
                {
                    message += "\nPeringatan gate: " + match.Reject +
                               ". Lamaran tetap dikirim untuk diproses.";
                    if (match.MissingMustHave != null && match.MissingMustHave.Count > 0)
                    {
                        List<string> missingSkills = new List<string>();
                        for (int i = 0; i < match.MissingMustHave.Count; i++)
                            missingSkills.Add(match.MissingMustHave[i].DisplayName());
                        message += "\nSkill wajib yang kurang: " +
                                   string.Join(", ", missingSkills.ToArray()) + ".";
                    }
                }
            }

            ReportApplyResult(message, result.Ok);
            if (result.Ok)
            {
                if (panelNavigator != null)
                    panelNavigator.ShowDesktop();
                else
                    Debug.LogError("PanelNavigator belum di-assign; panel tetap terbuka.", this);
            }
        }

        void ReportApplyResult(string message, bool success)
        {
            if (applyStatusText != null)
                applyStatusText.text = message;

            if (success)
                Debug.Log(message, this);
            else
                Debug.LogWarning(message, this);
        }

        void OnHoldingsChanged()
        {
            Refresh();
        }

        void Refresh()
        {
            ClearLabels();
            Array.Clear(_hasDisplayedSkills, 0, _hasDisplayedSkills.Length);
            Array.Clear(_displayedExperiences, 0, _displayedExperiences.Length);

            if (_session == null)
            {
                RefreshSelection();
                return;
            }

            if (_activeCategory == Category.Skills)
            {
                IReadOnlyList<SkillTag> skills = _session.OwnedSkills;
                if (skills == null)
                {
                    Debug.LogError("IGameSession.OwnedSkills tidak boleh null.", this);
                    return;
                }

                int count = Mathf.Min(VisibleItemCount, skills.Count);
                for (int i = 0; i < count; i++)
                {
                    _displayedSkills[i] = skills[i];
                    _hasDisplayedSkills[i] = true;
                    SetLabel(i, skills[i].DisplayName());
                }
            }
            else
            {
                IReadOnlyList<ExperienceCard> experiences = _session.OwnedExperiences;
                if (experiences == null)
                {
                    Debug.LogError("IGameSession.OwnedExperiences tidak boleh null.", this);
                    return;
                }

                int count = Mathf.Min(VisibleItemCount, experiences.Count);
                for (int i = 0; i < count; i++)
                {
                    ExperienceCard card = experiences[i];
                    if (card != null)
                    {
                        _displayedExperiences[i] = card;
                        SetLabel(i, card.DisplayName);
                    }
                }
            }

            RefreshSelection();
        }

        void EnsureSelectionCapacity()
        {
            GameConfig config = _session.Config;
            if (config == null)
            {
                Debug.LogError("IGameSession.Config tidak boleh null.", this);
                return;
            }

            _skillSelectionCapacity = Mathf.Max(0, config.CvSkillSlots);
            _experienceSelectionCapacity = Mathf.Max(0, config.CvExperienceSlots);
            EnsureSlotCapacity(
                _skillSelectionSlots,
                _skillSelectionLabels,
                _skillSelectionPlaceholders,
                Mathf.Max(0, config.CvSkillSlots),
                Category.Skills,
                ref _configuredSkillSlotCount);
            EnsureSlotCapacity(
                _experienceSelectionSlots,
                _experienceSelectionLabels,
                _experienceSelectionPlaceholders,
                Mathf.Max(0, config.CvExperienceSlots),
                Category.Experiences,
                ref _configuredExperienceSlotCount);
        }

        void EnsureSlotCapacity(
            List<Transform> slots,
            List<TMP_Text> labels,
            List<string> placeholders,
            int capacity,
            Category category,
            ref int configuredSlotCount)
        {
            while (slots.Count > 0 && slots.Count < capacity)
            {
                Transform template = slots[0];
                GameObject clone = Instantiate(template.gameObject, template.parent, false);
                clone.name = category == Category.Skills
                    ? "Skill Selection Slot " + (slots.Count + 1)
                    : "Experience Selection Slot " + (slots.Count + 1);
                clone.SetActive(false);

                Button clonedButton = clone.GetComponent<Button>();
                if (clonedButton != null)
                    clonedButton.onClick.RemoveAllListeners();

                TMP_Text label = clone.GetComponentInChildren<TMP_Text>(true);
                if (label == null)
                {
                    Debug.LogError("Slot hasil duplikasi tidak memiliki TextMeshPro child.", clone);
                    Destroy(clone);
                    return;
                }

                string placeholder = category == Category.Skills
                    ? "[SKILL " + (slots.Count + 1) + "]"
                    : "[EXPERIENCES " + (slots.Count + 1) + "]";
                label.text = placeholder;
                slots.Add(clone.transform);
                labels.Add(label);
                placeholders.Add(placeholder);
            }

            for (int i = configuredSlotCount; i < slots.Count; i++)
            {
                Button button = slots[i].GetComponent<Button>();
                if (button == null)
                    button = slots[i].gameObject.AddComponent<Button>();
                button.interactable = true;

                int slotIndex = i;
                if (category == Category.Skills)
                    button.onClick.AddListener(() => RemoveSkillAt(slotIndex));
                else
                    button.onClick.AddListener(() => RemoveExperienceAt(slotIndex));
            }

            configuredSlotCount = slots.Count;
        }

        static void MakeDropZoneRaycastable(CvMakerDropZone zone)
        {
            Graphic graphic = zone.GetComponent<Graphic>();
            if (graphic == null)
            {
                Image image = zone.gameObject.AddComponent<Image>();
                image.color = new Color(1f, 1f, 1f, 0f);
                graphic = image;
            }

            graphic.raycastTarget = true;
        }

        void RefreshSelection()
        {
            RefreshSkillSelection();
            RefreshExperienceSelection();
        }

        void RefreshSkillSelection()
        {
            for (int i = 0; i < _skillSelectionSlots.Count; i++)
            {
                bool hasCard = i < _draft.Skills.Count;
                _skillSelectionLabels[i].text = hasCard
                    ? _draft.Skills[i].DisplayName()
                    : _skillSelectionPlaceholders[i];
                _skillSelectionSlots[i].gameObject.SetActive(i < _skillSelectionCapacity);
            }
        }

        void RefreshExperienceSelection()
        {
            for (int i = 0; i < _experienceSelectionSlots.Count; i++)
            {
                bool hasCard = i < _draft.Experiences.Count;
                _experienceSelectionLabels[i].text = hasCard
                    ? _draft.Experiences[i].DisplayName
                    : _experienceSelectionPlaceholders[i];
                _experienceSelectionSlots[i].gameObject.SetActive(i < _experienceSelectionCapacity);
            }
        }

        internal void BeginDrag(int index, PointerEventData eventData)
        {
            ClearDragState();
            if (index < 0 || index >= _itemBoxes.Length || _session == null)
                return;

            if (_activeCategory == Category.Skills)
            {
                if (!_hasDisplayedSkills[index])
                    return;

                _draggedCategory = Category.Skills;
                _draggedSkill = _displayedSkills[index];
            }
            else
            {
                ExperienceCard card = _displayedExperiences[index];
                if (card == null)
                    return;

                _draggedCategory = Category.Experiences;
                _draggedExperience = card;
            }

            _hasDraggedCard = true;
            CreateDragGhost(_itemBoxes[index], eventData);
        }

        internal void DragCard(PointerEventData eventData)
        {
            if (_dragGhostRect != null)
                MoveDragGhost(eventData);
        }

        internal void EndDrag()
        {
            if (_dragGhost != null)
                Destroy(_dragGhost);

            _dragGhost = null;
            _dragGhostRect = null;
            _clearDragPayloadInLateUpdate = _hasDraggedCard;
        }

        internal void DropOnSkills()
        {
            if (_hasDraggedCard)
                AddDraggedSkill();
        }

        internal void DropOnExperiences()
        {
            if (_hasDraggedCard)
                AddDraggedExperience();
        }

        void AddDraggedSkill()
        {
            if (_draggedCategory != Category.Skills)
            {
                Debug.LogWarning("Experience hanya dapat dijatuhkan pada area Experiences.", this);
                return;
            }

            if (_session == null)
                return;

            GameConfig config = _session.Config;
            if (config == null)
            {
                Debug.LogError("IGameSession.Config tidak boleh null.", this);
                return;
            }

            if (_draft.Skills.Contains(_draggedSkill))
            {
                Debug.LogWarning("Skill tersebut sudah ada di CV.", this);
                return;
            }

            int capacity = Mathf.Max(0, config.CvSkillSlots);
            if (_draft.Skills.Count >= capacity)
            {
                Debug.LogWarning("CV sudah mencapai batas " + capacity + " skill.", this);
                return;
            }

            if (_draft.Skills.Count >= _skillSelectionSlots.Count)
            {
                Debug.LogError("Slot tampilan Skills tidak cukup untuk kapasitas CV.", this);
                return;
            }

            _draft.Skills.Add(_draggedSkill);
            RefreshSelection();
        }

        void AddDraggedExperience()
        {
            if (_draggedCategory != Category.Experiences)
            {
                Debug.LogWarning("Skill hanya dapat dijatuhkan pada area Skills.", this);
                return;
            }

            if (_session == null || _draggedExperience == null)
                return;

            GameConfig config = _session.Config;
            if (config == null)
            {
                Debug.LogError("IGameSession.Config tidak boleh null.", this);
                return;
            }

            for (int i = 0; i < _draft.Experiences.Count; i++)
            {
                ExperienceCard existing = _draft.Experiences[i];
                bool sameId = existing != null &&
                              !string.IsNullOrEmpty(existing.Id) &&
                              string.Equals(existing.Id, _draggedExperience.Id, StringComparison.Ordinal);
                if (ReferenceEquals(existing, _draggedExperience) || sameId)
                {
                    Debug.LogWarning("Experience tersebut sudah ada di CV.", this);
                    return;
                }
            }

            int capacity = Mathf.Max(0, config.CvExperienceSlots);
            if (_draft.Experiences.Count >= capacity)
            {
                Debug.LogWarning("CV sudah mencapai batas " + capacity + " experience.", this);
                return;
            }

            if (_draft.Experiences.Count >= _experienceSelectionSlots.Count)
            {
                Debug.LogError("Slot tampilan Experiences tidak cukup untuk kapasitas CV.", this);
                return;
            }

            _draft.Experiences.Add(_draggedExperience);
            RefreshSelection();
        }

        void RemoveSkillAt(int index)
        {
            if (index < 0 || index >= _draft.Skills.Count)
                return;

            _draft.Skills.RemoveAt(index);
            RefreshSelection();
        }

        void RemoveExperienceAt(int index)
        {
            if (index < 0 || index >= _draft.Experiences.Count)
                return;

            _draft.Experiences.RemoveAt(index);
            RefreshSelection();
        }

        void CreateDragGhost(GameObject source, PointerEventData eventData)
        {
            _dragGhost = Instantiate(source, dragCanvas.transform, false);
            _dragGhost.name = "CV Drag Preview";
            _dragGhostRect = _dragGhost.GetComponent<RectTransform>();
            RectTransform sourceRect = source.GetComponent<RectTransform>();
            if (_dragGhostRect != null && sourceRect != null)
            {
                _dragGhostRect.anchorMin = new Vector2(0.5f, 0.5f);
                _dragGhostRect.anchorMax = new Vector2(0.5f, 0.5f);
                _dragGhostRect.pivot = sourceRect.pivot;
                _dragGhostRect.sizeDelta = sourceRect.rect.size;
            }

            CanvasGroup canvasGroup = _dragGhost.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = _dragGhost.AddComponent<CanvasGroup>();
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            _dragGhost.transform.SetAsLastSibling();
            MoveDragGhost(eventData);
        }

        void MoveDragGhost(PointerEventData eventData)
        {
            Vector2 localPoint;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _canvasRect,
                    eventData.position,
                    eventData.pressEventCamera,
                    out localPoint))
                _dragGhostRect.anchoredPosition = localPoint;
        }

        void ClearDragState()
        {
            if (_dragGhost != null)
                Destroy(_dragGhost);

            _dragGhost = null;
            _dragGhostRect = null;
            _hasDraggedCard = false;
            _clearDragPayloadInLateUpdate = false;
            _draggedExperience = null;
        }

        void ClearDragPayload()
        {
            _hasDraggedCard = false;
            _draggedExperience = null;
        }

        void ClearLabels()
        {
            for (int i = 0; i < _itemLabels.Length; i++)
                SetLabel(i, string.Empty);
        }

        void SetLabel(int index, string value)
        {
            string text = value ?? string.Empty;
            if (_itemLabels[index] != null)
                _itemLabels[index].text = text;
            if (_itemBoxes[index] != null)
                _itemBoxes[index].SetActive(!string.IsNullOrEmpty(text));
        }
    }
}
