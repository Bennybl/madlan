import { NUMBER_FORMAT } from "../format.js";

export default function CoverageCard({ dataset, error }) {
  return (
    <section className="panel" aria-labelledby="coverage-heading">
      <h2 id="coverage-heading">כיסוי המדגם</h2>
      {error && <p className="status-error">{error}</p>}
      {!error && !dataset && <p role="status">טוען נתוני כיסוי…</p>}
      {dataset && (
        <dl className="coverage-grid">
          <div>
            <dt>סה״כ עסקאות במדגם</dt>
            <dd>{NUMBER_FORMAT.format(dataset.dealCount)}</dd>
          </div>
          <div>
            <dt>עסקאות תקינות לחישוב</dt>
            <dd>{NUMBER_FORMAT.format(dataset.usableDealCount)}</dd>
          </div>
          <div>
            <dt>עסקאות עם דיווחים סותרים</dt>
            <dd>{NUMBER_FORMAT.format(dataset.conflictingDealCount)}</dd>
          </div>
          <div>
            <dt>דיווחים גולמיים שנטענו</dt>
            <dd>{NUMBER_FORMAT.format(dataset.reportCount)}</dd>
          </div>
        </dl>
      )}
    </section>
  );
}
