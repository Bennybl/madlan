export const NUMBER_FORMAT = new Intl.NumberFormat("he-IL");

export function formatCurrency(value) {
  return value === null || value === undefined ? "אין מספיק נתונים" : "₪" + NUMBER_FORMAT.format(Math.round(value));
}

export const FIELD_LABELS = {
  Price: "מחיר (₪)",
  PricePerSqm: 'מחיר למ"ר (₪)',
  SizeSqm: 'שטח (מ"ר)',
  Rooms: "מספר חדרים",
  Floor: "קומה",
  YearBuilt: "שנת בנייה"
};

const CURRENCY_FIELDS = new Set(["Price", "PricePerSqm"]);

export function formatFieldValue(field, value) {
  if (value === null || value === undefined) return "—";
  return CURRENCY_FIELDS.has(field) ? formatCurrency(value) : NUMBER_FORMAT.format(value);
}

export const AGGREGATE_LABELS = {
  Count: "ספירה",
  Average: "ממוצע",
  Median: "חציון",
  Min: "מינימום",
  Max: "מקסימום",
  Outliers: "חריגים"
};

export const GROUP_BY_LABELS = {
  City: "עיר",
  Neighborhood: "שכונה",
  PropertyType: "סוג נכס",
  Condition: "מצב",
  Source: "מקור",
  Rooms: "מספר חדרים",
  Floor: "קומה",
  YearBuilt: "שנת בנייה",
  HasElevator: "מעלית",
  HasParking: "חניה",
  HasBalcony: "מרפסת",
  HasSafeRoom: 'ממ"ד'
};

const BOOLEAN_GROUP_BY_FIELDS = new Set(["HasElevator", "HasParking", "HasBalcony", "HasSafeRoom"]);

export function formatGroupValue(groupByField, groupValue) {
  if (!BOOLEAN_GROUP_BY_FIELDS.has(groupByField)) return groupValue;
  return groupValue === "1" ? "כן" : groupValue === "0" ? "לא" : groupValue;
}

export function describeDataQuery(query) {
  if (!query) return "";
  const parts = [];
  const aggregateLabel = AGGREGATE_LABELS[query.aggregate] || query.aggregate;
  const fieldLabel = query.field ? FIELD_LABELS[query.field] || query.field : null;
  parts.push(fieldLabel ? `${aggregateLabel} של ${fieldLabel}` : aggregateLabel);

  if (query.groupBy) {
    parts.push(`מקובץ לפי ${GROUP_BY_LABELS[query.groupBy] || query.groupBy}`);
  }

  if (query.rank && query.rank > 1 && (query.aggregate === "Min" || query.aggregate === "Max")) {
    parts.push(`דירוג ${query.rank}`);
  }

  if (query.limit) {
    parts.push(`מוגבל ל-${query.limit} (${query.descending === false ? "עולה" : "יורד"})`);
  }

  const filtersText = describeFilters(query.filters);
  if (filtersText && !filtersText.startsWith("לא זוהו")) {
    parts.push(filtersText);
  }

  return parts.join(" · ");
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

export function formatDealDate(normalized) {
  if (!normalized.DealDateStart) return "לא דווח";
  if (normalized.DealDatePrecision === "month") {
    return normalized.DealDateStart + " עד " + normalized.DealDateEnd + " (דיוק חודשי)";
  }
  return normalized.DealDateStart;
}
