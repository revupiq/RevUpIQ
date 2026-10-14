// Division ordering: drag & drop and the arrow buttons.
// The visual order is the order players see (number 1 = first). Every change
// saves the complete order in one request so it can never leave ties or gaps.
document.addEventListener("DOMContentLoaded", () => {
    let dragged = null;      // the .division-link being dragged
    let saving = false;

    const grid = () => document.getElementById("divisionOrderGrid");
    const items = () =>
        Array.from(grid()?.querySelectorAll(".division-link[data-division-id]") ?? []);

    function token() {
        const input = document.querySelector('input[name="__RequestVerificationToken"]');
        return input ? input.value : "";
    }

    function renumber() {
        items().forEach((el, i) => {
            const badge = el.querySelector(".division-order-badge");
            if (badge) badge.textContent = String(i + 1);
        });
    }

    function clearMarkers() {
        document
            .querySelectorAll(".is-drop-before, .is-drop-after")
            .forEach(el => el.classList.remove("is-drop-before", "is-drop-after"));
    }

    async function save(previousOrder) {
        const g = grid();
        if (!g || saving) return;

        saving = true;
        g.classList.add("is-saving");

        try {
            const res = await fetch("/Divisions/Reorder", {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                    "RequestVerificationToken": token()
                },
                body: JSON.stringify({ ids: items().map(el => Number(el.dataset.divisionId)) })
            });

            if (!res.ok) {
                const data = await res.json().catch(() => null);
                throw new Error((data && data.error) || "Could not save the new order.");
            }

            // Reload so the page (and any ties warning) reflects what was saved
            location.reload();
        } catch (err) {
            // Put the cards back the way they were
            const create = g.querySelector(".division-create-card");
            previousOrder.forEach(el => g.insertBefore(el, create));
            renumber();
            alert(err.message);
        } finally {
            saving = false;
            g.classList.remove("is-saving");
        }
    }

    // ---- Arrow buttons (kept; they move one place and save the whole order) ----
    document.addEventListener("click", (e) => {
        const btn = e.target.closest(".division-sort-button");
        if (!btn) return;

        e.preventDefault();
        e.stopPropagation();

        const link = btn.closest(".division-link");
        const g = grid();
        if (!link || !g || saving) return;

        const before = items();
        const index = before.indexOf(link);
        const targetIndex = btn.dataset.dir === "LEFT" ? index - 1 : index + 1;
        if (targetIndex < 0 || targetIndex >= before.length) return;

        const other = before[targetIndex];
        if (btn.dataset.dir === "LEFT") g.insertBefore(link, other);
        else g.insertBefore(link, other.nextSibling);

        renumber();
        save(before);
    }, true);

    // ---- Drag & drop ----
    document.addEventListener("dragstart", (e) => {
        const link = e.target.closest && e.target.closest(".division-link[data-division-id]");
        if (!link || saving) return;

        dragged = link;
        link.classList.add("is-dragging");

        if (e.dataTransfer) {
            e.dataTransfer.effectAllowed = "move";
            e.dataTransfer.setData("text/plain", link.dataset.divisionId);
        }
    });

    document.addEventListener("dragover", (e) => {
        if (!dragged) return;

        const over = e.target.closest && e.target.closest(".division-link[data-division-id]");
        const g = grid();
        if (!g || !g.contains(e.target)) return;

        e.preventDefault();
        if (e.dataTransfer) e.dataTransfer.dropEffect = "move";

        clearMarkers();
        if (!over || over === dragged) return;

        const rect = over.getBoundingClientRect();
        const after = e.clientX > rect.left + rect.width / 2;
        over.classList.add(after ? "is-drop-after" : "is-drop-before");
    });

    document.addEventListener("drop", (e) => {
        if (!dragged) return;

        const g = grid();
        if (!g || !g.contains(e.target)) return;

        e.preventDefault();

        const over = e.target.closest(".division-link[data-division-id]");
        const before = items();
        const moving = dragged;

        if (over && over !== moving) {
            const rect = over.getBoundingClientRect();
            const after = e.clientX > rect.left + rect.width / 2;
            g.insertBefore(moving, after ? over.nextSibling : over);
        } else if (!over) {
            // Dropped on empty space in the grid: send it to the end
            g.insertBefore(moving, g.querySelector(".division-create-card"));
        }

        clearMarkers();
        moving.classList.remove("is-dragging");
        dragged = null;

        const after = items();
        const changed = after.some((el, i) => el !== before[i]);
        if (!changed) return;

        renumber();
        save(before);
    });

    document.addEventListener("dragend", () => {
        if (dragged) dragged.classList.remove("is-dragging");
        dragged = null;
        clearMarkers();
    });
});
