# kobert_full_test.py
import torch
from kobert_transformers import get_kobert_model, get_tokenizer

def main():
    # 1️⃣ Load tokenizer and model
    print("Loading KoBERT tokenizer and model...")
    tokenizer = get_tokenizer()
    model = get_kobert_model()

    # 2️⃣ Input sentence
    sentence = "안녕하세요. 한국어 BERT 모델을 테스트합니다."

    # 3️⃣ Tokenization (text → subwords)
    tokens = tokenizer.tokenize(sentence)
    print("\n🧩 Tokenized subwords:")
    print(tokens)

    # 4️⃣ Convert to IDs and prepare model input
    inputs = tokenizer(
        sentence,
        return_tensors="pt",
        padding=True,
        truncation=True,
        max_length=64
    )
    print("\n📘 Token IDs:", inputs["input_ids"])
    print("📗 Attention mask:", inputs["attention_mask"])

    # 5️⃣ Run the model forward pass
    with torch.no_grad():
        outputs = model(**inputs)

    # 6️⃣ Inspect the result
    print("\n✅ Model loaded successfully and ran inference!")
    print("Output shape:", outputs.last_hidden_state.shape)
    print("First token vector (first 5 dims):")
    print(outputs.last_hidden_state[0, 0, :5])

    # 7️⃣ Decode token IDs back to text
    decoded = tokenizer.decode(inputs["input_ids"][0])
    print("\n🔁 Decoded back to text:")
    print(decoded)

if __name__ == "__main__":
    main()
