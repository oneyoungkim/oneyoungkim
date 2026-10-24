// 행인1의 메인이벤트 — 드릴 14종 묶음(Settings/DrillBook.asset). ScriptableObject 는 파일 이름 = 클래스 이름이어야 에셋으로 저장된다(DrillTypes.cs 에서 떼어 냄)
using UnityEngine;

namespace Haengin
{
    public sealed class DrillBook : ScriptableObject
    {
        public DrillDef[] Drills = new DrillDef[0];
        public DrillDef Find(string id) { foreach (var d in Drills) if (d != null && d.Id == id) return d; return null; }
        public int PlayableCount { get { int n = 0; foreach (var d in Drills) if (d.Playable) n++; return n; } }
    }
}
