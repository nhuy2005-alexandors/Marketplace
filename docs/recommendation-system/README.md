# Hệ Thống Recommendation MiniShop (Phong cách Netflix) & Tích Hợp ChatBot RAG

Thư mục này cung cấp tài liệu hướng dẫn toàn diện, giải pháp kiến trúc, danh sách model pre-trained sẵn trên Hugging Face và các phương án triển khai hệ thống gợi ý sản phẩm (Recommendation System) cá nhân hóa cho MiniShop, tích hợp mượt mà với ChatBot RAG hiện có mà **không cần chỉnh sửa đè hỏng codebase hiện tại**.

---

## 📑 Mục lục tài liệu

| Tài liệu | Nội dung chính |
|----------|----------------|
| **[01. Models Hugging Face & Pre-trained](01-models-huggingface.md)** | Tổng hợp các pre-trained models tốt nhất trên Hugging Face (kèm link URL trực tiếp), hỗ trợ Tiếng Việt & Đa ngôn ngữ cho Embeddings, Reranking, và Sequential Recommendation. |
| **[02. Kiến Trúc Recommendation Kiểu Netflix](02-netflix-recommendation-architecture.md)** | Phân tích cơ chế gợi ý 2 giai đoạn (Candidate Generation & Ranking), Collaborative Filtering từ `OrderItem`/`Review`, Content-based từ `Product`, và Session-based từ hành vi tìm kiếm. |
| **[03. Hướng Dẫn Tích Hợp Vào ChatBot RAG](03-rag-chatbot-integration.md)** | Kế hoạch tích hợp vào `ChatBot.tsx` & `ragService.ts`: Context Injection, Prompt template cho LLM (Gemini), kết hợp giỏ hàng/wishlist/lịch sử mua với câu hỏi người dùng. |
| **[04. Các Phương Án Triển Khai Kỹ Thuật](04-backend-implementation-options.md)** | So sánh 3 kiến trúc triển khai: (A) Client-Side Transformers.js, (B) .NET 9 Backend với ONNX Runtime / ML.NET, (C) Microservice Python FastAPI + Vector DB. |
| **[Sample Code / Code Mẫu Tham Khảo](sample-code/)** | Các script mẫu hoàn chỉnh (TypeScript, Python, C#) để tham khảo và tái sử dụng khi bạn bắt đầu code. |

---

## 🎯 Tại sao mô hình Netflix phù hợp với MiniShop?

Netflix nổi tiếng với việc kết hợp nhiều tầng gợi ý:
1. **"Top Picks for You"**: Đề xuất cá nhân hóa dựa trên toàn bộ lịch sử (mua hàng, đánh giá, danh mục ưa thích).
2. **"Because you watched X" -> "Vì bạn đã mua / quan tâm X"**: Item-to-Item Similarity (sản phẩm tương đồng về ngữ nghĩa tính năng hoặc thường được mua cùng).
3. **"Trending / Popular"**: Dành cho người dùng mới (Cold-Start Problem).
4. **"Context-Aware & Session Intent"**: Khi người dùng chat với Chatbot hoặc gõ tìm kiếm, hệ thống lập tức hiểu ý định tức thời (Real-time intent) và điều chỉnh danh sách gợi ý.

### Sơ đồ luồng kết hợp với ChatBot RAG

```mermaid
flowchart TD
    User([Người dùng / Khách hàng]) -->|1. Chat / Tìm kiếm| ChatBot[ChatBot RAG UI]
    
    subgraph Data Sources [Dữ liệu MiniShop]
        History[(Lịch sử mua hàng: OrderItem)]
        Wishlist[(Wishlist & Giỏ hàng)]
        Reviews[(Đánh giá & Rating)]
        Catalog[(Kho sản phẩm: Product)]
    end

    subgraph Recommendation Engine [Bộ máy gợi ý 2 tầng]
        Retrieval[Tầng 1: Candidate Retrieval\n- Vector Embeddings\n- Collaborative Filtering\n- Top 20-50 ứng viên]
        Ranking[Tầng 2: Re-ranking / Scoring\n- Cross-Encoder / BGE-Reranker\n- Lọc theo giá, tồn kho\n- Top 3-5 sản phẩm tối ưu]
    end

    Data Sources --> Retrieval
    ChatBot -->|Truyền User Context + Query| Retrieval
    Retrieval --> Ranking
    Ranking -->|Sản phẩm đề xuất| RAGPrompt[RAG Prompt Builder]
    RAGPrompt -->|Context + Query| LLM[Gemini 1.5 / 2.0 / OpenAI]
    LLM -->|Câu trả lời kèm Card sản phẩm| ChatBot
    ChatBot --> User
```

---

## 🚀 Lộ trình triển khai đề xuất

1. **Giai đoạn 1 (Nhanh & Tối ưu nhất)**: Sử dụng mô hình Semantic Bi-Encoder từ Hugging Face (ví dụ `bkai-foundation-models/vietnamese-bi-encoder` hoặc `BAAI/bge-m3`) để nâng cấp khả năng hiểu sản phẩm từ từ khóa thô sang Vector Similarity.
2. **Giai đoạn 2 (Tích hợp Lịch sử)**: Khi khách hàng đã đăng nhập (`Customer`), lấy danh sách các sản phẩm từ `OrderItem` và `WishlistItem` để tạo profile vector sở thích của khách.
3. **Giai đoạn 3 (Tích hợp ChatBot)**: Bổ sung context "Khách hàng này từng mua..." vào prompt của Gemini trong `ragService.ts` để chatbot tư vấn tự nhiên như chuyên viên bán hàng cá nhân.
