(function () {
    function getHost() {
        return document.getElementById('user-game-history');
    }

    function getUserId() {
        const host = getHost();
        return host ? host.getAttribute('data-user-id') : null;
    }

    async function loadGameHistory(divisionId) {
        const userId = getUserId();

        console.log('[UserGameHistory] userId=', userId, 'divisionId=', divisionId);

        if (!userId) return;

        let url = `/Users/UserGameHistory?id=${encodeURIComponent(userId)}`;
        if (divisionId) url += `&divisionId=${encodeURIComponent(divisionId)}`;

        console.log('[UserGameHistory] GET', url);

        const html = await fetch(url).then(r => r.text());

        const host = getHost();
        if (host) host.innerHTML = html;
    }

    document.addEventListener('change', (e) => {
        const t = e.target;
        if (t && t.id === 'divisionFilter') {
            loadGameHistory(t.value);
        }
    });

    window.UserGameHistory = { load: loadGameHistory };

    document.addEventListener('DOMContentLoaded', () => {
        window.UserGameHistory.load();
    });
})();
