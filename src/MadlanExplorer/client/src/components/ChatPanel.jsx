import { useEffect, useRef, useState } from "react";
import { fetchJson } from "../api.js";
import MessageBubble from "./MessageBubble.jsx";
import ThinkingBubble from "./ThinkingBubble.jsx";

const ASK_TIMEOUT_MS = 320000;

const EXAMPLE_QUESTIONS = [
  "מה המחיר החציוני של דירת 4 חדרים בחולון ב-2025?",
  "כמה עסקאות דירות נמכרו בתל אביב?",
  "מה הדירה היקרה ביותר מבין כל הדירות?",
  "כמה תשתלם לי הדירה שלי בעוד שנה?"
];

export default function ChatPanel({ onFiltersSuggested, onResult }) {
  const [messages, setMessages] = useState([]);
  const [question, setQuestion] = useState("");
  const [thinking, setThinking] = useState(false);
  const scrollRef = useRef(null);

  useEffect(() => {
    if (scrollRef.current) {
      scrollRef.current.scrollTop = scrollRef.current.scrollHeight;
    }
  }, [messages, thinking]);

  function buildPromptWithClarificationContext(trimmed) {
    const last = messages[messages.length - 1];
    if (!last || last.role !== "assistant" || last.response?.status !== "clarification") {
      return trimmed;
    }

    const previousUser = [...messages].reverse().find((m) => m.role === "user");
    if (!previousUser) {
      return trimmed;
    }

    return `${previousUser.text} (המערכת שאלה הבהרה: "${last.response.message}"; תשובת המשתמש: "${trimmed}")`;
  }

  async function submit(text) {
    const trimmed = text.trim();
    if (!trimmed || thinking) return;

    const promptToSend = buildPromptWithClarificationContext(trimmed);

    setMessages((prev) => [...prev, { id: crypto.randomUUID(), role: "user", text: trimmed }]);
    setQuestion("");
    setThinking(true);

    try {
      const response = await fetchJson(
        "/api/ask",
        {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ prompt: promptToSend })
        },
        ASK_TIMEOUT_MS
      );

      setMessages((prev) => [...prev, { id: crypto.randomUUID(), role: "assistant", response }]);

      if (response.status === "query" && response.filters && onFiltersSuggested) {
        onFiltersSuggested(response.filters);
      }

      if (response.status === "query" && response.result && onResult) {
        onResult({ prompt: trimmed, filters: response.filters, result: response.result });
      }

      if (response.status === "deal" && response.dealId && onResult) {
        onResult({ prompt: trimmed, dealId: response.dealId });
      }
    } catch (error) {
      setMessages((prev) => [...prev, { id: crypto.randomUUID(), role: "assistant", error: error.message }]);
    } finally {
      setThinking(false);
    }
  }

  function handleSubmit(event) {
    event.preventDefault();
    submit(question);
  }

  return (
    <section className="panel chat-panel" aria-labelledby="chat-heading">
      <h2 id="chat-heading">שאלו בשפה חופשית</h2>
      <p className="hint">
        המערכת מאמתת את השאלה מול הנתונים ומציגה תשובה מבוססת ראיות. אם משהו נכשל, הסינון הידני למטה תמיד זמין.
      </p>

      <div className="chat-messages" ref={scrollRef} role="log" aria-live="polite">
        {messages.length === 0 && <p className="hint">שאלו שאלה על מדגם העסקאות, למשל:</p>}
        {messages.map((message) => (
          <MessageBubble key={message.id} message={message} />
        ))}
        {thinking && <ThinkingBubble />}
      </div>

      <div className="example-questions">
        <span className="hint">דוגמאות:</span>
        {EXAMPLE_QUESTIONS.map((example) => (
          <button
            key={example}
            type="button"
            className="example-question"
            disabled={thinking}
            onClick={() => submit(example)}
          >
            {example}
          </button>
        ))}
      </div>

      <form onSubmit={handleSubmit} className="chat-input-form">
        <label htmlFor="chat-question" className="visually-hidden">השאלה שלכם</label>
        <textarea
          id="chat-question"
          rows={2}
          value={question}
          disabled={thinking}
          onChange={(event) => setQuestion(event.target.value)}
          onKeyDown={(event) => {
            if (event.key === "Enter" && !event.shiftKey) {
              event.preventDefault();
              submit(question);
            }
          }}
        />
        <button type="submit" disabled={thinking}>
          {thinking ? "מעבד…" : "שאל"}
        </button>
      </form>
    </section>
  );
}
