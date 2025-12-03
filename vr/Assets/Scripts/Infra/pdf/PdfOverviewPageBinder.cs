using App.Services;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class PdfOverviewPageBinder : MonoBehaviour
{
    [Header("Meta")]
    public TMP_Text tTitle;
    public TMP_Text tCompany;
    public TMP_Text tJob;
    public TMP_Text tTotal;

    [Header("Summary")]
    public TMP_Text tSummary;

    [Header("Charts")]
    public RadarChartGraphic radar;
    public HeartRateChartGraphic heart;

    // ReportDetailDto 그대로 사용
    public void Bind(
        ReportDetailDto data,
        string companyText,
        string jobText,
        string totalText,
        List<QuestionRange> questionRanges = null
        )
    {
        if (tCompany != null) tCompany.text = companyText;
        if (tJob != null) tJob.text = jobText;
        if (tTotal != null) tTotal.text = totalText;

        if (tSummary != null)
        {
            // 기존 요약 텍스트 바인딩 그대로
            tSummary.text = ReportDetailController.MdToTmp(data.comment);
        }

        if (tTitle) tTitle.SetText("면접 결과 분석");

        if (tCompany) tCompany.SetText(companyText ?? "");
        if (tJob) tJob.SetText(jobText ?? "");

        // tDate / 총 진행 시간: 값이 "-" 이거나 비어 있으면 비활성화
        if (tTotal)
        {
            if (string.IsNullOrWhiteSpace(totalText) || totalText.Contains("-"))
            {
                tTotal.gameObject.SetActive(false);
            }
            else
            {
                tTotal.gameObject.SetActive(true);
                tTotal.SetText(totalText);
            }
        }

        // 요약(comment) – 기존 MdToTmp 재사용 (아래에서 public 으로 바꿔서 호출)
        if (tSummary)
        {
            tSummary.richText = true;
            var c = (data != null ? data.comment : null);
            tSummary.text = string.IsNullOrEmpty(c)
                ? "(요약 없음)"
                : ReportDetailController.MdToTmp(c);
        }

        // 레이더 차트
        if (data != null && radar != null)
        {
            radar.SetAxisLabels(new[] { "의사소통", "적응성", "팀워크", "직무 능력", "진실성" });

            var my = ToRadarArray(data.myScores);
            var avg = ToRadarArray(data.averageScores);
            radar.SetScores(my, avg);
        }

        // 심박수 그래프
        if (heart != null && data.heartBeats != null && data.heartBeats.Count >= 2)
        {
            // HeartBeatSampleDto → int bpm 리스트로 변환
            var beats = new List<int>(data.heartBeats.Count);
            foreach (var hb in data.heartBeats)
            {
                beats.Add(hb.bpm);
            }

            if (beats.Count >= 2)
            {
                int count = beats.Count;

                float sum = 0f;
                int min = int.MaxValue;
                int max = int.MinValue;

                for (int i = 0; i < count; i++)
                {
                    int v = beats[i];
                    sum += v;
                    if (v < min) min = v;
                    if (v > max) max = v;
                }

                // 화면 리포트와 동일한 방식으로 기준선/범위 계산
                int baselineBpm = Mathf.RoundToInt(sum / count);

                float dLower = Mathf.Abs(baselineBpm - min);
                float dUpper = Mathf.Abs(max - baselineBpm);
                float d = Mathf.Max(dLower, dUpper);

                int pad = Mathf.Max(2, Mathf.CeilToInt(d * 0.1f));
                int halfRange = Mathf.CeilToInt(d + pad);

                int yMin = Mathf.Max(40, baselineBpm - halfRange);
                int yMax = baselineBpm + halfRange;

                // HeartRateChartGraphic 내부에서 이동평균 + 다운샘플링 + 색칠/라인 모두 처리
                heart.SetValues(beats, baselineBpm, yMin, yMax);

                // 질문별 구간/경계선/라벨까지 함께 적용
                if (questionRanges != null && questionRanges.Count > 0)
                {
                    heart.SetQuestionRanges(questionRanges);
                }
                else
                {
                    heart.SetQuestionRanges(null);
                }
            }
            else
            {
                heart.gameObject.SetActive(false);
            }
        }
        else if (heart != null)
        {
            heart.gameObject.SetActive(false);
        }

    }

    static float[] ToRadarArray(ScoresDto s)
    {
        if (s == null) return null;

        float S(float v) => Mathf.Clamp(v, 0f, 5f) * 20f;

        return new float[]
        {
            S(s.Communication),
            S(s.Adaptability),
            S(s.Teamwork_Leadership),
            S(s.Job_Competency),
            S(s.Integrity)
        };
    }
}