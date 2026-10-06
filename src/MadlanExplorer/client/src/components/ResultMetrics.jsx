import { NUMBER_FORMAT, formatCurrency, formatMetricValue, METRIC_LABELS, SINGLE_DEAL_METRICS, describeWarning, describeRankedMetric, GROUP_BY_LABELS, OUTLIER_FIELD_LABELS, formatOutlierValue } from "../format.js";
import DealDetailCard from "./DealDetailCard.jsx";
import EvidenceTable from "./EvidenceTable.jsx";

export default function ResultMetrics({ result }) {
  if (!result) return null;

  if (result.transactionCount === 0) {
    return <p>לא נמצאו עסקאות התואמות לסינון שנבחר. נסו להרחיב את טווח החיפוש.</p>;
  }

  const requestedMetrics = result.requestedMetrics || [];
  const rankedMetrics = result.rankedMetrics || [];
  const groups = result.groups || [];
  const outliers = result.outliers || [];
  const singleDealMetrics = requestedMetrics.filter((m) => SINGLE_DEAL_METRICS.has(m.metric) && m.dealId);

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
        {requestedMetrics.map((metricResult) => (
          <article className="metric-card" key={metricResult.metric}>
            <h3>{METRIC_LABELS[metricResult.metric] || metricResult.metric}</h3>
            <p className="metric-value">{formatMetricValue(metricResult.metric, metricResult.value)}</p>
            {metricResult.dealId && <p className="metric-count">עסקה: {metricResult.dealId}</p>}
          </article>
        ))}
        {rankedMetrics.map((rankedResult) => (
          <article className="metric-card" key={`${rankedResult.metric}-${rankedResult.rank}`}>
            <h3>{describeRankedMetric(rankedResult.metric, rankedResult.rank)}</h3>
            <p className="metric-value">{formatMetricValue(rankedResult.metric, rankedResult.value)}</p>
            {rankedResult.dealId && <p className="metric-count">עסקה: {rankedResult.dealId}</p>}
          </article>
        ))}
      </div>

      {(singleDealMetrics.length > 0 || rankedMetrics.some((r) => r.dealId)) && (
        <div className="single-deal-detail">
          <h4>פרטי העסקה המבוקשת</h4>
          {singleDealMetrics.map((metricResult) => (
            <div key={metricResult.metric}>
              <p className="hint">{METRIC_LABELS[metricResult.metric] || metricResult.metric}</p>
              <DealDetailCard dealId={metricResult.dealId} />
            </div>
          ))}
          {rankedMetrics.filter((r) => r.dealId).map((rankedResult) => (
            <div key={`${rankedResult.metric}-${rankedResult.rank}`}>
              <p className="hint">{describeRankedMetric(rankedResult.metric, rankedResult.rank)}</p>
              <DealDetailCard dealId={rankedResult.dealId} />
            </div>
          ))}
        </div>
      )}

      {result.groupBy && groups.length > 0 && (
        <div className="grouped-results">
          <h4>פילוח לפי {GROUP_BY_LABELS[result.groupBy] || result.groupBy}</h4>
          <div className="grouped-results-list">
            {groups.map((group) => (
              <article className="group-card" key={group.groupValue}>
                <h5>{group.groupValue}</h5>
                <p className="metric-count">{NUMBER_FORMAT.format(group.transactionCount)} עסקאות</p>
                {(group.requestedMetrics || []).map((metricResult) => (
                  <p key={metricResult.metric} className="group-metric-line">
                    {METRIC_LABELS[metricResult.metric] || metricResult.metric}: {formatMetricValue(metricResult.metric, metricResult.value)}
                    {metricResult.dealId && ` (עסקה: ${metricResult.dealId})`}
                  </p>
                ))}
                {(group.rankedMetrics || []).map((rankedResult) => (
                  <p key={`${rankedResult.metric}-${rankedResult.rank}`} className="group-metric-line">
                    {describeRankedMetric(rankedResult.metric, rankedResult.rank)}: {formatMetricValue(rankedResult.metric, rankedResult.value)}
                    {rankedResult.dealId && ` (עסקה: ${rankedResult.dealId})`}
                  </p>
                ))}
              </article>
            ))}
          </div>
        </div>
      )}

      {result.outlierField && (
        <div className="outliers-panel">
          <h4>חריגים — {OUTLIER_FIELD_LABELS[result.outlierField] || result.outlierField}</h4>
          <p className="hint">
            חריג מוגדר כאן כערך הרחוק מהטווח הטיפוסי (שיטת הטווח הבין-רבעוני, IQR) בתוך כל קבוצה
            {result.groupBy ? ` (${GROUP_BY_LABELS[result.groupBy] || result.groupBy})` : ""}; זו אינה בדיקה סטטיסטית
            פורמלית של התאמה להתפלגות נורמלית.
          </p>
          {outliers.length === 0 ? (
            <p className="hint">לא נמצאו חריגים במדגם התואם.</p>
          ) : (
            <ul className="outliers-list">
              {outliers.map((outlier) => (
                <li key={`${outlier.groupValue ?? "all"}-${outlier.dealId}`}>
                  {outlier.groupValue && <strong>{outlier.groupValue}: </strong>}
                  עסקה {outlier.dealId} — {formatOutlierValue(result.outlierField, outlier.value)}
                  {" "}(טווח טיפוסי: {formatOutlierValue(result.outlierField, outlier.lowerBound)}
                  {" "}עד {formatOutlierValue(result.outlierField, outlier.upperBound)})
                </li>
              ))}
            </ul>
          )}
        </div>
      )}

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
