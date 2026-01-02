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

    if (bgInput) {
        bgInput.addEventListener("change", function () {
            const file = bgInput.files && bgInput.files[0];
            if (!file) return;

            const divisionId = divisionIdEl ? divisionIdEl.value : null;
            if (!divisionId) {
                alert("Division id not found on the page.");
                bgInput.value = "";
                return;
            }

            const uploadUrl = bgInput.dataset.uploadUrl || "/Divisions/UploadDivisionBackground";

            const formData = new FormData();
            formData.append("divisionId", divisionId);
            formData.append("backgroundImage", file);

            const headers = { "X-Requested-With": "XMLHttpRequest" };
            if (antiForgeryToken) headers["RequestVerificationToken"] = antiForgeryToken;

            fetch(uploadUrl, { method: "POST", body: formData, headers })
                .then(async function (response) {
                    if (response.ok) {
                        window.location.reload();
                        return;
                    }

                    const text = await response.text().catch(() => "");
                    alert(text || "Error uploading background.");
                })
                .catch(function () {
                    alert("Error uploading background.");
                })
                .finally(function () {
                    bgInput.value = "";
                });
        });
    }
});
