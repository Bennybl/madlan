import { NUMBER_FORMAT, formatCurrency, describeWarning } from "../format.js";
import EvidenceTable from "./EvidenceTable.jsx";

export default function ResultMetrics({ result }) {
  if (!result) return null;

  if (result.transactionCount === 0) {
    return <p>לא נמצאו עסקאות התואמות לסינון שנבחר. נסו להרחיב את טווח החיפוש.</p>;
  }

  return (
    <div className="result-metrics">
      <div className="metric-grid">
        <article className="metric-card">
          <h3>מספר עסקאות</h3>
          <p className="metric-value">{NUMBER_FORMAT.format(result.transactionCount)}</p>
        </article>
        <article className="metric-card">
          <h3>מחיר חציוני (₪)</h3>
          <p className="metric-value">{formatCurrency(result.medianPriceNis)}</p>
          <p className="metric-count">מבוסס על {NUMBER_FORMAT.format(result.priceContributorCount)} עסקאות עם מחיר תקין</p>
        </article>
        <article className="metric-card">
          <h3>מחיר למ"ר חציוני (₪)</h3>
          <p className="metric-value">{formatCurrency(result.medianPricePerSqm)}</p>
          <p className="metric-count">מבוסס על {NUMBER_FORMAT.format(result.pricePerSqmContributorCount)} עסקאות עם מחיר ושטח תקינים</p>
        </article>
      </div>

      {result.warnings && result.warnings.length > 0 && (
        <div className="warnings-panel">
          <h4>אזהרות</h4>
          <ul>
            {result.warnings.map((warning) => (
              <li key={warning}>{describeWarning(warning)}</li>
            ))}
          </ul>
        </div>
      )}

      <EvidenceTable dealIds={result.contributorDealIds} hasMoreEvidence={result.hasMoreEvidence} />
    </div>
  );
}
