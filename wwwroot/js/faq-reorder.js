// FAQ ordering: drag & drop and the arrow buttons, like the division order.
// The list order is the order the app shows. Every change saves the order of
// the questions on the page in one request (with the category filter, if any).
document.addEventListener("DOMContentLoaded", () => {
    const list = document.getElementById("faqList");
    if (!list) return;

    let dragged = null;      // the .faq-card being dragged
    let saving = false;

    const items = () => Array.from(list.querySelectorAll(".faq-card[data-faq-id]"));

    function token() {
        const input = document.querySelector('input[name="__RequestVerificationToken"]');
        return input ? input.value : "";
    }

    function renumber() {
        const all = items();
        all.forEach((el, i) => {
            const badge = el.querySelector(".faq-order-badge");
            if (badge) badge.textContent = String(i + 1);

            const up = el.querySelector('.faq-sort-button[data-dir="UP"]');
            const down = el.querySelector('.faq-sort-button[data-dir="DOWN"]');
            if (up) up.disabled = i === 0;
            if (down) down.disabled = i === all.length - 1;
        });
    }

    // Where a drop would land: next to the card under the pointer, or next to the
    // nearest card when the pointer is in a gap between cards. Null over the dragged card.
    function dropTarget(e) {
        const over = e.target.closest(".faq-card[data-faq-id]");
        if (over === dragged) return null;

        if (over) {
            const rect = over.getBoundingClientRect();
            return { card: over, after: e.clientY > rect.top + rect.height / 2 };
        }

        const cards = items().filter(el => el !== dragged);
        for (const card of cards) {
            const rect = card.getBoundingClientRect();
            if (e.clientY < rect.top + rect.height / 2) return { card, after: false };
        }

        const last = cards[cards.length - 1];
        return last ? { card: last, after: true } : null;
    }

    function clearMarkers() {
        list.querySelectorAll(".is-drop-before, .is-drop-after")
            .forEach(el => el.classList.remove("is-drop-before", "is-drop-after"));
    }

    async function save(previousOrder) {
        if (saving) return;

        saving = true;
        list.classList.add("is-saving");

        try {
            const res = await fetch("/Faqs/Reorder", {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                    "RequestVerificationToken": token()
                },
                body: JSON.stringify({
                    ids: items().map(el => Number(el.dataset.faqId)),
                    category: list.dataset.category || null
                })
            });

            if (!res.ok) {
                const data = await res.json().catch(() => null);

                if (data && data.error) throw new Error(data.error);

                // An empty 400 means the page's security token is stale, which happens
                // when the app was restarted while this page was open.
                if (res.status === 400) {
                    throw new Error("This page is out of date (the app was restarted). Reload the page and try again.");
                }

                throw new Error("Could not save the new order (error " + res.status + ").");
            }

            // Saved: the page already shows the new order
            renumber();
        } catch (err) {
            // Put the questions back the way they were
            previousOrder.forEach(el => list.appendChild(el));
            renumber();
            alert(err.message);
        } finally {
            saving = false;
            list.classList.remove("is-saving");
        }
    }

    // ---- Arrow buttons: move one place and save ----
    list.addEventListener("click", (e) => {
        const btn = e.target.closest(".faq-sort-button");
        if (!btn) return;

        e.preventDefault();

        const card = btn.closest(".faq-card");
        if (!card || saving) return;

        const before = items();
        const index = before.indexOf(card);
        const targetIndex = btn.dataset.dir === "UP" ? index - 1 : index + 1;
        if (targetIndex < 0 || targetIndex >= before.length) return;

        const other = before[targetIndex];
        if (btn.dataset.dir === "UP") list.insertBefore(card, other);
        else list.insertBefore(card, other.nextSibling);

        renumber();
        save(before);
    });

    // ---- Drag & drop ----
    list.addEventListener("dragstart", (e) => {
        const card = e.target.closest && e.target.closest(".faq-card[data-faq-id]");
        if (!card || saving) return;

        dragged = card;
        card.classList.add("is-dragging");

        if (e.dataTransfer) {
            e.dataTransfer.effectAllowed = "move";
            e.dataTransfer.setData("text/plain", card.dataset.faqId);
        }
    });

    list.addEventListener("dragover", (e) => {
        if (!dragged) return;

        e.preventDefault();
        if (e.dataTransfer) e.dataTransfer.dropEffect = "move";

        clearMarkers();
        const target = dropTarget(e);
        if (target) target.card.classList.add(target.after ? "is-drop-after" : "is-drop-before");
    });

    list.addEventListener("drop", (e) => {
        if (!dragged) return;

        e.preventDefault();

        const target = dropTarget(e);
        const before = items();
        const moving = dragged;

        if (target) list.insertBefore(moving, target.after ? target.card.nextSibling : target.card);

        clearMarkers();
        moving.classList.remove("is-dragging");
        dragged = null;

        const changed = items().some((el, i) => el !== before[i]);
        if (!changed) return;

        renumber();
        save(before);
    });

    list.addEventListener("dragend", () => {
        if (dragged) dragged.classList.remove("is-dragging");
        dragged = null;
        clearMarkers();
    });
});
