const TOKEN_KEY = "shiftsync.token";

export function getToken() {
  return localStorage.getItem(TOKEN_KEY);
}

export function setToken(token) {
  if (token) localStorage.setItem(TOKEN_KEY, token);
  else localStorage.removeItem(TOKEN_KEY);
}

export async function api(path, options = {}) {
  const headers = new Headers(options.headers || {});
  const token = getToken();
  if (token) headers.set("Authorization", `Bearer ${token}`);

  let body = options.body;
  if (body !== undefined && !(body instanceof FormData)) {
    headers.set("Content-Type", "application/json");
    body = JSON.stringify(body);
  }

  const response = await fetch(path, { method: options.method || "GET", headers, body });
  if (response.status === 401 && path !== "/api/auth/login") {
    setToken(null);
    if (!location.hash.startsWith("#/login")) location.hash = "#/login";
  }

  const text = await response.text();
  let data = null;
  if (text) {
    try {
      data = JSON.parse(text);
    } catch {
      data = { detail: text };
    }
  }

  if (!response.ok) {
    const error = new Error(problemMessage(data, response.status));
    error.status = response.status;
    error.payload = data;
    throw error;
  }

  return data;
}

function problemMessage(data, status) {
  if (!data) return `Request failed (${status}).`;
  if (Array.isArray(data.conflicts) && data.conflicts.length)
    return data.conflicts.map((item) => item.message).join(" ");
  if (data.errors && typeof data.errors === "object") {
    const lines = Object.values(data.errors).flat();
    if (lines.length) return lines.join(" ");
  }
  return data.detail || data.title || `Request failed (${status}).`;
}

export function mondayOf(date) {
  const copy = new Date(date.getFullYear(), date.getMonth(), date.getDate());
  copy.setDate(copy.getDate() - ((copy.getDay() + 6) % 7));
  return copy;
}

export function addDays(date, days) {
  const copy = new Date(date.getFullYear(), date.getMonth(), date.getDate());
  copy.setDate(copy.getDate() + days);
  return copy;
}

export function isoDate(date) {
  const month = String(date.getMonth() + 1).padStart(2, "0");
  const day = String(date.getDate()).padStart(2, "0");
  return `${date.getFullYear()}-${month}-${day}`;
}

export function parseIsoDate(value) {
  const [year, month, day] = value.split("-").map(Number);
  return new Date(year, month - 1, day);
}

export function clock(iso) {
  return String(iso).slice(11, 16);
}

export function shortTime(value) {
  const [hour, minute] = value.split(":");
  return minute === "00" ? String(Number(hour)) : `${Number(hour)}:${minute}`;
}

export function rangeLabel(start, end) {
  return `${shortTime(start)}–${shortTime(end)}`;
}

export function hoursBetween(startIso, endIso) {
  const [sh, sm] = clock(startIso).split(":").map(Number);
  const [eh, em] = clock(endIso).split(":").map(Number);
  return (eh * 60 + em - (sh * 60 + sm)) / 60;
}

export function esc(value) {
  return String(value ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;");
}

export const positions = [
  { id: "SalesFloor", label: "Sales floor" },
  { id: "Cash", label: "Cash" },
  { id: "Stockroom", label: "Stockroom" },
  { id: "FittingRoom", label: "Fitting room" }
];

export function positionLabel(id) {
  return positions.find((item) => item.id === id)?.label || id;
}

export const dayNames = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];

export function weekLabel(monday) {
  const end = addDays(monday, 6);
  const sameMonth = monday.getMonth() === end.getMonth();
  const left = monday.toLocaleDateString("en-CA", { month: "long", day: "numeric" });
  const right = end.toLocaleDateString("en-CA", sameMonth ? { day: "numeric" } : { month: "short", day: "numeric" });
  return `${left}–${right}`;
}

export function longDate(iso) {
  return parseIsoDate(iso).toLocaleDateString("en-CA", { weekday: "short", month: "short", day: "numeric" });
}

export function initials(person) {
  return `${person.firstName?.[0] || ""}${person.lastName?.[0] || ""}`.toUpperCase();
}
