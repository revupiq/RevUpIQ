document.addEventListener("DOMContentLoaded", function () {

    document.addEventListener("click", function (e) {

        const gameBtn = e.target.closest(".filter-mode");
        if (gameBtn) {

            document.querySelectorAll(".filter-mode").forEach(b =>
                b.classList.remove("selected")
            );

            gameBtn.classList.add("selected");
            return;
        }


        const day = e.target.closest(".filter-date");
        if (day) {

            document.querySelectorAll(".filter-date").forEach(d =>
                d.classList.remove("selected")
            );

            day.classList.add("selected");
        }

    });

});
