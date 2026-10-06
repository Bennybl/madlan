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

  if (response.status === "deal") {
    return (
      <div className="chat-message chat-message-assistant">
        <div className="chat-bubble chat-bubble-assistant">
          <p className="verified-summary">נמצאה עסקה {response.dealId}. פרטי העסקה המלאים מוצגים בטבלת התוצאות מתחת לצ'אט.</p>
        </div>
      </div>
    );
  }

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
        {response.summary ? (
          <p className="verified-summary">{response.summary}</p>
        ) : (
          <p className="hint">{response.message || "סיכום מאומת אינו זמין כעת. ראו את שלבי הבדיקה בטבלת התוצאות מתחת לצ'אט."}</p>
        )}

        <p className="hint">שלבי הבדיקה והנתונים המלאים מוצגים בטבלת התוצאות מתחת לצ'אט.</p>
      </div>
    </div>
  );
}
