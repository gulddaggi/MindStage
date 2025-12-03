using System.Collections;
using System.Collections.Generic;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public static class PdfExporterOneImage
{
    // 코루틴 버전: 프레임 끝에서 UI 캡처 → 1페이지 이미지 PDF 생성
    public static IEnumerator SaveUiToPdfCoroutine(
        RectTransform target, string outPath, int dpi = 150,
        Action<string> onDone = null, Action<Exception> onError = null)
    {
        // --- 레이아웃 안정화 & 프레임 끝 대기 (try 바깥) ---
        Canvas.ForceUpdateCanvases();
        yield return null;
        yield return new WaitForEndOfFrame();

        Camera tempCam = null;
        RenderTexture rt = null;
        Texture2D screen = null;

        // 캔버스 조회
        var canvas = target.GetComponentInParent<Canvas>();
        if (canvas == null) { onError?.Invoke(new Exception("No Canvas found for target.")); yield break; }

        var oldMode = canvas.renderMode;
        var oldCam = canvas.worldCamera;

        Exception error = null;
        try
        {
            // --- 임시 UI 카메라 준비 ---
            tempCam = new GameObject("UICaptureCam (temp)").AddComponent<Camera>();
            tempCam.orthographic = true;
            tempCam.clearFlags = CameraClearFlags.SolidColor;
            tempCam.backgroundColor = Color.clear;
            int uiMask = LayerMask.GetMask("UI");
            tempCam.cullingMask = (uiMask != 0) ? uiMask : ~0;
            tempCam.enabled = false; // 수동 Render()

            // Overlay → 일시적으로 Camera 모드로 전환
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = tempCam;
            Canvas.ForceUpdateCanvases();

            // --- UI 전체 렌더를 텍스처로 ---
            int rtW = Screen.width, rtH = Screen.height;
            rt = new RenderTexture(rtW, rtH, 24, RenderTextureFormat.ARGB32);
            tempCam.targetTexture = rt;
            tempCam.Render();
            tempCam.targetTexture = null;

            var prev = RenderTexture.active;
            RenderTexture.active = rt;

            screen = new Texture2D(rtW, rtH, TextureFormat.RGBA32, false, false);
            screen.ReadPixels(new Rect(0, 0, rtW, rtH), 0, 0);
            screen.Apply();
            RenderTexture.active = prev;

            // --- 캡처 영역(4점) → 스크린 사각형 ---
            var wc = new Vector3[4];
            target.GetWorldCorners(wc);

            // ScreenSpaceCamera 상태이므로 worldCamera 사용
            Camera coordCam = canvas.worldCamera;
            var sp = new Vector2[4];
            for (int i = 0; i < 4; i++)
                sp[i] = RectTransformUtility.WorldToScreenPoint(coordCam, wc[i]);

            float minX = Mathf.Min(sp[0].x, sp[1].x, sp[2].x, sp[3].x);
            float maxX = Mathf.Max(sp[0].x, sp[1].x, sp[2].x, sp[3].x);
            float minY = Mathf.Min(sp[0].y, sp[1].y, sp[2].y, sp[3].y);
            float maxY = Mathf.Max(sp[0].y, sp[1].y, sp[2].y, sp[3].y);

            int x = Mathf.Clamp(Mathf.RoundToInt(minX), 0, screen.width - 1);
            int y = Mathf.Clamp(Mathf.RoundToInt(minY), 0, screen.height - 1);
            int w = Mathf.Clamp(Mathf.RoundToInt(maxX - minX), 1, screen.width - x);
            int h = Mathf.Clamp(Mathf.RoundToInt(maxY - minY), 1, screen.height - y);

            if (w < 10 || h < 10) { x = 0; y = 0; w = screen.width; h = screen.height; }

            // --- 잘라내기 & 투명 배경을 흰색으로 채우기(옵션) ---
            var cropped = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pix = screen.GetPixels(x, y, w, h);
            cropped.SetPixels(pix);
            cropped.Apply();

            var cols = cropped.GetPixels32();
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i].a < 255)
                {
                    float a = cols[i].a / 255f;
                    byte r = (byte)Mathf.RoundToInt(cols[i].r * a + 255 * (1 - a));
                    byte g = (byte)Mathf.RoundToInt(cols[i].g * a + 255 * (1 - a));
                    byte b = (byte)Mathf.RoundToInt(cols[i].b * a + 255 * (1 - a));
                    cols[i] = new Color32(r, g, b, 255);
                }
            }
            cropped.SetPixels32(cols);
            cropped.Apply();

            byte[] jpg = cropped.EncodeToJPG(90);

            UnityEngine.Object.Destroy(cropped);
            UnityEngine.Object.Destroy(screen);

            // --- PDF 저장 ---
            WriteSingleImagePdf(jpg, w, h, outPath, dpi);
        }
        catch (Exception ex)
        {
            error = ex;
        }
        finally
        {
            // 원복 & 정리 (yield 사용하지 않음)
            canvas.renderMode = oldMode;
            canvas.worldCamera = oldCam;
            Canvas.ForceUpdateCanvases();

            if (tempCam) UnityEngine.Object.Destroy(tempCam.gameObject);
            if (rt) rt.Release();
            if (screen) UnityEngine.Object.Destroy(screen);
        }

        if (error != null) { onError?.Invoke(error); yield break; }
        onDone?.Invoke(outPath);
    }


    // 단일 이미지 페이지 PDF
    static void WriteSingleImagePdf(byte[] jpg, int texW, int texH, string outPath, int dpi)
    {
        using var fs = new FileStream(outPath, FileMode.Create, FileAccess.Write);
        using var bw = new BinaryWriter(fs, Encoding.ASCII);

        bw.Write(Ascii("%PDF-1.4\n%\xE2\xE3\xCF\xD3\n"));

        long xrefStart;
        int obj = 1;
        var xref = new System.Collections.Generic.List<long>();

        // 1) 이미지
        xref.Add(fs.Position);
        bw.Write(Obj(obj++));
        bw.Write(Ascii("<< /Type /XObject /Subtype /Image "));
        bw.Write(Ascii($"/Width {texW} /Height {texH} "));
        bw.Write(Ascii("/ColorSpace /DeviceRGB /BitsPerComponent 8 "));
        bw.Write(Ascii("/Filter /DCTDecode "));
        bw.Write(Ascii($"/Length {jpg.Length} >>\nstream\n"));
        bw.Write(jpg);
        bw.Write(Ascii("\nendstream\nendobj\n"));

        // 2) 리소스
        xref.Add(fs.Position);
        bw.Write(Obj(obj++));
        bw.Write(Ascii("<< /ProcSet [/PDF /ImageC] /XObject << /Im1 1 0 R >> >>\nendobj\n"));

        // 3) 페이지 (픽셀→포인트 변환)
        float ptPerInch = 72f;
        float wPt = texW / (float)dpi * ptPerInch;
        float hPt = texH / (float)dpi * ptPerInch;

        xref.Add(fs.Position);
        bw.Write(Obj(obj++));
        bw.Write(Ascii($"<< /Type /Page /Parent 5 0 R /Resources 2 0 R /MediaBox [0 0 {wPt:0.##} {hPt:0.##}] /Contents 4 0 R >>\nendobj\n"));

        // 4) 컨텐츠: 이미지 그리기
        var content = $"q {wPt:0.##} 0 0 {hPt:0.##} 0 0 cm /Im1 Do Q\n";
        var contentBytes = Encoding.ASCII.GetBytes(content);
        xref.Add(fs.Position);
        bw.Write(Obj(obj++));
        bw.Write(Ascii($"<< /Length {contentBytes.Length} >>\nstream\n"));
        bw.Write(contentBytes);
        bw.Write(Ascii("\nendstream\nendobj\n"));

        // 5) 페이지 트리
        xref.Add(fs.Position);
        bw.Write(Obj(obj++));
        bw.Write(Ascii("<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n"));

        // 6) 카탈로그
        xref.Add(fs.Position);
        bw.Write(Obj(obj++));
        bw.Write(Ascii("<< /Type /Catalog /Pages 5 0 R >>\nendobj\n"));

        // XRef
        xrefStart = fs.Position;
        bw.Write(Ascii($"xref\n0 {xref.Count + 1}\n"));
        bw.Write(Ascii("0000000000 65535 f \n"));
        foreach (var pos in xref) bw.Write(Ascii(pos.ToString("0000000000") + " 00000 n \n"));
        bw.Write(Ascii($"trailer\n<< /Size {xref.Count + 1} /Root 6 0 R >>\nstartxref\n{xrefStart}\n%%EOF"));
    }

    public static IEnumerator CaptureRectToJpgCoroutine(
        RectTransform target,
        Action<byte[], int, int> onCaptured,
        Action<Exception> onError = null)
    {
        // 레이아웃 안정화
        Canvas.ForceUpdateCanvases();
        yield return null;
        yield return new WaitForEndOfFrame();

        Camera tempCam = null;
        RenderTexture rt = null;
        Texture2D screen = null;

        var canvas = target.GetComponentInParent<Canvas>();
        if (canvas == null) { onError?.Invoke(new Exception("No Canvas found for target.")); yield break; }

        var oldMode = canvas.renderMode;
        var oldCam = canvas.worldCamera;

        try
        {
            // 임시 UI 카메라
            tempCam = new GameObject("UICaptureCam (temp)").AddComponent<Camera>();
            tempCam.orthographic = true;
            tempCam.clearFlags = CameraClearFlags.SolidColor;
            tempCam.backgroundColor = Color.clear;
            int uiMask = LayerMask.GetMask("UI");
            tempCam.cullingMask = (uiMask != 0) ? uiMask : ~0;
            tempCam.enabled = false;

            // Overlay → Camera 전환
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = tempCam;
            Canvas.ForceUpdateCanvases();

            // 전체 UI 렌더
            int rtW = Screen.width, rtH = Screen.height;
            rt = new RenderTexture(rtW, rtH, 24, RenderTextureFormat.ARGB32);
            tempCam.targetTexture = rt;
            tempCam.Render();
            tempCam.targetTexture = null;

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            screen = new Texture2D(rtW, rtH, TextureFormat.RGBA32, false, false);
            screen.ReadPixels(new Rect(0, 0, rtW, rtH), 0, 0);
            screen.Apply();
            RenderTexture.active = prev;

            // 캡처 사각형 산출
            var wc = new Vector3[4]; target.GetWorldCorners(wc);
            var sp = new Vector2[4];
            var cam = canvas.worldCamera;
            for (int i = 0; i < 4; i++) sp[i] = RectTransformUtility.WorldToScreenPoint(cam, wc[i]);

            float minX = Mathf.Min(sp[0].x, sp[1].x, sp[2].x, sp[3].x);
            float maxX = Mathf.Max(sp[0].x, sp[1].x, sp[2].x, sp[3].x);
            float minY = Mathf.Min(sp[0].y, sp[1].y, sp[2].y, sp[3].y);
            float maxY = Mathf.Max(sp[0].y, sp[1].y, sp[2].y, sp[3].y);

            int x = Mathf.Clamp(Mathf.RoundToInt(minX), 0, screen.width - 1);
            int y = Mathf.Clamp(Mathf.RoundToInt(minY), 0, screen.height - 1);
            int w = Mathf.Clamp(Mathf.RoundToInt(maxX - minX), 1, screen.width - x);
            int h = Mathf.Clamp(Mathf.RoundToInt(maxY - minY), 1, screen.height - y);
            if (w < 10 || h < 10) { x = 0; y = 0; w = screen.width; h = screen.height; }

            // 잘라내고 알파를 흰색으로 합성
            var cropped = new Texture2D(w, h, TextureFormat.RGBA32, false);
            cropped.SetPixels(screen.GetPixels(x, y, w, h));
            cropped.Apply();

            var cols = cropped.GetPixels32();
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i].a < 255)
                {
                    float a = cols[i].a / 255f;
                    byte r = (byte)Mathf.RoundToInt(cols[i].r * a + 255 * (1 - a));
                    byte g = (byte)Mathf.RoundToInt(cols[i].g * a + 255 * (1 - a));
                    byte b = (byte)Mathf.RoundToInt(cols[i].b * a + 255 * (1 - a));
                    cols[i] = new Color32(r, g, b, 255);
                }
            }
            cropped.SetPixels32(cols); cropped.Apply();

            byte[] jpg = cropped.EncodeToJPG(90);
            UnityEngine.Object.Destroy(cropped);
            UnityEngine.Object.Destroy(screen);

            onCaptured?.Invoke(jpg, w, h);
        }
        catch (Exception ex)
        {
            onError?.Invoke(ex);
        }
        finally
        {
            canvas.renderMode = oldMode;
            canvas.worldCamera = oldCam;
            Canvas.ForceUpdateCanvases();

            if (tempCam) UnityEngine.Object.Destroy(tempCam.gameObject);
            if (rt) rt.Release();
            if (screen) UnityEngine.Object.Destroy(screen);
        }
    }


    static byte[] Obj(int n) => Encoding.ASCII.GetBytes($"{n} 0 obj\n");
    static byte[] Ascii(string s) => Encoding.ASCII.GetBytes(s);
}