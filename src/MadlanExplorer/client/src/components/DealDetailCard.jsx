import { useEffect, useState } from "react";
import { fetchJson } from "../api.js";
import { describeLocalityMethod, formatDealDate } from "../format.js";

function ReportFields({ report }) {
  let normalized = null;
  try {
    normalized = JSON.parse(report.normalizedJson);
  } catch {
    normalized = null;
  }

  let qualityFlags = [];
  try {
    qualityFlags = JSON.parse(report.qualityFlagsJson) || [];
  } catch {
    qualityFlags = [];
  }

  if (!normalized) return null;

  return (
    <div className="report-detail">
      <strong>דיווח מקור #{report.sourceRowNumber}</strong>
      <dl>
        <dt>עיר (לאחר נרמול)</dt>
        <dd>{normalized.City || "—"}</dd>
        {normalized.Locality && (
          <>
            <dt>מקור שם העיר</dt>
            <dd>{normalized.Locality.OriginalValue || "—"}</dd>
            <dt>שיטת תיקון שם העיר</dt>
            <dd>{describeLocalityMethod(normalized.Locality.Method)}</dd>
          </>
        )}
        <dt>שכונה</dt>
        <dd>{normalized.Neighborhood || "—"}</dd>
        <dt>סוג נכס</dt>
        <dd>{normalized.PropertyType || "—"}</dd>
        <dt>חדרים</dt>
        <dd>{normalized.Rooms ?? "—"}</dd>
        <dt>שטח (מ"ר)</dt>
        <dd>{normalized.SizeSqm ?? "—"}</dd>
        <dt>קומה</dt>
        <dd>{normalized.Floor ?? "—"}</dd>
        <dt>שנת בנייה</dt>
        <dd>{normalized.YearBuilt ?? "—"}</dd>
        <dt>מצב</dt>
        <dd>{normalized.Condition || "—"}</dd>
        <dt>מחיר (₪)</dt>
        <dd>{normalized.PriceNis ?? "—"}</dd>
        <dt>תאריך עסקה</dt>
        <dd>{formatDealDate(normalized)}</dd>
        <dt>מקור הדיווח</dt>
        <dd>{normalized.Source || "—"}</dd>
        {qualityFlags.length > 0 && (
          <>
            <dt>דגלי איכות</dt>
            <dd>{qualityFlags.join(", ")}</dd>
          </>
        )}
      </dl>
    </div>
  );
}

export default function DealDetailCard({ dealId }) {
  const [detail, setDetail] = useState(null);
  const [error, setError] = useState(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);
    setDetail(null);

    fetchJson("/api/deals/" + encodeURIComponent(dealId))
      .then((data) => {
        if (!cancelled) setDetail(data);
      })
      .catch((err) => {
        if (!cancelled) setError(err.message);
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [dealId]);

  if (loading) return <p className="hint">טוען פרטי עסקה…</p>;
  if (error) return <p className="status-error">{error}</p>;
  if (!detail) return null;

  return (
    <div className="report-detail">
      <dl>
        <dt>סטטוס</dt>
        <dd>{detail.conflictStatus === "usable" ? "תקינה" : "דיווחים סותרים"}</dd>
        <dt>מספר דיווחים</dt>
        <dd>{detail.reportCount}</dd>
        <dt>דיווחים שונים זה מזה</dt>
        <dd>{detail.distinctReportCount}</dd>
      </dl>
      {detail.reports.map((report) => (
        <ReportFields key={report.reportId} report={report} />
      ))}
    </div>
  );
}
