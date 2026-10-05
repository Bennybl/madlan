(function () {
  "use strict";

  const NETWORK_ERROR_MESSAGE = "לא ניתן היה להתחבר לשרת. בדקו את החיבור ונסו שוב.";
  const NUMBER_FORMAT = new Intl.NumberFormat("he-IL");

  const dealDetailCache = new Map();

  function byId(id) {
    return document.getElementById(id);
  }

  async function fetchJson(url, options) {
    let response;
    try {
      response = await fetch(url, options);
    } catch {
      throw new Error(NETWORK_ERROR_MESSAGE);
    }

    let body = null;
    try {
      body = await response.json();
    } catch {
      body = null;
    }

    if (!response.ok) {
      const message = body && typeof body.message === "string" ? body.message : NETWORK_ERROR_MESSAGE;
      throw new Error(message);
    }

    return body;
  }

  function clearChildren(element) {
    while (element.firstChild) {
      element.removeChild(element.firstChild);
    }
  }

  function addCoverageEntry(dl, label, value) {
    const dt = document.createElement("dt");
    dt.textContent = label;
    const dd = document.createElement("dd");
    dd.textContent = value;
    dl.append(dt, dd);
  }

  function populateDatalist(listId, values) {
    const list = byId(listId);
    clearChildren(list);
    for (const value of values) {
      const option = document.createElement("option");
      option.value = value;
      list.append(option);
    }
  }

  async function loadCoverage() {
    const status = byId("coverage-status");
    const list = byId("coverage-list");
    try {
      const dataset = await fetchJson("/api/dataset");
      clearChildren(list);
      addCoverageEntry(list, "סה״כ עסקאות במדגם", NUMBER_FORMAT.format(dataset.dealCount));
      addCoverageEntry(list, "עסקאות תקינות לחישוב", NUMBER_FORMAT.format(dataset.usableDealCount));
      addCoverageEntry(list, "עסקאות עם דיווחים סותרים", NUMBER_FORMAT.format(dataset.conflictingDealCount));
      addCoverageEntry(list, "דיווחים גולמיים שנטענו", NUMBER_FORMAT.format(dataset.reportCount));
      list.hidden = false;
      status.hidden = true;

      populateDatalist("city-options", dataset.cities);
      populateDatalist("neighborhood-options", dataset.neighborhoods);
      populateDatalist("property-type-options", dataset.propertyTypes);

      const footer = byId("dataset-footer-note");
      footer.textContent = "גרסת מדגם: " + dataset.datasetHash;
    } catch (error) {
      status.textContent = error.message;
      status.classList.add("status-error");
    }
  }

  function readFilters() {
    const filters = {};
    const city = byId("filter-city").value.trim();
    const neighborhood = byId("filter-neighborhood").value.trim();
    const propertyType = byId("filter-property-type").value.trim();
    const minimumRooms = byId("filter-min-rooms").value;
    const maximumRooms = byId("filter-max-rooms").value;
    const startDate = byId("filter-start-date").value;
    const endDate = byId("filter-end-date").value;

    if (city) filters.city = city;
    if (neighborhood) filters.neighborhood = neighborhood;
    if (propertyType) filters.propertyType = propertyType;
    if (minimumRooms !== "") filters.minimumRooms = Number(minimumRooms);
    if (maximumRooms !== "") filters.maximumRooms = Number(maximumRooms);
    if (startDate) filters.startDate = startDate;
    if (endDate) filters.endDate = endDate;

    return filters;
  }

  function formatCurrency(value) {
    return value === null || value === undefined ? "אין מספיק נתונים" : "₪" + NUMBER_FORMAT.format(Math.round(value));
  }

  function renderWarnings(warnings) {
    const panel = byId("warnings-panel");
    const list = byId("warnings-list");
    clearChildren(list);

    if (!warnings || warnings.length === 0) {
      panel.hidden = true;
      return;
    }

    for (const warning of warnings) {
      const item = document.createElement("li");
      item.textContent = describeWarning(warning);
      list.append(item);
    }

    panel.hidden = false;
  }

  function describeWarning(code) {
    switch (code) {
      case "low_price_reported":
        return "נמצאה עסקה עם מחיר מתחת ל-100,000 ₪; ייתכן שזהו טעות דיווח.";
      case "price_metric_has_fewer_than_five_contributors":
        return "מחיר חציוני מבוסס על פחות מחמש עסקאות — מדגם קטן, יש להיזהר מהסקת מסקנות רחבות.";
      case "price_per_sqm_metric_has_fewer_than_five_contributors":
        return "מחיר למ\"ר חציוני מבוסס על פחות מחמש עסקאות — מדגם קטן, יש להיזהר מהסקת מסקנות רחבות.";
      case "supplied_price_per_sqm_mismatch":
        return "נמצאה עסקה שבה המחיר למ\"ר שדווח אינו תואם למחיר ולשטח שדווחו.";
      default:
        return code;
    }
  }

  function renderEvidenceTable(dealIds, hasMoreEvidence) {
    const body = byId("evidence-table-body");
    clearChildren(body);
    dealDetailCache.clear();

    for (const dealId of dealIds) {
      const row = document.createElement("tr");

      const idCell = document.createElement("td");
      idCell.textContent = dealId;

      const detailCell = document.createElement("td");
      const toggleButton = document.createElement("button");
      toggleButton.type = "button";
      toggleButton.className = "deal-detail-trigger";
      toggleButton.textContent = "הצג פירוט";
      toggleButton.addEventListener("click", () => toggleDealDetail(dealId, row, toggleButton));
      detailCell.append(toggleButton);

      row.append(idCell, detailCell);
      body.append(row);
    }

    const notice = byId("evidence-more-notice");
    if (hasMoreEvidence) {
      notice.textContent = "יש עסקאות תומכות נוספות שלא מוצגות ברשימה זו; המדדים לעיל מחושבים על כל העסקאות התואמות, לא רק על הרשימה המוצגת.";
      notice.hidden = false;
    } else {
      notice.hidden = true;
    }
  }

  async function toggleDealDetail(dealId, row, toggleButton) {
    const existingDetailRow = row.nextElementSibling;
    if (existingDetailRow && existingDetailRow.dataset.detailFor === dealId) {
      const isHidden = existingDetailRow.hidden;
      existingDetailRow.hidden = !isHidden;
      toggleButton.textContent = isHidden ? "הסתר פירוט" : "הצג פירוט";
      return;
    }

    toggleButton.disabled = true;
    try {
      const detail = dealDetailCache.has(dealId) ? dealDetailCache.get(dealId) : await fetchJson("/api/deals/" + encodeURIComponent(dealId));
      dealDetailCache.set(dealId, detail);

      const detailRow = document.createElement("tr");
      detailRow.dataset.detailFor = dealId;
      const detailCell = document.createElement("td");
      detailCell.colSpan = 2;
      detailCell.append(buildDealDetailElement(detail));
      detailRow.append(detailCell);
      row.after(detailRow);
      toggleButton.textContent = "הסתר פירוט";
    } catch (error) {
      const errorRow = document.createElement("tr");
      const errorCell = document.createElement("td");
      errorCell.colSpan = 2;
      errorCell.className = "status-error";
      errorCell.textContent = error.message;
      errorRow.append(errorCell);
      row.after(errorRow);
    } finally {
      toggleButton.disabled = false;
    }
  }

  function buildDealDetailElement(detail) {
    const container = document.createElement("div");
    container.className = "report-detail";

    const summary = document.createElement("dl");
    addCoverageEntry(summary, "סטטוס", detail.conflictStatus === "usable" ? "תקינה" : "דיווחים סותרים");
    addCoverageEntry(summary, "מספר דיווחים", String(detail.reportCount));
    addCoverageEntry(summary, "דיווחים שונים זה מזה", String(detail.distinctReportCount));
    container.append(summary);

    for (const report of detail.reports) {
      container.append(buildReportElement(report));
    }

    return container;
  }

  function buildReportElement(report) {
    const wrapper = document.createElement("div");
    wrapper.className = "report-detail";

    const heading = document.createElement("strong");
    heading.textContent = "דיווח מקור #" + report.sourceRowNumber;
    wrapper.append(heading);

    const dl = document.createElement("dl");

    let normalized = null;
    try {
      normalized = JSON.parse(report.normalizedJson);
    } catch {
      normalized = null;
    }

    if (normalized) {
      addCoverageEntry(dl, "עיר (לאחר נרמול)", normalized.City || "—");
      if (normalized.Locality) {
        addCoverageEntry(dl, "מקור שם העיר", normalized.Locality.OriginalValue || "—");
        addCoverageEntry(dl, "שיטת תיקון שם העיר", describeLocalityMethod(normalized.Locality.Method));
      }
      addCoverageEntry(dl, "שכונה", normalized.Neighborhood || "—");
      addCoverageEntry(dl, "סוג נכס", normalized.PropertyType || "—");
      addCoverageEntry(dl, "חדרים", normalized.Rooms ?? "—");
      addCoverageEntry(dl, "שטח (מ\"ר)", normalized.SizeSqm ?? "—");
      addCoverageEntry(dl, "מחיר (₪)", normalized.PriceNis ?? "—");
      addCoverageEntry(dl, "תאריך עסקה", formatDealDate(normalized));
      addCoverageEntry(dl, "מקור הדיווח", normalized.Source || "—");
    }

    let qualityFlags = [];
    try {
      qualityFlags = JSON.parse(report.qualityFlagsJson) || [];
    } catch {
      qualityFlags = [];
    }

    if (qualityFlags.length > 0) {
      addCoverageEntry(dl, "דגלי איכות", qualityFlags.join(", "));
    }

    wrapper.append(dl);
    return wrapper;
  }

  function describeLocalityMethod(method) {
    switch (method) {
      case "exact":
        return "התאמה מדויקת לקטלוג היישובים";
      case "typo":
        return "תוקן אוטומטית מתוך הקטלוג (טעות הקלדה סבירה)";
      case "ambiguous":
        return "השם מעורפל — לא תוקן אוטומטית";
      default:
        return "לא נמצאה התאמה בקטלוג היישובים";
    }
  }

  function formatDealDate(normalized) {
    if (!normalized.DealDateStart) {
      return "לא דווח";
    }

    if (normalized.DealDatePrecision === "month") {
      return normalized.DealDateStart + " עד " + normalized.DealDateEnd + " (דיוק חודשי)";
    }

    return normalized.DealDateStart;
  }

  function renderResults(filters, response) {
    const status = byId("results-status");
    const content = byId("results-content");
    const result = response.result;

    if (result.transactionCount === 0) {
      content.hidden = true;
      status.hidden = false;
      status.classList.remove("status-error");
      status.textContent = "לא נמצאו עסקאות התואמות לסינון שנבחר. נסו להרחיב את טווח החיפוש.";
      return;
    }

    status.hidden = true;
    content.hidden = false;

    byId("metric-transaction-count").textContent = NUMBER_FORMAT.format(result.transactionCount);
    byId("metric-median-price").textContent = formatCurrency(result.medianPriceNis);
    byId("metric-price-contributors").textContent = "מבוסס על " + NUMBER_FORMAT.format(result.priceContributorCount) + " עסקאות עם מחיר תקין";
    byId("metric-median-price-per-sqm").textContent = formatCurrency(result.medianPricePerSqm);
    byId("metric-price-per-sqm-contributors").textContent = "מבוסס על " + NUMBER_FORMAT.format(result.pricePerSqmContributorCount) + " עסקאות עם מחיר ושטח תקינים";

    renderWarnings(result.warnings);
    renderEvidenceTable(result.contributorDealIds, result.hasMoreEvidence);
  }

  async function submitFilters(filters) {
    const status = byId("results-status");
    const content = byId("results-content");
    content.hidden = true;
    status.hidden = false;
    status.classList.remove("status-error");
    status.textContent = "מחשב תוצאות…";

    try {
      const response = await fetchJson("/api/query", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(filters)
      });
      renderResults(filters, response);
    } catch (error) {
      content.hidden = true;
      status.hidden = false;
      status.classList.add("status-error");
      status.textContent = error.message;
    }
  }

  async function lookupDeal(dealId) {
    const status = byId("deal-lookup-status");
    const result = byId("deal-lookup-result");
    clearChildren(result);
    status.classList.remove("status-error");

    if (!dealId) {
      status.textContent = "יש להזין מזהה עסקה.";
      return;
    }

    status.textContent = "מחפש…";
    try {
      const detail = await fetchJson("/api/deals/" + encodeURIComponent(dealId));
      status.textContent = "";
      result.append(buildDealDetailElement(detail));
    } catch (error) {
      status.classList.add("status-error");
      status.textContent = error.message;
    }
  }

  function init() {
    loadCoverage();

    byId("filters-form").addEventListener("submit", (event) => {
      event.preventDefault();
      submitFilters(readFilters());
    });

    byId("clear-filters").addEventListener("click", () => {
      byId("filters-form").reset();
      byId("results-content").hidden = true;
      const status = byId("results-status");
      status.hidden = false;
      status.classList.remove("status-error");
      status.textContent = "בחרו סינון ולחצו \"חפש\" כדי לראות תוצאות.";
    });

    byId("deal-lookup-form").addEventListener("submit", (event) => {
      event.preventDefault();
      lookupDeal(byId("deal-lookup-id").value.trim());
    });
  }

  document.addEventListener("DOMContentLoaded", init);
})();
