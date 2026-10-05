const NETWORK_ERROR_MESSAGE = "לא ניתן היה להתחבר לשרת. בדקו את החיבור ונסו שוב.";
const REQUEST_TIMEOUT_MESSAGE = "הבקשה ארכה זמן רב מדי ולא התקבלה תשובה. נסו שוב או השתמשו בסינון הידני.";

export async function fetchJson(url, options, timeoutMs) {
  const controller = timeoutMs ? new AbortController() : null;
  const fetchOptions = controller ? { ...options, signal: controller.signal } : options;
  const timeoutId = controller ? setTimeout(() => controller.abort(), timeoutMs) : null;

  let response;
  try {
    response = await fetch(url, fetchOptions);
  } catch {
    throw new Error(controller && controller.signal.aborted ? REQUEST_TIMEOUT_MESSAGE : NETWORK_ERROR_MESSAGE);
  } finally {
    if (timeoutId) clearTimeout(timeoutId);
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
