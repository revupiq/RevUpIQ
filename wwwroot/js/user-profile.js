document.addEventListener('click', function (e) {

    const profileEditBtn = e.target.closest('[data-profile-edit]');
    if (profileEditBtn) {
        const row = profileEditBtn.closest('[data-editable]');
        if (!row) return;

        const view = row.querySelector('.view');
        const edit = row.querySelector('.edit');
        const form = profileEditBtn.closest('form');
        if (!view || !edit || !form) return;

        const isEditing = !edit.hasAttribute('hidden');

        if (isEditing) {
            view.textContent = edit.value ?? '';
            edit.setAttribute('hidden', true);
            view.style.display = '';
            profileEditBtn.textContent = 'Edit';

            fetch(form.action, {
                method: 'POST',
                body: new FormData(form),
                headers: { 'X-Requested-With': 'XMLHttpRequest' }
            });
        } else {
            edit.removeAttribute('hidden');
            view.style.display = 'none';
            profileEditBtn.textContent = 'Done';
            edit.focus();
        }

        return;
    }

    const banBtn = e.target.closest('[data-ban-user]');
    if (banBtn) {
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

        fetch('/Users/BanUser', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'X-Requested-With': 'XMLHttpRequest'
            },
            body: JSON.stringify({
                userId: userId,
                duration: duration,
                unit: unit
            })
        }).then(() => {
            const backdrop = document.getElementById('ban-modal-backdrop');
            const modal = document.getElementById('ban-modal');
            if (!backdrop || !modal) return;

            backdrop.style.display = 'none';
            modal.style.display = 'none';
            location.reload();
        });

        return;
    }

    const unbanBtn = e.target.closest('[data-unban-user]');
    if (unbanBtn) {
        const userId = unbanBtn.dataset.userId;
        if (!userId) return;

        fetch('/Users/UnbanUser', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'X-Requested-With': 'XMLHttpRequest'
            },
            body: JSON.stringify({ userId: userId })
        }).then(() => {
            location.reload();
        });

        return;
    }

});

document.addEventListener('change', function (e) {

    const freeToggle = e.target.closest('#freeAccountToggle');
    if (!freeToggle) return;

    const userId = freeToggle.dataset.userId;
    const isFree = freeToggle.checked;

    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;

    fetch('/Users/UpdateFreeAccount', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/x-www-form-urlencoded',
            'X-Requested-With': 'XMLHttpRequest',
            ...(token ? { 'RequestVerificationToken': token } : {})
        },
        body: new URLSearchParams({ userId, isFree })
    }).then(async res => {
        if (!res.ok) {
            freeToggle.checked = !isFree;
            alert('Failed to update Free Account');
            return;
        }

        const label = freeToggle.closest('.admin-toggle')?.querySelector('.toggle-text');
        if (label) label.textContent = isFree ? 'FREE' : 'PAID';
    });

});
