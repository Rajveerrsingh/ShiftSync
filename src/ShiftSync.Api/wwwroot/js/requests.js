import { api, clock, esc, isoDate, longDate, rangeLabel } from "./api.js";
import { bindRetry, bindWeek, pageStatus, weekControls } from "./ui.js";

export async function renderRequests(main, ctx) {
  main.innerHTML = `<section class="page"><p class="loading">Checking the request pile…</p></section>`;
  try {
    const [timeOff, swaps, week] = await Promise.all([
      api("/api/requests/time-off"),
      api("/api/requests/swaps"),
      api(`/api/schedule?weekStart=${isoDate(ctx.state.weekStart)}`)
    ]);
    draw(main, ctx, { timeOff, swaps, week });
  } catch (error) {
    main.innerHTML = pageStatus(error.message, "bad", true);
    bindRetry(main, ctx);
  }
}

function draw(main, ctx, { timeOff, swaps, week }) {
  const me = ctx.state.me;
  const manager = me.role === "Manager";
  const mine = week.shifts.filter((shift) => shift.employeeId === me.id);
  const others = week.shifts.filter((shift) => shift.employeeId !== me.id);
  const coworkers = week.employees.filter((person) => person.id !== me.id);

  main.innerHTML = `
    <section class="page">
      <header class="page-head">
        <div>
          <p class="kicker">${manager ? "Review the pile" : "Your requests"}</p>
          <h1>Time off and swaps</h1>
        </div>
        ${weekControls(ctx.state.weekStart)}
      </header>
      <div class="split-panels">
        <section class="panel">
          <h2>Time off</h2>
          <form id="time-form" class="stack">
            <div class="split">
              <label>From <input name="startDate" type="date" required value="${isoDate(ctx.state.weekStart)}" /></label>
              <label>To <input name="endDate" type="date" required value="${isoDate(ctx.state.weekStart)}" /></label>
            </div>
            <label>Reason <input name="reason" required maxlength="400" placeholder="Appointment, trip, family" /></label>
            <p class="form-error" hidden></p>
            <button class="btn primary" type="submit">Request time off</button>
          </form>
          ${listTimeOff(timeOff, manager, me.id)}
        </section>
        <section class="panel">
          <h2>Shift swaps</h2>
          <form id="swap-form" class="stack">
            <label>My shift
              <select name="shiftId" ${mine.length ? "" : "disabled"}>
                ${mine.length ? mine.map((shift) => optionShift(shift)).join("") : `<option>No shift this week</option>`}
              </select>
            </label>
            <label>What do you need?
              <select name="kind">
                <option value="Cover">Someone to cover it</option>
                <option value="Exchange">An exchange</option>
              </select>
            </label>
            <label data-cover>Coworker
              <select name="targetEmployeeId">
                ${coworkers.map((person) => `<option value="${person.id}">${esc(person.firstName)} ${esc(person.lastName)}</option>`).join("")}
              </select>
            </label>
            <label data-exchange hidden>Their shift
              <select name="counterShiftId">
                ${others.map((shift) => optionShift(shift, true)).join("")}
              </select>
            </label>
            <label>Note <input name="note" maxlength="400" placeholder="Optional context for the manager" /></label>
            <p class="form-error" hidden></p>
            <button class="btn primary" type="submit" ${mine.length ? "" : "disabled"}>Send request</button>
          </form>
          ${listSwaps(swaps, manager, me.id)}
        </section>
      </div>
    </section>`;

  bindWeek(main, ctx);
  const swapForm = main.querySelector("#swap-form");
  const kind = swapForm.querySelector('[name="kind"]');
  const syncKind = () => {
    const exchange = kind.value === "Exchange";
    swapForm.querySelector("[data-cover]").hidden = exchange;
    swapForm.querySelector("[data-exchange]").hidden = !exchange;
  };
  kind.addEventListener("change", syncKind);

  main.querySelector("#time-form").addEventListener("submit", async (event) => {
    event.preventDefault();
    const form = event.currentTarget;
    const data = Object.fromEntries(new FormData(form).entries());
    try {
      await api("/api/requests/time-off", { method: "POST", body: data });
      ctx.toast("Time off requested.");
      ctx.reload();
    } catch (error) {
      showFormError(form, error.message);
    }
  });

  swapForm.addEventListener("submit", async (event) => {
    event.preventDefault();
    const data = Object.fromEntries(new FormData(swapForm).entries());
    const body = {
      kind: data.kind,
      shiftId: Number(data.shiftId),
      note: data.note,
      targetEmployeeId: data.kind === "Cover" ? Number(data.targetEmployeeId) : null,
      counterShiftId: data.kind === "Exchange" ? Number(data.counterShiftId) : null
    };
    try {
      await api("/api/requests/swaps", { method: "POST", body });
      ctx.toast("Swap sent to the manager.");
      ctx.reload();
    } catch (error) {
      showFormError(swapForm, error.message);
    }
  });

  main.querySelectorAll("[data-review]").forEach((button) => {
    button.addEventListener("click", () => review(ctx, button));
  });
  main.querySelectorAll("[data-cancel]").forEach((button) => {
    button.addEventListener("click", async () => {
      button.disabled = true;
      try {
        await api(`/api/requests/${button.dataset.cancel}/${button.dataset.id}/cancel`, { method: "POST" });
        ctx.toast("Request cancelled.");
        ctx.reload();
      } catch (error) {
        ctx.toast(error.message, "bad");
        button.disabled = false;
      }
    });
  });
}

function optionShift(shift, withName) {
  const label = `${withName ? `${shift.employeeName} · ` : ""}${shift.startsAt.slice(0, 10)} ${rangeLabel(clock(shift.startsAt), clock(shift.endsAt))}`;
  return `<option value="${shift.id}">${esc(label)}</option>`;
}

function listTimeOff(rows, manager, myId) {
  if (!rows.length) return `<p class="muted empty">No time off requests.</p>`;
  return `<ul class="request-list">${rows.map((row) => `
    <li>
      <div class="request-top">
        <strong>${esc(row.employeeName)}</strong>
        ${pill(row.status)}
      </div>
      <p>${esc(longDate(row.startDate))} – ${esc(longDate(row.endDate))}</p>
      <p>${esc(row.reason)}</p>
      ${row.warnings?.length ? `<p class="banner warn">${row.warnings.map(esc).join("<br>")}</p>` : ""}
      ${row.reviewNote ? `<p class="muted">Note: ${esc(row.reviewNote)}</p>` : ""}
      ${actions("time-off", row, manager, row.employeeId === myId)}
    </li>`).join("")}</ul>`;
}

function listSwaps(rows, manager, myId) {
  if (!rows.length) return `<p class="muted empty">No swap requests.</p>`;
  return `<ul class="request-list">${rows.map((row) => `
    <li>
      <div class="request-top">
        <strong>${esc(row.requesterName)}</strong>
        ${pill(row.status)}
      </div>
      <p>${esc(row.kind === "Cover" ? "Cover" : "Exchange")} · ${esc(describeSwap(row))}</p>
      ${row.note ? `<p>${esc(row.note)}</p>` : ""}
      ${row.reviewNote ? `<p class="muted">Note: ${esc(row.reviewNote)}</p>` : ""}
      ${actions("swaps", row, manager, row.requesterId === myId)}
    </li>`).join("")}</ul>`;
}

function describeSwap(row) {
  const mine = `${row.shift.startsAt.slice(0, 10)} ${rangeLabel(clock(row.shift.startsAt), clock(row.shift.endsAt))}`;
  if (row.kind === "Cover") return `${mine} → ${row.targetEmployeeName || "a coworker"}`;
  const other = row.counterShift
    ? `${row.counterShift.employeeName} ${row.counterShift.startsAt.slice(0, 10)} ${rangeLabel(clock(row.counterShift.startsAt), clock(row.counterShift.endsAt))}`
    : "another shift";
  return `${mine} ↔ ${other}`;
}

function actions(kind, row, manager, owner) {
  if (row.status !== "Pending") return "";
  const note = manager ? `<input class="note" data-note="${row.id}" placeholder="Note (optional)" maxlength="400" />` : "";
  const review = manager
    ? `<button type="button" class="btn primary" data-review="approve" data-kind="${kind}" data-id="${row.id}">Approve</button>
       <button type="button" class="btn ghost" data-review="deny" data-kind="${kind}" data-id="${row.id}">Deny</button>`
    : "";
  const cancel = owner || manager
    ? `<button type="button" class="btn ghost" data-cancel="${kind}" data-id="${row.id}">Cancel</button>`
    : "";
  return `<div class="card-actions">${note}${review}${cancel}</div>`;
}

function pill(status) {
  const tone = status === "Approved" ? "ok" : status === "Denied" ? "bad" : status === "Cancelled" ? "muted" : "warn";
  return `<span class="pill ${tone}">${esc(status)}</span>`;
}

async function review(ctx, button) {
  const id = button.dataset.id;
  const kind = button.dataset.kind;
  const approve = button.dataset.review === "approve";
  const note = button.closest("li")?.querySelector(`[data-note="${id}"]`)?.value || "";
  button.disabled = true;
  try {
    await api(`/api/requests/${kind}/${id}/review`, { method: "POST", body: { approve, note } });
    ctx.toast(approve ? "Approved." : "Denied.");
    ctx.reload();
  } catch (error) {
    ctx.toast(error.message, "bad");
    button.disabled = false;
  }
}

function showFormError(form, message) {
  const box = form.querySelector(".form-error");
  box.textContent = message;
  box.hidden = false;
}
