import { describeFilters } from "../format.js";
import ResultMetrics from "./ResultMetrics.jsx";

export default function ResultsPanel({ data }) {
  return (
    <section className="panel results-panel" aria-labelledby="results-heading">
      <h2 id="results-heading">טבלת התוצאות</h2>

      {!data ? (
        <p className="hint">שאלו שאלה בצ'אט למעלה כדי לראות כאן את טבלת התוצאות המלאה שלה.</p>
      ) : (
        <>
          <p className="interpretation-line">השאלה: {data.prompt}</p>
          <p className="hint">איך הבנו את השאלה: {describeFilters(data.filters)}</p>
          <ResultMetrics result={data.result} />
        </>
      )}
    </section>
  );
}
