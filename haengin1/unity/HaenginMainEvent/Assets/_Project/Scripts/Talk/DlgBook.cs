// 행인1의 메인이벤트 — 대사 묶음 에셋(Settings/Dialogue.asset — StorySetup 이 Data/Story/dlg/ep*.tsv 에서 만든다)
using System.Collections.Generic;
using UnityEngine;

namespace Haengin
{
    [CreateAssetMenu(menuName = "Haengin/DlgBook")]
    public sealed class DlgBook : ScriptableObject
    {
        public DlgLine[] Lines = new DlgLine[0];

        /// 장면 하나의 줄(TSV 순서)
        public List<DlgLine> Get(string scene)
        {
            var r = new List<DlgLine>();
            if (string.IsNullOrEmpty(scene) || Lines == null) return r;
            foreach (var l in Lines) if (l.Scene == scene) r.Add(l);
            return r;
        }

        public bool Has(string scene)
        {
            if (string.IsNullOrEmpty(scene) || Lines == null) return false;
            foreach (var l in Lines) if (l.Scene == scene) return true;
            return false;
        }
    }
}
