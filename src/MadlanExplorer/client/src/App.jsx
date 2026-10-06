import { useEffect, useState } from "react";
import { fetchJson } from "./api.js";
import CoverageCard from "./components/CoverageCard.jsx";
import ChatPanel from "./components/ChatPanel.jsx";
import ResultsPanel from "./components/ResultsPanel.jsx";
import ManualFiltersPanel from "./components/ManualFiltersPanel.jsx";
import DealLookupPanel from "./components/DealLookupPanel.jsx";
import DefinitionsPanel from "./components/DefinitionsPanel.jsx";

export default function App() {
  const [dataset, setDataset] = useState(null);
  const [datasetError, setDatasetError] = useState(null);
  const [chatResult, setChatResult] = useState(null);

  useEffect(() => {
    fetchJson("/api/dataset")
      .then(setDataset)
      .catch((err) => setDatasetError(err.message));
  }, []);

  return (
    <>
      <header className="page-header">
        <h1>חוקר עסקאות נדל"ן — מדלן</h1>
        <p className="subtitle">שאלו שאלה בשפה חופשית או סננו ידנית, עם הצגה מלאה של החשבון והראיות שמאחורי כל מספר.</p>
      </header>

      <main>
        <CoverageCard dataset={dataset} error={datasetError} />
        <ChatPanel onResult={setChatResult} />
        <ResultsPanel data={chatResult} />
        <ManualFiltersPanel suggestedFilters={null} dataset={dataset} />
        <DealLookupPanel />
        <DefinitionsPanel />
      </main>

      <footer className="page-footer">
        {dataset && <p>גרסת מדגם: {dataset.datasetHash}</p>}
      </footer>
    </>
  );
}
