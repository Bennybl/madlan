import { NUMBER_FORMAT, describeDataQuery, formatFieldValue, formatGroupValue } from "../format.js";

function StepRow({ row, field, groupBy }) {
  return (
    <tr>
      <td>{row.groupValue != null ? formatGroupValue(groupBy, row.groupValue) : "—"}</td>
      <td>{formatFieldValue(field, row.value)}</td>
      <td>{row.dealId || "—"}</td>
      {(row.lowerBound != null || row.upperBound != null) && (
        <td>
          {row.lowerBound != null && row.upperBound != null
            ? `${formatFieldValue(field, row.lowerBound)} – ${formatFieldValue(field, row.upperBound)}`
            : "—"}
        </td>
      )}
    </tr>
  );
}

export default function QuerySteps({ steps }) {
  if (!steps || steps.length === 0) return null;

  return (
    <div className="query-steps">
      <h4>שלבי הבדיקה</h4>
      <p className="hint">הנתון הבא מציג כל שאילתה שהמערכת ביצעה בפועל כדי להגיע לתשובה, לפי הסדר.</p>
      {steps.map((step, index) => {
        const hasBounds = step.result.rows.some((row) => row.lowerBound != null);
        return (
          <article className="query-step" key={index}>
            <h5>
              שלב {index + 1}: {describeDataQuery(step.query)}
            </h5>
            <p className="metric-count">{NUMBER_FORMAT.format(step.result.transactionCount)} עסקאות תואמות לסינון</p>
            {step.result.rows.length > 0 && (
              <div className="query-step-table-wrap">
                <table className="query-step-table">
                  <thead>
                    <tr>
                      <th>קבוצה</th>
                      <th>ערך</th>
                      <th>עסקה</th>
                      {hasBounds && <th>טווח טיפוסי</th>}
                    </tr>
                  </thead>
                  <tbody>
                    {step.result.rows.map((row, rowIndex) => (
                      <StepRow key={rowIndex} row={row} field={step.query.field} groupBy={step.query.groupBy} />
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </article>
        );
      })}
    </div>
  );
}
