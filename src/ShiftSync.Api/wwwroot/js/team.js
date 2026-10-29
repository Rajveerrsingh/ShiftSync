import { api, dayNames, esc, rangeLabel } from "./api.js";
import { bindRetry, pageStatus } from "./ui.js";

const fullDays = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"];

export async function renderTeam(main, ctx) {
  main.innerHTML = `<section class="page"><p class="loading">Loading the roster…</p></section>`;
  try {
    const people = await api("/api/employees");
    draw(main, ctx, people);
  } catch (error) {
    main.innerHTML = pageStatus(error.message, "bad", true);
    bindRetry(main, ctx);
  }
}

function draw(main, ctx, people) {
  const manager = ctx.state.me.role === "Manager";
  main.innerHTML = `
    <section class="page">
      <header class="page-head">
        <div>
          <p class="kicker">Northline Outfitters</p>
          <h1>The floor team</h1>
        </div>
        ${manager ? `<button type="button" class="btn primary" id="add-person">Add someone</button>` : ""}
      </header>
      ${people.length === 0 ? `<div class="banner">The roster is empty.</div>` : ""}
      <div class="cards">
        ${people.map((person) => card(person, ctx)).join("")}
      </div>
    </section>`;

  main.querySelector("#add-person")?.addEventListener("click", () => openCreate(ctx));
  main.querySelectorAll("[data-hours]").forEach((button) => {
    const person = people.find((item) => item.id === Number(button.dataset.hours));
    button.addEventListener("click", () => openAvailability(ctx, person));
  });
  main.querySelectorAll("[data-edit]").forEach((button) => {
    const person = people.find((item) => item.id === Number(button.dataset.edit));
    button.addEventListener("click", () => openEdit(ctx, person));
  });
}

function card(person, ctx) {
  const manager = ctx.state.me.role === "Manager";
  const mine = person.id === ctx.state.me.id;
  const canEditHours = manager || mine;
  const chips = person.availability.length
    ? person.availability.map((window) => `<li>${dayNames[window.dayOfWeek]} ${esc(rangeLabel(window.start, window.end))}</li>`).join("")
    : `<li class="muted">No hours set</li>`;
  return `
    <article class="person ${person.isActive ? "" : "inactive"}">
      <header>
        <span class="avatar" style="background:${esc(person.color)}">${esc(person.firstName[0])}${esc(person.lastName[0])}</span>
        <span>
          <strong>${esc(person.firstName)} ${esc(person.lastName)}</strong>
          <em>${esc(person.role)}${person.isActive ? "" : " · inactive"}</em>
        </span>
      </header>
      <p>${esc(person.email)}</p>
      <p class="muted">${person.phone ? esc(person.phone) + " · " : ""}${trim(person.maxWeeklyHours)} h cap</p>
      <ul class="chips">${chips}</ul>
      <div class="card-actions">
        ${canEditHours ? `<button type="button" class="btn ghost" data-hours="${person.id}">${mine && !manager ? "My hours" : "Availability"}</button>` : ""}
        ${manager ? `<button type="button" class="btn ghost" data-edit="${person.id}">Profile</button>` : ""}
      </div>
    </article>`;
}

function openCreate(ctx) {
  ctx.openModal(`
    <form class="modal" id="person-form" role="dialog" aria-modal="true" aria-labelledby="person-title">
      <header>
        <h2 id="person-title">Add someone</h2>
        <button type="button" data-close aria-label="Close">Close</button>
      </header>
      <div class="split">
        <label>First name <input name="firstName" required maxlength="60" /></label>
        <label>Last name <input name="lastName" required maxlength="60" /></label>
      </div>
      <label>Email <input name="email" type="email" required /></label>
      <label>Phone <input name="phone" maxlength="30" /></label>
      <div class="split">
        <label>Role
          <select name="role">
            <option value="Associate">Associate</option>
            <option value="Manager">Manager</option>
          </select>
        </label>
        <label>Weekly cap <input name="maxWeeklyHours" type="number" min="1" max="60" value="32" required /></label>
      </div>
      <label>Temporary password <input name="password" type="text" minlength="8" required value="Northline#1" /></label>
      <p class="form-error" hidden></p>
      <footer>
        <span></span>
        <button class="btn primary" type="submit">Add to the roster</button>
      </footer>
    </form>`, (modal) => {
    const form = modal.querySelector("#person-form");
    form.addEventListener("submit", async (event) => {
      event.preventDefault();
      const data = Object.fromEntries(new FormData(form).entries());
      data.maxWeeklyHours = Number(data.maxWeeklyHours);
      try {
        await api("/api/employees", { method: "POST", body: data });
        ctx.closeModal();
        ctx.toast(`${data.firstName} is on the roster.`);
        ctx.reload();
      } catch (error) {
        const box = form.querySelector(".form-error");
        box.textContent = error.message;
        box.hidden = false;
      }
    });
  });
}

function openEdit(ctx, person) {
  ctx.openModal(`
    <form class="modal" id="edit-form" role="dialog" aria-modal="true" aria-labelledby="edit-title">
      <header>
        <h2 id="edit-title">Edit ${esc(person.firstName)}</h2>
        <button type="button" data-close aria-label="Close">Close</button>
      </header>
      <div class="split">
        <label>First name <input name="firstName" required maxlength="60" value="${esc(person.firstName)}" /></label>
        <label>Last name <input name="lastName" required maxlength="60" value="${esc(person.lastName)}" /></label>
      </div>
      <label>Phone <input name="phone" maxlength="30" value="${esc(person.phone)}" /></label>
      <label>Weekly cap <input name="maxWeeklyHours" type="number" min="1" max="60" required value="${esc(person.maxWeeklyHours)}" /></label>
      <label class="check"><input name="isActive" type="checkbox" ${person.isActive ? "checked" : ""} /> Active on the schedule</label>
      <p class="form-error" hidden></p>
      <footer>
        <span></span>
        <button class="btn primary" type="submit">Save profile</button>
      </footer>
    </form>`, (modal) => {
    const form = modal.querySelector("#edit-form");
    form.addEventListener("submit", async (event) => {
      event.preventDefault();
      const data = new FormData(form);
      try {
        await api(`/api/employees/${person.id}`, {
          method: "PUT",
          body: {
            firstName: data.get("firstName"),
            lastName: data.get("lastName"),
            phone: data.get("phone"),
            maxWeeklyHours: Number(data.get("maxWeeklyHours")),
            isActive: data.get("isActive") === "on"
          }
        });
        ctx.closeModal();
        ctx.toast("Profile saved.");
        ctx.reload();
      } catch (error) {
        const box = form.querySelector(".form-error");
        box.textContent = error.message;
        box.hidden = false;
      }
    });
  });
}

function openAvailability(ctx, person) {
  const rows = fullDays.map((name, index) => {
    const window = person.availability.find((item) => item.dayOfWeek === index);
    return `
      <div class="avail-row">
        <label class="check"><input type="checkbox" name="on-${index}" ${window ? "checked" : ""} /> ${name}</label>
        <input type="time" name="start-${index}" value="${esc(window?.start || "10:00")}" />
        <input type="time" name="end-${index}" value="${esc(window?.end || "18:00")}" />
      </div>`;
  }).join("");

  ctx.openModal(`
    <form class="modal" id="hours-form" role="dialog" aria-modal="true" aria-labelledby="hours-title">
      <header>
        <h2 id="hours-title">${esc(person.firstName)}'s hours</h2>
        <button type="button" data-close aria-label="Close">Close</button>
      </header>
      <p class="muted">A shift has to sit inside one of these windows. Uncheck a day they can't work.</p>
      ${rows}
      <p class="form-error" hidden></p>
      <footer>
        <span></span>
        <button class="btn primary" type="submit">Save availability</button>
      </footer>
    </form>`, (modal) => {
    const form = modal.querySelector("#hours-form");
    form.addEventListener("submit", async (event) => {
      event.preventDefault();
      const data = new FormData(form);
      const windows = [];
      for (let day = 0; day < 7; day += 1) {
        if (data.get(`on-${day}`) !== "on") continue;
        windows.push({ dayOfWeek: day, start: data.get(`start-${day}`), end: data.get(`end-${day}`) });
      }
      try {
        await api(`/api/employees/${person.id}/availability`, { method: "PUT", body: windows });
        ctx.closeModal();
        ctx.toast("Availability saved.");
        ctx.reload();
      } catch (error) {
        const box = form.querySelector(".form-error");
        box.textContent = error.message;
        box.hidden = false;
      }
    });
  });
}

function trim(value) {
  const number = Number(value);
  return Number.isInteger(number) ? String(number) : number.toFixed(1);
}
