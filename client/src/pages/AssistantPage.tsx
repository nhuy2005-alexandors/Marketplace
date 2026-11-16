import { useEffect, useRef, useState } from "react";
import { Bot, Send, Sparkles } from "lucide-react";
import { useAssistantChat } from "../api/hooks";
import { apiError } from "../api/client";
import { useAuth } from "../store/auth";
import type { AssistantChatResponse, AssistantConversationTurn } from "../types";
import { ProductCard } from "../components/ProductCard";
import { Button, Card, Input, PageHeader } from "../components/ui";

type ChatMessage = {
  id: number;
  role: "user" | "assistant";
  content: string;
  response?: AssistantChatResponse;
};

const ANSWER_SOURCE_LABELS: Record<string, string> = {
  generated: "Gemini chọn sản phẩm · dữ liệu catalog",
  fallback: "Backend-grounded catalog fallback",
  "no-evidence": "Không tìm thấy bằng chứng",
  recommendation: "Recommendation Engine",
};

export function AssistantPage() {
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [input, setInput] = useState("");
  const [error, setError] = useState("");
  const nextMessageId = useRef(1);
  const conversationId = useRef<string | undefined>(undefined);
  const requestController = useRef<AbortController | null>(null);
  const messagesEndRef = useRef<HTMLDivElement | null>(null);
  const user = useAuth((state) => state.user);
  const token = useAuth((state) => state.token);
  const identity = `${user?.id ?? "anonymous"}:${token ?? ""}`;
  const identityRef = useRef(identity);
  identityRef.current = identity;
  const previousIdentity = useRef(identity);
  const isCustomer = user?.role === "Customer";
  const chat = useAssistantChat();

  useEffect(() => {
    if (previousIdentity.current === identity) return;
    previousIdentity.current = identity;
    requestController.current?.abort();
    requestController.current = null;
    conversationId.current = undefined;
    nextMessageId.current = 1;
    setMessages([]);
    setInput("");
    setError("");
  }, [identity]);

  useEffect(() => () => requestController.current?.abort(), []);

  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: "smooth", block: "end" });
  }, [messages, chat.isPending]);

  const submit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const query = input.trim();
    if (!query || chat.isPending) return;

    const recentTurns: AssistantConversationTurn[] = messages
      .slice(-6)
      .map((message) => ({
        role: message.role,
        content: message.content.slice(0, 500),
      }));

    const userMessage: ChatMessage = {
      id: nextMessageId.current++,
      role: "user",
      content: query,
    };

    setError("");
    setMessages((current) => [...current, userMessage]);
    setInput("");
    const requestIdentity = identity;
    const controller = new AbortController();
    requestController.current?.abort();
    requestController.current = controller;

    try {
      const response = await chat.mutateAsync({
        body: { query, conversation: { conversationId: conversationId.current, recentTurns } },
        signal: controller.signal,
      });
      if (controller.signal.aborted || requestIdentity !== identityRef.current) return;
      conversationId.current = response.conversationId ?? conversationId.current;
      const assistantMessage: ChatMessage = {
        id: nextMessageId.current++,
        role: "assistant",
        content: response.answer,
        response,
      };
      setMessages((current) => [...current, assistantMessage]);
    } catch (requestError) {
      if (controller.signal.aborted || requestIdentity !== identityRef.current) return;
      setError(apiError(requestError, "Không thể kết nối với trợ lý. Vui lòng thử lại."));
    } finally {
      if (requestController.current === controller) requestController.current = null;
    }
  };

  return (
    <div className="max-w-5xl mx-auto px-4 py-6 animate-fade-in">
      <PageHeader
        title="Trợ lý mua sắm"
        subtitle="Hỏi về sản phẩm, thương hiệu, thông số kỹ thuật và mức giá trong catalog."
      />

      {!isCustomer && (
        <div className="rounded-xl bg-brand-50 dark:bg-brand-900/20 text-brand-800 dark:text-brand-200 px-4 py-3 text-sm mb-4">
          Bạn vẫn có thể tra cứu catalog. Đăng nhập bằng tài khoản khách hàng để nhận gợi ý cá nhân hóa.
        </div>
      )}

      <Card className="p-4">
        <div className="min-h-[45vh] max-h-[65vh] overflow-y-auto space-y-4 pr-1" aria-live="polite">
          {messages.length === 0 && (
            <div className="min-h-[40vh] flex flex-col items-center justify-center text-center px-4">
              <Sparkles className="w-10 h-10 text-brand-500 mb-3" aria-hidden />
              <h2 className="font-semibold text-lg">Bạn đang tìm sản phẩm nào?</h2>
              <p className="muted text-sm mt-1 max-w-md">
                Mô tả nhu cầu hoặc hỏi về thuộc tính, thương hiệu và ngân sách. Câu trả lời sẽ dựa trên catalog backend.
              </p>
            </div>
          )}

          {messages.map((message) => (
            <div
              key={message.id}
              className={"flex " + (message.role === "user" ? "justify-end" : "justify-start")}
            >
              <div
                className={
                  "w-full max-w-3xl rounded-2xl p-4 " +
                  (message.role === "user"
                    ? "bg-brand-600 text-white"
                    : "surface")
                }
              >
                <div className="flex items-center gap-2 mb-2 text-xs font-semibold">
                  {message.role === "assistant" && <Bot className="w-4 h-4" aria-hidden />}
                  {message.role === "user" ? "Bạn" : "Trợ lý"}
                </div>
                <p className="text-sm whitespace-pre-wrap">{message.content}</p>

                {message.response && (
                  <>
                    <div className="flex flex-wrap gap-x-3 gap-y-1 mt-3 text-[11px] muted">
                      <span>{ANSWER_SOURCE_LABELS[message.response.answerSource] ?? message.response.answerSource}</span>
                      <span>Retrieval: {message.response.retrievalMode}</span>
                      <span>Evidence: {message.response.evidenceStrength}</span>
                    </div>
                    {message.response.products.length > 0 && (
                      <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 mt-4">
                        {message.response.products.map((product) => (
                          <ProductCard key={product.id} product={product} />
                        ))}
                      </div>
                    )}
                  </>
                )}
              </div>
            </div>
          ))}

          {chat.isPending && (
            <div className="flex justify-start">
              <div className="surface rounded-2xl px-4 py-3 text-sm muted">
                Đang tìm sản phẩm và đối chiếu bằng chứng...
              </div>
            </div>
          )}
          <div ref={messagesEndRef} />
        </div>

        {error && <p role="alert" className="text-rose-500 text-sm mt-3">{error}</p>}

        <form onSubmit={submit} className="flex gap-2 mt-4">
          <Input
            value={input}
            onChange={(event) => setInput(event.target.value)}
            maxLength={1000}
            placeholder="Ví dụ: tìm tai nghe chống ồn dưới 100 USD..."
            aria-label="Câu hỏi cho trợ lý mua sắm"
            disabled={chat.isPending}
          />
          <Button type="submit" disabled={!input.trim() || chat.isPending}>
            <Send className="w-4 h-4" aria-hidden />
            Gửi
          </Button>
        </form>
        <p className="muted text-xs mt-2">
          Trợ lý gửi tối đa 6 lượt hội thoại gần nhất để giữ ngữ cảnh. Thông tin sản phẩm lấy từ catalog hiện tại.
        </p>
      </Card>
    </div>
  );
}
