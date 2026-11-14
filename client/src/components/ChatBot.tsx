import { useState, useEffect, useRef } from "react";
import { Link } from "react-router-dom";
import {
  Bot,
  Sparkles,
  Send,
  RotateCcw,
  ShoppingCart,
  Store,
  CheckCircle2,
  AlertTriangle,
  HelpCircle,
  ExternalLink,
  ChevronDown,
} from "lucide-react";
import { executeRagQuery, type RagResult } from "../services/ragService";
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
  "✨ Gợi ý món phù hợp theo sở thích",
  "🎯 Mấy món đang gợi ý trên màn hình có gì hay?",
  "🎧 Tìm tai nghe chống ồn",
  "⌨️ Có bàn phím cơ không?",
  "📚 Gợi ý sách lập trình",
  "👕 Áo thun dưới $30",
];

export function ChatBot() {
  const [isOpen, setIsOpen] = useState(false);
  const [input, setInput] = useState("");
  const [isLoading, setIsLoading] = useState(false);
  const user = useAuth((s) => s.user);
  const isCustomer = user?.role === "Customer";
  const addToCart = useAddToCart();

  const [messages, setMessages] = useState<ChatMessage[]>(() => [
    {
      id: "welcome-1",
      sender: "bot",
      text:
        "Xin chào! Tôi là **Trợ lý mua sắm MiniShop**.\n\n" +
        "Tôi có thể tìm sản phẩm trong catalog và trả lời dựa trên thông tin sản phẩm hiện có. Giá và tồn kho trên thẻ sản phẩm lấy từ hệ thống.\n\n" +
        "Bạn cần tìm sản phẩm gì hôm nay?",
      timestamp: new Date().toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" }),
    },
  ]);

  const messagesRef = useRef(messages);
  const messagesEndRef = useRef<HTMLDivElement>(null);
  const handleSendRef = useRef<(queryText?: string) => Promise<void>>(async () => {});
  const conversationIdRef = useRef<string | undefined>(undefined);

  useEffect(() => {
    messagesRef.current = messages;
  }, [messages]);

  // Xóa API key cũ mà các phiên bản trước từng lưu ở trình duyệt.
  useEffect(() => {
    try {
      localStorage.removeItem("minishop_gemini_key");
      sessionStorage.removeItem("minishop_gemini_key");
    } catch {
      // Storage may be unavailable in private browsing modes.
    }
  }, []);

  useEffect(() => {
    conversationIdRef.current = undefined;
    setMessages([{
      id: "welcome-reset",
      sender: "bot",
      text: "Hộp thoại đã được làm mới. Bạn cần tìm kiếm hay muốn nhận gợi ý sản phẩm nào trên sàn?",
      timestamp: new Date().toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" }),
    }]);
  }, [user?.id]);

  // Lắng nghe sự kiện click "Hỏi AI Chatbot về món này" từ màn hình chính
  useEffect(() => {
    const handleCustomAsk = (e: Event) => {
      const customEvent = e as CustomEvent<{ query: string }>;
      if (customEvent.detail?.query) {
        setIsOpen(true);
        handleSendRef.current(customEvent.detail.query);
      }
    };

    window.addEventListener("minishop:ask-bot", handleCustomAsk);
    return () => window.removeEventListener("minishop:ask-bot", handleCustomAsk);
  }, []);

  useEffect(() => {
    if (isOpen) {
      messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
    }
  }, [messages, isOpen]);

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
      const recentTurns = messagesRef.current
        .filter((message) => message.id !== "welcome-1" && message.id !== "welcome-reset")
        .slice(-4)
        .map((message) => ({ role: message.sender === "bot" ? "assistant" as const : "user" as const, content: message.text }));
      const ragResult = await executeRagQuery(userMsg.text, recentTurns, conversationIdRef.current);
      conversationIdRef.current = ragResult.conversationId;

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

  useEffect(() => {
    handleSendRef.current = handleSend;
  });

  const handleResetChat = () => {
    conversationIdRef.current = undefined;
    setMessages([
      {
        id: "welcome-reset",
        sender: "bot",
        text: "Hộp thoại đã được làm mới. Bạn cần tìm kiếm hay muốn nhận gợi ý sản phẩm nào trên sàn?",
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
          aria-label="Mở Trợ lý AI Mua sắm (RAG & Recommendation)"
          className="fixed bottom-6 right-6 z-50 flex items-center gap-2.5 px-4 py-3 bg-gradient-to-r from-indigo-600 via-blue-600 to-sky-600 hover:from-indigo-700 hover:to-blue-700 text-white rounded-full shadow-2xl hover:shadow-indigo-500/30 hover:scale-105 active:scale-95 transition-all duration-200 group border border-white/20"
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
            <div className="text-[10px] text-white/80 mt-0.5">RAG & Gợi ý theo sở thích</div>
          </div>
        </button>
      )}

      {/* Cửa sổ Chatbot chính */}
      {isOpen && (
        <div className="fixed bottom-6 right-6 z-50 w-[95vw] sm:w-[450px] h-[640px] max-h-[85vh] flex flex-col bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-3xl shadow-2xl overflow-hidden backdrop-blur-md animate-in fade-in slide-in-from-bottom-5 duration-300">
          {/* Header */}
          <div className="px-4 py-3.5 bg-gradient-to-r from-indigo-600 via-blue-600 to-sky-600 text-white flex items-center justify-between shrink-0 shadow-md">
            <div className="flex items-center gap-2.5">
              <div className="w-9 h-9 rounded-full bg-white/20 flex items-center justify-center backdrop-blur-sm border border-white/30">
                <Bot className="w-5 h-5 text-white" />
              </div>
              <div>
                <div className="font-bold text-sm leading-tight flex items-center gap-1.5">
                  MiniShop AI Assistant
                  <span className="text-[10px] bg-emerald-400/25 text-emerald-100 font-mono px-1.5 py-0.2 rounded-full border border-emerald-300/30">
                    RAG + Rec
                  </span>
                </div>
                <div className="text-[11px] text-white/80 flex items-center gap-1 mt-0.5 truncate max-w-[220px]">
                  <span className="w-1.5 h-1.5 rounded-full bg-emerald-400 inline-block animate-ping shrink-0"></span>
                  <span className="truncate" title="Truy xuất sản phẩm từ catalog phía máy chủ">
                    Tra cứu sản phẩm trong catalog
                  </span>
                </div>
              </div>
            </div>

            <div className="flex items-center gap-1">
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

          {/* Danh sách tin nhắn */}
          <div className="flex-1 p-4 overflow-y-auto space-y-4 bg-slate-50/50 dark:bg-slate-950/40">
            {messages.map((msg) => {
              const allItemCards = [
                ...(msg.result?.matchedProducts || []),
                ...(msg.result?.similarProducts || []),
                ...(msg.result?.recommendedProducts || []),
              ];

              return (
                <div
                  key={msg.id}
                  className={`flex flex-col ${msg.sender === "user" ? "items-end" : "items-start"}`}
                >
                  <div
                    className={`max-w-[90%] rounded-2xl px-4 py-3 text-sm leading-relaxed shadow-sm ${
                      msg.sender === "user"
                        ? "bg-indigo-600 text-white rounded-br-none"
                        : "bg-white dark:bg-slate-800 text-slate-800 dark:text-slate-100 border border-slate-200/80 dark:border-slate-700/80 rounded-bl-none"
                    }`}
                  >
                    {/* Badge phân loại phản hồi RAG & Recommendation */}
                    {msg.sender === "bot" && msg.result && (
                      <div className="mb-2">
                        {msg.result.type === "recommendation" && (
                          <span className="inline-flex items-center gap-1 text-[11px] font-semibold bg-gradient-to-r from-indigo-100 to-sky-100 text-indigo-800 dark:from-indigo-950/80 dark:to-sky-950/80 dark:text-indigo-300 px-2.5 py-0.5 rounded-full border border-indigo-200 dark:border-indigo-800">
                            <Sparkles className="w-3.5 h-3.5 text-indigo-600 dark:text-indigo-400" />
                            Gợi ý cá nhân hóa (Đồng bộ Màn hình)
                          </span>
                        )}
                        {msg.result.type === "matched" && (
                          <span className={`inline-flex items-center gap-1 text-[11px] font-semibold px-2.5 py-0.5 rounded-full ${msg.result.evidenceStrength === "weak" ? "bg-amber-100 text-amber-800 dark:bg-amber-950/60 dark:text-amber-300" : "bg-emerald-100 text-emerald-800 dark:bg-emerald-950/60 dark:text-emerald-300"}`}>
                            {msg.result.evidenceStrength === "weak" ? <AlertTriangle className="w-3.5 h-3.5" /> : <CheckCircle2 className="w-3.5 h-3.5" />}
                            {msg.result.evidenceStrength === "weak" ? "Gợi ý gần với yêu cầu · chưa chắc khớp hoàn toàn" : "Có sản phẩm trên sàn"}
                          </span>
                        )}
                        {msg.result.type === "similar" && (
                          <span className="inline-flex items-center gap-1 text-[11px] font-semibold bg-amber-100 text-amber-800 dark:bg-amber-950/60 dark:text-amber-300 px-2.5 py-0.5 rounded-full">
                            <AlertTriangle className="w-3.5 h-3.5" /> Gợi ý sản phẩm na ná / liên quan
                          </span>
                        )}
                        {msg.result.type === "not_found" && (
                          <span className="inline-flex items-center gap-1 text-[11px] font-semibold bg-rose-100 text-rose-800 dark:bg-rose-950/60 dark:text-rose-300 px-2.5 py-0.5 rounded-full">
                            <HelpCircle className="w-3.5 h-3.5" /> {msg.result.evidenceStrength === "weak" ? "Chưa có gợi ý chắc chắn" : "Chưa có trên thị trường"}
                          </span>
                        )}
                      </div>
                    )}

                    {/* Nội dung tin nhắn */}
                    <div className="whitespace-pre-line text-sm">
                      {msg.text.split("\n").map((line, idx) => {
                        if (
                          line.startsWith("• ") ||
                          line.startsWith("🔹 ") ||
                          line.startsWith("🔸 ") ||
                          line.startsWith("🌟 ")
                        ) {
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

                    {/* Hiển thị thẻ sản phẩm tương tác */}
                    {allItemCards.length > 0 && (
                      <div className="mt-3.5 pt-3 border-t border-slate-100 dark:border-slate-700/60 space-y-2.5">
                        <div className="text-[11px] font-semibold text-slate-500 dark:text-slate-400 uppercase tracking-wider flex items-center justify-between">
                          <span>
                            {msg.result?.type === "recommendation"
                              ? "Sản phẩm AI gợi ý cho bạn:"
                              : msg.result?.matchedProducts && msg.result.matchedProducts.length > 0
                              ? "Sản phẩm tìm thấy:"
                              : "Sản phẩm gợi ý tương tự:"}
                          </span>
                          {msg.result?.type === "recommendation" && (
                            <span className="text-[10px] text-indigo-500 font-normal lowercase">
                              (đang có trên màn hình)
                            </span>
                          )}
                        </div>

                        <div className="grid grid-cols-1 gap-2">
                          {allItemCards.slice(0, 4).map((p) => (
                            <div
                              key={p.id}
                              className="flex items-center gap-3 p-2.5 bg-slate-50 dark:bg-slate-900/80 rounded-xl border border-slate-200/70 dark:border-slate-700/60 hover:border-indigo-400 dark:hover:border-indigo-500 transition group"
                            >
                              <img
                                src={p.imageUrl || "https://via.placeholder.com/100"}
                                alt={p.name}
                                className="w-14 h-14 object-cover rounded-lg shrink-0 border border-slate-200 dark:border-slate-700"
                              />
                              <div className="flex-1 min-w-0">
                                <Link
                                  to={`/products/${p.id}`}
                                  className="text-xs font-bold text-slate-800 dark:text-slate-100 hover:text-indigo-600 dark:hover:text-indigo-400 line-clamp-1 flex items-center gap-1"
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
                                  <span className="font-extrabold text-indigo-600 dark:text-indigo-400 text-xs">
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
                                  className="p-2 rounded-lg bg-indigo-600 hover:bg-indigo-700 text-white shrink-0 transition hover:scale-105 active:scale-95 disabled:opacity-50"
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
              );
            })}

            {isLoading && (
              <div className="flex items-center gap-2 p-3 bg-white dark:bg-slate-800 rounded-2xl border border-slate-200 dark:border-slate-700 max-w-[80%] shadow-sm">
                <Bot className="w-4 h-4 text-indigo-600 animate-spin" />
                <span className="text-xs text-slate-500 dark:text-slate-400 animate-pulse">
                  Đang đối chiếu dữ liệu kho & gợi ý cá nhân hóa...
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
                className="whitespace-nowrap text-[11px] px-2.5 py-1 rounded-full bg-slate-100 hover:bg-indigo-50 hover:text-indigo-600 dark:bg-slate-800 dark:hover:bg-slate-700 text-slate-600 dark:text-slate-300 border border-slate-200 dark:border-slate-700 transition"
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
              placeholder="Hỏi về sản phẩm, gợi ý sở thích, màn hình..."
              disabled={isLoading}
              className="flex-1 px-3.5 py-2.5 text-xs rounded-xl bg-slate-100 dark:bg-slate-800 border border-transparent focus:border-indigo-500 focus:bg-white dark:focus:bg-slate-900 text-slate-800 dark:text-slate-100 placeholder-slate-400 focus:outline-none transition"
            />
            <button
              onClick={() => handleSend()}
              disabled={!input.trim() || isLoading}
              className="p-2.5 bg-indigo-600 hover:bg-indigo-700 disabled:bg-slate-300 dark:disabled:bg-slate-700 text-white rounded-xl transition hover:scale-105 active:scale-95 disabled:scale-100 shadow-sm"
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
