export const NUMBER_FORMAT = new Intl.NumberFormat("he-IL");

export function formatCurrency(value) {
  return value === null || value === undefined ? "אין מספיק נתונים" : "₪" + NUMBER_FORMAT.format(Math.round(value));
}

export const METRIC_LABELS = {
  MedianPrice: "מחיר חציוני (₪)",
  AveragePrice: "מחיר ממוצע (₪)",
  MinPrice: "המחיר הזול ביותר (₪)",
  MaxPrice: "המחיר היקר ביותר (₪)",
  MedianPricePerSqm: 'מחיר למ"ר חציוני (₪)',
  AveragePricePerSqm: 'מחיר למ"ר ממוצע (₪)',
  MinPricePerSqm: 'מחיר למ"ר הזול ביותר (₪)',
  MaxPricePerSqm: 'מחיר למ"ר היקר ביותר (₪)',
  MedianSizeSqm: 'שטח חציוני (מ"ר)',
  AverageSizeSqm: 'שטח ממוצע (מ"ר)',
  MinSizeSqm: 'השטח הקטן ביותר (מ"ר)',
  MaxSizeSqm: 'השטח הגדול ביותר (מ"ר)',
  MedianRooms: "מספר חדרים חציוני",
  AverageRooms: "מספר חדרים ממוצע",
  MinRooms: "מספר החדרים הנמוך ביותר",
  MaxRooms: "מספר החדרים הגבוה ביותר",
  MedianFloor: "קומה חציונית",
  AverageFloor: "קומה ממוצעת",
  MinFloor: "הקומה הנמוכה ביותר",
  MaxFloor: "הקומה הגבוהה ביותר",
  MedianYearBuilt: "שנת בנייה חציונית",
  AverageYearBuilt: "שנת בנייה ממוצעת",
  MinYearBuilt: "שנת הבנייה המוקדמת ביותר",
  MaxYearBuilt: "שנת הבנייה המאוחרת ביותר"
};

export const CURRENCY_METRICS = new Set([
  "MedianPrice", "AveragePrice", "MinPrice", "MaxPrice",
  "MedianPricePerSqm", "AveragePricePerSqm", "MinPricePerSqm", "MaxPricePerSqm"
]);

export const SINGLE_DEAL_METRICS = new Set([
  "MinPrice", "MaxPrice", "MinPricePerSqm", "MaxPricePerSqm",
  "MinSizeSqm", "MaxSizeSqm", "MinRooms", "MaxRooms",
  "MinFloor", "MaxFloor", "MinYearBuilt", "MaxYearBuilt"
]);

const RANK_FIELD_INFO = {
  Price: { noun: "המחיר", min: "הזול", max: "היקר", suffix: " (₪)" },
  PricePerSqm: { noun: 'מחיר למ"ר', min: "הזול", max: "היקר", suffix: ' (₪)' },
  SizeSqm: { noun: "השטח", min: "הקטן", max: "הגדול", suffix: ' (מ"ר)' },
  Rooms: { noun: "מספר החדרים", min: "הנמוך", max: "הגבוה", suffix: "" },
  Floor: { noun: "הקומה", min: "הנמוכה", max: "הגבוהה", suffix: "" },
  YearBuilt: { noun: "שנת הבנייה", min: "המוקדמת", max: "המאוחרת", suffix: "" }
};

export function describeRankedMetric(metric, rank) {
  const isMax = metric.startsWith("Max");
  const field = metric.replace(/^(Min|Max)/, "");
  const info = RANK_FIELD_INFO[field];
  if (!info) return `${METRIC_LABELS[metric] || metric} — דירוג ${rank}`;

  const adjective = isMax ? info.max : info.min;
  const rankPhrase = rank <= 1 ? "ביותר" : `ה-${rank} ביותר`;
  return `${info.noun} ${adjective} ${rankPhrase}${info.suffix}`;
}

export function formatMetricValue(metric, value) {
  if (value === null || value === undefined) return "—";
  return CURRENCY_METRICS.has(metric) ? formatCurrency(value) : NUMBER_FORMAT.format(value);
}

export function describeFilters(filters) {
  if (!filters) return "לא זוהו סינונים ספציפיים בשאלה; מוצגות כל העסקאות התואמות.";
  const parts = [];

  if (filters.city) parts.push("עיר: " + filters.city);
  if (filters.neighborhood) parts.push("שכונה: " + filters.neighborhood);
  if (filters.propertyType) parts.push("סוג נכס: " + filters.propertyType);

  if (filters.minimumRooms != null || filters.maximumRooms != null) {
    if (filters.minimumRooms != null && filters.minimumRooms === filters.maximumRooms) {
      parts.push("חדרים: " + filters.minimumRooms);
    } else {
      const min = filters.minimumRooms != null ? filters.minimumRooms : "ללא מגבלה";
      const max = filters.maximumRooms != null ? filters.maximumRooms : "ללא מגבלה";
      parts.push("חדרים: בין " + min + " ל-" + max);
    }
  }

  if (filters.startDate || filters.endDate) {
    parts.push("טווח תאריכים: " + (filters.startDate || "ללא התחלה") + " עד " + (filters.endDate || "ללא סיום"));
  }

  if (filters.condition) parts.push("מצב: " + filters.condition);
  if (filters.source) parts.push("מקור: " + filters.source);
  if (filters.hasElevator != null) parts.push(filters.hasElevator ? "עם מעלית" : "ללא מעלית");
  if (filters.hasParking != null) parts.push(filters.hasParking ? "עם חניה" : "ללא חניה");
  if (filters.hasBalcony != null) parts.push(filters.hasBalcony ? "עם מרפסת" : "ללא מרפסת");
  if (filters.hasSafeRoom != null) parts.push(filters.hasSafeRoom ? "עם ממ\"ד" : "ללא ממ\"ד");

  return parts.length > 0 ? parts.join(" · ") : "לא זוהו סינונים ספציפיים בשאלה; מוצגות כל העסקאות התואמות.";
}

export function describeWarning(code) {
  switch (code) {
    case "low_price_reported":
      return "נמצאה עסקה עם מחיר מתחת ל-100,000 ₪; ייתכן שזהו טעות דיווח.";
    case "price_metric_has_fewer_than_five_contributors":
      return "מחיר חציוני מבוסס על פחות מחמש עסקאות — מדגם קטן, יש להיזהר מהסקת מסקנות רחבות.";
    case "price_per_sqm_metric_has_fewer_than_five_contributors":
      return 'מחיר למ"ר חציוני מבוסס על פחות מחמש עסקאות — מדגם קטן, יש להיזהר מהסקת מסקנות רחבות.';
    case "supplied_price_per_sqm_mismatch":
      return 'נמצאה עסקה שבה המחיר למ"ר שדווח אינו תואם למחיר ולשטח שדווחו.';
    default:
      return code;
  }
}

export function describeLocalityMethod(method) {
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

export const OUTLIER_FIELD_LABELS = {
  Price: "מחיר (₪)",
  PricePerSqm: 'מחיר למ"ר (₪)',
  SizeSqm: 'שטח (מ"ר)',
  Rooms: "מספר חדרים",
  Floor: "קומה",
  YearBuilt: "שנת בנייה"
};

const OUTLIER_CURRENCY_FIELDS = new Set(["Price", "PricePerSqm"]);

export function formatOutlierValue(field, value) {
  if (value === null || value === undefined) return "—";
  return OUTLIER_CURRENCY_FIELDS.has(field) ? formatCurrency(value) : NUMBER_FORMAT.format(value);
}

export const GROUP_BY_LABELS = {
  City: "עיר",
  Neighborhood: "שכונה",
  PropertyType: "סוג נכס",
  Condition: "מצב",
  Source: "מקור",
  Rooms: "מספר חדרים",
  Floor: "קומה",
  YearBuilt: "שנת בנייה"
};

export function formatDealDate(normalized) {
  if (!normalized.DealDateStart) return "לא דווח";
  if (normalized.DealDatePrecision === "month") {
    return normalized.DealDateStart + " עד " + normalized.DealDateEnd + " (דיוק חודשי)";
  }
  return normalized.DealDateStart;
}
