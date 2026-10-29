import { api, getToken, setToken, mondayOf, esc, initials } from "./api.js";
import { renderSchedule } from "./schedule.js";
import { renderDashboard } from "./dashboard.js";
import { renderTeam } from "./team.js";
import { renderRequests } from "./requests.js";

const root = document.getElementById("app");
const toast = document.getElementById("toast");
let toastTimer = 0;

const state = {
  me: null,
  weekStart: mondayOf(new Date())
};

const views = {
  schedule: renderSchedule,
  dashboard: renderDashboard,
  team: renderTeam,
  requests: renderRequests
};

function showToast(message, tone = "ok") {
  toast.textContent = message;
  toast.dataset.tone = tone;
  toast.hidden = false;
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => {
    toast.hidden = true;
  }, tone === "bad" ? 5600 : 2800);
}

function closeModal() {
  document.querySelector(".modal-back")?.remove();
}

function openModal(html, setup) {
  closeModal();
  const back = document.createElement("div");
  back.className = "modal-back";
  back.innerHTML = html;
  back.addEventListener("click", (event) => {
    if (event.target === back) closeModal();
  });
  document.body.appendChild(back);
  const onKey = (event) => {
    if (event.key === "Escape") {
      closeModal();
      document.removeEventListener("keydown", onKey);
    }
  };
  document.addEventListener("keydown", onKey);
  back.querySelector("[data-close]")?.addEventListener("click", closeModal);
  setup?.(back);
  back.querySelector("input, select, textarea")?.focus();
}

function context() {
  return { state, reload: () => route(), setWeek, toast: showToast, openModal, closeModal };
}

function setWeek(next) {
  state.weekStart = next;
  route();
}

window.addEventListener("hashchange", () => route());
boot();

async function boot() {
  if (getToken()) {
    try {
      state.me = await api("/api/auth/me");
    } catch {
      setToken(null);
      state.me = null;
    }
  }
  if (!location.hash) location.hash = state.me ? "#/schedule" : "#/login";
  route();
}

function currentView() {
  return (location.hash.replace(/^#\//, "") || "schedule").split("?")[0];
}

function route() {
  const name = currentView();
  if (!state.me || name === "login") {
    document.body.dataset.view = "login";
    renderLogin();
    return;
  }
  if (!views[name]) {
    location.hash = "#/schedule";
    return;
  }
  document.body.dataset.view = name;
  renderShell(name);
  views[name](document.getElementById("main"), context());
  refreshBadge();
}

function renderShell(active) {
  const me = state.me;
  root.innerHTML = `
    <div class="shell">
      <aside class="side">
        <a class="brand" href="#/schedule">
          <span class="mark" aria-hidden="true">S</span>
          <span>
            <strong>ShiftSync</strong>
            <em>Northline Outfitters</em>
          </span>
        </a>
        <nav aria-label="Primary">
          ${navLink("schedule", "Schedule", active)}
          ${navLink("dashboard", "Dashboard", active)}
          ${navLink("team", "Team", active)}
          ${navLink("requests", "Requests", active, true)}
        </nav>
        <div class="me">
          <span class="avatar" style="background:${esc(me.color)}">${esc(initials(me))}</span>
          <span>
            <strong>${esc(me.firstName)} ${esc(me.lastName)}</strong>
            <em>${esc(me.role)}</em>
          </span>
          <button type="button" id="sign-out">Sign out</button>
        </div>
      </aside>
      <main id="main"></main>
    </div>`;
  root.querySelector("#sign-out").addEventListener("click", () => {
    setToken(null);
    state.me = null;
    location.hash = "#/login";
    route();
  });
}

function navLink(id, label, active, badge) {
  return `<a href="#/${id}" ${active === id ? 'aria-current="page"' : ""}>${label}${badge ? '<span class="badge" data-badge hidden>0</span>' : ""}</a>`;
}

async function refreshBadge() {
  const badge = document.querySelector("[data-badge]");
  if (!badge || !state.me) return;
  try {
    const [timeOff, swaps] = await Promise.all([
      api("/api/requests/time-off"),
      api("/api/requests/swaps")
    ]);
    const count = [...timeOff, ...swaps].filter((item) => item.status === "Pending").length;
    badge.hidden = count === 0;
    badge.textContent = String(count);
  } catch {
    badge.hidden = true;
  }
}

async function renderLogin() {
  let accounts = [];
  let password = "Northline#1";
  try {
    const demo = await api("/api/auth/demo");
    accounts = demo.accounts || [];
    password = demo.password || password;
  } catch {
    accounts = [];
  }

  root.innerHTML = `
    <div class="login">
      <section class="login-hero">
        <p class="kicker">Queen Street West · Toronto</p>
        <h1>Who's on the floor this week.</h1>
        <p class="lede">ShiftSync is the break-room clipboard for Northline Outfitters. Build the week, catch overlaps before they hit Saturday, and keep the close covered.</p>
      </section>
      <section class="login-panel">
        <form id="login-form" class="card">
          <h2>Sign in</h2>
          <p class="muted">Managers write the schedule. Associates request swaps and time off.</p>
          <label>Email
            <input name="email" type="email" autocomplete="username" required value="rajveer.singh@northline.demo" />
          </label>
          <label>Password
            <input name="password" type="password" autocomplete="current-password" required value="${esc(password)}" />
          </label>
          <p class="form-error" id="login-error" hidden></p>
          <button class="btn primary" type="submit">Sign in</button>
        </form>
        ${accounts.length ? `
          <div class="demo">
            <p>Demo floor, shared password <code>${esc(password)}</code></p>
            <div class="demo-list">
              ${accounts.map((account) => `
                <button type="button" class="demo-card" data-email="${esc(account.email)}">
                  <strong>${esc(account.firstName)} ${esc(account.lastName)}</strong>
                  <em>${esc(account.role)}</em>
                </button>`).join("")}
            </div>
          </div>` : ""}
      </section>
    </div>`;

  const form = root.querySelector("#login-form");
  form.addEventListener("submit", async (event) => {
    event.preventDefault();
    const error = root.querySelector("#login-error");
    error.hidden = true;
    const data = new FormData(form);
    try {
      const result = await api("/api/auth/login", {
        method: "POST",
        body: { email: data.get("email"), password: data.get("password") }
      });
      setToken(result.token);
      state.me = result.employee;
      state.weekStart = mondayOf(new Date());
      if (location.hash === "#/schedule") route();
      else location.hash = "#/schedule";
    } catch (err) {
      error.textContent = err.message;
      error.hidden = false;
    }
  });

  root.querySelectorAll(".demo-card").forEach((button) => {
    button.addEventListener("click", () => {
      form.email.value = button.dataset.email;
      form.password.value = password;
      form.requestSubmit();
    });
  });
}
