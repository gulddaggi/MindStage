import re, numpy as np, torch
from kobert_transformers import get_tokenizer
import torch.nn as nn

DEVICE  = torch.device("cpu")   # 동적 양자화 모델은 CPU 추론 권장
MAX_LEN = 256

LABEL2ID = {'uncertain': 0, 'negative': 1, 'positive': 2}
ID2LABEL = {v: k for k, v in LABEL2ID.items()}

MODEL_PATH = "ai/v1_code/using_custom_models/model_emotion_quantized.pt"

class BertClassifier(nn.Module):
    def __init__(self, bert, hidden_size=768, num_classes=3, dr_rate=0.3, class_weights=None):
        super().__init__()
        self.bert = bert
        self.dropout = nn.Dropout(p=dr_rate) if dr_rate and dr_rate > 0 else nn.Identity()
        self.classifier = nn.Linear(hidden_size, num_classes)
        self.loss_fn = nn.CrossEntropyLoss(weight=class_weights) if class_weights is not None else nn.CrossEntropyLoss()

    def forward(self, input_ids, attention_mask=None, token_type_ids=None, labels=None):
        out = self.bert(input_ids=input_ids, attention_mask=attention_mask, token_type_ids=token_type_ids)
        pooled = out.pooler_output if getattr(out, "pooler_output", None) is not None else out[0][:, 0]
        logits = self.classifier(self.dropout(pooled))
        if labels is not None:
            loss = self.loss_fn(logits, labels)
            return logits, loss
        return logits, None

# 1) 문장 분할
def split_sentences(paragraph: str) : 
    lines = [s.strip() for s in paragraph.strip().splitlines() if s.strip()]
    sents = []
    for line in lines : 
        pieces = re.split(r'(?<=[\.!?])\s+|(?<=다\.)\s+|(?<=요\.)\s+', line)
        sents += [p.strip() for p in pieces if p and p.strip()]
    return sents

# 2) 모델/ 토크나이저 로드
print("[Load] tokenizer =>")
tokenizer = get_tokenizer()

print("f[Load] quantized model => {MODEL_PATH}")
try :
    import sys as _sys
    main_mod = _sys.modules.get('__main__')
    if main_mod is not None and not hasattr(main_mod, 'BertClassifier'):
        setattr(main_mod, 'BertClassifier', BertClassifier)
except Exception as e:
    pass

try : 
    model = torch.load(MODEL_PATH, map_location=DEVICE)
except AttributeError : 
    import sys as _sys
    main_mod = _sys.modules.get('__main__')
    if main_mod is not None and hasattr(main_mod, 'BertClassifier'):
        setattr(main_mod, 'BertClassifier', BertClassifier)
    model = torch.load(MODEL_PATH, map_location=DEVICE)
model.eval()

# 3) 감정예측
@torch.no_grad() # 
def predict_sentences(sent_list) : 
    results, probs_all = [], [] # 리스트 형태로 받으면 문장단위 softmax확률을 probs_all에 저장
    for sent in sent_list : 
        enc = tokenizer(
            sent,
            padding = 'max_length',
            truncation = True,
            max_length = MAX_LEN,
            return_tensors = 'pt',
            return_token_type_ids = True
        )

        input_ids =  enc["input_ids"].to(DEVICE)
        attention_mask = enc["attention_mask"].to(DEVICE)
        token_type_ids = enc.get("token_type_ids", attention_mask.new_zeros(attention_mask.size())).to(DEVICE)

        out = model(input_ids, attention_mask, token_type_ids)
        logits = out[0] if isinstance(out, (tuple, list)) else out

        prob = torch.softmax(logits, dim=1).squeeze().cpu().numpy()
        pred_id = int(prob.argmax())
        results.append({
            "text": sent,
            "pred_id": pred_id,
            "pred_label": ID2LABEL[pred_id],
            "probs": {
                "uncertain": float(prob[LABEL2ID['uncertain']]),
                "negative":  float(prob[LABEL2ID['negative']]),
                "positive":  float(prob[LABEL2ID['positive']]),
            }
        })
        probs_all.append(prob)

    para_summary = None
    if probs_all:
        mean_prob = np.stack(probs_all, axis=0).mean(axis=0)
        para_summary = {
            "paragraph_pred_id": int(mean_prob.argmax()),
            "paragraph_pred_label": ID2LABEL[int(mean_prob.argmax())],
            "paragraph_probs": {
                "uncertain": float(mean_prob[LABEL2ID['uncertain']]),
                "negative":  float(mean_prob[LABEL2ID['negative']]),
                "positive":  float(mean_prob[LABEL2ID['positive']]),
            }
        }
    return results, para_summary

# 4) 테스트
if __name__ == "__main__" : 
    paragraph = """
    공부를 해도 실력이 오르지 않아 많이 속상했습니다. 하지만, 이를 딛고 일어나 열심히 노력한 끝에 좋은 성적을 받게 됐습니다. 앞으로도 꾸준히 노력해서 더 나은 결과를 얻고 싶습니다.
"""
    sents = split_sentences(paragraph)
    sent_results, para_result = predict_sentences(sents)
    print("=== Sentence-level ===")
    for r in sent_results: 
        print(f"- {r['text']}")
        print(f" -> pred: {r['pred_label']} probs={r['probs']}")
        print("\n=== Paragraph-level (mean of probs) ===")
        print(para_result)