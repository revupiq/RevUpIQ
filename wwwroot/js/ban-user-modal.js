document.addEventListener('click', async function (e) {

    const openBtn = e.target.closest('[data-ban-user]');
    if (openBtn) {
        const backdrop = document.getElementById('ban-modal-backdrop');
        const modal = document.getElementById('ban-modal');
        if (!backdrop || !modal) return;

        backdrop.style.display = 'block';
        modal.style.display = 'block';
        return;
    }

    const closeBtn =
        e.target.closest('#ban-close') ||
        e.target.closest('#ban-cancel') ||
        e.target.closest('#ban-modal-backdrop');

    if (closeBtn) {
        const backdrop = document.getElementById('ban-modal-backdrop');
        const modal = document.getElementById('ban-modal');
        if (!backdrop || !modal) return;

        backdrop.style.display = 'none';
        modal.style.display = 'none';
        return;
    }

    const confirmBtn = e.target.closest('#ban-confirm');
    if (confirmBtn) {
        const userId = confirmBtn.dataset.userId;
        const duration = parseInt(document.getElementById('ban-value').value, 10);
        const unit = document.getElementById('ban-unit').value;

        if (!userId || !duration) return;

        await fetch('/Users/BanUser', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken':
                    document.querySelector('input[name="__RequestVerificationToken"]')?.value
            },
            body: JSON.stringify({
                userId: userId,
                duration: duration,
                unit: unit
            })
        });

        const backdrop = document.getElementById('ban-modal-backdrop');
        const modal = document.getElementById('ban-modal');
        if (!backdrop || !modal) return;

        backdrop.style.display = 'none';
        modal.style.display = 'none';
        return;
    }

});
