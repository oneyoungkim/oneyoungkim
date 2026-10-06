// 행인1의 메인이벤트 — 컷신 목록 에셋(Settings/CutLib.asset — CutSetup 이 Data/Story/cuts/*.playable 을 모아 넣는다)
using System;
using UnityEngine;
using UnityEngine.Timeline;

namespace Haengin
{
    [CreateAssetMenu(menuName = "Haengin/CutLib")]
    public sealed class CutLib : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string Name;
            public TimelineAsset Timeline;
        }

        public Entry[] Cuts = new Entry[0];

        public TimelineAsset Get(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            var e = Array.Find(Cuts, x => x != null && x.Name == name);
            return e != null ? e.Timeline : null;
        }
    }
}
