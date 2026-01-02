document.addEventListener("click", async function (e) {
    const modeBtn = e.target.closest(".filter-mode");
    const timeBtn = e.target.closest(".filter-time");
    const divisionSelect = document.querySelector(".division-select");

    if (!modeBtn && !timeBtn && e.target !== divisionSelect) return;

    if (modeBtn) {
        document.querySelectorAll(".filter-mode").forEach(b =>
            b.classList.remove("selected")
        );
        modeBtn.classList.add("selected");
    }

    if (timeBtn) {
        document.querySelectorAll(".filter-time").forEach(b =>
            b.classList.remove("selected")
        );
        timeBtn.classList.add("selected");
    }

    const mode =
        (modeBtn ?? document.querySelector(".filter-mode.selected"))?.dataset.mode ?? "All";

    const time =
        (timeBtn ?? document.querySelector(".filter-time.selected"))?.dataset.time ?? "Daily";

    const divisionId = divisionSelect?.value ?? 0;

    const url = `/Leaderboard/LoadLeaderboard?mode=${mode}&time=${time}&divisionId=${divisionId}`;
    const response = await fetch(url);
    const html = await response.text();

    document.getElementById("leaderboardHost").innerHTML = html;
});
