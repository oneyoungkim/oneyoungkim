// 행인1의 메인이벤트 — 임시 폴더 점검
// 대표 PC 사용자 환경변수 TMPDIR 이 ESTsoft 의 C:\Users\Public\Documents\ESTsoft\CreatorTemp 를 가리키는데,
// 그 폴더에는 새 파일을 만들 수 없다(FileNotFoundException). Unity 의 Mono 는 TMPDIR 을 TEMP 보다 먼저 보고,
// 처음 읽은 값을 프로세스가 끝날 때까지 캐시하므로 에디터 안에서 바꿔도 소용없다.
// → Unity 를 띄우기 "전에" TMPDIR 을 바꿔야 한다(README '주의' 참고). 이 스크립트는 문제를 찾아 알려 주기만 한다.
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Haengin.EditorTools
{
    [InitializeOnLoad]
    public static class TempPathGuard
    {
        public const string Guide =
            "Unity 를 띄우기 전에 TMPDIR 환경변수를 %TEMP% 로 바꾸세요. 예) PowerShell: $env:TMPDIR=$env:TEMP 후 Unity.exe 실행. " +
            "Hub 로 열 때도 같은 문제가 생기므로 사용자 환경변수 TMPDIR 을 지우거나 바꾸는 것이 근본 해결(대표 결정).";

        static TempPathGuard() => Ensure(false);

        /// Path.GetTempFileName() 이 되는지 확인. 안 되면 경고를 남기고 false.
        public static bool Ensure(bool verbose)
        {
            try
            {
                string probe = Path.GetTempFileName();
                File.Delete(probe);
                if (verbose) Debug.Log($"[TempPathGuard] 임시 폴더 정상: {Path.GetTempPath()}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TempPathGuard] 임시 파일을 만들 수 없음({Path.GetTempPath()}, TMPDIR={Environment.GetEnvironmentVariable("TMPDIR")}): " +
                                 $"{e.GetType().Name}. Burst AOT 빌드 단계 등이 실패합니다. {Guide}");
                return false;
            }
        }
    }
}
