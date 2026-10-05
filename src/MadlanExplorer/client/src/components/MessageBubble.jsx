import { describeFilters } from "../format.js";
import ResultMetrics from "./ResultMetrics.jsx";

export default function MessageBubble({ message }) {
  if (message.role === "user") {
    return (
      <div className="chat-message chat-message-user">
        <div className="chat-bubble chat-bubble-user">{message.text}</div>
      </div>
    );
  }

  if (message.error) {
    return (
      <div className="chat-message chat-message-assistant">
        <div className="chat-bubble chat-bubble-assistant chat-bubble-error">{message.error}</div>
      </div>
    );
  }

  const response = message.response;
  if (!response) return null;

  if (response.status !== "query") {
    return (
      <div className="chat-message chat-message-assistant">
        <div className="chat-bubble chat-bubble-assistant">
          {response.message || "השאלה דורשת הבהרה נוספת. נסו לנסח אותה מחדש או השתמשו בסינון הידני למטה."}
        </div>
      </div>
    );
  }

  return (
    <div className="chat-message chat-message-assistant">
      <div className="chat-bubble chat-bubble-assistant">
        <p className="interpretation-line">איך הבנו את השאלה: {describeFilters(response.filters)}</p>

        {response.summary ? (
          <p className="verified-summary">{response.summary}</p>
        ) : (
          <p className="hint">{response.message || "סיכום מאומת אינו זמין כעת. מוצגות התוצאות המחושבות בלבד."}</p>
        )}

        <ResultMetrics result={response.result} />
      </div>
    </div>
  );
}
