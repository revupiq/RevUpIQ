document.addEventListener("DOMContentLoaded", function () {
    let selectedMode = document.querySelector(".filter-mode.selected")?.dataset.mode
        || document.getElementById("model-Mode")?.textContent
        || "";

    let selectedDate = (document.getElementById("model-Date")?.textContent || "").trim();

    let selectedTime = document.querySelector(".filter-time.selected")?.dataset.time
        || "Daily";

    function getDivisionId() {
        const el = document.getElementById("model-Id");
        return el ? el.textContent : null;
    }

    function updateCalendarModeText() {
        const label = document.querySelector(`.filter-mode.selected .game-mode-label`)?.textContent
            || selectedMode
            || "";
        const target = document.getElementById("calendar-mode-text");
        if (target) target.textContent = label;
    }

    function parseYM(iso) {
        const p = (iso || "").split("-");
        if (p.length !== 3) return null;
        const y = parseInt(p[0], 10);
        const m = parseInt(p[1], 10);
        if (!y || !m) return null;
        return { y, m };
    }

    let cursor = parseYM(selectedDate) || { y: new Date().getFullYear(), m: new Date().getMonth() + 1 };

    function shiftCursor(delta) {
        cursor.m += delta;
        if (cursor.m < 1) { cursor.m = 12; cursor.y -= 1; }
        if (cursor.m > 12) { cursor.m = 1; cursor.y += 1; }
    }

    function loadCalendar() {
        const divisionId = getDivisionId();
        if (!selectedMode || !divisionId) return;

        const loading = document.getElementById("calendarLoading");
        const host = document.getElementById("calendarHost");

        if (loading) loading.style.display = "block";
        if (host) host.style.display = "none";

        const baseUrl =
            `/MiniGames/Calendar?divisionId=${encodeURIComponent(divisionId)}&mode=${encodeURIComponent(selectedMode)}&year=${cursor.y}&month=${cursor.m}`;

        const url = selectedDate
            ? `${baseUrl}&date=${encodeURIComponent(selectedDate)}`
            : baseUrl;

        fetch(url)
            .then(r => r.text())
            .then(html => {
                if (host) {
                    host.innerHTML = html;
                    host.style.display = "block";
                }
            })
            .finally(() => {
                if (loading) loading.style.display = "none";
            });
    }


    function loadMiniGameSetting() {
        if (!selectedMode) return;

        fetch(`/MiniGames/MiniGameSetting?mode=${encodeURIComponent(selectedMode)}`)
            .then(r => r.text())
            .then(html => {
                const host = document.getElementById("miniGameSettingHost");
                if (host) host.innerHTML = html;
            });
    }

    function loadMiniGame() {
        const divisionId = getDivisionId();
        if (!selectedMode || !selectedDate || !divisionId) return;

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
                const host = document.getElementById("miniGameHost");
                if (host) host.innerHTML = html;
            });
    }

    updateCalendarModeText();

    document.addEventListener("click", function (e) {
        if (e.target.id === "prev-month") {
            shiftCursor(-1);
            selectedDate = "";
            loadCalendar();
            return;
        }

        if (e.target.id === "next-month") {
            shiftCursor(1);
            selectedDate = "";
            loadCalendar();
            return;
        }

        const gameBtn = e.target.closest(".filter-mode");
        if (gameBtn) {
            document.querySelectorAll(".filter-mode").forEach(b => b.classList.remove("selected"));
            gameBtn.classList.add("selected");

            selectedMode = gameBtn.dataset.mode;

            selectedDate = "";
            loadCalendar();
            updateCalendarModeText();
            loadMiniGameSetting();
            return;
        }

        const day = e.target.closest(".filter-date");
        if (day) {
            document.querySelectorAll(".filter-date").forEach(d => d.classList.remove("selected"));
            day.classList.add("selected");

            selectedDate = day.dataset.date;

            const ym = parseYM(selectedDate);
            if (ym) cursor = ym;

            loadMiniGame();
            return;
        }

        const timeBtn = e.target.closest(".filter-time");
        if (timeBtn) {
            document.querySelectorAll(".filter-time").forEach(t => t.classList.remove("selected"));
            timeBtn.classList.add("selected");
            selectedTime = timeBtn.dataset.time;
            loadMiniGame();
            return;
        }
    });

    loadCalendar();
    loadMiniGameSetting();
    loadMiniGame();
});
