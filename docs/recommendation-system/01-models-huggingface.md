# Danh Sách Pre-trained Models Trên Hugging Face Phù Hợp Cho MiniShop

Tài liệu này tổng hợp các mô hình Machine Learning & Deep Learning có sẵn trên **Hugging Face Hub** và các thư viện mã nguồn mở hàng đầu, được chọn lọc kỹ lưỡng để phù hợp với đặc thù sản phẩm thương mại điện tử (Tên sản phẩm ngắn, mô tả kỹ thuật, ngôn ngữ Tiếng Việt + Tiếng Anh).

---

## 1. Mô hình Bi-Encoder (Dense Retrieval / Vector Embeddings)

*Nhiệm vụ: Chuyển đổi tên, mô tả sản phẩm và truy vấn tìm kiếm của người dùng thành các vector embedding (dày 384/768/1024 chiều). Đo độ tương đồng bằng Cosine Similarity hoặc Dot Product.*

### 1.1. `bkai-foundation-models/vietnamese-bi-encoder`
- **Link Hugging Face**: [https://huggingface.co/bkai-foundation-models/vietnamese-bi-encoder](https://huggingface.co/bkai-foundation-models/vietnamese-bi-encoder)
- **Tổ chức**: Trung tâm Quốc tế Nghiên cứu về AI (BKAI - Đại học Bách Khoa Hà Nội).
- **Kích thước**: ~540 MB (135M tham số, kiến trúc RoBERTa-base).
- **Chiều vector (Dimension)**: 768.
- **Ưu điểm cho MiniShop**:
  - Tối ưu chuyên biệt cho ngữ nghĩa Tiếng Việt (xử lý tốt từ ngữ viết tắt, từ lóng, cụm từ thương mại điện tử).
  - Tốc độ suy luận rất nhanh trên CPU, không đòi hỏi GPU mạnh.
- **Ứng dụng**: Nhúng toàn bộ `Product.Name + Product.Description` thành vector để tìm kiếm ngữ nghĩa theo câu hỏi của khách hàng.

### 1.2. `BAAI/bge-m3` (Khuyên dùng cho hệ thống đa ngôn ngữ)
- **Link Hugging Face**: [https://huggingface.co/BAAI/bge-m3](https://huggingface.co/BAAI/bge-m3)
- **Tổ chức**: Beijing Academy of Artificial Intelligence (BAAI).
- **Kích thước**: ~2.2 GB (570M tham số).
- **Đặc tính độc đáo**: Hỗ trợ đồng thời 3 cơ chế:
  1. **Dense Retrieval** (Vector embedding thông thường, 1024 chiều).
  2. **Sparse Retrieval** (Tương tự BM25 / từ khóa chính xác).
  3. **Multi-Vector** (ColBERT style - so khớp chi tiết từng token).
- **Ưu điểm cho MiniShop**:
  - Hỗ trợ hơn 100 ngôn ngữ, cực kỳ xuất sắc với Tiếng Việt và Tiếng Anh lẫn lộn (ví dụ: *"tai nghe bluetooth sony chong on"*).
  - Độ dài context lên đến 8192 tokens.
- **Ứng dụng**: Candidate Generation số 1 cho RAG chatbot và Recommendation.

### 1.3. `sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2`
- **Link Hugging Face**: [https://huggingface.co/sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2](https://huggingface.co/sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2)
- **Kích thước**: ~470 MB (117M tham số).
- **Chiều vector**: 384.
- **Ưu điểm**: Cực nhẹ, có thể chuyển đổi sang ONNX để chạy trực tiếp trong ứng dụng .NET 9 (`Microsoft.ML.OnnxRuntime`) hoặc chạy ngay trên trình duyệt thông qua thư viện `@xenova/transformers`.

---

## 2. Mô hình Cross-Encoder / Re-Ranker (Tầng Xếp Hạng Chuẩn Xác)

*Nhiệm vụ: Nhận cặp `(Sở thích người dùng / Câu hỏi, Ứng viên sản phẩm)` và chấm điểm tương quan trực tiếp từ 0 đến 1. Chính xác hơn nhiều so với chỉ so sánh Cosine Similarity đơn thuần.*

### 2.1. `BAAI/bge-reranker-v2-m3`
- **Link Hugging Face**: [https://huggingface.co/BAAI/bge-reranker-v2-m3](https://huggingface.co/BAAI/bge-reranker-v2-m3)
- **Tổ chức**: BAAI.
- **Kích thước**: ~2.2 GB.
- **Khả năng**: Đánh giá sự ăn khớp giữa ý định của khách hàng và tính năng sản phẩm một cách chi tiết ở tầng từ vựng và ngữ cảnh.
- **Ứng dụng**: Sau khi tầng Bi-Encoder lấy ra 20-30 sản phẩm tiềm năng, Reranker sẽ sắp xếp lại để chọn ra top 3-5 sản phẩm xứng đáng nhất để đưa vào prompt cho Chatbot trả lời khách hàng.

### 2.2. `cross-encoder/ms-marco-MiniLM-L-6-v2`
- **Link Hugging Face**: [https://huggingface.co/cross-encoder/ms-marco-MiniLM-L-6-v2](https://huggingface.co/cross-encoder/ms-marco-MiniLM-L-6-v2)
- **Kích thước**: ~90 MB (siêu nhẹ).
- **Tốc độ**: < 10ms trên CPU cho 10 sản phẩm.
- **Phù hợp**: Khi cần độ trễ thấp và chạy trên máy tính cấu hình tiêu chuẩn.

---

## 3. Mô hình Multimodal (Hình ảnh + Văn bản sản phẩm)

*Trong MiniShop, mỗi sản phẩm đều có trường `ImageUrl`. Khi khách hàng gửi hình ảnh hoặc muốn tìm sản phẩm "trông tương tự", mô hình Multimodal là lựa chọn hàng đầu.*

### 3.1. `sentence-transformers/clip-ViT-B-32-multilingual-v1`
- **Link Hugging Face**: [https://huggingface.co/sentence-transformers/clip-ViT-B-32-multilingual-v1](https://huggingface.co/sentence-transformers/clip-ViT-B-32-multilingual-v1)
- **Kích thước**: ~600 MB.
- **Tính năng**: Ánh xạ cả hình ảnh sản phẩm (`ImageUrl`) và văn bản mô tả vào chung một không gian vector 512 chiều.
- **Ứng dụng**:
  - Gợi ý sản phẩm có phong cách thiết kế / màu sắc tương tự sản phẩm khách vừa mua.
  - Cho phép người dùng tìm kiếm sản phẩm bằng ảnh hoặc mô tả kiểu dáng trực quan qua Chatbot.

---

## 4. Mô hình Sequential & Session-based Recommendation (Netflix-Style)

*Netflix dựa nhiều vào chuỗi hành vi: "Người xem vừa xem xong tập 1, tập 2 -> tiếp theo sẽ là gì?". Trong TMĐT: "Khách vừa mua Bàn phím cơ -> Khách tìm tiếp Kê tay hoặc Keycap".*

### 4.1. `recbole/SASRec` & `recbole/BERT4Rec`
- **Kho lưu trữ mã nguồn**: [https://github.com/RUCAIBox/RecBole](https://github.com/RUCAIBox/RecBole)
- **Mô hình trên HF Hub**: Có thể tải checkpoint weights qua [RecBole Model Hub](https://huggingface.co/models?search=recbole).
- **Nguyên lý (Self-Attention Sequential Recommendation)**:
  - Dùng cơ chế Transformer Self-Attention để mô hình hóa chuỗi sản phẩm: $[P_1, P_2, P_3] \rightarrow P_{next}$.
  - Nhận đầu vào là chuỗi `ProductId` mà khách hàng đã tương tác (xem, click, bỏ giỏ hàng `CartItem`, mua `OrderItem`).
- **Ưu điểm**: Bắt kịp sự thay đổi sở thích ngắn hạn của khách hàng trong phiên truy cập hiện tại.

### 4.2. `benfred/implicit` (Collaborative Filtering: ALS & BPR)
- **Kho lưu trữ mã nguồn**: [https://github.com/benfred/implicit](https://github.com/benfred/implicit)
- **Mô hình**: Alternating Least Squares (ALS) và Bayesian Personalized Ranking (BPR).
- **Điểm mạnh với MiniShop**:
  - Hoạt động cực tốt với dữ liệu ngầm định (Implicit Feedback): Khách đã mua (`OrderItem`), khách đã thêm yêu thích (`WishlistItem`), khách đánh giá 5 sao (`Review`).
  - Huấn luyện chỉ mất vài giây cho hàng nghìn sản phẩm, tiêu tốn cực ít RAM.
  - Cung cấp sẵn hàm `similar_items(product_id)` (chính là tính năng *"Sản phẩm tương tự"* kiểu Netflix).

---

## 📊 Bảng tóm tắt lựa chọn mô hình theo nhu cầu

| Nhu cầu của bạn | Model khuyên dùng | Nguồn tải | Chi phí tài nguyên |
|-----------------|-------------------|-----------|--------------------|
| **Tìm kiếm & RAG Tiếng Việt chuẩn** | `bkai-foundation-models/vietnamese-bi-encoder` | [Hugging Face](https://huggingface.co/bkai-foundation-models/vietnamese-bi-encoder) | CPU tiêu chuẩn (RAM 1GB) |
| **Đa ngôn ngữ & Chất lượng cao nhất** | `BAAI/bge-m3` | [Hugging Face](https://huggingface.co/BAAI/bge-m3) | Khuyên dùng GPU hoặc CPU đa nhân |
| **Xếp hạng chính xác nhất trước khi gửi LLM** | `BAAI/bge-reranker-v2-m3` | [Hugging Face](https://huggingface.co/BAAI/bge-reranker-v2-m3) | RAM 2-3GB |
| **Chạy trực tiếp trong React / C# siêu nhẹ** | `sentence-transformers/all-MiniLM-L6-v2` | [Hugging Face](https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2) | Siêu nhẹ (~90MB, chạy in-memory) |
| **Item-to-Item ("Vì bạn đã mua X")** | Thuật toán ALS (thư viện `implicit`) | [GitHub implicit](https://github.com/benfred/implicit) | Cực nhanh, tính trước offline lưu vào DB |
