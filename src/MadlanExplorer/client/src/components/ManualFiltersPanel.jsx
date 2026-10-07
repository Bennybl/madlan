import { useEffect, useState } from "react";
import { fetchJson } from "../api.js";
import ResultMetrics from "./ResultMetrics.jsx";

const EMPTY_FORM = {
  city: "",
  neighborhood: "",
  propertyType: "",
  minimumRooms: "",
  maximumRooms: "",
  startDate: "",
  endDate: ""
};

function toFormValues(filters) {
  if (!filters) return EMPTY_FORM;
  return {
    city: filters.city || "",
    neighborhood: filters.neighborhood || "",
    propertyType: filters.propertyType || "",
    minimumRooms: filters.minimumRooms ?? "",
    maximumRooms: filters.maximumRooms ?? "",
    startDate: filters.startDate || "",
    endDate: filters.endDate || ""
  };
}

function toRequestFilters(form) {
  const filters = {};
  if (form.city.trim()) filters.city = form.city.trim();
  if (form.neighborhood.trim()) filters.neighborhood = form.neighborhood.trim();
  if (form.propertyType.trim()) filters.propertyType = form.propertyType.trim();
  if (form.minimumRooms !== "") filters.minimumRooms = Number(form.minimumRooms);
  if (form.maximumRooms !== "") filters.maximumRooms = Number(form.maximumRooms);
  if (form.startDate) filters.startDate = form.startDate;
  if (form.endDate) filters.endDate = form.endDate;
  return filters;
}

export default function ManualFiltersPanel({ suggestedFilters, dataset }) {
  const [form, setForm] = useState(EMPTY_FORM);
  const [result, setResult] = useState(null);
  const [error, setError] = useState(null);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (suggestedFilters) {
      setForm(toFormValues(suggestedFilters));
    }
  }, [suggestedFilters]);

  function updateField(field, value) {
    setForm((prev) => ({ ...prev, [field]: value }));
  }

  async function handleSubmit(event) {
    event.preventDefault();
    if (loading) return;
    setLoading(true);
    setError(null);

    try {
      const response = await fetchJson("/api/query", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(toRequestFilters(form))
      });
      setResult(response.result);
    } catch (err) {
      setResult(null);
      setError(err.message);
    } finally {
      setLoading(false);
    }
  }

  function handleClear() {
    setForm(EMPTY_FORM);
    setResult(null);
    setError(null);
  }

  return (
    <section className="panel" aria-labelledby="filters-heading">
      <h2 id="filters-heading">סינון ידני</h2>
      <p className="hint">
        הסינון הידני עובד תמיד, גם אם מנוע השאלות החכם אינו זמין. עריכת הסינון ולחיצה על "חפש" תמיד מריצה חיפוש ישיר על
        הנתונים, ללא תשובה מאומתת.
      </p>

      <form onSubmit={handleSubmit}>
        <div className="field-grid">
          <div className="field">
            <label htmlFor="filter-city">עיר</label>
            <input
              id="filter-city"
              type="text"
              list="city-options"
              value={form.city}
              onChange={(event) => updateField("city", event.target.value)}
            />
            <datalist id="city-options">
              {(dataset?.cities || []).map((city) => (
                <option key={city} value={city} />
              ))}
            </datalist>
          </div>
          <div className="field">
            <label htmlFor="filter-neighborhood">שכונה</label>
            <input
              id="filter-neighborhood"
              type="text"
              list="neighborhood-options"
              value={form.neighborhood}
              onChange={(event) => updateField("neighborhood", event.target.value)}
            />
            <datalist id="neighborhood-options">
              {(dataset?.neighborhoods || []).map((neighborhood) => (
                <option key={neighborhood} value={neighborhood} />
              ))}
            </datalist>
          </div>
          <div className="field">
            <label htmlFor="filter-property-type">סוג נכס</label>
            <input
              id="filter-property-type"
              type="text"
              list="property-type-options"
              value={form.propertyType}
              onChange={(event) => updateField("propertyType", event.target.value)}
            />
            <datalist id="property-type-options">
              {(dataset?.propertyTypes || []).map((propertyType) => (
                <option key={propertyType} value={propertyType} />
              ))}
            </datalist>
          </div>
          <div className="field">
            <label htmlFor="filter-min-rooms">חדרים מינימום</label>
            <input
              id="filter-min-rooms"
              type="number"
              min="0"
              step="0.5"
              value={form.minimumRooms}
              onChange={(event) => updateField("minimumRooms", event.target.value)}
            />
          </div>
          <div className="field">
            <label htmlFor="filter-max-rooms">חדרים מקסימום</label>
            <input
              id="filter-max-rooms"
              type="number"
              min="0"
              step="0.5"
              value={form.maximumRooms}
              onChange={(event) => updateField("maximumRooms", event.target.value)}
            />
          </div>
          <div className="field">
            <label htmlFor="filter-start-date">מתאריך</label>
            <input
              id="filter-start-date"
              type="date"
              value={form.startDate}
              onChange={(event) => updateField("startDate", event.target.value)}
            />
          </div>
          <div className="field">
            <label htmlFor="filter-end-date">עד תאריך</label>
            <input
              id="filter-end-date"
              type="date"
              value={form.endDate}
              onChange={(event) => updateField("endDate", event.target.value)}
            />
          </div>
        </div>
        <div className="actions">
          <button type="submit" disabled={loading}>{loading ? "מחפש…" : "חפש"}</button>
          <button type="button" onClick={handleClear} disabled={loading}>נקה סינון</button>
        </div>
      </form>

      {error && <p className="status-error">{error}</p>}
      {!error && result && <ResultMetrics result={result} />}
    </section>
  );
}
