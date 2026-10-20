document.addEventListener("DOMContentLoaded", function () {
    const modal = document.getElementById("editModal");
    const openBtn = document.getElementById("openEdit");
    const closeBtn = document.getElementById("closeEdit");
    const form = document.querySelector("#editModal form");

    const bgInput = document.getElementById("divisionBackgroundInput");
    const divisionIdEl =
        document.querySelector('input[name="id"]') ||
        document.querySelector('input[name="Id"]') ||
        document.querySelector('input[name="divisionId"]') ||
        document.querySelector('input[name="DivisionId"]');

    const antiForgeryToken = document.querySelector('input[name="__RequestVerificationToken"]')?.value;

    if (openBtn && modal) {
        openBtn.addEventListener("click", function () {
            modal.style.display = "flex";
        });
    }

    if (closeBtn && modal) {
        closeBtn.addEventListener("click", function () {
            modal.style.display = "none";
        });
    }

    if (modal) {
        modal.addEventListener("click", function (e) {
            if (e.target === modal) modal.style.display = "none";
        });
    }

    if (form) {
        form.addEventListener("submit", function (e) {
            e.preventDefault();

            const url = form.action;
            const formData = new FormData(form);

            const headers = { "X-Requested-With": "XMLHttpRequest" };
            if (antiForgeryToken) headers["RequestVerificationToken"] = antiForgeryToken;

            fetch(url, { method: "POST", body: formData, headers })
                .then(async function (response) {
                    if (response.ok) {
                        if (modal) modal.style.display = "none";
                        window.location.reload();
                        return;
                    }

                    const text = await response.text().catch(() => "");
                    alert(text || "Error saving division.");
                })
                .catch(function () {
                    alert("Error saving division.");
                });
        });
    }

    // ---------- Store IDs (locked on the page, edited in a modal) ----------

    const storeIdsModal = document.getElementById("storeIdsModal");
    const storeIdsForm = document.getElementById("storeIdsForm");
    const openStoreIdsBtn = document.getElementById("openStoreIds");
    const closeStoreIdsBtn = document.getElementById("closeStoreIds");

    if (storeIdsModal && storeIdsForm) {
        // Closing drops unsaved edits so the modal reopens with the saved IDs
        function closeStoreIds() {
            storeIdsForm.reset();
            storeIdsModal.style.display = "none";
        }

        if (openStoreIdsBtn) {
            openStoreIdsBtn.addEventListener("click", function () {
                storeIdsModal.style.display = "flex";
            });
        }

        if (closeStoreIdsBtn) closeStoreIdsBtn.addEventListener("click", closeStoreIds);

        storeIdsModal.addEventListener("click", function (e) {
            if (e.target === storeIdsModal) closeStoreIds();
        });

        // Lists each changed ID (old -> new) and saves only after the admin confirms
        storeIdsForm.addEventListener("submit", function (e) {
            const changes = [];

            storeIdsForm.querySelectorAll("input.admin-input").forEach(function (input) {
                const before = input.defaultValue.trim();
                const after = input.value.trim();
                if (before === after) return;

                const label = storeIdsForm.querySelector('label[for="' + input.id + '"]').textContent.trim();
                changes.push(label + ":\n    " + (before || "(not set)") + "  →  " + (after || "REMOVED"));
            });

            if (changes.length === 0) {
                e.preventDefault();
                closeStoreIds();
                return;
            }

            const divisionName = storeIdsForm.dataset.divisionName || "this division";
            const message =
                "Change the store IDs for " + divisionName + "?\n\n" +
                changes.join("\n\n") +
                "\n\nThe app uses these IDs to sell this division's subscription.";

            if (!confirm(message)) e.preventDefault();
        });
    }

    // ---------- Image uploads (file picker or drag & drop) ----------

    // Posts a file to the server, reloads the page on success.
    // `busyEl` gets the "is-uploading" class while the request is running.
    function uploadImage(url, fields, busyEl, errorMessage) {
        const divisionId = divisionIdEl ? divisionIdEl.value : null;
        if (!divisionId) {
            alert("Division id not found on the page.");
            return Promise.resolve();
        }

        const formData = new FormData();
        formData.append("divisionId", divisionId);
        Object.keys(fields).forEach(function (key) {
            formData.append(key, fields[key]);
        });

        const headers = { "X-Requested-With": "XMLHttpRequest" };
        if (antiForgeryToken) headers["RequestVerificationToken"] = antiForgeryToken;

        if (busyEl) busyEl.classList.add("is-uploading");

        return fetch(url, { method: "POST", body: formData, headers })
            .then(async function (response) {
                if (response.ok) {
                    window.location.reload();
                    return;
                }

                const text = await response.text().catch(() => "");
                alert(text || errorMessage);
            })
            .catch(function () {
                alert(errorMessage);
            })
            .finally(function () {
                if (busyEl) busyEl.classList.remove("is-uploading");
            });
    }

    // Makes `zone` accept a dropped image file and passes it to `onFile`.
    function enableDropZone(zone, onFile) {
        ["dragenter", "dragover"].forEach(function (type) {
            zone.addEventListener(type, function (e) {
                e.preventDefault();
                if (e.dataTransfer) e.dataTransfer.dropEffect = "copy";
                zone.classList.add("is-dragover");
            });
        });

        zone.addEventListener("dragleave", function (e) {
            if (e.relatedTarget && zone.contains(e.relatedTarget)) return;
            zone.classList.remove("is-dragover");
        });

        zone.addEventListener("drop", function (e) {
            e.preventDefault();
            zone.classList.remove("is-dragover");

            const file = e.dataTransfer && e.dataTransfer.files && e.dataTransfer.files[0];
            if (!file) return;

            if (!file.type || file.type.indexOf("image/") !== 0) {
                alert("Please drop an image file (PNG, JPG or WebP).");
                return;
            }

            onFile(file);
        });
    }

    const iconInputs = document.querySelectorAll("input[data-icon-kind]");
    const dropZones = [];

    iconInputs.forEach(function (input) {
        const button = input.closest("label");
        const card = input.closest(".division-icon-card");

        function send(file) {
            return uploadImage(
                "/Divisions/UploadIcon",
                { kind: input.dataset.iconKind, icon: file },
                button,
                "Error uploading icon."
            );
        }

        input.addEventListener("change", function () {
            const file = input.files && input.files[0];
            if (!file) return;

            send(file).finally(function () {
                input.value = "";
            });
        });

        if (card) {
            enableDropZone(card, send);
            dropZones.push(card);
        }
    });

    if (bgInput) {
        const button = bgInput.closest("label");
        const zone = bgInput.closest(".division-background");

        function sendBackground(file) {
            return uploadImage(
                bgInput.dataset.uploadUrl || "/Divisions/UploadDivisionBackground",
                { backgroundImage: file },
                button,
                "Error uploading background."
            );
        }

        bgInput.addEventListener("change", function () {
            const file = bgInput.files && bgInput.files[0];
            if (!file) return;

            sendBackground(file).finally(function () {
                bgInput.value = "";
            });
        });

        if (zone) {
            enableDropZone(zone, sendBackground);
            dropZones.push(zone);
        }
    }

    // Stop the browser from opening an image that is dropped outside a zone.
    if (dropZones.length > 0) {
        ["dragover", "drop"].forEach(function (type) {
            window.addEventListener(type, function (e) {
                if (e.dataTransfer && Array.from(e.dataTransfer.types || []).indexOf("Files") !== -1) {
                    e.preventDefault();
                }
            });
        });
    }
});
