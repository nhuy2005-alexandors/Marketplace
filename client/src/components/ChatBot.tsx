import { useState, useEffect, useRef } from "react";
import { Link } from "react-router-dom";
import {
  Bot,
  Sparkles,
  X,
  Send,
  RotateCcw,
  Settings,
  ShoppingCart,
  Store,
  CheckCircle2,
  AlertTriangle,
  HelpCircle,
  ExternalLink,
  ChevronDown,
} from "lucide-react";
import type { Product } from "../types";
import { executeRagQuery, fetchMarketProducts, type RagResult } from "../services/ragService";
import { useAddToCart } from "../api/hooks";
import { useAuth } from "../store/auth";

interface ChatMessage {
  id: string;
  sender: "user" | "bot";
  text: string;
  timestamp: string;
  result?: RagResult;
}

const QUICK_PROMPTS = [
  "🎧 Tìm tai nghe chống ồn",
  "⌨️ Có bàn phím cơ không?",
  "📚 Gợi ý sách lập trình",
  "👕 Áo thun dưới $30",
  "🖱️ Có chuột gaming không?", // Test gợi ý na ná
  "🚗 Có bán xe ô tô không?",   // Test thông báo chưa có
];

export function ChatBot() {
  const [isOpen, setIsOpen] = useState(false);
  const [input, setInput] = useState("");
  const [isLoading, setIsLoading] = useState(false);
  const [products, setProducts] = useState<Product[]>([]);
  const [showSettings, setShowSettings] = useState(false);
  const [geminiKey, setGeminiKey] = useState<string>(() => {
    return localStorage.getItem("minishop_gemini_key") || "";
  });

  const [messages, setMessages] = useState<ChatMessage[]>(() => [
    {
      id: "welcome-1",
      sender: "bot",
      text:
        "Xin chào! Tôi là **Trợ lý AI (RAG)** của sàn thương mại điện tử MiniShop.\n\n" +
        "Tôi đã được kết nối và đọc toàn bộ dữ liệu sản phẩm trong kho của sàn.\n" +
        "• Nếu sản phẩm có trên sàn: Tôi sẽ **đề xuất chi tiết** cho bạn.\n" +
        "• Nếu chưa có đúng mẫu: Tôi sẽ **gợi ý sản phẩm na ná / cùng loại**.\n" +
        "• Nếu hoàn toàn không có: Tôi sẽ **thông báo rõ ràng** để bạn tiết kiệm thời gian.\n\n" +
        "Bạn cần tìm sản phẩm gì hôm nay?",
      timestamp: new Date().toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" }),
    },
  ]);

  const messagesEndRef = useRef<HTMLDivElement>(null);
  const addToCart = useAddToCart();
  const isCustomer = useAuth((s) => s.user?.role === "Customer");

  // Tải danh sách toàn bộ sản phẩm trên sàn để phục vụ RAG
  useEffect(() => {
    fetchMarketProducts().then((data) => {
      setProducts(data);
    });
  }, []);

  useEffect(() => {
    if (isOpen) {
      messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
    }
  }, [messages, isOpen]);

  const handleSaveKey = (key: string) => {
    setGeminiKey(key);
    localStorage.setItem("minishop_gemini_key", key.trim());
    setShowSettings(false);
  };

  const handleSend = async (queryText?: string) => {
    const textToSend = queryText || input;
    if (!textToSend.trim() || isLoading) return;

    const userMsg: ChatMessage = {
      id: "u-" + Date.now(),
      sender: "user",
      text: textToSend.trim(),
      timestamp: new Date().toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" }),
    };

    setMessages((prev) => [...prev, userMsg]);
    setInput("");
    setIsLoading(true);

    try {
      // Đảm bảo có sản phẩm mới nhất
      let currentProducts = products;
      if (currentProducts.length === 0) {
        currentProducts = await fetchMarketProducts();
        setProducts(currentProducts);
      }

      // Thực thi bộ máy RAG
      const ragResult = await executeRagQuery(userMsg.text, currentProducts, geminiKey);

      const botMsg: ChatMessage = {
        id: "b-" + Date.now(),
        sender: "bot",
        text: ragResult.message,
        timestamp: new Date().toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" }),
        result: ragResult,
      };

      setMessages((prev) => [...prev, botMsg]);
    } catch (err) {
      console.error("Chat error:", err);
      setMessages((prev) => [
        ...prev,
        {
          id: "err-" + Date.now(),
          sender: "bot",
          text: "Xin lỗi, đã xảy ra lỗi trong quá trình xử lý câu hỏi. Vui lòng thử lại sau giây lát!",
          timestamp: new Date().toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" }),
        },
      ]);
    } finally {
      setIsLoading(false);
    }
  };

  const handleResetChat = () => {
    setMessages([
      {
        id: "welcome-reset",
        sender: "bot",
        text: "Hộp thoại đã được làm mới. Bạn cần tìm kiếm hay so sánh sản phẩm nào trên sàn?",
        timestamp: new Date().toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" }),
      },
    ]);
  };

  return (
    <>
      {/* Nút bấm nổi mở Chatbot */}
      {!isOpen && (
        <button
          onClick={() => setIsOpen(true)}
          aria-label="Mở Trợ lý AI Mua sắm (RAG)"
          className="fixed bottom-6 right-6 z-50 flex items-center gap-2.5 px-4 py-3 bg-gradient-to-r from-blue-600 via-indigo-600 to-sky-600 hover:from-blue-700 hover:to-indigo-700 text-white rounded-full shadow-2xl hover:shadow-indigo-500/30 hover:scale-105 active:scale-95 transition-all duration-200 group border border-white/20"
        >
          <div className="relative">
            <Bot className="w-6 h-6 animate-pulse" />
            <span className="absolute -top-1 -right-1 flex h-2.5 w-2.5">
              <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-emerald-400 opacity-75"></span>
              <span className="relative inline-flex rounded-full h-2.5 w-2.5 bg-emerald-500"></span>
            </span>
          </div>
          <div className="text-left hidden sm:block">
            <div className="text-xs font-semibold leading-none flex items-center gap-1">
              AI Chatbot <Sparkles className="w-3 h-3 text-amber-300" />
            </div>
            <div className="text-[10px] text-white/80 mt-0.5">Hỏi đáp RAG sản phẩm</div>
          </div>
        </button>
      )}

      {/* Cửa sổ Chatbot chính */}
      {isOpen && (
        <div className="fixed bottom-6 right-6 z-50 w-[95vw] sm:w-[440px] h-[620px] max-h-[85vh] flex flex-col bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-3xl shadow-2xl overflow-hidden backdrop-blur-md animate-in fade-in slide-in-from-bottom-5 duration-300">
          {/* Header */}
          <div className="px-4 py-3.5 bg-gradient-to-r from-blue-600 via-indigo-600 to-sky-600 text-white flex items-center justify-between shrink-0 shadow-md">
            <div className="flex items-center gap-2.5">
              <div className="w-9 h-9 rounded-full bg-white/20 flex items-center justify-center backdrop-blur-sm border border-white/30">
                <Bot className="w-5 h-5 text-white" />
              </div>
              <div>
                <div className="font-bold text-sm leading-tight flex items-center gap-1.5">
                  MiniShop AI Assistant
                  <span className="text-[10px] bg-emerald-400/20 text-emerald-100 font-mono px-1.5 py-0.5 rounded-full border border-emerald-300/30">
                    RAG
                  </span>
                </div>
                <div className="text-[11px] text-white/80 flex items-center gap-1 mt-0.5">
                  <span className="w-1.5 h-1.5 rounded-full bg-emerald-400 inline-block animate-ping"></span>
                  Đọc {products.length} sản phẩm theo thời gian thực
                </div>
              </div>
            </div>

            <div className="flex items-center gap-1">
              <button
                onClick={() => setShowSettings(!showSettings)}
                title="Cài đặt Gemini API Key (Tùy chọn)"
                className="p-1.5 hover:bg-white/20 rounded-lg transition text-white/90"
              >
                <Settings className="w-4 h-4" />
              </button>
              <button
                onClick={handleResetChat}
                title="Xóa lịch sử chat"
                className="p-1.5 hover:bg-white/20 rounded-lg transition text-white/90"
              >
                <RotateCcw className="w-4 h-4" />
              </button>
              <button
                onClick={() => setIsOpen(false)}
                title="Đóng chatbox"
                className="p-1.5 hover:bg-white/20 rounded-lg transition text-white/90"
              >
                <ChevronDown className="w-5 h-5" />
              </button>
            </div>
          </div>

          {/* Modal / Bảng cấu hình Gemini Key (Tùy chọn nâng cao) */}
          {showSettings && (
            <div className="p-3.5 bg-slate-100 dark:bg-slate-800 border-b border-slate-200 dark:border-slate-700 text-xs">
              <div className="font-semibold text-slate-800 dark:text-slate-200 mb-1 flex items-center justify-between">
                <span>Cấu hình Gemini API (Tùy chọn)</span>
                <button
                  onClick={() => setShowSettings(false)}
                  className="text-slate-400 hover:text-slate-600 dark:hover:text-slate-200"
                >
                  <X className="w-3.5 h-3.5" />
                </button>
              </div>
              <p className="text-slate-500 dark:text-slate-400 text-[11px] mb-2 leading-relaxed">
                Mặc định Chatbot đã tích hợp sẵn <strong>bộ máy RAG cục bộ</strong> cực nhanh & không cần key. Nếu bạn muốn LLM Gemini sinh câu văn mượt mà hơn, hãy dán API key của Google vào đây:
              </p>
              <div className="flex gap-2">
                <input
                  type="password"
                  placeholder="AIzaSy..."
                  value={geminiKey}
                  onChange={(e) => setGeminiKey(e.target.value)}
                  className="flex-1 px-2.5 py-1.5 rounded-lg border border-slate-300 dark:border-slate-600 bg-white dark:bg-slate-900 text-xs text-slate-800 dark:text-slate-100 focus:outline-none focus:ring-1 focus:ring-blue-500"
                />
                <button
                  onClick={() => handleSaveKey(geminiKey)}
                  className="px-3 py-1.5 bg-blue-600 text-white rounded-lg font-medium hover:bg-blue-700 transition"
                >
                  Lưu
                </button>
              </div>
            </div>
          )}

          {/* Danh sách tin nhắn */}
          <div className="flex-1 p-4 overflow-y-auto space-y-4 bg-slate-50/50 dark:bg-slate-950/40">
            {messages.map((msg) => (
              <div
                key={msg.id}
                className={`flex flex-col ${msg.sender === "user" ? "items-end" : "items-start"}`}
              >
                <div
                  className={`max-w-[90%] rounded-2xl px-4 py-3 text-sm leading-relaxed shadow-sm ${
                    msg.sender === "user"
                      ? "bg-blue-600 text-white rounded-br-none"
                      : "bg-white dark:bg-slate-800 text-slate-800 dark:text-slate-100 border border-slate-200/80 dark:border-slate-700/80 rounded-bl-none"
                  }`}
                >
                  {/* Badge phân loại phản hồi RAG nếu là Bot */}
                  {msg.sender === "bot" && msg.result && (
                    <div className="mb-2">
                      {msg.result.type === "matched" && (
                        <span className="inline-flex items-center gap-1 text-[11px] font-semibold bg-emerald-100 text-emerald-800 dark:bg-emerald-950/60 dark:text-emerald-300 px-2.5 py-0.5 rounded-full">
                          <CheckCircle2 className="w-3.5 h-3.5" /> Có sản phẩm trên sàn
                        </span>
                      )}
                      {msg.result.type === "similar" && (
                        <span className="inline-flex items-center gap-1 text-[11px] font-semibold bg-amber-100 text-amber-800 dark:bg-amber-950/60 dark:text-amber-300 px-2.5 py-0.5 rounded-full">
                          <AlertTriangle className="w-3.5 h-3.5" /> Gợi ý sản phẩm na ná / liên quan
                        </span>
                      )}
                      {msg.result.type === "not_found" && (
                        <span className="inline-flex items-center gap-1 text-[11px] font-semibold bg-rose-100 text-rose-800 dark:bg-rose-950/60 dark:text-rose-300 px-2.5 py-0.5 rounded-full">
                          <HelpCircle className="w-3.5 h-3.5" /> Chưa có trên thị trường
                        </span>
                      )}
                    </div>
                  )}

                  {/* Nội dung tin nhắn hiển thị định dạng */}
                  <div className="whitespace-pre-line text-sm">
                    {msg.text.split("\n").map((line, idx) => {
                      // Xử lý các dòng gạch đầu dòng và in đậm cơ bản
                      if (line.startsWith("• ") || line.startsWith("🔹 ") || line.startsWith("🔸 ")) {
                        return (
                          <div key={idx} className="my-0.5 pl-1">
                            {formatRichText(line)}
                          </div>
                        );
                      }
                      return (
                        <p key={idx} className={line === "" ? "h-2" : "my-0.5"}>
                          {formatRichText(line)}
                        </p>
                      );
                    })}
                  </div>

                  {/* Hiển thị thẻ sản phẩm tương tác nếu có kết quả phù hợp hoặc na ná */}
                  {msg.result &&
                    (msg.result.matchedProducts.length > 0 || msg.result.similarProducts.length > 0) && (
                      <div className="mt-3.5 pt-3 border-t border-slate-100 dark:border-slate-700/60 space-y-2.5">
                        <div className="text-[11px] font-semibold text-slate-500 dark:text-slate-400 uppercase tracking-wider">
                          {msg.result.matchedProducts.length > 0
                            ? "Sản phẩm tìm thấy:"
                            : "Sản phẩm gợi ý tương tự:"}
                        </div>
                        <div className="grid grid-cols-1 gap-2">
                          {[...msg.result.matchedProducts, ...msg.result.similarProducts].map((p) => (
                            <div
                              key={p.id}
                              className="flex items-center gap-3 p-2.5 bg-slate-50 dark:bg-slate-900/80 rounded-xl border border-slate-200/70 dark:border-slate-700/60 hover:border-blue-400 dark:hover:border-blue-500 transition group"
                            >
                              <img
                                src={p.imageUrl || "https://via.placeholder.com/100"}
                                alt={p.name}
                                className="w-14 h-14 object-cover rounded-lg shrink-0 border border-slate-200 dark:border-slate-700"
                              />
                              <div className="flex-1 min-w-0">
                                <Link
                                  to={`/products/${p.id}`}
                                  className="text-xs font-bold text-slate-800 dark:text-slate-100 hover:text-blue-600 dark:hover:text-blue-400 line-clamp-1 flex items-center gap-1"
                                >
                                  {p.name}
                                  <ExternalLink className="w-3 h-3 opacity-0 group-hover:opacity-100 transition shrink-0" />
                                </Link>
                                <div className="text-[11px] text-slate-500 dark:text-slate-400 flex items-center gap-1.5 mt-0.5">
                                  <span>{p.categoryName}</span>
                                  <span>•</span>
                                  <span className="flex items-center gap-0.5 truncate">
                                    <Store className="w-3 h-3 shrink-0" />
                                    {p.sellerShopName}
                                  </span>
                                </div>
                                <div className="flex items-center justify-between mt-1">
                                  <span className="font-extrabold text-blue-600 dark:text-blue-400 text-xs">
                                    ${p.price.toFixed(2)}
                                  </span>
                                  {p.stock > 0 ? (
                                    <span className="text-[10px] text-emerald-600 dark:text-emerald-400 font-medium">
                                      Kho: {p.stock}
                                    </span>
                                  ) : (
                                    <span className="text-[10px] text-rose-500 font-medium">Hết hàng</span>
                                  )}
                                </div>
                              </div>

                              {isCustomer && p.stock > 0 && (
                                <button
                                  onClick={() => addToCart.mutate({ productId: p.id, quantity: 1 })}
                                  disabled={addToCart.isPending}
                                  title="Thêm vào giỏ"
                                  className="p-2 rounded-lg bg-blue-600 hover:bg-blue-700 text-white shrink-0 transition hover:scale-105 active:scale-95 disabled:opacity-50"
                                >
                                  <ShoppingCart className="w-3.5 h-3.5" />
                                </button>
                              )}
                            </div>
                          ))}
                        </div>
                      </div>
                    )}
                </div>
                <span className="text-[10px] text-slate-400 mt-1 px-1">{msg.timestamp}</span>
              </div>
            ))}

            {isLoading && (
              <div className="flex items-center gap-2 p-3 bg-white dark:bg-slate-800 rounded-2xl border border-slate-200 dark:border-slate-700 max-w-[80%] shadow-sm">
                <Bot className="w-4 h-4 text-blue-600 animate-spin" />
                <span className="text-xs text-slate-500 dark:text-slate-400 animate-pulse">
                  Đang truy xuất và đối chiếu dữ liệu sản phẩm...
                </span>
              </div>
            )}
            <div ref={messagesEndRef} />
          </div>

          {/* Gợi ý câu hỏi nhanh (Quick Prompts) */}
          <div className="px-3 py-2 bg-white dark:bg-slate-900 border-t border-slate-100 dark:border-slate-800 flex gap-1.5 overflow-x-auto no-scrollbar">
            {QUICK_PROMPTS.map((prompt, i) => (
              <button
                key={i}
                onClick={() => handleSend(prompt)}
                disabled={isLoading}
                className="whitespace-nowrap text-[11px] px-2.5 py-1 rounded-full bg-slate-100 hover:bg-blue-50 hover:text-blue-600 dark:bg-slate-800 dark:hover:bg-slate-700 text-slate-600 dark:text-slate-300 border border-slate-200 dark:border-slate-700 transition"
              >
                {prompt}
              </button>
            ))}
          </div>

          {/* Ô nhập tin nhắn */}
          <div className="p-3 bg-white dark:bg-slate-900 border-t border-slate-200 dark:border-slate-800 flex items-center gap-2">
            <input
              type="text"
              value={input}
              onChange={(e) => setInput(e.target.value)}
              onKeyDown={(e) => e.key === "Enter" && handleSend()}
              placeholder="Hỏi về sản phẩm, giá, gợi ý mua sắm..."
              disabled={isLoading}
              className="flex-1 px-3.5 py-2.5 text-xs rounded-xl bg-slate-100 dark:bg-slate-800 border border-transparent focus:border-blue-500 focus:bg-white dark:focus:bg-slate-900 text-slate-800 dark:text-slate-100 placeholder-slate-400 focus:outline-none transition"
            />
            <button
              onClick={() => handleSend()}
              disabled={!input.trim() || isLoading}
              className="p-2.5 bg-blue-600 hover:bg-blue-700 disabled:bg-slate-300 dark:disabled:bg-slate-700 text-white rounded-xl transition hover:scale-105 active:scale-95 disabled:scale-100 shadow-sm"
            >
              <Send className="w-4 h-4" />
            </button>
          </div>
        </div>
      )}
    </>
  );
}

// Hàm format text markdown cơ bản (**bold**, *italic*)
function formatRichText(text: string) {
  const parts = text.split(/(\*\*.*?\*\*|\*.*?\*)/g);
  return parts.map((part, i) => {
    if (part.startsWith("**") && part.endsWith("**")) {
      return (
        <strong key={i} className="font-semibold text-slate-900 dark:text-white">
          {part.slice(2, -2)}
        </strong>
      );
    }
    if (part.startsWith("*") && part.endsWith("*")) {
      return (
        <em key={i} className="italic text-slate-600 dark:text-slate-300">
          {part.slice(1, -1)}
        </em>
      );
    }
    return part;
  });
}
