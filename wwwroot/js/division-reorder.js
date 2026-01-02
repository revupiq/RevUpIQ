document.addEventListener("DOMContentLoaded", () => {
    console.log("[division-reorder] loaded");

    document.addEventListener("click", async (e) => {
        const btn = e.target.closest(".division-sort-button");
        if (!btn) return;

        e.preventDefault();
        e.stopPropagation();

        const card = btn.closest(".division-card");
        if (!card) return;

        const id = card.dataset.id;

        const dir = btn.dataset.dir === "LEFT" ? "DOWN" : "UP";

        console.log(`Division ${id} move ${dir}`);

        try {
            const res = await fetch("/Divisions/Move", {
                method: "POST",
                headers: {
                    "Content-Type": "application/x-www-form-urlencoded"
                },
                body: `id=${id}&direction=${dir}`
            });

            if (!res.ok) {
                console.error("Move failed");
                return;
            }

            location.reload();
        }
        catch (err) {
            console.error("Request error", err);
        }

    }, true);
});
