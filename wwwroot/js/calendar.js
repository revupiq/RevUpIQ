(function () {

    let today = new Date();
    let current = new Date(today.getFullYear(), today.getMonth(), 1);

    function pad(n) { return n < 10 ? "0" + n : n; }

    function renderCalendar() {
        const title = document.getElementById("calendar-title");
        const daysContainer = document.getElementById("calendar-days");

        if (!title || !daysContainer) return false;

        let year = current.getFullYear();
        let month = current.getMonth();
        let monthName = current.toLocaleString("en-US", { month: "long" });

        title.textContent = monthName + " " + year;

        let daysInMonth = new Date(year, month + 1, 0).getDate();
        let html = "";

        for (let d = 1; d <= daysInMonth; d++) {
            let date = new Date(year, month, d);
            let iso = year + "-" + pad(month + 1) + "-" + pad(d);

            let isToday =
                date.getFullYear() === today.getFullYear() &&
                date.getMonth() === today.getMonth() &&
                date.getDate() === today.getDate();

            html += `
                <div class="calendar-day filter-date ${isToday ? "today selected" : ""}"
                     data-filter-name="GameDate"
                     data-date="${iso}">
                     <div class="calendar-week">${date.toLocaleString("en-US", { weekday: "short" })}</div>
                     <div class="calendar-number">${d}</div>
                     <div class="calendar-month">${date.toLocaleString("en-US", { month: "short" })}</div>
                     ${isToday ? "<div class='calendar-today'>Today</div>" : ""}
                </div>
            `;
        }

        daysContainer.innerHTML = html;
        return true;
    }

    function waitForCalendar() {
        if (renderCalendar()) return;
        setTimeout(waitForCalendar, 50);
    }

    document.addEventListener("click", function (e) {
        if (e.target.id === "prev-month") {
            current.setMonth(current.getMonth() - 1);
            renderCalendar();
        }

        if (e.target.id === "next-month") {
            current.setMonth(current.getMonth() + 1);
            renderCalendar();
        }
    });

    waitForCalendar();

})();
