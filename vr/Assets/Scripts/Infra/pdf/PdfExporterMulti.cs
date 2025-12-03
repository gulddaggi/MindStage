using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public static class PdfExporterMulti
{
    public static IEnumerator SaveUiPagesToPdfCoroutine(
        IList<RectTransform> pages, string outPath, int dpi = 150,
        Action<string> onDone = null, Action<Exception> onError = null)
    {
        if (pages == null || pages.Count == 0)
            yield break;

        var imgs = new List<(byte[] jpg, int w, int h)>();

        // 각 페이지의 원래 활성 상태 기억
        var originalActive = new bool[pages.Count];
        for (int i = 0; i < pages.Count; i++)
        {
            var rt = pages[i];
            originalActive[i] = rt && rt.gameObject.activeSelf;
        }

        // 1) 페이지별로 하나씩만 켜 놓고 캡처
        for (int i = 0; i < pages.Count; i++)
        {
            var target = pages[i];
            if (!target) continue;

            // i번째 페이지만 활성화, 나머지는 비활성화
            for (int j = 0; j < pages.Count; j++)
            {
                var rt = pages[j];
                if (!rt) continue;
                rt.gameObject.SetActive(j == i);
            }

            // 레이아웃/렌더링 한 프레임 반영
            Canvas.ForceUpdateCanvases();
            yield return null;

            // 실제 캡처
            yield return CaptureRectToJpgCoroutine(
                target,
                (jpg, w, h) => imgs.Add((jpg, w, h))
            );
        }

        // 2) 페이지 활성 상태 원복
        for (int i = 0; i < pages.Count; i++)
        {
            var rt = pages[i];
            if (rt)
                rt.gameObject.SetActive(originalActive[i]);
        }

        // 3) PDF 생성
        try
        {
            WriteMultiImagePdf(imgs, outPath, dpi);
            onDone?.Invoke(outPath);
        }
        catch (Exception ex)
        {
            onError?.Invoke(ex);
        }
    }


    static IEnumerator CaptureRectToJpgCoroutine(
        RectTransform target,
        Action<byte[], int, int> onCaptured)
    {
        if (target == null)
            yield break;

        // [수정 1] 타겟 UI가 속한 캔버스와 카메라 찾기
        var canvas = target.GetComponentInParent<Canvas>();
        Camera cam = null;
        if (canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            cam = canvas.worldCamera;

        // 레이아웃 강제 갱신 + 한 프레임 기다리기
        Canvas.ForceUpdateCanvases();
        yield return null;
        yield return new WaitForEndOfFrame();

        // [수정 2] 캡처 소스 결정 (화면 vs RenderTexture)
        // Hidden Camera를 쓰는 경우 cam.targetTexture가 설정되어 있음
        RenderTexture sourceRT = null;
        int sourceW = Screen.width;
        int sourceH = Screen.height;

        if (cam != null && cam.targetTexture != null)
        {
            sourceRT = cam.targetTexture;
            sourceW = sourceRT.width;
            sourceH = sourceRT.height;
        }

        var prevRT = RenderTexture.active;
        Texture2D screen = new Texture2D(sourceW, sourceH, TextureFormat.RGBA32, false, false);

        try
        {
            // [수정 3] 타겟 텍스처에서 읽어오기
            RenderTexture.active = sourceRT; // null이면 화면, 값이 있으면 RT
            screen.ReadPixels(new Rect(0, 0, sourceW, sourceH), 0, 0);
            screen.Apply();

            // -------------------------
            // 2) target RectTransform 의 화면 좌표 계산
            // -------------------------
            var wc = new Vector3[4];
            target.GetWorldCorners(wc);

            var sp = new Vector2[4];
            for (int i = 0; i < 4; i++)
            {
                // WorldToScreenPoint는 타겟이 RT에 그려질 경우, RT 기준 픽셀 좌표를 반환함
                sp[i] = RectTransformUtility.WorldToScreenPoint(cam, wc[i]);
            }

            float minX = Mathf.Min(sp[0].x, sp[1].x, sp[2].x, sp[3].x);
            float maxX = Mathf.Max(sp[0].x, sp[1].x, sp[2].x, sp[3].x);
            float minY = Mathf.Min(sp[0].y, sp[1].y, sp[2].y, sp[3].y);
            float maxY = Mathf.Max(sp[0].y, sp[1].y, sp[2].y, sp[3].y);

            int x = Mathf.Clamp(Mathf.RoundToInt(minX), 0, sourceW - 1);
            int y = Mathf.Clamp(Mathf.RoundToInt(minY), 0, sourceH - 1);
            int w = Mathf.Clamp(Mathf.RoundToInt(maxX - minX), 1, sourceW - x);
            int h = Mathf.Clamp(Mathf.RoundToInt(maxY - minY), 1, sourceH - y);

            // 너무 작으면 전체 화면으로 fallback
            if (w < 16 || h < 16)
            {
                x = 0; y = 0;
                w = sourceW; h = sourceH;
            }

            // -------------------------
            // 3) 잘라내기 + 알파 플래튼 + JPG 인코딩
            // -------------------------
            Texture2D cropped = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
            var cols = screen.GetPixels(x, y, w, h);

            // 알파가 0인 부분은 흰색 배경으로
            for (int i = 0; i < cols.Length; i++)
            {
                float a = cols[i].a;
                if (a < 0.999f)
                {
                    float r = cols[i].r * a + 1f * (1f - a);
                    float g = cols[i].g * a + 1f * (1f - a);
                    float b = cols[i].b * a + 1f * (1f - a);
                    cols[i] = new Color(r, g, b, 1f);
                }
            }

            cropped.SetPixels(cols);
            cropped.Apply();

            var jpg = cropped.EncodeToJPG(90);
            onCaptured?.Invoke(jpg, w, h);

            UnityEngine.Object.Destroy(cropped);
        }
        finally
        {
            RenderTexture.active = prevRT;
            if (screen) UnityEngine.Object.Destroy(screen);
        }
    }

    static void WriteMultiImagePdf(List<(byte[] jpg, int w, int h)> pages, string outPath, int dpi)
    {
        if (pages == null || pages.Count == 0)
            return;

        using var fs = new FileStream(outPath, FileMode.Create, FileAccess.Write);
        using var bw = new BinaryWriter(fs, Encoding.ASCII);

        // PDF 헤더
        bw.Write(Ascii("%PDF-1.4\n%\xE2\xE3\xCF\xD3\n"));

        int pageCount = pages.Count;
        float ptPerInch = 72f;

        // 1) 오브젝트 번호 예약
        int obj = 1;

        var imgObjIdx = new int[pageCount];
        var resObjIdx = new int[pageCount];
        var contentObjIdx = new int[pageCount];
        var pageObjIdx = new int[pageCount];
        var wPts = new float[pageCount];
        var hPts = new float[pageCount];

        for (int i = 0; i < pageCount; i++)
        {
            imgObjIdx[i] = obj++;
            resObjIdx[i] = obj++;
            contentObjIdx[i] = obj++;
            pageObjIdx[i] = obj++;
        }

        int pagesObj = obj++;
        int catalogObj = obj++;

        int maxObj = catalogObj;
        var xref = new long[maxObj + 1];

        // 2) 내용 작성
        for (int i = 0; i < pageCount; i++)
        {
            var (jpg, w, h) = pages[i];

            float wPt = w / (float)dpi * ptPerInch;
            float hPt = h / (float)dpi * ptPerInch;
            wPts[i] = wPt;
            hPts[i] = hPt;

            // 2-1) 이미지
            xref[imgObjIdx[i]] = fs.Position;
            bw.Write(Ascii($"{imgObjIdx[i]} 0 obj\n"));
            bw.Write(Ascii("<< /Type /XObject /Subtype /Image /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode "));
            bw.Write(Ascii($"/Width {w} /Height {h} /Length {jpg.Length} >>\nstream\n"));
            bw.Write(jpg);
            bw.Write(Ascii("\nendstream\nendobj\n"));

            // 2-2) 리소스
            xref[resObjIdx[i]] = fs.Position;
            bw.Write(Ascii($"{resObjIdx[i]} 0 obj\n"));
            bw.Write(Ascii($"<< /ProcSet [/PDF /ImageC] /XObject << /Im1 {imgObjIdx[i]} 0 R >> >>\nendobj\n"));

            // 2-3) 컨텐츠
            string content = $"q {wPt:0.##} 0 0 {hPt:0.##} 0 0 cm /Im1 Do Q\n";
            byte[] contentBytes = Encoding.ASCII.GetBytes(content);

            xref[contentObjIdx[i]] = fs.Position;
            bw.Write(Ascii($"{contentObjIdx[i]} 0 obj\n"));
            bw.Write(Ascii($"<< /Length {contentBytes.Length} >>\nstream\n"));
            bw.Write(contentBytes);
            bw.Write(Ascii("\nendstream\nendobj\n"));
        }

        // 3) 페이지 객체
        for (int i = 0; i < pageCount; i++)
        {
            xref[pageObjIdx[i]] = fs.Position;
            bw.Write(Ascii($"{pageObjIdx[i]} 0 obj\n"));
            bw.Write(Ascii(
                $"<< /Type /Page /Parent {pagesObj} 0 R " +
                $"/Resources {resObjIdx[i]} 0 R " +
                $"/MediaBox [0 0 {wPts[i]:0.##} {hPts[i]:0.##}] " +
                $"/Contents {contentObjIdx[i]} 0 R >>\nendobj\n"
            ));
        }

        // 4) Pages 객체
        xref[pagesObj] = fs.Position;
        bw.Write(Ascii($"{pagesObj} 0 obj\n"));
        var kids = new StringBuilder("[");
        for (int i = 0; i < pageCount; i++)
            kids.Append($"{pageObjIdx[i]} 0 R ");
        kids.Append("]");
        bw.Write(Ascii($"<< /Type /Pages /Kids {kids} /Count {pageCount} >>\nendobj\n"));

        // 5) Catalog 객체
        xref[catalogObj] = fs.Position;
        bw.Write(Ascii($"{catalogObj} 0 obj\n"));
        bw.Write(Ascii($"<< /Type /Catalog /Pages {pagesObj} 0 R >>\nendobj\n"));

        // 6) Xref
        long xrefStart = fs.Position;
        bw.Write(Ascii($"xref\n0 {maxObj + 1}\n"));
        bw.Write(Ascii("0000000000 65535 f \n"));
        for (int i = 1; i <= maxObj; i++)
        {
            long pos = xref[i];
            bw.Write(Ascii((pos <= 0 ? "0000000000 65535 f " : pos.ToString("0000000000") + " 00000 n ") + "\n"));
        }

        bw.Write(Ascii($"trailer\n<< /Size {maxObj + 1} /Root {catalogObj} 0 R >>\nstartxref\n{xrefStart}\n%%EOF"));
    }

    static byte[] Obj(int n) => Encoding.ASCII.GetBytes($"{n} 0 obj\n");
    static byte[] Ascii(string s) => Encoding.ASCII.GetBytes(s);
}