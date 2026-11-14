using System.Collections.Generic;
using UnityEngine;

namespace JobGame.Examples
{
    // PreviewMatch aman dipanggil setiap kali pemain mengubah isi CV.
    public sealed class ExampleCvPreview : MonoBehaviour
    {
        IGameSession _session;
        JobPosting _posting;
        readonly CvDraft _draft = new CvDraft();

        public void Bind(IGameSession session, JobPosting posting)
        {
            _session = session;
            _posting = posting;
            RefreshPreview();
        }

        public void SetSkills(IEnumerable<SkillTag> skills)
        {
            _draft.Skills = new List<SkillTag>(skills);
            RefreshPreview();
        }

        public void SetExperiences(IEnumerable<ExperienceCard> experiences)
        {
            _draft.Experiences = new List<ExperienceCard>(experiences);
            RefreshPreview();
        }

        void RefreshPreview()
        {
            if (_session == null || _posting == null)
                return;

            MatchResult result = _session.PreviewMatch(_posting, _draft);
            Debug.Log(string.Format(
                "Preview {0}: score {1}, reject {2}, experience {3}",
                _posting.Template.Title,
                result.Score,
                result.Reject,
                result.ExperienceCredibility), this);
        }
    }
}
