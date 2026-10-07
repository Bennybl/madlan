import { Fragment, useState } from "react";
import DealDetailCard from "./DealDetailCard.jsx";

export default function EvidenceTable({ dealIds, hasMoreEvidence }) {
  const [expandedDealId, setExpandedDealId] = useState(null);
  const [visible, setVisible] = useState(false);

  if (!dealIds || dealIds.length === 0) return null;

  return (
    <div className="evidence-panel">
      <button type="button" className="deal-detail-trigger" onClick={() => setVisible((v) => !v)}>
        {visible ? "הסתר עסקאות תומכות" : `הצג עסקאות תומכות (${dealIds.length})`}
      </button>

      {visible && (
        <>
          {hasMoreEvidence && (
            <p className="hint">
              יש עסקאות תומכות נוספות שלא מוצגות ברשימה זו; המדדים מחושבים על כל העסקאות התואמות, לא רק על הרשימה המוצגת.
            </p>
          )}
          <table>
            <caption className="visually-hidden">רשימת מזהי העסקאות שתומכות בתוצאה המחושבת</caption>
            <thead>
              <tr>
                <th scope="col">מזהה עסקה</th>
                <th scope="col">פירוט</th>
              </tr>
            </thead>
            <tbody>
              {dealIds.map((dealId) => (
                <Fragment key={dealId}>
                  <tr>
                    <td>{dealId}</td>
                    <td>
                      <button
                        type="button"
                        className="deal-detail-trigger"
                        onClick={() => setExpandedDealId((current) => (current === dealId ? null : dealId))}
                      >
                        {expandedDealId === dealId ? "הסתר פירוט" : "הצג פירוט"}
                      </button>
                    </td>
                  </tr>
                  {expandedDealId === dealId && (
                    <tr>
                      <td colSpan={2}>
                        <DealDetailCard dealId={dealId} />
                      </td>
                    </tr>
                  )}
                </Fragment>
              ))}
            </tbody>
          </table>
        </>
      )}
    </div>
  );
}
