// 행인1의 메인이벤트 — 화면 촬영·화소 읽기 공용(M2 ZStageShots.Shot 과 같은 방식: 오버레이 캔버스를 찍는 동안만 카메라 공간으로)
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Haengin.Tests
{
    public static class Shots
    {
        public static bool Graphics => SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null;

        public static string Arg(string name)
        {
            var a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++)
                if (string.Equals(a[i], name, System.StringComparison.OrdinalIgnoreCase)) return a[i + 1];
            return null;
        }

        /// 카메라를 w×h(× 슈퍼샘플)로 그려 Texture2D 로(호출한 쪽이 Destroy). ui = 오버레이 캔버스도 함께
        public static Texture2D Grab(Camera c, int w, int h, int ss, bool ui)
        {
            var big = new RenderTexture(new RenderTextureDescriptor(w * ss, h * ss, RenderTextureFormat.ARGB32, 24) { sRGB = true, msaaSamples = 1 });
            var small = new RenderTexture(new RenderTextureDescriptor(w, h, RenderTextureFormat.ARGB32, 0) { sRGB = true });
            big.Create(); small.Create();
            var prevT = c.targetTexture;
            var prevA = RenderTexture.active;
            var overlays = ui ? Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(x => x.isRootCanvas && x.renderMode == RenderMode.ScreenSpaceOverlay && x.isActiveAndEnabled).ToList() : new List<Canvas>();
            Texture2D tex;
            try
            {
                c.targetTexture = big;
                c.aspect = (float)w / h;
                foreach (var o in overlays) { o.renderMode = RenderMode.ScreenSpaceCamera; o.worldCamera = c; o.planeDistance = Mathf.Max(c.nearClipPlane + 0.01f, 0.5f - o.sortingOrder * 0.004f); }
                Canvas.ForceUpdateCanvases();
                c.Render();
                UnityEngine.Graphics.Blit(big, small);
                RenderTexture.active = small;
                tex = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
            }
            finally
            {
                foreach (var o in overlays) { o.renderMode = RenderMode.ScreenSpaceOverlay; o.worldCamera = null; }
                c.targetTexture = prevT;
                c.ResetAspect();
                RenderTexture.active = prevA;
                big.Release(); small.Release();
                Object.Destroy(big); Object.Destroy(small);
            }
            return tex;
        }

        public static string Shot(Camera c, string path, int w = 1920, int h = 1080, int ss = 2, bool ui = true)
        {
            var t = Grab(c, w, h, ss, ui);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, t.EncodeToPNG());
            Object.Destroy(t);
            return path;
        }

        /// sRGB 색의 L*(CIE)
        public static float LStar(Color32 c)
        {
            float Lin(byte v) { float x = v / 255f; return x <= 0.04045f ? x / 12.92f : Mathf.Pow((x + 0.055f) / 1.055f, 2.4f); }
            float y = 0.2126f * Lin(c.r) + 0.7152f * Lin(c.g) + 0.0722f * Lin(c.b);
            return y > 0.008856f ? 116f * Mathf.Pow(y, 1f / 3f) - 16f : 903.3f * y;
        }
    }
}
