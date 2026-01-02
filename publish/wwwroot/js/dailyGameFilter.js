document.addEventListener("DOMContentLoaded", function () {

    let selectedMode = document.querySelector(".filter-mode.selected")?.dataset.mode
        || document.getElementById("model-Mode")?.textContent;

    let selectedDate = document.querySelector(".filter-date.selected")?.dataset.date
        || document.getElementById("model-Date")?.textContent;

    document.addEventListener("click", function (e) {

        // ---------- GAME MODE ----------
        const gameBtn = e.target.closest(".filter-mode");
        if (gameBtn) {
            document.querySelectorAll(".filter-mode").forEach(b =>
                b.classList.remove("selected")
            );
            gameBtn.classList.add("selected");
            selectedMode = gameBtn.dataset.mode;

            loadMiniGame();
            return;
        }

        // ---------- DATE ----------
        const day = e.target.closest(".filter-date");
        if (day) {
            document.querySelectorAll(".filter-date").forEach(d =>
                d.classList.remove("selected")
            );
            day.classList.add("selected");
            selectedDate = day.dataset.date;

            loadMiniGame();
        }
    });

    function loadMiniGame() {
        const divisionIdEl = document.getElementById("model-Id");
        const divisionId = divisionIdEl ? divisionIdEl.textContent : null;

        if (!selectedMode || !selectedDate || !divisionId) {
            return;
        }

        fetch("/MiniGames/SetGameMode", {
            method: "POST",
            headers: { "Content-Type": "application/x-www-form-urlencoded; charset=UTF-8" },
            body: new URLSearchParams({
                DivisionId: divisionId,
                Mode: selectedMode,
                Date: selectedDate
            }).toString()
        })
        .then(r => r.text())
        .then(html => {
            document.getElementById("miniGameHost").innerHTML = html;
        });
    }

});
