import { addDays, esc, isoDate, mondayOf, weekLabel } from "./api.js";

export function weekControls(weekStart) {
  return `
    <div class="week-controls">
      <button type="button" data-week="-1" aria-label="Previous week">Prev</button>
      <button type="button" data-week="0">This week</button>
      <button type="button" data-week="1" aria-label="Next week">Next</button>
      <strong>${esc(weekLabel(weekStart))}</strong>
    </div>`;
}

export function bindWeek(scope, ctx) {
  scope.querySelectorAll("[data-week]").forEach((button) => {
    button.addEventListener("click", () => {
      const direction = Number(button.dataset.week);
      ctx.setWeek(direction === 0 ? mondayOf(new Date()) : addDays(ctx.state.weekStart, direction * 7));
    });
  });
}

export function pageStatus(message, tone, retry) {
  return `
    <section class="page">
      <div class="banner ${tone}">
        <p>${esc(message)}</p>
        ${retry ? '<button type="button" id="retry">Try again</button>' : ""}
      </div>
    </section>`;
}

export function bindRetry(scope, ctx) {
  scope.querySelector("#retry")?.addEventListener("click", () => ctx.reload());
}

export { isoDate };
