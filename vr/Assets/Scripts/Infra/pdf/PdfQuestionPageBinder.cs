using App.Services;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class PdfQuestionPageBinder : MonoBehaviour
{
    public TMP_Text tTitle;          // 상단 제목("질문 1" + 질문 내용 요약)
    public TMP_Text tAvg;            // 평균 심박수 텍스트(지금은 아직 - bpm 자리만)
    public Transform contentRoot;    // QnA 아이템들이 붙을 Content
    public GameObject qnaItemPrefab; // QnA 한 줄 프리팹 (Question / Answer 자식 포함)

    /// <summary>
    /// 한 문항(메인 질문 + 꼬리질문들)을 PDF용 페이지에 바인딩.
    /// </summary>
    public void Bind(
        int questionIndex,
        QnaItemDto main,
        IList<QnaItemDto> followUps,
        int avgBpm)
    {
        if (!contentRoot || !qnaItemPrefab || main == null) return;

        // ----- 제목 -----
        if (tTitle)
        {
            tTitle.richText = true;

            // 메인 질문 텍스트가 null 일 수도 있어서 방어
            var q = main.question ?? main.relatedQuestion ?? string.Empty;

            tTitle.SetText($"질문 {questionIndex}");

        }

        // ----- 평균 심박수 (지금은 문항별 데이터 없으니 자리만 표시) -----
        if (tAvg)
        {
            if (avgBpm > 0)
            {
                tAvg.text = $"평균 심박수 : {avgBpm} bpm";
                // 활성 색상 (필요 시 색상 조정)
                var c = tAvg.color;
                tAvg.color = new Color(c.r, c.g, c.b, 1.0f);
            }
            else
            {
                tAvg.text = "평균 심박수 : - bpm";
                // 반투명 처리
                var c = tAvg.color;
                tAvg.color = new Color(c.r, c.g, c.b, 0.5f);
            }
        }

        // ----- 기존 QnA 아이템 제거 -----
        for (int i = contentRoot.childCount - 1; i >= 0; i--)
            Destroy(contentRoot.GetChild(i).gameObject);

        // ----- 1) 메인 QnA -----
        AddQnA(main.question, main.answer, main.labels);

        // ----- 2) 꼬리질문 QnA들 -----
        if (followUps != null)
        {
            foreach (var fu in followUps)
            {
                if (fu == null) continue;
                AddQnA(fu.relatedQuestion, fu.answer, fu.labels);
            }
        }
    }

    /// <summary>
    /// 단일 QnA 항목을 프리팹으로 생성하고, labels에 따라 answer 문장 색을 입힌다.
    /// </summary>
    void AddQnA(string questionText, string answerText, IList<int> labels)
    {
        var item = Instantiate(qnaItemPrefab, contentRoot).transform;

        // 질문 텍스트
        var qNode = item.Find("Question");
        if (qNode)
        {
            var qText = qNode.GetComponentInChildren<TMP_Text>(true);
            if (qText)
            {
                qText.richText = true;
                qText.SetText(questionText ?? string.Empty);
            }
        }

        // 답변 텍스트 (+ 감정 색상 적용)
        var aNode = item.Find("Answer");
        if (aNode)
        {
            var aText = aNode.GetComponentInChildren<TMP_Text>(true);
            if (aText)
            {
                aText.richText = true;

                // ReportDetailController.BuildColoredAnswer:
                //  - . , 기준으로 문장 분리
                //  - labels[i] == 0: 흰색, 1: 빨간색, 2: 파란색
                //  - labels 부족/없음 → 나머지는 전부 흰색(중립)
                string colored =
                    ReportDetailController.BuildColoredAnswer(
                        answerText ?? string.Empty,
                        labels
                    );

                aText.text = colored;
            }
        }
    }
}
