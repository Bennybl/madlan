import { useState } from "react";
import DealDetailCard from "./DealDetailCard.jsx";

export default function DealLookupPanel() {
  const [dealId, setDealId] = useState("");
  const [lookedUpDealId, setLookedUpDealId] = useState(null);
  const [validationMessage, setValidationMessage] = useState("");

  function handleSubmit(event) {
    event.preventDefault();
    const trimmed = dealId.trim();
    if (!trimmed) {
      setValidationMessage("יש להזין מזהה עסקה.");
      setLookedUpDealId(null);
      return;
    }
    setValidationMessage("");
    setLookedUpDealId(trimmed);
  }

  return (
    <section className="panel" aria-labelledby="lookup-heading">
      <h2 id="lookup-heading">בדיקת עסקה לפי מזהה</h2>
      <p className="hint">
        שימושי כשלקוח מציין מספר עסקה ספציפי, כולל עסקאות עם דיווחים סותרים שאינן מופיעות בתוצאות הסינון.
      </p>
      <form onSubmit={handleSubmit}>
        <div className="field">
          <label htmlFor="deal-lookup-id">מזהה עסקה</label>
          <input id="deal-lookup-id" type="text" value={dealId} onChange={(event) => setDealId(event.target.value)} />
        </div>
        <div className="actions">
          <button type="submit">בדוק עסקה</button>
        </div>
      </form>
      {validationMessage && <p className="status-error">{validationMessage}</p>}
      {lookedUpDealId && <DealDetailCard key={lookedUpDealId} dealId={lookedUpDealId} />}
    </section>
  );
}
