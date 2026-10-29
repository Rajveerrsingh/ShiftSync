import {
  api, addDays, clock, esc, hoursBetween, isoDate, mondayOf, parseIsoDate, positionLabel, positions, rangeLabel, dayNames
} from "./api.js";
import { bindRetry, bindWeek, pageStatus, weekControls } from "./ui.js";

export async function renderSchedule(main, ctx) {
  main.innerHTML = `<section class="page"><p class="loading">Pulling the week together…</p></section>`;
  try {
    const week = await api(`/api/schedule?weekStart=${isoDate(ctx.state.weekStart)}`);
    draw(main, ctx, week);
  } catch (error) {
    main.innerHTML = pageStatus(error.message, "bad", true);
    bindRetry(main, ctx);
  }
}

function draw(main, ctx, week) {
  const manager = ctx.state.me.role === "Manager";
  const days = Array.from({ length: 7 }, (_, index) => addDays(ctx.state.weekStart, index));
  const totalHours = week.shifts.reduce((sum, shift) => sum + hoursBetween(shift.startsAt, shift.endsAt), 0);
  const gapLabel = week.gaps.length === 0
    ? "Floor is covered"
    : `${week.gaps.length} coverage gap${week.gaps.length === 1 ? "" : "s"}`;

  main.innerHTML = `
    <section class="page">
      <header class="page-head">
        <div>
          <p class="kicker">${esc(week.storeName)} · ${esc(week.city)}</p>
          <h1>${esc(weekLabelSafe(ctx))}</h1>
        </div>
        <div class="page-actions">
          ${weekControls(ctx.state.weekStart)}
          <p class="stat-line"><strong>${trimHours(totalHours)} h</strong> scheduled · <a href="#/dashboard">${esc(gapLabel)}</a></p>
          ${manager ? `<button type="button" class="btn primary" id="add-shift">Add shift</button>` : ""}
        </div>
      </header>
      ${week.shifts.length === 0 ? `<div class="banner">Nothing is on the grid yet.${manager ? " Add a shift in an empty cell." : ""}</div>` : ""}
      <div class="board-scroll">
        <div class="board" role="table" aria-label="Weekly schedule">
          <div class="board-row head" role="row">
            <div class="who" role="columnheader">Team</div>
            ${days.map((day) => dayHead(day, week.hours)).join("")}
          </div>
          ${week.employees.map((person) => row(person, days, week, manager)).join("")}
          <div class="board-row coverage" role="row">
            <div class="who"><strong>Coverage</strong><em>Need ${week.minimumStaff}</em></div>
            ${days.map((day) => coverageCell(day, week)).join("")}
          </div>
        </div>
      </div>
      <ul class="legend">
        ${positions.map((item) => `<li class="pos-${item.id.toLowerCase()}"><span></span>${esc(item.label)}</li>`).join("")}
        <li class="legend-heat"><span class="heat" data-level="ok"></span> covered</li>
        <li class="legend-heat"><span class="heat" data-level="thin"></span> short</li>
      </ul>
    </section>`;

  bindWeek(main, ctx);
  main.querySelector("#add-shift")?.addEventListener("click", () => openShift(ctx, week, {}));
  main.querySelectorAll("[data-add]").forEach((button) => {
    button.addEventListener("click", () => openShift(ctx, week, {
      employeeId: Number(button.dataset.add),
      day: button.dataset.day
    }));
  });
  main.querySelectorAll("[data-shift]").forEach((button) => {
    button.addEventListener("click", () => {
      const shift = week.shifts.find((item) => item.id === Number(button.dataset.shift));
      if (shift) openShift(ctx, week, { shift });
    });
  });
}

function weekLabelSafe(ctx) {
  const end = addDays(ctx.state.weekStart, 6);
  const left = ctx.state.weekStart.toLocaleDateString("en-CA", { month: "long", day: "numeric" });
  const right = end.toLocaleDateString("en-CA", { month: "short", day: "numeric" });
  return `Week of ${left}–${right}`;
}

function dayHead(day, hours) {
  const window = hours.find((item) => item.dayOfWeek === day.getDay());
  const open = !window || window.isClosed ? "Closed" : rangeLabel(window.open, window.close);
  return `
    <div class="day" role="columnheader">
      <span>${dayNames[day.getDay()]}</span>
      <strong>${day.getDate()}</strong>
      <em>${esc(open)}</em>
    </div>`;
}

function row(person, days, week, manager) {
  const booked = week.shifts
    .filter((shift) => shift.employeeId === person.id)
    .reduce((sum, shift) => sum + hoursBetween(shift.startsAt, shift.endsAt), 0);
  return `
    <div class="board-row" role="row">
      <div class="who">
        <span class="swatch" style="background:${esc(person.color)}"></span>
        <span>
          <strong>${esc(person.firstName)} ${esc(person.lastName)}</strong>
          <em>${trimHours(booked)} / ${trimHours(person.maxWeeklyHours)} h</em>
        </span>
      </div>
      ${days.map((day) => cell(person, day, week, manager)).join("")}
    </div>`;
}

function cell(person, day, week, manager) {
  const key = isoDate(day);
  const shifts = week.shifts.filter((shift) => shift.employeeId === person.id && shift.startsAt.slice(0, 10) === key);
  return `
    <div class="cell" role="cell" data-day="${esc(dayNames[day.getDay()])} ${day.getDate()}">
      ${shifts.map((shift) => `
        <button type="button" class="shift pos-${shift.position.toLowerCase()}" data-shift="${shift.id}">
          <span class="when">${esc(rangeLabel(clock(shift.startsAt), clock(shift.endsAt)))}</span>
          <span class="where">${esc(positionLabel(shift.position))}${shift.flags?.includes("timeoff") ? " · leave" : ""}</span>
        </button>`).join("")}
      ${manager ? `<button type="button" class="add" data-add="${person.id}" data-day="${key}">Add</button>` : ""}
    </div>`;
}

function coverageCell(day, week) {
  const window = week.hours.find((item) => item.dayOfWeek === day.getDay());
  if (!window || window.isClosed) return `<div class="cell quiet">Closed</div>`;
  const blocks = [];
  let cursor = minutes(window.open);
  const close = minutes(window.close);
  while (cursor < close) {
    const next = Math.min(cursor + 60, close);
    const count = week.shifts.filter((shift) => overlapsHour(shift, isoDate(day), cursor, next)).length;
    const level = count >= week.minimumStaff ? "ok" : count === 0 ? "bad" : "thin";
    blocks.push(`<i class="heat" data-level="${level}" title="${labelHour(cursor)}–${labelHour(next)} · ${count} scheduled"></i>`);
    cursor = next;
  }
  return `<div class="cell heats">${blocks.join("")}</div>`;
}

function overlapsHour(shift, day, startMin, endMin) {
  if (shift.startsAt.slice(0, 10) !== day) return false;
  const start = minutes(clock(shift.startsAt));
  const end = minutes(clock(shift.endsAt));
  return start < endMin && end > startMin;
}

function minutes(value) {
  const [hour, minute] = value.split(":").map(Number);
  return hour * 60 + minute;
}

function labelHour(total) {
  const hour = Math.floor(total / 60);
  const minute = total % 60;
  return `${String(hour).padStart(2, "0")}:${String(minute).padStart(2, "0")}`;
}

function trimHours(value) {
  const number = Number(value);
  return Number.isInteger(number) ? String(number) : number.toFixed(1);
}

function openShift(ctx, week, { shift, employeeId, day }) {
  const manager = ctx.state.me.role === "Manager";
  if (!manager) {
    if (!shift) return;
    ctx.openModal(`
      <div class="modal" role="dialog" aria-modal="true" aria-labelledby="shift-title">
        <header>
          <h2 id="shift-title">${esc(shift.employeeName)}</h2>
          <button type="button" data-close aria-label="Close">Close</button>
        </header>
        <p class="lede">${esc(rangeLabel(clock(shift.startsAt), clock(shift.endsAt)))} · ${esc(positionLabel(shift.position))}</p>
        ${shift.notes ? `<p>${esc(shift.notes)}</p>` : ""}
        ${shift.flags?.includes("timeoff") ? `<p class="banner warn">Approved time off covers this day.</p>` : ""}
        ${shift.employeeId === ctx.state.me.id ? `<p class="muted"><a href="#/requests">Request a swap or cover</a> if you can't make it.</p>` : ""}
      </div>`);
    return;
  }

  const selectedEmployee = shift?.employeeId || employeeId || week.employees[0]?.id;
  const selectedDay = shift ? shift.startsAt.slice(0, 10) : (day || isoDate(ctx.state.weekStart));
  const start = shift ? clock(shift.startsAt) : "10:00";
  const end = shift ? clock(shift.endsAt) : "18:00";

  ctx.openModal(`
    <form class="modal" id="shift-form" role="dialog" aria-modal="true" aria-labelledby="shift-title">
      <header>
        <h2 id="shift-title">${shift ? "Edit shift" : "Add a shift"}</h2>
        <button type="button" data-close aria-label="Close">Close</button>
      </header>
      <label>Person
        <select name="employeeId">
          ${week.employees.map((person) => `<option value="${person.id}" ${person.id === selectedEmployee ? "selected" : ""}>${esc(person.firstName)} ${esc(person.lastName)}</option>`).join("")}
        </select>
      </label>
      <label>Day
        <input name="day" type="date" required value="${esc(selectedDay)}" />
      </label>
      <div class="split">
        <label>Start <input name="start" type="time" required value="${esc(start)}" /></label>
        <label>End <input name="end" type="time" required value="${esc(end)}" /></label>
      </div>
      <label>Position
        <select name="position">
          ${positions.map((item) => `<option value="${item.id}" ${shift?.position === item.id ? "selected" : ""}>${esc(item.label)}</option>`).join("")}
        </select>
      </label>
      <label>Notes
        <input name="notes" maxlength="280" value="${esc(shift?.notes || "")}" placeholder="Optional, like a truck delivery" />
      </label>
      <div class="conflicts" hidden></div>
      <footer>
        ${shift ? `<button type="button" class="btn danger" id="delete-shift">Delete</button>` : `<span></span>`}
        <span class="footer-actions">
          <button type="button" class="btn ghost" id="check-shift">Check</button>
          <button type="submit" class="btn primary">Save shift</button>
        </span>
      </footer>
    </form>`, (modal) => {
    const form = modal.querySelector("#shift-form");
    const showConflicts = (messages) => {
      const box = form.querySelector(".conflicts");
      if (!messages?.length) {
        box.hidden = true;
        box.innerHTML = "";
        return;
      }
      box.hidden = false;
      box.innerHTML = messages.map((message) => `<p>${esc(message)}</p>`).join("");
    };
    const payload = () => {
      const data = new FormData(form);
      return {
        employeeId: Number(data.get("employeeId")),
        startsAt: `${data.get("day")}T${String(data.get("start")).slice(0, 5)}:00`,
        endsAt: `${data.get("day")}T${String(data.get("end")).slice(0, 5)}:00`,
        position: data.get("position"),
        notes: data.get("notes")
      };
    };
    form.querySelector("#check-shift").addEventListener("click", async () => {
      try {
        const conflicts = await api("/api/schedule/shifts/preview", {
          method: "POST",
          body: { ...payload(), shiftId: shift?.id ?? null }
        });
        if (!conflicts.length) {
          showConflicts([]);
          ctx.toast("That shift fits.");
        } else {
          showConflicts(conflicts.map((item) => item.message));
        }
      } catch (error) {
        showConflicts([error.message]);
      }
    });
    form.addEventListener("submit", async (event) => {
      event.preventDefault();
      const button = form.querySelector('[type="submit"]');
      button.disabled = true;
      try {
        const body = payload();
        if (shift) await api(`/api/schedule/shifts/${shift.id}`, { method: "PUT", body });
        else await api("/api/schedule/shifts", { method: "POST", body });
        ctx.closeModal();
        ctx.toast(shift ? "Shift updated." : "Shift added.");
        ctx.setWeek(mondayOf(parseIsoDate(body.startsAt.slice(0, 10))));
      } catch (error) {
        const conflicts = error.payload?.conflicts?.map((item) => item.message);
        showConflicts(conflicts?.length ? conflicts : [error.message]);
        button.disabled = false;
      }
    });
    const remove = form.querySelector("#delete-shift");
    remove?.addEventListener("click", async () => {
      if (remove.dataset.armed !== "yes") {
        remove.dataset.armed = "yes";
        remove.textContent = "Confirm delete";
        return;
      }
      remove.disabled = true;
      try {
        await api(`/api/schedule/shifts/${shift.id}`, { method: "DELETE" });
        ctx.closeModal();
        ctx.toast("Shift removed.");
        ctx.reload();
      } catch (error) {
        ctx.toast(error.message, "bad");
        remove.disabled = false;
      }
    });
  });
}
