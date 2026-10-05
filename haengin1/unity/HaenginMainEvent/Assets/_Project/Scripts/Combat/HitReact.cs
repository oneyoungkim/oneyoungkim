// 행인1의 메인이벤트 — 피격 반응 비주얼 (docs/08_M2_전투_설계.md 5-1·5-6)
// 피격 젖힘(절차, 시안 FLINCH 표 → Humanoid 뼈의 캐릭터 기준 회전, 게임 시간 e^(−9t) 감소, 뼈마다 최대 60°) ·
// 피격자 번쩍(재질 이미시브 흰색 × 0.5, 0.1초 실제 시간) · 히트 셰이크(히트스톱 동안 가로 ±1.5cm·앞뒤 ±1.0cm) ·
// 다운 기울기(클립 연결 전 임시: 발을 축으로 뒤로 눕힘) · 임시 공격 자세(클립 연결 전: 팔·다리를 앞으로 — 9단계에서 클립으로 바뀜).
// Model = 모델 루트(시우 Siwoo_Model · 허수아비 · 캡슐). 실행 순서 100 의 LateUpdate = 애니메이터가 자세를 쓴 다음.
using UnityEngine;

namespace Haengin
{
    [DefaultExecutionOrder(100), DisallowMultipleComponent]
    public sealed class HitReact : MonoBehaviour
    {
        public Transform Model;
        public Fighter Owner;

        enum B { Hips, Spine, Chest, Neck, Head, LUpper, RUpper, LLower, RLower, LThigh, RThigh, LCalf, RCalf, Count }

        Animator anim;
        readonly Transform[] bones = new Transform[(int)B.Count];
        readonly Quaternion[] pre = new Quaternion[(int)B.Count], post = new Quaternion[(int)B.Count];
        readonly bool[] applied = new bool[(int)B.Count];
        readonly Vector3[] flinch = new Vector3[(int)B.Count];     // 도(°): x 앞(+)/뒤(−) 숙임, y 돌림, z 기울임 — 시안 FLINCH 와 같은 뜻
        Vector3 basePos;
        Quaternion baseRot;
        bool baseSet;
        float flashLeft;
        Renderer[] rends;
        MaterialPropertyBlock mpb;
        System.Random rng;
        float hipDrop;
        bool wasActive;

        /// 0 = 서 있음 … 1 = 누움(다운 임시 비주얼)
        public float DownAmount;
        /// 히트 셰이크 중인가(ImpactFx 가 마지막 피격자에게 켬)
        public bool Shaking;
        public Vector3 JitterNow { get; private set; }
        public float FlashLeft => flashLeft;
        public bool Humanoid => anim != null && anim.isHuman;
        /// 지금 머리 젖힘(°, 정보·테스트용)
        public Vector3 HeadFlinch => flinch[(int)B.Head];

        void Awake() => Init();

        /// 모델·주인 지정(조립 때). 뼈·렌더러를 다시 찾는다
        public void Setup(Transform model, Fighter owner)
        {
            Model = model;
            Owner = owner;
            Init();
        }

        void Init()
        {
            if (Owner == null) Owner = GetComponentInParent<Fighter>();
            if (Model == null) Model = transform;
            baseSet = false;
            for (int i = 0; i < bones.Length; i++) { bones[i] = null; applied[i] = false; }
            anim = Model.GetComponentInChildren<Animator>(true);
            if (anim != null && anim.isHuman)
            {
                bones[(int)B.Hips] = anim.GetBoneTransform(HumanBodyBones.Hips);
                bones[(int)B.Spine] = anim.GetBoneTransform(HumanBodyBones.Spine);
                bones[(int)B.Chest] = anim.GetBoneTransform(HumanBodyBones.Chest) ?? anim.GetBoneTransform(HumanBodyBones.UpperChest);
                bones[(int)B.Neck] = anim.GetBoneTransform(HumanBodyBones.Neck);
                bones[(int)B.Head] = anim.GetBoneTransform(HumanBodyBones.Head);
                bones[(int)B.LUpper] = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                bones[(int)B.RUpper] = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
                bones[(int)B.LLower] = anim.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                bones[(int)B.RLower] = anim.GetBoneTransform(HumanBodyBones.RightLowerArm);
                bones[(int)B.LThigh] = anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                bones[(int)B.RThigh] = anim.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                bones[(int)B.LCalf] = anim.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
                bones[(int)B.RCalf] = anim.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            }
            rends = Model.GetComponentsInChildren<Renderer>(true);
            if (mpb == null) mpb = new MaterialPropertyBlock();
            var t = Owner != null ? Owner.T : CombatTuning.Default;
            rng = new System.Random(t.Seed + (Owner != null ? StableHash(Owner.Label) : 0));
        }

        static int StableHash(string s)
        {
            unchecked { int h = 17; foreach (char c in s ?? "") h = h * 31 + c; return h; }
        }

        void CaptureBase()
        {
            if (baseSet || Model == null) return;
            basePos = Model.localPosition;
            baseRot = Model.localRotation;
            baseSet = true;
        }

        public void ResetPose()
        {
            for (int i = 0; i < flinch.Length; i++) flinch[i] = Vector3.zero;
            hipDrop = 0f;
            DownAmount = 0f;
            flashLeft = 0f;
            Shaking = false;
        }

        // ───────────────────────── 젖힘 (08 5-6, 라디안 → 도)
        /// side: 훅이 들어온 쪽(+1 = 맞는 사람의 왼쪽 얼굴 → 오른쪽으로 돌아감)
        public void Flinch(FlinchKind kind, float w, float side = 1f)
        {
            switch (kind)
            {
                case FlinchKind.Head:
                    Add(B.Head, -28.6f, 0f, 0f, w); Add(B.Neck, -11.5f, 0f, 0f, w); Add(B.Chest, -14.3f, 0f, 0f, w); Add(B.Spine, -5.7f, 0f, 0f, w);
                    break;
                case FlinchKind.Hook:
                    Add(B.Head, -8.6f, 43.0f * side, 14.3f * side, w); Add(B.Neck, 0f, 11.5f * side, 0f, w); Add(B.Chest, 0f, 18.3f * side, -4.6f * side, w);
                    break;
                case FlinchKind.Upper:
                    Add(B.Head, -51.6f, 0f, 0f, w); Add(B.Neck, -20.1f, 0f, 0f, w); Add(B.Chest, -24.1f, 0f, 0f, w); Add(B.Spine, -8.6f, 0f, 0f, w);
                    break;
                case FlinchKind.Body:
                    Add(B.Head, 14.3f, 0f, 0f, w); Add(B.Chest, 31.5f, 0f, 0f, w); Add(B.Spine, 17.2f, 0f, 0f, w);
                    Add(B.LUpper, 23f, 0f, 0f, w); Add(B.RUpper, 23f, 0f, 0f, w);
                    hipDrop += 0.1f * 0.85f * w * 0.5f;
                    break;
            }
        }

        void Add(B b, float x, float y, float z, float w)
        {
            var t = Owner != null ? Owner.T : CombatTuning.Default;
            var v = flinch[(int)b] + new Vector3(x, y, z) * w;
            float m = t.FlinchMax;
            flinch[(int)b] = new Vector3(Mathf.Clamp(v.x, -m, m), Mathf.Clamp(v.y, -m, m), Mathf.Clamp(v.z, -m, m));
        }

        public void Flash()
        {
            var t = Owner != null ? Owner.T : CombatTuning.Default;
            flashLeft = t.FlashTime;
            ApplyFlash(t.FlashLevel);
        }

        void ApplyFlash(float level)
        {
            if (rends == null) return;
            var c = new Color(level, level, level, 1f);
            foreach (var r in rends)
            {
                if (r == null) continue;
                r.GetPropertyBlock(mpb);
                if (level > 0f)
                {
                    var mat = r.sharedMaterial;
                    if (mat != null && mat.HasProperty("_Emissive_Color")) mpb.SetColor("_Emissive_Color", c);
                    else if (mat != null && mat.HasProperty("_BaseColor")) mpb.SetColor("_BaseColor", Color.Lerp(mat.GetColor("_BaseColor"), Color.white, level));
                    else mpb.SetColor("_Color", Color.Lerp(Color.grey, Color.white, level));
                }
                else mpb.Clear();
                r.SetPropertyBlock(mpb);
            }
        }

        // ───────────────────────── 임시 공격 자세(클립 연결 전)
        /// 0 = 대기, 1 = 끝까지 뻗음. 손: 0 왼손(잽·훅) 1 오른손(크로스·어퍼) 2 오른발(앞차기) 3 무릎(오른)
        public void Strike(int limb, float amount, bool hook = false, bool upper = false)
        {
            strikeLimb = limb; strikeAmt = Mathf.Clamp01(amount); strikeHook = hook; strikeUpper = upper;
        }
        int strikeLimb = -1;
        float strikeAmt;
        bool strikeHook, strikeUpper;

        void LateUpdate()
        {
            CaptureBase();
            var t = Owner != null ? Owner.T : CombatTuning.Default;
            float dt = TimeFx.Dt;
            float k = Mathf.Exp(-t.FlinchDecay * dt);
            for (int i = 0; i < flinch.Length; i++) flinch[i] *= k;
            hipDrop *= k;

            if (flashLeft > 0f)
            {
                flashLeft -= TimeFx.RealDt;
                if (flashLeft <= 0f) { flashLeft = 0f; ApplyFlash(0f); }
            }

            // 셰이크: 히트스톱 동안 마지막 피격자만
            JitterNow = Vector3.zero;
            if (Shaking && TimeFx.InHitStop)
                JitterNow = new Vector3(((float)rng.NextDouble() - 0.5f) * 2f * t.JitterX, 0f, ((float)rng.NextDouble() - 0.5f) * 2f * t.JitterZ);
            else Shaking = false;

            // 아무 반응도 없으면 손대지 않는다(탐색 중 — M1 자세 그대로). 끝난 첫 프레임은 한 번 더 돌려 원래대로
            bool active = flashLeft > 0f || JitterNow != Vector3.zero || DownAmount > 1e-4f || (strikeLimb >= 0 && strikeAmt > 1e-4f) || hipDrop > 1e-4f;
            if (!active) for (int i = 0; i < flinch.Length && !active; i++) active = flinch[i].sqrMagnitude > 1e-4f;
            if (!active && !wasActive) return;
            wasActive = active;

            var charRot = Owner != null ? Quaternion.Euler(0f, Owner.Yaw, 0f) : transform.rotation;
            if (Humanoid) ApplyBones(charRot);

            if (Model != null && baseSet)
            {
                // 캡슐(뼈 없음)은 몸 전체를 기울여 젖힘을 보인다
                float pitch = Humanoid ? 0f : (flinch[(int)B.Chest].x + flinch[(int)B.Spine].x + flinch[(int)B.Head].x * 0.3f) * 0.6f;
                float yawF = Humanoid ? 0f : flinch[(int)B.Chest].y * 0.5f;
                float roll = Humanoid ? 0f : flinch[(int)B.Head].z * 0.4f;
                float down = DownAmount * DownAmount * (3f - 2f * DownAmount);
                var rot = baseRot * Quaternion.Euler(pitch - 85f * down, yawF, -roll);
                // 눕힐 땐 몸 두께만큼 올려 바닥에 묻히지 않게
                var lift = Vector3.up * (0.12f * down) - Vector3.up * (Humanoid ? 0f : hipDrop);
                var j = Model.parent != null ? Model.parent.InverseTransformVector(charRot * JitterNow) : charRot * JitterNow;
                Model.localPosition = basePos + j + lift;
                Model.localRotation = rot;
            }
        }

        void ApplyBones(Quaternion charRot)
        {
            var right = charRot * Vector3.right;
            var up = charRot * Vector3.up;
            var fwd = charRot * Vector3.forward;
            for (int i = 0; i < bones.Length; i++)
            {
                var b = bones[i];
                if (b == null) continue;
                // 애니메이터가 이번 프레임 자세를 다시 쓰지 않았으면(히트스톱 등) 지난번 더한 것을 먼저 되돌린다
                if (applied[i] && Quaternion.Angle(b.localRotation, post[i]) < 0.01f) b.localRotation = pre[i];
                pre[i] = b.localRotation;
                var f = flinch[i] + StrikeAdd((B)i);
                if (f.sqrMagnitude > 1e-4f)
                {
                    var add = Quaternion.AngleAxis(f.x, right) * Quaternion.AngleAxis(f.y, up) * Quaternion.AngleAxis(f.z, fwd);
                    b.rotation = add * b.rotation;
                }
                post[i] = b.localRotation;
                applied[i] = true;
            }
        }

        /// 임시 공격 자세: 위팔을 앞으로 들어 뻗고(훅은 옆에서 돌아 들어옴, 어퍼는 아래에서 위로), 다리는 허벅지를 앞으로
        Vector3 StrikeAdd(B b)
        {
            if (strikeLimb < 0 || strikeAmt <= 0f) return Vector3.zero;
            float a = strikeAmt;
            switch (strikeLimb)
            {
                case 0:   // 왼손(잽 · 훅: 팔을 옆으로 들어 팔꿈치를 굽혀 돌려 침)
                    if (strikeHook)
                    {
                        if (b == B.LUpper) return new Vector3(-75f * a, 35f * a, 0f);
                        if (b == B.LLower) return new Vector3(0f, 75f * a, 0f);
                        if (b == B.Chest) return new Vector3(0f, 28f * a, 0f);
                        break;
                    }
                    if (b == B.LUpper) return new Vector3(-85f * a, 15f * a, 0f);
                    if (b == B.Chest) return new Vector3(0f, 18f * a, 0f);
                    break;
                case 1:   // 오른손(크로스 · 어퍼: 위팔은 앞 아래, 아래팔을 세워 올려 침 · 큰 훅)
                    if (strikeUpper)
                    {
                        if (b == B.RUpper) return new Vector3(-45f * a, 0f, 0f);
                        if (b == B.RLower) return new Vector3(-95f * a, 0f, 0f);
                        if (b == B.Chest) return new Vector3(-6f * a, -20f * a, 0f);
                        if (b == B.Spine) return new Vector3(-6f * a, 0f, 0f);
                        break;
                    }
                    if (strikeHook)
                    {
                        if (b == B.RUpper) return new Vector3(-75f * a, -35f * a, 0f);
                        if (b == B.RLower) return new Vector3(0f, -75f * a, 0f);
                        if (b == B.Chest) return new Vector3(0f, -30f * a, 0f);
                        break;
                    }
                    if (b == B.RUpper) return new Vector3(-85f * a, -15f * a, 0f);
                    if (b == B.Chest) return new Vector3(0f, -22f * a, 0f);
                    break;
                case 2:   // 앞차기
                    if (b == B.RThigh) return new Vector3(-85f * a, 0f, 0f);
                    if (b == B.Spine) return new Vector3(-12f * a, 0f, 0f);
                    break;
                case 3:   // 무릎
                    if (b == B.RThigh) return new Vector3(-95f * a, 0f, 0f);
                    if (b == B.RCalf) return new Vector3(110f * a, 0f, 0f);
                    if (b == B.Chest) return new Vector3(10f * a, 0f, 0f);
                    break;
            }
            return Vector3.zero;
        }
    }
}
