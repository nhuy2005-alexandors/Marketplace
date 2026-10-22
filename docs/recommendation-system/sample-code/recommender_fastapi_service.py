"""
FILE THAM KHẢO (SAMPLE CODE): recommender_fastapi_service.py

Microservice Recommendation độc lập bằng Python (FastAPI + Hugging Face Transformers).
Chạy trên port 8000, có thể gọi từ .NET API (C#) hoặc trực tiếp từ React Frontend.

Cài đặt thư viện:
    pip install fastapi uvicorn sentence-transformers pydantic numpy
Chạy service:
    python recommender_fastapi_service.py
"""

from typing import List, Optional
import numpy as np
from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel
from sentence_transformers import SentenceTransformer

# 1. Khởi tạo FastAPI
app = FastAPI(
    title="MiniShop Recommendation Service",
    description="Hệ thống gợi ý sản phẩm phong cách Netflix sử dụng mô hình Hugging Face"
)

# Cấu hình CORS để React chạy tại localhost:5173 gọi được
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# 2. Tải mô hình Hugging Face (Multilingual / Tiếng Việt)
# Có thể đổi sang: "bkai-foundation-models/vietnamese-bi-encoder" hoặc "BAAI/bge-m3"
MODEL_NAME = "sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2"
print(f"Đang tải mô hình từ Hugging Face: {MODEL_NAME}...")
model = SentenceTransformer(MODEL_NAME)
print("Tải mô hình thành công!")

# Bộ nhớ đệm lưu trữ vector sản phẩm (In-memory Vector Store)
products_db = []
product_embeddings = None  # numpy array shape (N, Dim)


# 3. Định nghĩa Schemas
class ProductItem(BaseModel):
    id: int
    name: str
    description: Optional[str] = ""
    category_name: str
    price: float


class IndexProductsRequest(BaseModel):
    products: List[ProductItem]


class RecommendForUserRequest(BaseModel):
    purchased_product_ids: List[int]
    top_k: int = 5


class SimilarItemsRequest(BaseModel):
    product_id: int
    top_k: int = 5


class HybridSearchRequest(BaseModel):
    query: str
    purchased_product_ids: Optional[List[int]] = []
    top_k: int = 5


# 4. Endpoints API

@app.post("/api/index-products", summary="Tính toán và lập chỉ mục Vector cho toàn bộ sản phẩm")
def index_products(req: IndexProductsRequest):
    global products_db, product_embeddings
    products_db = req.products
    if not products_db:
        return {"message": "Danh sách rỗng", "count": 0}

    # Kết hợp tên, danh mục và mô tả để tạo đoạn văn bản đại diện
    texts = [
        f"{p.name} - Danh mục: {p.category_name}. {p.description or ''}"
        for p in products_db
    ]
    # Sinh embeddings chuẩn hóa (Normalized L2 để tính Cosine bằng Dot Product)
    emb = model.encode(texts, normalize_embeddings=True)
    product_embeddings = np.array(emb, dtype=np.float32)
    return {"message": "Lập chỉ mục thành công", "count": len(products_db)}


@app.post("/api/recommend/similar-items", summary="Gợi ý sản phẩm tương tự ('Because you bought X')")
def recommend_similar_items(req: SimilarItemsRequest):
    global products_db, product_embeddings
    if product_embeddings is None or len(products_db) == 0:
        raise HTTPException(status_code=400, detail="Chưa có dữ liệu sản phẩm. Vui lòng gọi /api/index-products trước.")

    # Tìm index của sản phẩm mục tiêu
    target_idx = next((i for i, p in enumerate(products_db) if p.id == req.product_id), None)
    if target_idx is None:
        raise HTTPException(status_code=404, detail="Không tìm thấy sản phẩm trong chỉ mục.")

    target_vec = product_embeddings[target_idx]
    # Tính Cosine similarity: Dot product giữa target_vec với tất cả vector còn lại
    scores = np.dot(product_embeddings, target_vec)

    # Lấy top k sản phẩm có điểm cao nhất (loại bỏ chính nó)
    sorted_indices = np.argsort(scores)[::-1]
    recommendations = []
    for idx in sorted_indices:
        if idx == target_idx:
            continue
        recommendations.append({
            "product": products_db[idx],
            "similarity_score": float(scores[idx])
        })
        if len(recommendations) >= req.top_k:
            break

    return {"source_product_id": req.product_id, "recommendations": recommendations}


@app.post("/api/recommend/for-user", summary="Gợi ý cá nhân hóa dựa trên lịch sử mua sắm của User")
def recommend_for_user(req: RecommendForUserRequest):
    global products_db, product_embeddings
    if product_embeddings is None or len(products_db) == 0:
        raise HTTPException(status_code=400, detail="Chưa có dữ liệu sản phẩm.")

    purchased_set = set(req.purchased_product_ids)
    user_item_indices = [i for i, p in enumerate(products_db) if p.id in purchased_set]

    # Nếu người dùng chưa từng mua gì -> Gợi ý sản phẩm đầu bảng
    if not user_item_indices:
        return {"mode": "cold_start", "recommendations": products_db[:req.top_k]}

    # Tính vector trung bình sở thích của User (User Profile Vector)
    user_vec = np.mean(product_embeddings[user_item_indices], axis=0)
    user_vec = user_vec / np.linalg.norm(user_vec)  # Chuẩn hóa L2

    scores = np.dot(product_embeddings, user_vec)
    sorted_indices = np.argsort(scores)[::-1]

    recommendations = []
    for idx in sorted_indices:
        # Không gợi ý lại các món khách đã mua rồi
        if products_db[idx].id in purchased_set:
            continue
        recommendations.append({
            "product": products_db[idx],
            "match_score": float(scores[idx])
        })
        if len(recommendations) >= req.top_k:
            break

    return {"mode": "personalized", "recommendations": recommendations}


if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="127.0.0.1", port=8000)
