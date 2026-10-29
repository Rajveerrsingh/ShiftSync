import { api, clock, esc, isoDate, longDate, positionLabel, rangeLabel } from "./api.js";
import { bindRetry, bindWeek, pageStatus, weekControls } from "./ui.js";

export async function renderDashboard(main, ctx) {
  main.innerHTML = `<section class="page"><p class="loading">Adding up the week…</p></section>`;
  try {
    const dash = await api(`/api/dashboard?weekStart=${isoDate(ctx.state.weekStart)}`);
    draw(main, ctx, dash);
  } catch (error) {
    main.innerHTML = pageStatus(error.message, "bad", true);
    bindRetry(main, ctx);
  }
}

function draw(main, ctx, dash) {
  const peak = Math.max(1, ...dash.days.map((day) => day.hours));
  main.innerHTML = `
    <section class="page">
      <header class="page-head">
        <div>
          <p class="kicker">${esc(dash.storeName)}</p>
          <h1>Labour and coverage</h1>
        </div>
        ${weekControls(ctx.state.weekStart)}
      </header>
      <div class="stats">
        <article>
          <span>Hours scheduled</span>
          <strong>${trim(dash.totalHours)}</strong>
        </article>
        <article>
          <span>People on the grid</span>
          <strong>${dash.scheduledPeople}</strong>
        </article>
        <article>
          <span>Coverage gaps</span>
          <strong>${dash.gapCount}</strong>
        </article>
        <article>
          <span>Waiting on you</span>
          <strong>${dash.pendingTimeOff + dash.pendingSwaps}</strong>
          <a href="#/requests">Open requests</a>
        </article>
      </div>
      <div class="split-panels">
        <section class="panel">
          <h2>Hours by day</h2>
          <div class="bars">
            ${dash.days.map((day) => `
              <div class="bar-row">
                <span>${esc(longDate(day.date).slice(0, 3))}</span>
                <div class="track"><div class="fill" style="width:${Math.round((day.hours / peak) * 100)}%"></div></div>
                <em>${trim(day.hours)} h</em>
              </div>`).join("")}
          </div>
        </section>
        <section class="panel">
          <h2>By position</h2>
          ${dash.positions.length === 0 ? `<p class="muted">No shifts this week.</p>` : `
            <ul class="position-list">
              ${dash.positions.map((item) => `
                <li>
                  <span class="dot pos-${item.position.toLowerCase()}"></span>
                  ${esc(positionLabel(item.position))}
                  <strong>${trim(item.hours)} h</strong>
                </li>`).join("")}
            </ul>`}
        </section>
      </div>
      <section class="panel">
        <h2>Where the floor is thin</h2>
        ${dash.gaps.length === 0
          ? `<p class="muted">Every open hour has the minimum on the floor.</p>`
          : `<ul class="gap-list">
              ${dash.gaps.map((gap) => `
                <li>
                  <strong>${esc(longDate(gap.date))}</strong>
                  <span>${esc(rangeLabel(gap.start, gap.end))}</span>
                  <em>${gap.scheduled} scheduled, need ${gap.minimum}</em>
                </li>`).join("")}
            </ul>`}
      </section>
      <section class="panel">
        <h2>Today</h2>
        ${todayCopy(dash)}
      </section>
    </section>`;
  bindWeek(main, ctx);
}

function todayCopy(dash) {
  if (!dash.includesToday) return `<p class="muted">Today isn't in this week. Flip back to the current week to see who's in.</p>`;
  if (!dash.today.length) return `<p class="muted">Nobody is rostered today.</p>`;
  return `<ul class="today-list">
    ${dash.today.map((shift) => `
      <li>
        <strong>${esc(shift.employeeName)}</strong>
        <span>${esc(rangeLabel(clock(shift.startsAt), clock(shift.endsAt)))}</span>
        <em>${esc(positionLabel(shift.position))}</em>
      </li>`).join("")}
  </ul>`;
}

function trim(value) {
  const number = Number(value);
  return Number.isInteger(number) ? String(number) : number.toFixed(1);
}
