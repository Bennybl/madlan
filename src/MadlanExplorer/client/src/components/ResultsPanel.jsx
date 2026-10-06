import DealDetailCard from "./DealDetailCard.jsx";
import QuerySteps from "./QuerySteps.jsx";

export default function ResultsPanel({ data }) {
  return (
    <section className="panel results-panel" aria-labelledby="results-heading">
      <h2 id="results-heading">טבלת התוצאות</h2>

      {!data ? (
        <p className="hint">שאלו שאלה בצ'אט למעלה כדי לראות כאן את שלבי הבדיקה והתוצאות המלאות.</p>
      ) : data.dealId ? (
        <>
          <p className="interpretation-line">השאלה: {data.prompt}</p>
          <DealDetailCard dealId={data.dealId} />
        </>
      ) : (
        <>
          <p className="interpretation-line">השאלה: {data.prompt}</p>
          <QuerySteps steps={data.steps} />
        </>
      )}
    </section>
  );
}
