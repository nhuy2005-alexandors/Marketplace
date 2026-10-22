# Các Phương Án Triển Khai Kỹ Thuật (Implementation Options)

Tùy thuộc vào tài nguyên phần cứng và mục đích (học tập, đồ án môn học, hay sản phẩm thực tế), bạn có 3 phương án kiến trúc để lựa chọn. Cả 3 phương án đều có thể triển khai song song hoặc độc lập mà không ảnh hưởng đến codebase .NET và React hiện tại.

---

## 1. So Sánh Nhanh 3 Phương Án

| Tiêu chí | Phương Án A: Client-Side (Transformers.js) | Phương Án B: Tích Hợp Native .NET 9 (ONNX) | Phương Án C: Microservice Python (FastAPI + FAISS) |
|----------|--------------------------------------------|--------------------------------------------|---------------------------------------------------|
| **Vị trí chạy model** | Trình duyệt người dùng (WebAssembly/WebGPU) | Trực tiếp trong Backend C# ASP.NET Core 9 | Server Python riêng biệt (Port 8000) |
| **Sửa đổi Backend** | **0% (Không cần đụng vào C#)** | Thêm 1 Controller & Service C# | Thêm 1 API route proxy hoặc gọi trực tiếp từ client |
| **Tài nguyên Server** | 0 MB RAM Server | ~300 - 500 MB RAM | ~1 - 2 GB RAM (PyTorch) |
| **Độ trễ (Latency)** | Cực nhanh (< 5ms sau khi tải model) | Rất nhanh (< 15ms) | Nhanh (~20 - 50ms qua REST) |
| **Mức độ phức tạp** | Rất thấp (Dễ nhất cho Frontend) | Trung bình (Chuẩn kiến trúc C#) | Trung bình (Cần cài môi trường Python) |
| **Phù hợp nhất cho** | Demo đồ án, thử nghiệm nhanh, tiết kiệm server | Hệ thống production đồng bộ .NET | Khi cần huấn luyện lại mô hình chuyên sâu |

---

## 2. Chi Tiết Phương Án A: Client-Side (Transformers.js / Hugging Face in Browser)

Hugging Face phát triển thư viện **`@xenova/transformers`** cho phép chạy trực tiếp các mô hình AI trên trình duyệt bằng WebAssembly và ONNX Runtime Web.

### Cách hoạt động:
1. Khi khách hàng mở MiniShop hoặc mở ChatBot, trình duyệt tự động tải phiên bản lượng tử hóa (Quantized ONNX, chỉ ~30MB - 45MB) của model `sentence-transformers/all-MiniLM-L6-v2` hoặc `Xenova/bge-small-en-v1.5`.
2. Model được lưu vào Cache/IndexedDB của trình duyệt, các lần vào sau không cần tải lại.
3. Khi người dùng chat hoặc click sản phẩm:
   - Client tự tính vector embedding của truy vấn.
   - Client tự tính Cosine Similarity với danh sách sản phẩm `products` (đã có sẵn trong state của React).
   - Trả về kết quả tức thì với độ trễ 0 giây!

### Thư viện cài đặt (nếu muốn dùng):
```bash
cd client
npm install @xenova/transformers
```

---

## 3. Chi Tiết Phương Án B: Tích Hợp Native Vào .NET 9 Backend (ONNX Runtime)

Microsoft hỗ trợ thư viện **`Microsoft.ML.OnnxRuntime`** chính chủ, cho phép nạp các file mô hình `.onnx` xuất từ Hugging Face trực tiếp trong C# mà không cần cài đặt Python.

### Các bước triển khai trong `ECommerce.API`:
1. **Tải file ONNX từ Hugging Face**:
   Ví dụ tải `model.onnx` và `tokenizer.json` của model `paraphrase-multilingual-MiniLM-L12-v2` về thư mục `src/ECommerce.Infrastructure/AiModels/`.
2. **Cài đặt Nuget**:
   ```bash
   dotnet add src/ECommerce.Infrastructure package Microsoft.ML.OnnxRuntime
   dotnet add src/ECommerce.Infrastructure package Microsoft.ML.Tokenizers
   ```
3. **Tạo Service `IRecommendationService`**:
   - Khi khởi động ứng dụng, nạp model vào memory (Singleton).
   - Tính toán embedding cho toàn bộ sản phẩm trong database và lưu tạm vào `IMemoryCache`.
   - Cung cấp 2 API endpoint mới:
     - `GET /api/recommendations/for-me`: Lấy gợi ý dựa trên đơn hàng của User đang đăng nhập.
     - `GET /api/recommendations/similar/{productId}`: Lấy sản phẩm tương đồng với 1 sản phẩm cụ thể.

---

## 4. Chi Tiết Phương Án C: Microservice Python Độc Lập (FastAPI + FAISS)

Đây là chuẩn kiến trúc của các công ty công nghệ lớn (bao gồm cả mô hình của Netflix/Shopee).

### Kiến trúc:
```
[React SPA] ----> [ASP.NET Core 9 API (:5215)] ----> [SQL Server LocalDB]
      |                                              
      +-------------> [Python RecSys Microservice (:8000)]
                            ├── FastAPI
                            ├── Hugging Face (BAAI/bge-m3)
                            ├── Vector Search (FAISS Index)
                            └── ALS Collaborative Filtering (implicit)
```

### Ưu điểm vượt trội:
- Bạn có thể tận dụng toàn bộ hệ sinh thái AI mạnh mẽ nhất của Python: PyTorch, Hugging Face `transformers`, `sentence_transformers`, `faiss-cpu`, `scikit-learn`.
- Không làm nặng backend .NET.
- Có thể viết cron job định kỳ hàng ngày để quét bảng `Orders` và `OrderItems` trong SQL Server rồi chạy thuật toán Matrix Factorization (ALS) để cập nhật ma trận gợi ý.
