import { NUMBER_FORMAT, formatCurrency, formatMetricValue, METRIC_LABELS, SINGLE_DEAL_METRICS, describeWarning, describeRankedMetric } from "../format.js";
import DealDetailCard from "./DealDetailCard.jsx";
import EvidenceTable from "./EvidenceTable.jsx";

export default function ResultMetrics({ result }) {
  if (!result) return null;

  if (result.transactionCount === 0) {
    return <p>לא נמצאו עסקאות התואמות לסינון שנבחר. נסו להרחיב את טווח החיפוש.</p>;
  }

  const requestedMetrics = result.requestedMetrics || [];
  const rankedMetrics = result.rankedMetrics || [];
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
